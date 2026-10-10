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
    // Generates the Arena scene, its materials, generated textures and prefabs from code,
    // so the whole level is reproducible. Run from the menu or in batch mode:
    //   -executeMethod WardenZero.EditorTools.SceneBuilder.Build
    public static class SceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Arena.unity";
        const string GenDir = "Assets/Art/Generated";
        const string MatDir = "Assets/Materials";
        const string PrefabDir = "Assets/Prefabs";

        [MenuItem("Warden Zero/Rebuild Arena Scene")]
        public static void Build()
        {
            Directory.CreateDirectory(GenDir);
            Directory.CreateDirectory(MatDir);
            Directory.CreateDirectory(PrefabDir);
            WriteGeneratedTextures();
            AssetDatabase.ImportAsset("Assets/Art", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            ConfigurePipeline();

            // Materials
            var spriteMat = SaveMaterial("SpriteUnlit", Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"), Color.white);
            var floorMat = SaveMaterial("Floor", Lit(), Color.white);
            floorMat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(GenDir + "/deck.png"));
            floorMat.SetTextureScale("_BaseMap", new Vector2((GameConfig.ArenaW + 40) / 8, (GameConfig.ArenaD + 40) / 8));
            floorMat.SetFloat("_Smoothness", 0.2f);
            var wallMat = SaveMaterial("Wall", Lit(), GameConfig.PanelEdge);
            wallMat.SetFloat("_Smoothness", 0.3f);
            var wallTopMat = SaveMaterial("WallTop", Lit(), GameConfig.BgMid);
            wallTopMat.SetFloat("_Smoothness", 0.1f);
            var barrierMat = SaveMaterial("Barrier", Lit(), GameConfig.Panel);
            barrierMat.SetFloat("_Smoothness", 0.2f);
            var rimMat = SaveMaterial("Rim", Unlit(), GameConfig.Accent * 0.9f);
            var stripMat = SaveMaterial("Strip", Unlit(), GameConfig.Magenta * 0.9f);
            var boltMat = SaveMaterial("Bolt", Unlit(), new Color(0.65f, 1f, 1f));
            AssetDatabase.SaveAssets();

            var blob = LoadSprite(GenDir + "/blob.png");
            var ring = LoadSprite(GenDir + "/ring.png");

            var boltPrefab = MakeBoltPrefab(boltMat);
            var enemyPrefab = MakeEnemyPrefab(spriteMat, blob);

            // Scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.36f, 0.43f, 0.6f);
            RenderSettings.ambientEquatorColor = new Color(0.16f, 0.18f, 0.26f);
            RenderSettings.ambientGroundColor = new Color(0.04f, 0.05f, 0.1f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.011f;
            RenderSettings.fogColor = GameConfig.BgDeep;

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(0.86f, 0.9f, 1f);
            sun.intensity = 1.4f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.LookRotation(new Vector3(-0.45f, -1f, -0.3f));

            BuildArena(floorMat, wallMat, wallTopMat, rimMat, barrierMat, stripMat, spriteMat, ring);

            // Camera
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = GameConfig.BgDeep;
            cam.fieldOfView = GameConfig.CameraFovDegrees;
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = 260;
            camGo.AddComponent<UniversalAdditionalCameraData>();
            camGo.AddComponent<AudioListener>();
            camGo.transform.position = GameConfig.CameraOffset;
            camGo.transform.LookAt(new Vector3(0, 0, GameConfig.CameraLookAhead));
            var follow = camGo.AddComponent<CameraFollow>();

            // Warden
            var warden = new GameObject("Warden");
            var player = warden.AddComponent<PlayerController>();
            player.cam = cam;
            // Order 1 keeps the Warden readable when a crowd overlaps him.
            player.body = MakeSprite("Body", warden.transform, spriteMat, LoadSprite("Assets/Art/Hero/idle.png"), 1);
            player.body.gameObject.AddComponent<Billboard>();
            MakeDecal("Shadow", warden.transform, spriteMat, blob, new Vector2(1.5f, 1f), new Color(0, 0, 0, 0.7f), -1);
            var glow = new GameObject("Glow").AddComponent<Light>();
            glow.transform.SetParent(warden.transform, false);
            glow.transform.localPosition = new Vector3(0, 2.4f, 0);
            glow.type = LightType.Point;
            glow.color = GameConfig.Accent;
            glow.range = 12;
            glow.intensity = 6;
            player.reticle = MakeDecal("Reticle", null, spriteMat, ring, Vector2.one * 0.9f, GameConfig.Accent, -1).transform;
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

            // Game manager + HUD
            var gmGo = new GameObject("GameManager");
            var gm = gmGo.AddComponent<GameManager>();
            gm.player = player;
            gm.cameraFollow = follow;
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

        static void BuildArena(Material floor, Material wall, Material wallTop, Material rim, Material barrier, Material strip, Material spriteMat, Sprite ring)
        {
            var root = new GameObject("Arena").transform;

            var ground = Box("Floor", root, floor, new Vector3(0, -0.05f, 0), new Vector3(GameConfig.ArenaW + 40, 0.1f, GameConfig.ArenaD + 40));
            ground.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            // A box's top face maps the texture once, so tiling comes from the material scale.

            var spawnRing = MakeDecal("SpawnRing", root, spriteMat, ring, Vector2.one * 7, GameConfig.Accent * 0.6f, -2);
            spawnRing.transform.position = new Vector3(0, 0.02f, 0);

            const float h = GameConfig.WallHeight;
            const float r = 0.07f;
            for (int i = 0; i < GameConfig.Walls.Length; i++)
            {
                var w = GameConfig.Walls[i];
                float sx = w.MaxX - w.MinX, sz = w.MaxZ - w.MinZ;
                var c = new Vector3((w.MinX + w.MaxX) / 2, 0, (w.MinZ + w.MaxZ) / 2);
                var block = Box("Wall" + i, root, wall, c + Vector3.up * h / 2, new Vector3(sx, h, sz)).transform;
                Box("Top", block, wallTop, c + Vector3.up * (h + 0.02f), new Vector3(sx - 0.1f, 0.04f, sz - 0.1f));
                // Thin glowing rim around the top edge.
                Box("Rim", block, rim, c + new Vector3(0, h + 0.02f, sz / 2), new Vector3(sx + r, 0.06f, r));
                Box("Rim", block, rim, c + new Vector3(0, h + 0.02f, -sz / 2), new Vector3(sx + r, 0.06f, r));
                Box("Rim", block, rim, c + new Vector3(sx / 2, h + 0.02f, 0), new Vector3(r, 0.06f, sz + r));
                Box("Rim", block, rim, c + new Vector3(-sx / 2, h + 0.02f, 0), new Vector3(r, 0.06f, sz + r));
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
                var b = Box("Barrier", root, barrier, pos + Vector3.up * 0.75f, new Vector3(size.x, 1.5f, size.z)).transform;
                Box("Strip", b, strip, pos + Vector3.up * 1.52f, new Vector3(size.x + 0.02f, 0.06f, size.z + 0.02f));
            }
        }

        // ------------------------------------------------------------------ prefabs

        static Bolt MakeBoltPrefab(Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = "Bolt";
            go.transform.localScale = new Vector3(0.22f, 0.22f, 0.75f);
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
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
            MakeDecal("Shadow", go.transform, spriteMat, blob, new Vector2(1.2f, 0.8f), new Color(0, 0, 0, 0.6f), -1);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + "/Enemy.prefab");
            Object.DestroyImmediate(go);
            return prefab.GetComponent<Enemy>();
        }

        // ------------------------------------------------------------------ HUD

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

            // Health bar, top left.
            Label(t, "HealthLabel", new Vector2(0, 1), new Vector2(24, -18), new Vector2(300, 22), 16, TextAnchor.LowerLeft, GameConfig.TextDim).text = "WARDEN";
            var back = Panel(t, "HealthBack", new Vector2(0, 1), new Vector2(24, -42), new Vector2(300, 20), GameConfig.HealthBack);
            var fill = Panel(back, "HealthFill", Vector2.zero, Vector2.zero, Vector2.zero, GameConfig.Health);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            hud.healthFill = fill;
            hud.healthText = Label(back, "HealthText", new Vector2(0, 1), Vector2.zero, new Vector2(300, 20), 14, TextAnchor.MiddleCenter, GameConfig.TextBright);
            hud.dashText = Label(t, "Dash", new Vector2(0, 1), new Vector2(24, -68), new Vector2(300, 22), 15, TextAnchor.UpperLeft, GameConfig.Accent);

            hud.waveText = Label(t, "Wave", new Vector2(0.5f, 1), new Vector2(-150, -16), new Vector2(300, 40), 28, TextAnchor.UpperCenter, GameConfig.TextBright);
            hud.scoreText = Label(t, "Score", new Vector2(1, 1), new Vector2(-324, -16), new Vector2(300, 40), 26, TextAnchor.UpperRight, GameConfig.Gold);
            hud.bannerText = Label(t, "Banner", new Vector2(0.5f, 0.5f), new Vector2(-500, 150), new Vector2(1000, 300), 54, TextAnchor.MiddleCenter, GameConfig.Accent);
            hud.bannerText.fontStyle = FontStyle.Bold;
            hud.bannerText.enabled = false;
            Label(t, "Hint", new Vector2(0.5f, 0), new Vector2(-400, 40), new Vector2(800, 28), 15, TextAnchor.LowerCenter, GameConfig.TextDim).text =
                "WASD move   ·   mouse aim   ·   hold left mouse fire   ·   SPACE dash";
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
            go.GetComponent<Image>().color = color;
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
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            go.GetComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.8f);
            return text;
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

        // A sprite lying flat on the ground (shadows, rings).
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

        static Shader Lit() => Shader.Find("Universal Render Pipeline/Lit");
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

        // WebGL uses the "Mobile" quality level; render it at full resolution with MSAA
        // and a sharper shadow map over a shorter distance (the camera sees ~40 m).
        static void ConfigurePipeline()
        {
            for (int i = 0; i < QualitySettings.count; i++)
            {
                if (QualitySettings.GetRenderPipelineAssetAt(i) is UniversalRenderPipelineAsset urp)
                {
                    urp.renderScale = 1f;
                    urp.msaaSampleCount = 4;
                    urp.shadowDistance = 45;
                    urp.mainLightShadowmapResolution = 2048;
                    EditorUtility.SetDirty(urp);
                }
            }
        }

        // Procedural textures: deck floor plates, soft shadow blob and a thin ring.
        static void WriteGeneratedTextures()
        {
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
            File.WriteAllBytes(GenDir + "/deck.png", deck.EncodeToPNG());

            const int R = 128;
            var blob = new Texture2D(R, R, TextureFormat.RGBA32, false);
            var ring = new Texture2D(R, R, TextureFormat.RGBA32, false);
            for (int y = 0; y < R; y++)
            for (int x = 0; x < R; x++)
            {
                float d = new Vector2(x - R / 2 + 0.5f, y - R / 2 + 0.5f).magnitude / (R / 2);
                blob.SetPixel(x, y, new Color(0, 0, 0, Mathf.Pow(Mathf.Clamp01(1 - d), 1.5f)));
                float band = Mathf.Clamp01(1 - Mathf.Abs(d - 0.9f) / 0.06f);
                ring.SetPixel(x, y, new Color(1, 1, 1, band));
            }
            File.WriteAllBytes(GenDir + "/blob.png", blob.EncodeToPNG());
            File.WriteAllBytes(GenDir + "/ring.png", ring.EncodeToPNG());
            AssetDatabase.Refresh();
        }
    }
}
