using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace WardenZero.EditorTools
{
    // Generates the Arena scene, its materials, post-processing profile, generated textures
    // and prefabs from code, so the whole level is reproducible. Run from the menu or in batch mode:
    //   -executeMethod WardenZero.EditorTools.SceneBuilder.Build
    // The look follows src/render/Stage.ts: dark deck, glowing cyan rims (Babylon's glow layer
    // becomes URP bloom on HDR emissives), ACES tone mapping, exposure 1.2, contrast 1.15, vignette.
    public static partial class SceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Arena.unity";
        const string GenDir = "Assets/Art/Generated";
        const string MatDir = "Assets/Materials";
        const string PrefabDir = "Assets/Prefabs";
        const string ProfilePath = "Assets/Settings/ArenaPostFX.asset";

        // Draw order inside the transparent queue: ground glows, then sprites, then additive FX.
        const int GroundGlowQueue = 2950;
        const int FxQueue = 3100;

        [MenuItem("Warden Zero/Rebuild Scenes")]
        public static void Build()
        {
            PrepareAssets();
            BuildArenaScene();
            BuildGreenfangScene();
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene(GreenfangPath, true),
            };
            AssetDatabase.SaveAssets();
            Debug.Log("[SceneBuilder] Arena and Greenfang scenes written");
        }

        // true: the rigged Tripo model (WardenModelView); false: the 2D sprite Warden.
        const bool UseModelWarden = true;

        // Shared assets for both scenes, kept in static fields while building.
        static VolumeProfile profile;
        static Material spriteMat;
        static Material wardenMat;
        static Material floorMat;
        static Material wallMat;
        static Material wallTopMat;
        static Material barrierMat;
        static Material rimMat;
        static Material stripMat;
        static Material boltMat;
        static Texture2D ringTex;
        static Texture2D sparkTex;
        static Material haloMat;
        static Material spawnRingMat;
        static Material wardenRingMat;
        static Material reticleMat;
        static Material sparkMat;
        static Material muzzleMat;
        static Material trailMat;
        static Material critMat;
        static Material critTrailMat;
        static Material shockMat;
        static Material gemMat;
        static Material coinMat;
        static Material spitMat;
        static Material spitTrailMat;
        static Material heartMat;
        static Sprite blob;
        static Bolt boltPrefab;
        static Bolt critPrefab;
        static Enemy enemyPrefab;
        static Gem gemPrefab;
        static Spit spitPrefab;
        static Pickup heartPrefab;
        static Pickup coinPrefab;

        static void PrepareAssets()
        {
            Directory.CreateDirectory(GenDir);
            Directory.CreateDirectory(MatDir);
            Directory.CreateDirectory(PrefabDir);
            WriteGeneratedTextures();
            AssetDatabase.ImportAsset("Assets/Art", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            ConfigurePipeline();
            profile = BuildPostProfile();
            PrepareWarden3D();

            // Materials. Lit surfaces use the specular workflow so the floor gets Babylon's
            // blue-tinted sheen; environment reflections are off (Unity's default grey
            // reflection cube otherwise lifts the whole floor).
            spriteMat = SaveMaterial("SpriteUnlit", Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"), Color.white);
            // The Warden gets a thin glowing outline so he reads inside a crowd.
            wardenMat = SaveMaterial("WardenOutline", Shader.Find("WardenZero/SpriteOutline"), Color.white);
            wardenMat.SetColor("_OutlineColor", Hdr(GameConfig.Accent, 1.6f));
            wardenMat.SetFloat("_OutlineWidth", 0.012f);
            floorMat = LitMaterial("Floor", Color.white, new Color(0.22f, 0.28f, 0.4f) * 0.8f, 0.56f);
            floorMat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(GenDir + "/deck.png"));
            floorMat.SetTextureScale("_BaseMap", new Vector2((GameConfig.ArenaW + 40) / 8, (GameConfig.ArenaD + 40) / 8));
            wallMat = LitMaterial("Wall", GameConfig.PanelEdge, new Color(0.4f, 0.45f, 0.6f) * 0.5f, 0.6f);
            wallTopMat = LitMaterial("WallTop", GameConfig.BgMid, new Color(0.08f, 0.1f, 0.14f), 0.3f);
            barrierMat = LitMaterial("Barrier", GameConfig.Panel, new Color(0.3f, 0.3f, 0.4f) * 0.5f, 0.5f);
            rimMat = SaveMaterial("Rim", Unlit(), Hdr(GameConfig.Accent, 1.9f));
            stripMat = SaveMaterial("Strip", Unlit(), Hdr(GameConfig.Magenta, 2.2f));
            boltMat = SaveMaterial("Bolt", Unlit(), Hdr(new Color(0.56f, 1f, 1f), 4f));
            ringTex = AssetDatabase.LoadAssetAtPath<Texture2D>(GenDir + "/ring.png");
            sparkTex = AssetDatabase.LoadAssetAtPath<Texture2D>(GenDir + "/spark.png");
            haloMat = GlowMaterial("RimHalo", AssetDatabase.LoadAssetAtPath<Texture2D>(GenDir + "/halo.png"), Hdr(GameConfig.Accent, 0.22f), GroundGlowQueue);
            spawnRingMat = GlowMaterial("SpawnRingGlow", ringTex, Hdr(GameConfig.Accent, 1.4f), GroundGlowQueue);
            wardenRingMat = GlowMaterial("WardenRingGlow", ringTex, Hdr(GameConfig.Accent, 0.8f), GroundGlowQueue);
            reticleMat = GlowMaterial("ReticleGlow", AssetDatabase.LoadAssetAtPath<Texture2D>(GenDir + "/reticle.png"), Hdr(GameConfig.Accent, 2.2f), GroundGlowQueue);
            sparkMat = GlowMaterial("Spark", sparkTex, Hdr(Color.white, 3f), FxQueue);
            muzzleMat = GlowMaterial("MuzzleFlash", sparkTex, Hdr(new Color(0.75f, 0.95f, 1f), 5f), FxQueue);
            trailMat = GlowMaterial("BoltTrail", sparkTex, Hdr(GameConfig.Accent, 2.5f), FxQueue);
            critMat = SaveMaterial("CritBolt", Unlit(), Hdr(GameConfig.Gold, 4.5f));
            critTrailMat = GlowMaterial("CritTrail", sparkTex, Hdr(GameConfig.Gold, 2.5f), FxQueue);
            shockMat = GlowMaterial("ShockRing", ringTex, Hdr(GameConfig.Gold, 2.5f), FxQueue);
            gemMat = LitMaterial("Gem", GameConfig.Hex(0x2a8fc0), new Color(0.6f, 0.6f, 0.7f), 0.85f);
            Emissive(gemMat, Hdr(GameConfig.Accent, 0.9f));
            coinMat = LitMaterial("Coin", GameConfig.Gold * 0.6f, new Color(0.8f, 0.7f, 0.4f), 0.8f);
            Emissive(coinMat, Hdr(GameConfig.Gold, 1.2f));
            spitMat = SaveMaterial("Spit", Unlit(), Hdr(GameConfig.Hex(0x9bff67), 3f));
            spitTrailMat = GlowMaterial("SpitTrail", sparkTex, Hdr(GameConfig.Hex(0x9bff67), 2f), FxQueue);
            heartMat = CutoutMaterial("Heart", AssetDatabase.LoadAssetAtPath<Texture2D>(GenDir + "/heart.png"), Hdr(GameConfig.Health, 2.2f));
            AssetDatabase.SaveAssets();

            blob = LoadSprite(GenDir + "/blob.png");
            boltPrefab = MakeBoltPrefab("Bolt", boltMat, trailMat, 0.22f, 0.75f);
            critPrefab = MakeBoltPrefab("CritBolt", critMat, critTrailMat, 0.28f, 1.0f);
            enemyPrefab = MakeEnemyPrefab(spriteMat, blob);
            gemPrefab = MakeGemPrefab(gemMat);
            spitPrefab = MakeSpitPrefab(spitMat, spitTrailMat);
            heartPrefab = MakePickupPrefab("Heart", true, heartMat, spriteMat, blob);
            coinPrefab = MakePickupPrefab("Coin", false, coinMat, spriteMat, blob);
        }

        static void BuildArenaScene()
        {
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
            CreateCore();
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        // Camera, Warden, effects, game manager, HUD and menus: the same in every scene.
        static GameManager CreateCore()
        {
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

            // Warden: gameplay (PlayerController) and drawing (a WardenView) are separate.
            var warden = new GameObject("Warden");
            var player = warden.AddComponent<PlayerController>();
            player.cam = cam;
            player.view = UseModelWarden ? (WardenView)BuildModelView(warden.transform) : BuildSpriteView(warden.transform, cam);
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
            player.boltPrefab = boltPrefab;
            player.critBoltPrefab = critPrefab;
            follow.target = warden.transform;

            // Effects
            var fx = new GameObject("Effects").AddComponent<Effects>();
            fx.sparks = MakeSparks(fx.gameObject, sparkMat);
            fx.cameraFollow = follow;
            fx.shockRing = GlowQuad("ShockRing", fx.transform, shockMat, 1).GetComponent<MeshRenderer>();

            // Game manager, HUD and menus
            var gmGo = new GameObject("GameManager");
            var gm = gmGo.AddComponent<GameManager>();
            gm.player = player;
            gm.cameraFollow = follow;
            gm.enemyPrefab = enemyPrefab;
            gm.gemPrefab = gemPrefab;
            gm.heartPrefab = heartPrefab;
            gm.coinPrefab = coinPrefab;
            gm.gruntWalk = Frames("Assets/Art/Enemies/grunt_walk", 4);
            gm.runnerWalk = Frames("Assets/Art/Enemies/runner_walk", 4);
            gm.skeletonTile = LoadSprite("Assets/Art/Pixel/skeleton_idle.png");
            gm.spiderTile = LoadSprite("Assets/Art/Pixel/spider_idle.png");
            gm.demonTile = LoadSprite("Assets/Art/Pixel/demon_idle.png");
            gm.spitPrefab = spitPrefab;
            gm.audioSource = gmGo.AddComponent<AudioSource>();
            gm.audioSource.playOnAwake = false;
            gm.shootSound = Clip("shoot");
            gm.enemyHitSound = Clip("enemy_hit");
            gm.enemyDieSound = Clip("enemy_die");
            gm.hurtSound = Clip("player_hurt");
            gm.dashSound = Clip("dash");
            gm.waveStartSound = Clip("wave_start");
            gm.gameOverSound = Clip("game_over");
            gm.bombSound = Clip("bomb");
            gm.pickupSound = Clip("pickup");
            gm.upgradeSound = Clip("upgrade_select");
            gm.hud = BuildHud();
            player.touch = gm.hud.touch;
            player.reticle = gm.hud.reticle;
            gm.menus = BuildMenus();
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            return gm;
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

        static Bolt MakeBoltPrefab(string name, Material mat, Material trailMat, float diameter, float length)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = name;
            go.transform.localScale = new Vector3(diameter, diameter, length);
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            var trail = go.AddComponent<TrailRenderer>();
            trail.sharedMaterial = trailMat;
            trail.time = 0.09f;
            trail.startWidth = diameter;
            trail.endWidth = 0f;
            trail.minVertexDistance = 0.1f;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            go.AddComponent<Bolt>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + "/" + name + ".prefab");
            Object.DestroyImmediate(go);
            return prefab.GetComponent<Bolt>();
        }

        // Spitter glob: a glowing green ball with a short trail.
        static Spit MakeSpitPrefab(Material mat, Material trailMat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = "Spit";
            go.transform.localScale = Vector3.one * 0.32f;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            var trail = go.AddComponent<TrailRenderer>();
            trail.sharedMaterial = trailMat;
            trail.time = 0.15f;
            trail.startWidth = 0.3f;
            trail.endWidth = 0;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            go.AddComponent<Spit>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + "/Spit.prefab");
            Object.DestroyImmediate(go);
            return prefab.GetComponent<Spit>();
        }

        // XP gem: a spinning glowing octahedron (Babylon buildGem, polyhedron type 1, size 0.24).
        static Gem MakeGemPrefab(Material mat)
        {
            string meshPath = GenDir + "/GemMesh.asset";
            AssetDatabase.DeleteAsset(meshPath);
            var mesh = Octahedron(0.24f);
            AssetDatabase.CreateAsset(mesh, meshPath);
            var go = new GameObject("Gem");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            go.AddComponent<Gem>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + "/Gem.prefab");
            Object.DestroyImmediate(go);
            return prefab.GetComponent<Gem>();
        }

        // Heart: a glowing camera-facing heart card. Coin: a spinning gold disc.
        static Pickup MakePickupPrefab(string name, bool heart, Material mat, Material spriteMat, Sprite blob)
        {
            var go = new GameObject(name);
            var pickup = go.AddComponent<Pickup>();
            pickup.isHeart = heart;
            GameObject visual;
            if (heart)
            {
                visual = GameObject.CreatePrimitive(PrimitiveType.Quad);
                visual.transform.localScale = Vector3.one * 0.6f;
                visual.AddComponent<Billboard>();
            }
            else
            {
                visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                visual.transform.localScale = new Vector3(0.42f, 0.03f, 0.42f);
                visual.transform.localRotation = Quaternion.Euler(90, 0, 0);
            }
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.name = "Visual";
            visual.transform.SetParent(go.transform, false);
            var mr = visual.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            pickup.visual = mr;
            var shadow = MakeDecal("Shadow", go.transform, spriteMat, blob, new Vector2(0.6f, 0.6f), new Color(0, 0, 0, 0.5f), -1);
            shadow.transform.localPosition = new Vector3(0, -0.68f, 0);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + "/" + name + ".prefab");
            Object.DestroyImmediate(go);
            return prefab.GetComponent<Pickup>();
        }

        // Flat-shaded octahedron with radius r.
        static Mesh Octahedron(float r)
        {
            var px = new Vector3(r, 0, 0); var nx = -px;
            var py = new Vector3(0, r, 0); var ny = -py;
            var pz = new Vector3(0, 0, r); var nz = -pz;
            var faces = new[]
            {
                (py, pz, px), (py, px, nz), (py, nz, nx), (py, nx, pz),
                (ny, px, pz), (ny, nz, px), (ny, nx, nz), (ny, pz, nx),
            };
            var verts = new System.Collections.Generic.List<Vector3>();
            var tris = new System.Collections.Generic.List<int>();
            foreach (var (a, b, c) in faces)
            {
                tris.Add(verts.Count); verts.Add(a);
                tris.Add(verts.Count); verts.Add(b);
                tris.Add(verts.Count); verts.Add(c);
            }
            var mesh = new Mesh { name = "Gem" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static Enemy MakeEnemyPrefab(Material spriteMat, Sprite blob)
        {
            var go = new GameObject("Enemy");
            var enemy = go.AddComponent<Enemy>();
            enemy.body = MakeSprite("Body", go.transform, spriteMat, LoadSprite("Assets/Art/Enemies/grunt_walk0.png"), 0);
            enemy.billboard = enemy.body.gameObject.AddComponent<Billboard>();
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
        // big wave number in the centre, score and coins on the right, ability chips and
        // the XP bar along the bottom, boss bar under the wave.
        static Hud BuildHud()
        {
            var canvasGo = NewCanvas("HUD", 0);
            var hud = canvasGo.AddComponent<Hud>();
            var t = canvasGo.transform;
            var top = new Vector2(0, 1);

            // Health, top left.
            hud.healthText = Label(t, "Health", top, new Vector2(18, -14), new Vector2(300, 16), 11, TextAnchor.UpperLeft, GameConfig.TextDim);
            hud.healthFill = Meter(t, "HealthBar", top, new Vector2(18, -34), new Vector2(262, 12), GameConfig.HealthBack, GameConfig.Health);

            // Wave, top centre.
            Label(t, "WaveLabel", new Vector2(0.5f, 1), new Vector2(-100, -14), new Vector2(200, 16), 11, TextAnchor.UpperCenter, GameConfig.TextDim).text = "WAVE";
            hud.waveText = Label(t, "Wave", new Vector2(0.5f, 1), new Vector2(-100, -29), new Vector2(200, 40), 30, TextAnchor.UpperCenter, GameConfig.TextBright);
            hud.waveText.fontStyle = FontStyle.Bold;

            // Boss bar under the wave (hidden until the Colossus arrives).
            var boss = new GameObject("Boss", typeof(RectTransform));
            var bossRt = (RectTransform)boss.transform;
            bossRt.SetParent(t, false);
            bossRt.anchorMin = bossRt.anchorMax = new Vector2(0.5f, 1);
            bossRt.pivot = top;
            bossRt.anchoredPosition = new Vector2(-260, -84);
            bossRt.sizeDelta = new Vector2(520, 34);
            Label(bossRt, "Name", top, Vector2.zero, new Vector2(520, 16), 11, TextAnchor.UpperCenter, GameConfig.Health).text = "WARDEN COLOSSUS";
            hud.bossFill = Meter(bossRt, "Bar", top, new Vector2(0, -18), new Vector2(520, 14), GameConfig.HealthBack, GameConfig.Hex(0xff3344));
            hud.bossBar = boss;
            boss.SetActive(false);

            // Score and coins, top right.
            Label(t, "ScoreLabel", new Vector2(1, 1), new Vector2(-218, -14), new Vector2(200, 16), 11, TextAnchor.UpperRight, GameConfig.TextDim).text = "SCORE";
            hud.scoreText = Label(t, "Score", new Vector2(1, 1), new Vector2(-218, -30), new Vector2(200, 34), 24, TextAnchor.UpperRight, GameConfig.TextBright);
            hud.scoreText.fontStyle = FontStyle.Bold;
            hud.coinsText = Label(t, "Coins", new Vector2(1, 1), new Vector2(-218, -60), new Vector2(200, 18), 13, TextAnchor.UpperRight, GameConfig.Gold);
            hud.coinsText.fontStyle = FontStyle.Bold;
            var dot = Panel(t, "CoinDot", new Vector2(1, 1), new Vector2(-48, -63), new Vector2(10, 10), GameConfig.Gold);
            dot.GetComponent<Image>().sprite = LoadSprite(GenDir + "/dot.png");

            // Ability chips and XP bar, bottom.
            (hud.dashText, hud.dashBar) = Chip(t, "DashChip", new Vector2(18, 66), 108);
            (hud.bombText, hud.bombBar) = Chip(t, "BombChip", new Vector2(134, 66), 120);
            hud.levelText = Label(t, "Level", Vector2.zero, new Vector2(18, 30), new Vector2(60, 18), 14, TextAnchor.UpperLeft, GameConfig.Accent);
            hud.levelText.fontStyle = FontStyle.Bold;
            var xp = Meter(t, "XpBar", Vector2.zero, new Vector2(70, 26), new Vector2(1192, 10), new Color(0.078f, 0.106f, 0.188f, 0.85f), GameConfig.Accent);
            var xpRt = (RectTransform)xp.parent.parent;
            xpRt.anchorMax = new Vector2(1, 0);
            xpRt.offsetMax = new Vector2(-18, xpRt.offsetMax.y);
            hud.xpFill = xp;

            // Touch scheme: floating stick plus round DASH and BOMB buttons on the right.
            var touch = canvasGo.AddComponent<TouchControls>();
            touch.canvasRect = (RectTransform)t;
            touch.desktopOnly = new[] { hud.dashText.transform.parent.parent.gameObject, hud.bombText.transform.parent.parent.gameObject };
            var ring = LoadSprite(GenDir + "/ring.png");
            var dotSprite = LoadSprite(GenDir + "/dot.png");
            var joy = Panel(t, "Stick", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(112, 112), new Color(0.9f, 0.93f, 1f, 0.35f));
            joy.pivot = new Vector2(0.5f, 0.5f);
            joy.GetComponent<Image>().sprite = ring;
            var knob = Panel(joy, "Knob", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(46, 46), new Color(GameConfig.Accent.r, GameConfig.Accent.g, GameConfig.Accent.b, 0.45f));
            knob.pivot = new Vector2(0.5f, 0.5f);
            knob.GetComponent<Image>().sprite = dotSprite;
            joy.gameObject.SetActive(false);
            touch.joy = joy;
            touch.knob = knob;
            (touch.dashButton, touch.dashGroup) = TouchButton(t, "DashButton", "DASH", GameConfig.Accent, 64, ring, dotSprite);
            (touch.bombButton, touch.bombGroup) = TouchButton(t, "BombButton", "BOMB", GameConfig.Gold, 150, ring, dotSprite);
            hud.touch = touch;

            // Centre banner at ~30% from the top, white with a cyan glow.
            hud.bannerText = Label(t, "Banner", new Vector2(0.5f, 0.5f), new Vector2(-600, 230), new Vector2(1200, 160), 60, TextAnchor.MiddleCenter, GameConfig.TextBright);
            hud.bannerText.fontStyle = FontStyle.Bold;
            hud.bannerText.lineSpacing = 1.1f;
            hud.bannerGlow = hud.bannerText.gameObject.AddComponent<Outline>();
            hud.bannerGlow.effectDistance = new Vector2(1.5f, -1.5f);
            hud.bannerGlow.effectColor = new Color(GameConfig.Accent.r, GameConfig.Accent.g, GameConfig.Accent.b, 0.2f);
            hud.bannerText.enabled = false;

            var reticle = Panel(t, "Reticle", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30, 30), GameConfig.Accent);
            reticle.pivot = new Vector2(0.5f, 0.5f);
            reticle.GetComponent<Image>().sprite = LoadSprite(GenDir + "/reticle.png");
            reticle.gameObject.AddComponent<Shadow>().effectColor = new Color(GameConfig.Accent.r, GameConfig.Accent.g, GameConfig.Accent.b, 0.5f);
            hud.reticle = reticle;
            return hud;
        }

        // A 74-unit round touch button on the right edge, `bottom` units up (Babylon .touch-btn).
        static (RectTransform, CanvasGroup) TouchButton(Transform parent, string name, string text, Color color, float bottom, Sprite ring, Sprite dot)
        {
            var b = Panel(parent, name, new Vector2(1, 0), new Vector2(-18 - 74, bottom + 74), new Vector2(74, 74), new Color(color.r, color.g, color.b, 0.12f));
            var img = b.GetComponent<Image>();
            img.sprite = dot;
            var edge = Panel(b, "Ring", new Vector2(0, 1), Vector2.zero, new Vector2(74, 74), color);
            edge.GetComponent<Image>().sprite = ring;
            var label = Text(b, "Label", 0, 0, 74, 74, 12, color, text, FontStyle.Bold);
            label.alignment = TextAnchor.MiddleCenter;
            var group = b.gameObject.AddComponent<CanvasGroup>();
            b.gameObject.SetActive(false);
            return (b, group);
        }

        // Overlays from index.html: menu, upgrade picker, pause and result.
        static Menus BuildMenus()
        {
            var canvasGo = NewCanvas("Menus", 10);
            canvasGo.AddComponent<GraphicRaycaster>();
            var menus = canvasGo.AddComponent<Menus>();
            var t = canvasGo.transform;

            // Main menu.
            var (menu, mp) = Overlay(t, "Menu", new Vector2(560, 470));
            Text(mp, "Eyebrow", 28, 24, 500, 16, 11, GameConfig.Accent, "UNITY  ·  URP 3D", FontStyle.Bold);
            Text(mp, "Title", 28, 46, 500, 76, 64, GameConfig.TextBright, "WARDEN <color=#4fd1ff>ZERO</color>", FontStyle.Bold);
            Text(mp, "Tag", 28, 126, 500, 20, 15, GameConfig.TextDim, "Hold the line. Collect the gems. Level up. Crush the horde.", FontStyle.Normal);
            string[,] controls =
            {
                { "WASD", "Move" }, { "Mouse", "Aim · hold left click to fire" }, { "Space", "Dash (brief invulnerability)" },
                { "E / Right click", "Bomb" }, { "Esc / P", "Pause" }, { "1 2 3", "Pick upgrade on level-up" },
            };
            menus.controlKeys = new Text[controls.GetLength(0)];
            menus.controlDescs = new Text[controls.GetLength(0)];
            for (int i = 0; i < controls.GetLength(0); i++)
            {
                menus.controlKeys[i] = Text(mp, "Key" + i, 28, 168 + i * 24, 140, 20, 13, GameConfig.TextBright, controls[i, 0], FontStyle.Bold);
                menus.controlDescs[i] = Text(mp, "Does" + i, 170, 168 + i * 24, 360, 20, 13, GameConfig.TextDim, controls[i, 1], FontStyle.Normal);
            }
            menus.playButton = MakeButton(mp, "Play", 28, 330, 504, 50, "PLAY", true);
            menus.greenfangButton = MakeButton(mp, "Greenfang", 28, 392, 504, 50, "OPERATION GREENFANG", false);
            menus.menuPanel = menu;

            // Upgrade picker.
            var (upgrade, up) = Overlay(t, "Upgrade", new Vector2(560, 400));
            menus.upgradeLevel = Text(up, "Eyebrow", 28, 24, 500, 16, 11, GameConfig.Accent, "LEVEL 2", FontStyle.Bold);
            Text(up, "Heading", 28, 44, 500, 34, 26, GameConfig.TextBright, "Choose an upgrade", FontStyle.Bold);
            menus.cards = new Button[3];
            menus.cardTitles = new Text[3];
            menus.cardDescriptions = new Text[3];
            menus.cardStacks = new Text[3];
            for (int i = 0; i < 3; i++)
            {
                var card = MakeButton(up, "Card" + i, 28, 96 + i * 96, 504, 84, "", false);
                var c = card.transform;
                card.GetComponent<Image>().color = GameConfig.BgMid;
                Text(c, "Key", 0, 0, 52, 84, 20, GameConfig.Accent, (i + 1).ToString(), FontStyle.Bold).alignment = TextAnchor.MiddleCenter;
                menus.cardTitles[i] = Text(c, "Name", 56, 18, 360, 22, 16, GameConfig.TextBright, "Upgrade", FontStyle.Bold);
                menus.cardDescriptions[i] = Text(c, "Desc", 56, 44, 360, 20, 13, GameConfig.TextDim, "Description", FontStyle.Normal);
                menus.cardStacks[i] = Text(c, "Stacks", 420, 0, 70, 84, 11, GameConfig.Gold, "0/3", FontStyle.Bold);
                menus.cardStacks[i].alignment = TextAnchor.MiddleRight;
                menus.cards[i] = card;
            }
            menus.upgradePanel = upgrade;

            // Pause.
            var (pause, pp) = Overlay(t, "Pause", new Vector2(420, 170));
            Text(pp, "Heading", 28, 24, 360, 34, 26, GameConfig.TextBright, "Paused", FontStyle.Bold);
            menus.resumeButton = MakeButton(pp, "Resume", 28, 86, 170, 48, "RESUME", true);
            menus.pauseMenuButton = MakeButton(pp, "MainMenu", 210, 86, 182, 48, "MAIN MENU", false);
            menus.pausePanel = pause;

            // Result.
            var (result, rp) = Overlay(t, "Result", new Vector2(460, 290));
            menus.resultEyebrow = Text(rp, "Eyebrow", 28, 24, 400, 16, 11, GameConfig.Accent, "RUN OVER", FontStyle.Bold);
            menus.resultTitle = Text(rp, "Title", 28, 44, 400, 34, 26, GameConfig.TextBright, "The line broke", FontStyle.Bold);
            string[] stats = { "WAVE", "SCORE", "KILLS", "LEVEL", "TIME" };
            var values = new Text[stats.Length];
            for (int i = 0; i < stats.Length; i++)
            {
                var label = Text(rp, stats[i] + "Label", 28 + i * 82, 100, 80, 14, 11, GameConfig.TextDim, stats[i], FontStyle.Bold);
                if (i == 0) menus.resultWaveLabel = label;
                values[i] = Text(rp, stats[i], 28 + i * 82, 118, 80, 30, 22, GameConfig.TextBright, "0", FontStyle.Bold);
            }
            menus.resultWave = values[0];
            menus.resultScore = values[1];
            menus.resultKills = values[2];
            menus.resultLevel = values[3];
            menus.resultTime = values[4];
            menus.retryButton = MakeButton(rp, "Retry", 28, 196, 190, 50, "PLAY AGAIN", true);
            menus.resultMenuButton = MakeButton(rp, "MainMenu", 230, 196, 202, 50, "MAIN MENU", false);
            menus.resultPanel = result;

            menus.HideAll();
            return menus;
        }

        static GameObject NewCanvas(string name, int order)
        {
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            return go;
        }

        // Full-screen dim backdrop with a centred bordered panel; returns (overlay, panel content).
        static (GameObject, Transform) Overlay(Transform parent, string name, Vector2 size)
        {
            var bg = Panel(parent, name, Vector2.zero, Vector2.zero, Vector2.zero, new Color(0.02f, 0.027f, 0.06f, 0.9f));
            bg.anchorMin = Vector2.zero;
            bg.anchorMax = Vector2.one;
            bg.offsetMin = bg.offsetMax = Vector2.zero;
            bg.GetComponent<Image>().raycastTarget = true;
            var border = Panel(bg, "Border", new Vector2(0.5f, 0.5f), new Vector2(-size.x / 2 - 1, size.y / 2 + 1), size + Vector2.one * 2, GameConfig.PanelEdge);
            var panel = Panel(border, "Panel", new Vector2(0, 1), new Vector2(1, -1), size, GameConfig.Panel);
            return (bg.gameObject, panel);
        }

        // Text placed by its top-left corner inside a panel (x right, y down).
        static Text Text(Transform parent, string name, float x, float y, float w, float h, int size, Color color, string text, FontStyle style)
        {
            var label = Label(parent, name, new Vector2(0, 1), new Vector2(x, -y), new Vector2(w, h), size, TextAnchor.UpperLeft, color);
            label.text = text;
            label.fontStyle = style;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            return label;
        }

        static Button MakeButton(Transform parent, string name, float x, float y, float w, float h, string text, bool primary)
        {
            var border = Panel(parent, name, new Vector2(0, 1), new Vector2(x, -y), new Vector2(w, h), primary ? GameConfig.Accent : GameConfig.PanelEdge);
            var img = border.GetComponent<Image>();
            img.raycastTarget = true;
            var inner = Panel(border, "Fill", new Vector2(0, 1), new Vector2(1, -1), new Vector2(w - 2, h - 2), primary ? GameConfig.Accent : GameConfig.Panel);
            var button = border.gameObject.AddComponent<Button>();
            button.targetGraphic = inner.GetComponent<Image>();
            var colors = button.colors;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1);
            button.colors = colors;
            if (text.Length > 0)
            {
                var label = Text(border, "Label", 0, 0, w, h, 15, primary ? GameConfig.Hex(0x05101a) : GameConfig.TextBright, text, FontStyle.Bold);
                label.alignment = TextAnchor.MiddleCenter;
                label.GetComponent<Shadow>().enabled = false;
            }
            return button;
        }

        // A thin bordered meter; returns the fill's RectTransform (scale it with anchorMax.x).
        static RectTransform Meter(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color back, Color fill)
        {
            var border = Panel(parent, name, anchor, pos, size + Vector2.one * 2, GameConfig.PanelEdge);
            var bg = Panel(border, "Back", new Vector2(0, 1), new Vector2(1, -1), size, back);
            bg.anchorMax = new Vector2(1, 1);
            bg.offsetMax = new Vector2(-1, bg.offsetMax.y);
            var f = Panel(bg, "Fill", Vector2.zero, Vector2.zero, Vector2.zero, fill);
            f.anchorMin = Vector2.zero;
            f.anchorMax = Vector2.one;
            f.offsetMin = f.offsetMax = Vector2.zero;
            return f;
        }

        // Ability chip (bottom left) with a cooldown underline; returns (label, underline).
        static (Text, Image) Chip(Transform parent, string name, Vector2 pos, float width)
        {
            var border = Panel(parent, name, Vector2.zero, pos, new Vector2(width, 28), GameConfig.PanelEdge);
            var chip = Panel(border, "Inner", new Vector2(0, 1), new Vector2(1, -1), new Vector2(width - 2, 26), new Color(0.078f, 0.106f, 0.188f, 0.92f));
            var label = Label(chip, "Text", new Vector2(0, 1), new Vector2(10, -6), new Vector2(width - 12, 16), 12, TextAnchor.UpperLeft, GameConfig.Accent);
            var bar = Panel(chip, "Cooldown", Vector2.zero, Vector2.zero, Vector2.zero, GameConfig.Accent);
            bar.anchorMin = Vector2.zero;
            bar.anchorMax = new Vector2(1, 0);
            bar.pivot = Vector2.zero;
            bar.offsetMin = Vector2.zero;
            bar.offsetMax = new Vector2(0, 2);
            return (label, bar.GetComponent<Image>());
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

        static void Emissive(Material mat, Color hdr)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", hdr);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }

        // URP Unlit with alpha clipping: an opaque card whose HDR colour blooms.
        static Material CutoutMaterial(string name, Texture2D tex, Color hdr)
        {
            var mat = SaveMaterial(name, Unlit(), hdr);
            mat.SetTexture("_BaseMap", tex);
            mat.SetFloat("_AlphaClip", 1);
            mat.SetFloat("_Cutoff", 0.5f);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.renderQueue = (int)RenderQueue.AlphaTest;
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

            // Heart: the implicit curve (x^2 + y^2 - 1)^3 - x^2 y^3 <= 0, with a soft edge.
            var heart = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            var dotTex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            for (int y = 0; y < 128; y++)
            for (int x = 0; x < 128; x++)
            {
                float hx = (x - 63.5f) / 46f, hy = (y - 58f) / 46f;
                float f = Mathf.Pow(hx * hx + hy * hy - 1, 3) - hx * hx * hy * hy * hy;
                heart.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(-f * 40)));
            }
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                float d = new Vector2(x - 15.5f, y - 15.5f).magnitude;
                dotTex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(15.5f - d)));
            }
            File.WriteAllBytes(GenDir + "/heart.png", heart.EncodeToPNG());
            File.WriteAllBytes(GenDir + "/dot.png", dotTex.EncodeToPNG());
            File.WriteAllBytes(GenDir + "/reticle.png", reticle.EncodeToPNG());
            File.WriteAllBytes(GenDir + "/spark.png", spark.EncodeToPNG());
            AssetDatabase.Refresh();
        }
    }
}
