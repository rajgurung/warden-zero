using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace WardenZero.EditorTools
{
    // Generates the Arena scene, its materials, post-processing profile, generated textures
    // and prefabs from code, so the whole level is reproducible. Run from the menu or in batch mode:
    //   -executeMethod WardenZero.EditorTools.SceneBuilder.Build
    // The look follows src/render/Stage.ts: dark deck, glowing cyan rims (Babylon's glow layer
    // becomes URP bloom on HDR emissives), ACES tone mapping, exposure 1.2, contrast 1.15, vignette.
    public static class SceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Arena.unity";
        const string GenDir = "Assets/Art/Generated";
        const string MatDir = "Assets/Materials";
        const string PrefabDir = "Assets/Prefabs";
        const string ProfilePath = "Assets/Settings/ArenaPostFX.asset";

        // Draw order inside the transparent queue: ground glows, then sprites, then additive FX.
        const int GroundGlowQueue = 2950;
        const int FxQueue = 3100;

        [MenuItem("Warden Zero/Rebuild Arena Scene")]
        public static void Build()
        {
            Directory.CreateDirectory(GenDir);
            Directory.CreateDirectory(MatDir);
            Directory.CreateDirectory(PrefabDir);
            WriteGeneratedTextures();
            AssetDatabase.ImportAsset("Assets/Art", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            ConfigurePipeline();
            var profile = BuildPostProfile();

            // Materials. Lit surfaces use the specular workflow so the floor gets Babylon's
            // blue-tinted sheen; environment reflections are off (Unity's default grey
            // reflection cube otherwise lifts the whole floor).
            var spriteMat = SaveMaterial("SpriteUnlit", Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"), Color.white);
            // The Warden gets a thin glowing outline so he reads inside a crowd.
            var wardenMat = SaveMaterial("WardenOutline", Shader.Find("WardenZero/SpriteOutline"), Color.white);
            wardenMat.SetColor("_OutlineColor", Hdr(GameConfig.Accent, 1.6f));
            wardenMat.SetFloat("_OutlineWidth", 0.012f);
            var floorMat = LitMaterial("Floor", Color.white, new Color(0.22f, 0.28f, 0.4f) * 0.8f, 0.56f);
            floorMat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(GenDir + "/deck.png"));
            floorMat.SetTextureScale("_BaseMap", new Vector2((GameConfig.ArenaW + 40) / 8, (GameConfig.ArenaD + 40) / 8));
            var wallMat = LitMaterial("Wall", GameConfig.PanelEdge, new Color(0.4f, 0.45f, 0.6f) * 0.5f, 0.6f);
            var wallTopMat = LitMaterial("WallTop", GameConfig.BgMid, new Color(0.08f, 0.1f, 0.14f), 0.3f);
            var barrierMat = LitMaterial("Barrier", GameConfig.Panel, new Color(0.3f, 0.3f, 0.4f) * 0.5f, 0.5f);
            var rimMat = SaveMaterial("Rim", Unlit(), Hdr(GameConfig.Accent, 1.9f));
            var stripMat = SaveMaterial("Strip", Unlit(), Hdr(GameConfig.Magenta, 2.2f));
            var boltMat = SaveMaterial("Bolt", Unlit(), Hdr(new Color(0.56f, 1f, 1f), 4f));
            var ringTex = AssetDatabase.LoadAssetAtPath<Texture2D>(GenDir + "/ring.png");
            var sparkTex = AssetDatabase.LoadAssetAtPath<Texture2D>(GenDir + "/spark.png");
            var haloMat = GlowMaterial("RimHalo", AssetDatabase.LoadAssetAtPath<Texture2D>(GenDir + "/halo.png"), Hdr(GameConfig.Accent, 0.22f), GroundGlowQueue);
            var spawnRingMat = GlowMaterial("SpawnRingGlow", ringTex, Hdr(GameConfig.Accent, 1.4f), GroundGlowQueue);
            var wardenRingMat = GlowMaterial("WardenRingGlow", ringTex, Hdr(GameConfig.Accent, 0.8f), GroundGlowQueue);
            var reticleMat = GlowMaterial("ReticleGlow", AssetDatabase.LoadAssetAtPath<Texture2D>(GenDir + "/reticle.png"), Hdr(GameConfig.Accent, 2.2f), GroundGlowQueue);
            var sparkMat = GlowMaterial("Spark", sparkTex, Hdr(Color.white, 3f), FxQueue);
            var muzzleMat = GlowMaterial("MuzzleFlash", sparkTex, Hdr(new Color(0.75f, 0.95f, 1f), 5f), FxQueue);
            var trailMat = GlowMaterial("BoltTrail", sparkTex, Hdr(GameConfig.Accent, 2.5f), FxQueue);
            AssetDatabase.SaveAssets();

            var blob = LoadSprite(GenDir + "/blob.png");
            var boltPrefab = MakeBoltPrefab(boltMat, trailMat);
            var enemyPrefab = MakeEnemyPrefab(spriteMat, blob);

            // Scene: ambient is a dim hemisphere like Babylon's HemisphericLight (0.6).
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.36f, 0.43f, 0.6f) * 0.75f;
            RenderSettings.ambientEquatorColor = new Color(0.1f, 0.12f, 0.18f);
            RenderSettings.ambientGroundColor = new Color(0.04f, 0.05f, 0.1f) * 0.6f;
            RenderSettings.reflectionIntensity = 0;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.011f;
            RenderSettings.fogColor = GameConfig.BgDeep;

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(0.86f, 0.9f, 1f);
            sun.intensity = 1.6f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.9f;
            sun.transform.rotation = Quaternion.LookRotation(new Vector3(-0.45f, -1f, -0.3f));

            var volume = new GameObject("PostFX").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;

            BuildArena(floorMat, wallMat, wallTopMat, rimMat, haloMat, barrierMat, stripMat, spawnRingMat);

            // Camera
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = GameConfig.BgDeep;
            cam.fieldOfView = GameConfig.CameraFovDegrees;
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = 260;
            cam.allowHDR = true;
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camGo.AddComponent<AudioListener>();
            camGo.transform.position = GameConfig.CameraOffset;
            camGo.transform.LookAt(new Vector3(0, 0, GameConfig.CameraLookAhead));
            var follow = camGo.AddComponent<CameraFollow>();

            // Warden
            var warden = new GameObject("Warden");
            var player = warden.AddComponent<PlayerController>();
            player.cam = cam;
            // Order 1 keeps the Warden readable when a crowd overlaps him.
            player.body = MakeSprite("Body", warden.transform, wardenMat, LoadSprite("Assets/Art/Hero/idle.png"), 1);
            player.body.gameObject.AddComponent<Billboard>();
            MakeDecal("Shadow", warden.transform, spriteMat, blob, new Vector2(1.5f, 1f), new Color(0, 0, 0, 0.75f), -1);
            // A soft cyan ring under him marks the Warden inside a crowd.
            GlowQuad("Ring", warden.transform, wardenRingMat, 1.9f).transform.localPosition = new Vector3(0, 0.03f, 0);
            var glow = new GameObject("Glow").AddComponent<Light>();
            glow.transform.SetParent(warden.transform, false);
            glow.transform.localPosition = new Vector3(0, 2.4f, 0);
            glow.type = LightType.Point;
            glow.color = GameConfig.Accent;
            glow.range = 12;
            glow.intensity = 2.2f;
            glow.shadows = LightShadows.None;
            player.glow = glow;
            var muzzle = GlowQuad("MuzzleFlash", warden.transform, muzzleMat, 1.1f);
            muzzle.transform.localRotation = Quaternion.identity;
            muzzle.AddComponent<Billboard>();
            player.muzzleFlash = muzzle.transform;
            player.reticle = GlowQuad("Reticle", null, reticleMat, 1.5f).transform;
            player.boltPrefab = boltPrefab;
            player.idle = LoadSprite("Assets/Art/Hero/idle.png");
            player.shoot = LoadSprite("Assets/Art/Hero/shoot.png");
            player.shootUp = LoadSprite("Assets/Art/Hero/shoot_up.png");
            player.shootDown = LoadSprite("Assets/Art/Hero/shoot_down.png");
            player.dash = LoadSprite("Assets/Art/Hero/dash.png");
            player.death = LoadSprite("Assets/Art/Hero/death.png");
            player.runDown = Frames("Assets/Art/Hero/run_down_", 6);
            player.runSide = Frames("Assets/Art/Hero/run_side_", 6);
            player.runUp = Frames("Assets/Art/Hero/run_up_", 6);
            follow.target = warden.transform;

            // Effects
            var fx = new GameObject("Effects").AddComponent<Effects>();
            fx.sparks = MakeSparks(fx.gameObject, sparkMat);
            fx.cameraFollow = follow;

            // Game manager + HUD
            var gmGo = new GameObject("GameManager");
            var gm = gmGo.AddComponent<GameManager>();
            gm.player = player;
            gm.enemyPrefab = enemyPrefab;
            gm.gruntWalk = Frames("Assets/Art/Enemies/grunt_walk", 4);
            gm.runnerWalk = Frames("Assets/Art/Enemies/runner_walk", 4);
            gm.audioSource = gmGo.AddComponent<AudioSource>();
            gm.audioSource.playOnAwake = false;
            gm.shootSound = Clip("shoot");
            gm.enemyHitSound = Clip("enemy_hit");
            gm.enemyDieSound = Clip("enemy_die");
            gm.hurtSound = Clip("player_hurt");
            gm.dashSound = Clip("dash");
            gm.waveStartSound = Clip("wave_start");
            gm.gameOverSound = Clip("game_over");
            gm.hud = BuildHud();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[SceneBuilder] Arena scene written to " + ScenePath);
        }

        // ------------------------------------------------------------------ arena

        static void BuildArena(Material floor, Material wall, Material wallTop, Material rim, Material halo, Material barrier, Material strip, Material spawnRing)
        {
            var root = new GameObject("Arena").transform;

            var ground = Box("Floor", root, floor, new Vector3(0, -0.05f, 0), new Vector3(GameConfig.ArenaW + 40, 0.1f, GameConfig.ArenaD + 40));
            ground.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            // A box's top face maps the texture once, so tiling comes from the material scale.

            GlowQuad("SpawnRing", root, spawnRing, 7.4f).transform.position = new Vector3(0, 0.02f, 0);

            const float h = GameConfig.WallHeight;
            const float r = 0.13f;
            for (int i = 0; i < GameConfig.Walls.Length; i++)
            {
                var w = GameConfig.Walls[i];
                float sx = w.MaxX - w.MinX, sz = w.MaxZ - w.MinZ;
                var c = new Vector3((w.MinX + w.MaxX) / 2, 0, (w.MinZ + w.MaxZ) / 2);
                var block = Box("Wall" + i, root, wall, c + Vector3.up * h / 2, new Vector3(sx, h, sz)).transform;
                Box("Top", block, wallTop, c + Vector3.up * (h + 0.02f), new Vector3(sx - 0.1f, 0.04f, sz - 0.1f));
                // Glowing rim around the top edge (HDR, so it blooms).
                foreach (var rimBox in new[]
                {
                    (new Vector3(0, 0, sz / 2), new Vector3(sx + r, 0.07f, r)),
                    (new Vector3(0, 0, -sz / 2), new Vector3(sx + r, 0.07f, r)),
                    (new Vector3(sx / 2, 0, 0), new Vector3(r, 0.07f, sz + r)),
                    (new Vector3(-sx / 2, 0, 0), new Vector3(r, 0.07f, sz + r)),
                })
                {
                    var rb = Box("Rim", block, rim, c + Vector3.up * (h + 0.02f) + rimBox.Item1, rimBox.Item2);
                    rb.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                    // Soft halo strip either side of the rim: Babylon's glow layer blurs with a
                    // 48 px kernel, wider than URP bloom spreads a thin line.
                    bool alongX = rimBox.Item2.x > rimBox.Item2.z;
                    float len = Mathf.Max(rimBox.Item2.x, rimBox.Item2.z);
                    var hq = GlowQuad("Halo", block, halo, 1);
                    hq.transform.position = c + Vector3.up * (h + 0.06f) + rimBox.Item1;
                    hq.transform.rotation = Quaternion.Euler(90, alongX ? 0 : 90, 0);
                    hq.transform.localScale = Vector3.one;
                    SetWorldScale(hq.transform, new Vector3(len + 1.2f, 1.8f, 1));
                }
            }

            // Perimeter barrier with a magenta warning strip.
            float hw = GameConfig.HalfW, hd = GameConfig.HalfD;
            var edges = new[]
            {
                (new Vector3(0, 0, hd + 0.5f), new Vector3(GameConfig.ArenaW + 2, 0, 1)),
                (new Vector3(0, 0, -hd - 0.5f), new Vector3(GameConfig.ArenaW + 2, 0, 1)),
                (new Vector3(hw + 0.5f, 0, 0), new Vector3(1, 0, GameConfig.ArenaD)),
                (new Vector3(-hw - 0.5f, 0, 0), new Vector3(1, 0, GameConfig.ArenaD)),
            };
            foreach (var (pos, size) in edges)
            {
                var b = Box("Barrier", root, barrier, pos + Vector3.up * 0.7f, new Vector3(size.x, 1.4f, size.z)).transform;
                Box("Strip", b, strip, pos + Vector3.up * 1.2f, new Vector3(size.x + 0.02f, 0.12f, size.z + 0.02f));
            }
        }

        // ------------------------------------------------------------------ prefabs and FX

        static Bolt MakeBoltPrefab(Material mat, Material trailMat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = "Bolt";
            go.transform.localScale = new Vector3(0.22f, 0.22f, 0.75f);
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            var trail = go.AddComponent<TrailRenderer>();
            trail.sharedMaterial = trailMat;
            trail.time = 0.09f;
            trail.startWidth = 0.22f;
            trail.endWidth = 0f;
            trail.minVertexDistance = 0.1f;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            go.AddComponent<Bolt>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + "/Bolt.prefab");
            Object.DestroyImmediate(go);
            return prefab.GetComponent<Bolt>();
        }

        static Enemy MakeEnemyPrefab(Material spriteMat, Sprite blob)
        {
            var go = new GameObject("Enemy");
            var enemy = go.AddComponent<Enemy>();
            enemy.body = MakeSprite("Body", go.transform, spriteMat, LoadSprite("Assets/Art/Enemies/grunt_walk0.png"), 0);
            enemy.body.gameObject.AddComponent<Billboard>();
            MakeDecal("Shadow", go.transform, spriteMat, blob, new Vector2(1.3f, 0.9f), new Color(0, 0, 0, 0.75f), -1);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + "/Enemy.prefab");
            Object.DestroyImmediate(go);
            return prefab.GetComponent<Enemy>();
        }

        // One looping world-space system with no emission; Effects.Burst emits into it.
        // Mirrors Babylon's burst: upward cone, gravity 16, life 0.2-0.6 s, additive.
        static ParticleSystem MakeSparks(GameObject go, Material mat)
        {
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 1;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
            main.gravityModifier = 16f / 9.81f;
            main.maxParticles = 1500;
            var emission = ps.emission;
            emission.rateOverTime = 0;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 50;
            shape.radius = 0.15f;
            shape.rotation = new Vector3(-90, 0, 0);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                         new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0, 1) });
            col.color = grad;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 1, 1, 0.3f));
            var pr = go.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = mat;
            pr.renderMode = ParticleSystemRenderMode.Billboard;
            pr.shadowCastingMode = ShadowCastingMode.Off;
            pr.receiveShadows = false;
            return ps;
        }

        // ------------------------------------------------------------------ HUD

        // Mirrors the Babylon DOM HUD: small dim uppercase labels, thin bordered meters,
        // big wave number in the centre, score on the right, ability chip bottom left.
        static Hud BuildHud()
        {
            var canvasGo = new GameObject("HUD");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            var hud = canvasGo.AddComponent<Hud>();
            var t = canvasGo.transform;
            var top = new Vector2(0, 1);

            // Health, top left.
            hud.healthText = Label(t, "Health", top, new Vector2(18, -14), new Vector2(300, 16), 11, TextAnchor.UpperLeft, GameConfig.TextDim);
            var border = Panel(t, "HealthBorder", top, new Vector2(18, -34), new Vector2(262, 12), GameConfig.PanelEdge);
            var back = Panel(border, "HealthBack", top, new Vector2(1, -1), new Vector2(260, 10), GameConfig.HealthBack);
            var fill = Panel(back, "HealthFill", Vector2.zero, Vector2.zero, Vector2.zero, GameConfig.Health);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            hud.healthFill = fill;

            // Wave, top centre.
            Label(t, "WaveLabel", new Vector2(0.5f, 1), new Vector2(-100, -14), new Vector2(200, 16), 11, TextAnchor.UpperCenter, GameConfig.TextDim).text = "WAVE";
            hud.waveText = Label(t, "Wave", new Vector2(0.5f, 1), new Vector2(-100, -29), new Vector2(200, 40), 30, TextAnchor.UpperCenter, GameConfig.TextBright);
            hud.waveText.fontStyle = FontStyle.Bold;

            // Score, top right.
            Label(t, "ScoreLabel", new Vector2(1, 1), new Vector2(-218, -14), new Vector2(200, 16), 11, TextAnchor.UpperRight, GameConfig.TextDim).text = "SCORE";
            hud.scoreText = Label(t, "Score", new Vector2(1, 1), new Vector2(-218, -30), new Vector2(200, 34), 24, TextAnchor.UpperRight, GameConfig.TextBright);
            hud.scoreText.fontStyle = FontStyle.Bold;

            // Dash chip, bottom left, with a cooldown underline.
            var chipBorder = Panel(t, "DashChip", Vector2.zero, new Vector2(18, 46), new Vector2(100, 28), GameConfig.PanelEdge);
            var chip = Panel(chipBorder, "Inner", top, new Vector2(1, -1), new Vector2(98, 26), new Color(0.078f, 0.106f, 0.188f, 0.92f));
            hud.dashText = Label(chip, "Text", top, new Vector2(10, -6), new Vector2(90, 16), 12, TextAnchor.UpperLeft, GameConfig.Accent);
            var bar = Panel(chip, "Cooldown", Vector2.zero, Vector2.zero, Vector2.zero, GameConfig.Accent);
            bar.anchorMin = Vector2.zero;
            bar.anchorMax = new Vector2(1, 0);
            bar.pivot = Vector2.zero;
            bar.offsetMin = Vector2.zero;
            bar.offsetMax = new Vector2(0, 2);
            hud.dashBar = bar.GetComponent<Image>();

            Label(t, "Hint", new Vector2(1, 0), new Vector2(-418, 30), new Vector2(400, 16), 11, TextAnchor.LowerRight, GameConfig.TextDim).text =
                "WASD MOVE   ·   MOUSE AIM   ·   HOLD LMB FIRE   ·   SPACE DASH";

            // Centre banner at ~30% from the top, white with a cyan glow.
            hud.bannerText = Label(t, "Banner", new Vector2(0.5f, 0.5f), new Vector2(-600, 230), new Vector2(1200, 160), 60, TextAnchor.MiddleCenter, GameConfig.TextBright);
            hud.bannerText.fontStyle = FontStyle.Bold;
            hud.bannerText.lineSpacing = 1.1f;
            hud.bannerGlow = hud.bannerText.gameObject.AddComponent<Outline>();
            hud.bannerGlow.effectDistance = new Vector2(1.5f, -1.5f);
            hud.bannerGlow.effectColor = new Color(GameConfig.Accent.r, GameConfig.Accent.g, GameConfig.Accent.b, 0.2f);
            hud.bannerText.enabled = false;
            return hud;
        }

        // anchor = corner the element hangs from; pos = its top-left offset from that corner.
        static RectTransform Panel(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return rt;
        }

        static Text Label(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize, TextAnchor align, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Shadow));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = align;
            text.color = color;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            go.GetComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.8f);
            return text;
        }

        // ------------------------------------------------------------------ post-processing

        // Babylon: GlowLayer 0.85, ACES, exposure 1.2, contrast 1.15, vignette weight 2.4.
        static VolumeProfile BuildPostProfile()
        {
            AssetDatabase.DeleteAsset(ProfilePath);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);

            var bloom = profile.Add<Bloom>();
            bloom.threshold.Override(0.85f);
            bloom.intensity.Override(2.8f);
            bloom.scatter.Override(0.82f);
            bloom.highQualityFiltering.Override(false);

            var tone = profile.Add<Tonemapping>();
            tone.mode.Override(TonemappingMode.ACES);

            var color = profile.Add<ColorAdjustments>();
            color.postExposure.Override(Mathf.Log(1.2f, 2));
            color.contrast.Override(15f);
            color.saturation.Override(0f);

            var vignette = profile.Add<Vignette>();
            vignette.color.Override(Color.black);
            vignette.intensity.Override(0.5f);
            vignette.smoothness.Override(0.55f);

            foreach (var c in profile.components)
            {
                c.name = c.GetType().Name;
                c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(c, profile);
            }
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        // ------------------------------------------------------------------ helpers

        static GameObject Box(string name, Transform parent, Material mat, Vector3 worldPos, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = name;
            go.transform.position = worldPos;
            go.transform.localScale = size;
            go.transform.SetParent(parent, true);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        // A flat quad on the ground with an additive glow material (rings, reticle).
        static GameObject GlowQuad(string name, Transform parent, Material mat, float size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localRotation = Quaternion.Euler(90, 0, 0);
            go.transform.localScale = new Vector3(size, size, 1);
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go;
        }

        static void SetWorldScale(Transform t, Vector3 scale)
        {
            var p = t.parent.lossyScale;
            // Parent is an unrotated box; the quad's local x/y map to world x and z (or z and x).
            bool alongX = Mathf.Approximately(t.rotation.eulerAngles.y, 0);
            t.localScale = alongX
                ? new Vector3(scale.x / p.x, scale.y / p.z, 1 / p.y)
                : new Vector3(scale.x / p.z, scale.y / p.x, 1 / p.y);
        }

        static SpriteRenderer MakeSprite(string name, Transform parent, Material mat, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = mat;
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        // A sprite lying flat on the ground (blob shadows).
        static SpriteRenderer MakeDecal(string name, Transform parent, Material mat, Sprite sprite, Vector2 size, Color color, int order)
        {
            var sr = MakeSprite(name, parent, mat, sprite, order);
            sr.transform.localPosition = new Vector3(0, 0.02f, 0);
            sr.transform.localRotation = Quaternion.Euler(90, 0, 0);
            sr.transform.localScale = new Vector3(size.x, size.y, 1);
            sr.color = color;
            return sr;
        }

        static Material SaveMaterial(string name, Shader shader, Color color)
        {
            if (shader == null) throw new System.Exception("Shader not found for material " + name);
            string path = MatDir + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // URP Lit in the specular workflow with environment reflections off.
        static Material LitMaterial(string name, Color baseColor, Color specular, float smoothness)
        {
            var mat = SaveMaterial(name, Shader.Find("Universal Render Pipeline/Lit"), baseColor);
            mat.SetFloat("_WorkflowMode", 0);
            mat.EnableKeyword("_SPECULAR_SETUP");
            mat.SetColor("_SpecColor", specular);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_EnvironmentReflections", 0);
            mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            return mat;
        }

        static Material GlowMaterial(string name, Texture2D tex, Color color, int queue)
        {
            var mat = SaveMaterial(name, Shader.Find("WardenZero/AdditiveGlow"), color);
            mat.SetTexture("_BaseMap", tex);
            mat.renderQueue = queue;
            return mat;
        }

        // An HDR colour: `c` scaled by `intensity` in linear space (like the HDR colour picker).
        static Color Hdr(Color c, float intensity)
        {
            var lin = c.linear * intensity;
            return new Color(lin.r, lin.g, lin.b, 1).gamma;
        }

        static Shader Unlit() => Shader.Find("Universal Render Pipeline/Unlit");

        static Sprite LoadSprite(string path)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s == null) throw new System.Exception("Sprite not found (check import settings): " + path);
            return s;
        }

        static Sprite[] Frames(string prefix, int count)
        {
            return Enumerable.Range(0, count).Select(i => LoadSprite(prefix + i + ".png")).ToArray();
        }

        static AudioClip Clip(string name)
        {
            return AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/" + name + ".mp3");
        }

        // WebGL uses the "Mobile" quality level: full render scale, 4x MSAA, HDR for bloom,
        // soft shadows and a sharper shadow map over a shorter distance (the camera sees ~40 m).
        static void ConfigurePipeline()
        {
            for (int i = 0; i < QualitySettings.count; i++)
            {
                if (QualitySettings.GetRenderPipelineAssetAt(i) is UniversalRenderPipelineAsset urp)
                {
                    urp.renderScale = 1f;
                    urp.msaaSampleCount = 4;
                    urp.supportsHDR = true;
                    urp.shadowDistance = 45;
                    urp.mainLightShadowmapResolution = 2048;
                    var so = new SerializedObject(urp);
                    so.FindProperty("m_SoftShadowsSupported").boolValue = true;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(urp);
                }
            }
        }

        // Procedural textures: deck plates, shadow blob, glow ring, reticle and spark.
        static void WriteGeneratedTextures()
        {
            // Deck: Babylon's deckTexture at half resolution (2 m plates, seams, rivets,
            // and a faint cyan conduit on one plate per tile).
            const int S = 256, P = 64;
            var rng = new System.Random(7);
            var deck = new Texture2D(S, S, TextureFormat.RGBA32, false);
            var seam = new Color32(26, 34, 64, 255);
            var rivet = new Color32(42, 54, 84, 255);
            for (int py = 0; py < S; py += P)
            for (int px = 0; px < S; px += P)
            {
                int v = rng.Next(6);
                var plate = new Color32((byte)(14 + v), (byte)(19 + v), (byte)(36 + v), 255);
                for (int y = 0; y < P; y++)
                for (int x = 0; x < P; x++)
                {
                    bool edge = x < 2 || y < 2 || x >= P - 2 || y >= P - 2;
                    bool isRivet = (x is >= 4 and < 6 || x is >= P - 6 and < P - 4) && (y is >= 4 and < 6 || y is >= P - 6 and < P - 4);
                    deck.SetPixel(px + x, py + y, edge ? seam : isRivet ? rivet : plate);
                }
            }
            var conduit = Color.Lerp(new Color32(15, 20, 37, 255), GameConfig.Accent, 0.18f);
            for (int y = P + 7; y < 2 * P - 7; y++) deck.SetPixel(P + P / 2, S - 1 - y, conduit);
            File.WriteAllBytes(GenDir + "/deck.png", deck.EncodeToPNG());

            const int R = 256;
            var blob = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            for (int y = 0; y < 128; y++)
            for (int x = 0; x < 128; x++)
            {
                float d = new Vector2(x - 63.5f, y - 63.5f).magnitude / 64;
                blob.SetPixel(x, y, new Color(0, 0, 0, Mathf.Pow(Mathf.Clamp01(1 - d), 1.5f)));
            }
            File.WriteAllBytes(GenDir + "/blob.png", blob.EncodeToPNG());

            // Ring: a crisp line at 0.93 of the radius with a soft halo either side.
            var ring = new Texture2D(R, R, TextureFormat.RGBA32, false);
            var reticle = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            var spark = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            for (int y = 0; y < R; y++)
            for (int x = 0; x < R; x++)
            {
                float d = new Vector2(x - R / 2 + 0.5f, y - R / 2 + 0.5f).magnitude / (R / 2);
                float line = Mathf.Clamp01(1 - Mathf.Abs(d - 0.93f) / 0.018f);
                float halo = Mathf.Pow(Mathf.Clamp01(1 - Mathf.Abs(d - 0.93f) / 0.07f), 2) * 0.35f;
                ring.SetPixel(x, y, new Color(1, 1, 1, Mathf.Max(line, halo)));
            }
            for (int y = 0; y < 128; y++)
            for (int x = 0; x < 128; x++)
            {
                float d = new Vector2(x - 63.5f, y - 63.5f).magnitude / 64;
                float circle = Mathf.Clamp01(1 - Mathf.Abs(d - 0.78f) / 0.07f);
                float dot = Mathf.Clamp01((0.12f - d) / 0.04f);
                reticle.SetPixel(x, y, new Color(1, 1, 1, Mathf.Max(circle, dot)));
            }
            for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                // Babylon's spark gradient: 1 at the centre, 0.65 at 30%, 0 at the edge.
                float d = new Vector2(x - 31.5f, y - 31.5f).magnitude / 32;
                float a = d < 0.3f ? Mathf.Lerp(1, 0.65f, d / 0.3f) : Mathf.Lerp(0.65f, 0, Mathf.Clamp01((d - 0.3f) / 0.7f));
                spark.SetPixel(x, y, new Color(1, 1, 1, a));
            }
            // Halo: a soft gaussian across the strip, fading out at both ends.
            var haloTex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float u = (x + 0.5f) / 64, v = (y + 0.5f) / 64 - 0.5f;
                float ends = Mathf.SmoothStep(0, 1, Mathf.Min(u, 1 - u) / 0.15f);
                haloTex.SetPixel(x, y, new Color(1, 1, 1, ends * Mathf.Exp(-v * v / (2 * 0.15f * 0.15f))));
            }
            File.WriteAllBytes(GenDir + "/halo.png", haloTex.EncodeToPNG());
            File.WriteAllBytes(GenDir + "/ring.png", ring.EncodeToPNG());
            File.WriteAllBytes(GenDir + "/reticle.png", reticle.EncodeToPNG());
            File.WriteAllBytes(GenDir + "/spark.png", spark.EncodeToPNG());
            AssetDatabase.Refresh();
        }
    }
}
