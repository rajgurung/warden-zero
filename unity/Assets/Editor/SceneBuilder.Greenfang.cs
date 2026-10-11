using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace WardenZero.EditorTools
{
    // Operation Greenfang's scene: the v1 jungle (e3bf116:src/scenes/JungleScene.ts) rebuilt in
    // the same 3D look as the arena. Shares the Warden, camera, effects, HUD and menus (CreateCore).
    public static partial class SceneBuilder
    {
        const string GreenfangPath = "Assets/Scenes/Greenfang.unity";

        // v1 jungle palette.
        static readonly Color JungleSky = GameConfig.Hex(0x0a1a0f);
        static readonly Color GroundBase = GameConfig.Hex(0x16331c);
        static readonly Color CanopyDark = GameConfig.Hex(0x123a1d);
        static readonly Color CanopyMid = GameConfig.Hex(0x1d5329);
        static readonly Color CanopyLight = GameConfig.Hex(0x2f7a3c);
        static readonly Color TrunkBrown = GameConfig.Hex(0x4a3422);
        static readonly Color MissionGreen = GameConfig.Hex(0x9bff67);

        static void BuildGreenfangScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.32f, 0.45f, 0.3f);
            RenderSettings.ambientEquatorColor = new Color(0.08f, 0.14f, 0.08f);
            RenderSettings.ambientGroundColor = new Color(0.03f, 0.05f, 0.03f);
            RenderSettings.reflectionIntensity = 0;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.012f;
            RenderSettings.fogColor = JungleSky;

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.8f);
            sun.intensity = 1.5f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            sun.transform.rotation = Quaternion.LookRotation(new Vector3(-0.45f, -1f, -0.3f));

            var volume = new GameObject("PostFX").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;

            var trunks = BuildJungle(out var beaconRings);

            var gm = CreateCore("", CameraFollow.View.High); // its strikes are aimed from above
            var player = gm.player;
            player.cam.backgroundColor = JungleSky;

            // Mission, strikes and their visuals.
            var missionGo = new GameObject("Mission");
            var mission = missionGo.AddComponent<GreenfangMission>();
            var strikes = missionGo.AddComponent<StrikeSystem>();
            mission.strikes = strikes;
            mission.trunks = trunks;
            mission.beaconRings = beaconRings;
            mission.strikeReady = Clip("strike_ready");
            mission.poundRing = GlowQuad("PoundTell", missionGo.transform, GlowMaterial("PoundTell", ringTex, Hdr(GameConfig.Hex(0xff5a4a), 2f), FxQueue), 1).GetComponent<MeshRenderer>();
            mission.poundRing.enabled = false;
            strikes.zoneRing = GlowQuad("ArtilleryZone", missionGo.transform, GlowMaterial("ArtilleryZone", ringTex, Hdr(GameConfig.Gold, 1.8f), GroundGlowQueue), 1).GetComponent<MeshRenderer>();
            strikes.zoneRing.enabled = false;
            var line = GlowQuad("AirLine", missionGo.transform, GlowMaterial("AirLine", AssetDatabase.LoadAssetAtPath<Texture2D>(GenDir + "/strip.png"), Hdr(GameConfig.Accent, 0.05f), GroundGlowQueue), 1);
            line.SetActive(false);
            strikes.airLine = line.transform;
            strikes.jet = MakeJet(missionGo.transform);
            strikes.scorchPrefab = MakeScorchPrefab();
            strikes.shellWhistle = Clip("shell_whistle");
            strikes.jetPass = Clip("jet_pass");
            strikes.strikeReady = Clip("strike_ready");
            mission.hud = BuildMissionHud(gm.hud);
            gm.mission = mission;

            // Fireflies drifting around the Warden (v1 atmosphere particles).
            MakeFireflies(player.transform);

            EditorSceneManager.SaveScene(scene, GreenfangPath);
        }

        // Ground, trees (trunks block the Warden), bushes, decor, beacons and a tree-line border.
        // Returns trunk obstacles as (x, z, radius).
        static Vector3[] BuildJungle(out Renderer[] beaconRings)
        {
            var root = new GameObject("Jungle").transform;
            float W = GreenfangMission.WorldW, D = GreenfangMission.WorldD;
            // Albedo lifted so the v1 greens read under the moody light and fog.
            var groundMat = LitMaterial("JungleGround", new Color(1.9f, 1.9f, 1.9f, 1), new Color(0.1f, 0.14f, 0.08f), 0.25f);
            groundMat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(GenDir + "/jungle.png"));
            groundMat.SetTextureScale("_BaseMap", new Vector2((W + 40) / 8, (D + 40) / 8));
            var ground = Box("Ground", root, groundMat, new Vector3(0, -0.05f, 0), new Vector3(W + 40, 0.1f, D + 40));
            ground.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            var trunkMat = LitMaterial("Trunk", TrunkBrown, new Color(0.05f, 0.04f, 0.03f), 0.2f);
            var canopy = new[]
            {
                LitMaterial("CanopyDark", CanopyDark, new Color(0.05f, 0.08f, 0.05f), 0.3f),
                LitMaterial("CanopyMid", CanopyMid, new Color(0.05f, 0.08f, 0.05f), 0.3f),
                LitMaterial("CanopyLight", CanopyLight, new Color(0.06f, 0.1f, 0.06f), 0.35f),
            };
            var rockMat = LitMaterial("Rock", GameConfig.Hex(0x3a4a3a), new Color(0.08f, 0.08f, 0.08f), 0.3f);
            var grassMat = LitMaterial("Grass", GameConfig.Hex(0x23502b), new Color(0.04f, 0.06f, 0.04f), 0.2f);

            var rng = new System.Random(11);
            float Range(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var trunks = new List<Vector3>();
            Vector3 start = GreenfangMission.PlayerStart;

            // Interior foliage with clearings around the start and each beacon (v1 scatterFoliage).
            int placed = 0;
            for (int attempt = 0; placed < 70 && attempt < 500; attempt++)
            {
                var p = GreenfangMission.ToWorld(Range(80, 2800 - 80), Range(120, 1900 - 60));
                if (Vector3.Distance(p, start) < 280 * GameConfig.PX) continue;
                bool nearBeacon = false;
                foreach (var b in GreenfangMission.Beacons) nearBeacon |= Vector3.Distance(p, b) < 220 * GameConfig.PX;
                if (nearBeacon) continue;
                bool isTree = rng.Next(3) > 0;
                if (isTree)
                {
                    float s = Range(0.85f, 1.35f);
                    Tree(root, p, s, trunkMat, canopy, rng);
                    trunks.Add(new Vector3(p.x, 0.5f * s, p.z));
                }
                else
                {
                    Bush(root, p, Range(0.7f, 1.1f), canopy, rng);
                }
                placed++;
            }

            // A dense tree line just outside the bounds marks the edge of the operation.
            for (float x = -W / 2; x <= W / 2; x += 5)
            {
                Tree(root, new Vector3(x + Range(-1, 1), 0, D / 2 + Range(1.5f, 3.5f)), Range(1f, 1.5f), trunkMat, canopy, rng);
                Tree(root, new Vector3(x + Range(-1, 1), 0, -D / 2 - Range(1.5f, 3.5f)), Range(1f, 1.5f), trunkMat, canopy, rng);
            }
            for (float z = -D / 2; z <= D / 2; z += 5)
            {
                Tree(root, new Vector3(W / 2 + Range(1.5f, 3.5f), 0, z + Range(-1, 1)), Range(1f, 1.5f), trunkMat, canopy, rng);
                Tree(root, new Vector3(-W / 2 - Range(1.5f, 3.5f), 0, z + Range(-1, 1)), Range(1f, 1.5f), trunkMat, canopy, rng);
            }

            // Rocks and grass tufts (v1 buildGround decor).
            var decor = new GameObject("Decor").transform;
            decor.SetParent(root, false);
            for (int i = 0; i < 140; i++)
            {
                var p = new Vector3(Range(-W / 2, W / 2), 0, Range(-D / 2, D / 2));
                bool rock = rng.Next(4) == 0;
                var go = GameObject.CreatePrimitive(rock ? PrimitiveType.Cube : PrimitiveType.Sphere);
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.name = rock ? "Rock" : "Grass";
                go.transform.SetParent(decor, false);
                float s = Range(0.25f, 0.6f);
                go.transform.position = p + Vector3.up * (rock ? s * 0.2f : 0.05f);
                go.transform.rotation = Quaternion.Euler(Range(-15, 15), Range(0, 360), Range(-15, 15));
                go.transform.localScale = rock ? new Vector3(s, s * 0.6f, s * 0.8f) : new Vector3(s * 1.6f, s * 0.5f, s * 1.6f);
                var mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterial = rock ? rockMat : grassMat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
            }

            // Beacons: a glowing pylon and a breathing capture ring (LZ in gold).
            var pylonMat = LitMaterial("Pylon", GameConfig.Panel, new Color(0.2f, 0.2f, 0.25f), 0.5f);
            beaconRings = new Renderer[3];
            for (int i = 0; i < 3; i++)
            {
                var b = GreenfangMission.Beacons[i];
                var color = i == 2 ? GameConfig.Gold : MissionGreen;
                var pylon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Object.DestroyImmediate(pylon.GetComponent<Collider>());
                pylon.name = "Beacon" + i;
                pylon.transform.SetParent(root, false);
                pylon.transform.position = b + Vector3.up * 0.8f;
                pylon.transform.localScale = new Vector3(0.45f, 0.8f, 0.45f);
                pylon.GetComponent<MeshRenderer>().sharedMaterial = pylonMat;
                var cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.DestroyImmediate(cap.GetComponent<Collider>());
                cap.name = "Light";
                cap.transform.SetParent(root, false);
                cap.transform.position = b + Vector3.up * 1.75f;
                cap.transform.localScale = Vector3.one * 0.45f;
                cap.GetComponent<MeshRenderer>().sharedMaterial = SaveMaterial("BeaconLight" + i, Unlit(), Hdr(color, 3));
                var ringMat = GlowMaterial("BeaconRing" + i, ringTex, Hdr(color, 1.6f), GroundGlowQueue);
                var ring = GlowQuad("Ring" + i, root, ringMat, GreenfangMission.BeaconRadius * 2);
                ring.transform.position = b + Vector3.up * 0.04f;
                beaconRings[i] = ring.GetComponent<MeshRenderer>();
            }
            return trunks.ToArray();
        }

        // Low-poly jungle tree: a trunk and three canopy blobs. Casts shadows.
        static void Tree(Transform parent, Vector3 p, float s, Material trunkMat, Material[] canopy, System.Random rng)
        {
            var tree = new GameObject("Tree").transform;
            tree.SetParent(parent, false);
            tree.position = p;
            tree.rotation = Quaternion.Euler(0, rng.Next(360), 0);
            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(trunk.GetComponent<Collider>());
            trunk.transform.SetParent(tree, false);
            trunk.transform.localPosition = new Vector3(0, 1.3f * s, 0);
            trunk.transform.localScale = new Vector3(0.5f * s, 1.3f * s, 0.5f * s);
            trunk.GetComponent<MeshRenderer>().sharedMaterial = trunkMat;
            for (int i = 0; i < 3; i++)
            {
                var blob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.DestroyImmediate(blob.GetComponent<Collider>());
                blob.transform.SetParent(tree, false);
                float a = i * 2.1f;
                blob.transform.localPosition = new Vector3(Mathf.Cos(a) * 0.7f * s, (2.7f + i * 0.35f) * s, Mathf.Sin(a) * 0.7f * s);
                blob.transform.localScale = new Vector3(2.4f, 1.6f, 2.4f) * s * (1 - i * 0.12f);
                blob.GetComponent<MeshRenderer>().sharedMaterial = canopy[i];
            }
        }

        static void Bush(Transform parent, Vector3 p, float s, Material[] canopy, System.Random rng)
        {
            var bush = new GameObject("Bush").transform;
            bush.SetParent(parent, false);
            bush.position = p;
            for (int i = 0; i < 3; i++)
            {
                var blob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.DestroyImmediate(blob.GetComponent<Collider>());
                blob.transform.SetParent(bush, false);
                float a = i * 2.1f + rng.Next(10);
                blob.transform.localPosition = new Vector3(Mathf.Cos(a) * 0.45f * s, 0.35f * s, Mathf.Sin(a) * 0.45f * s);
                blob.transform.localScale = Vector3.one * (0.9f + i * 0.15f) * s;
                blob.GetComponent<MeshRenderer>().sharedMaterial = canopy[(i + 1) % 3];
            }
        }

        // The strike jet: fuselage, wings, tail and a glowing engine trail.
        static Transform MakeJet(Transform parent)
        {
            var jet = new GameObject("Jet").transform;
            jet.SetParent(parent, false);
            var body = LitMaterial("JetBody", GameConfig.Hex(0x2a3040), new Color(0.3f, 0.32f, 0.38f), 0.6f);
            Box("Fuselage", jet, body, Vector3.zero, new Vector3(0.6f, 0.4f, 3.4f));
            Box("Wings", jet, body, new Vector3(0, 0, -0.2f), new Vector3(4.8f, 0.08f, 1.2f));
            Box("Tail", jet, body, new Vector3(0, 0.45f, -1.4f), new Vector3(0.08f, 0.8f, 0.6f));
            var engine = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(engine.GetComponent<Collider>());
            engine.transform.SetParent(jet, false);
            engine.transform.localPosition = new Vector3(0, 0, -1.75f);
            engine.transform.localScale = Vector3.one * 0.4f;
            engine.GetComponent<MeshRenderer>().sharedMaterial = SaveMaterial("JetEngine", Unlit(), Hdr(GameConfig.Accent, 4));
            var trail = engine.AddComponent<TrailRenderer>();
            trail.sharedMaterial = trailMat;
            trail.time = 0.35f;
            trail.startWidth = 0.6f;
            trail.endWidth = 0;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            jet.gameObject.SetActive(false);
            return jet;
        }

        static SpriteRenderer MakeScorchPrefab()
        {
            var go = new GameObject("Scorch");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = spriteMat;
            sr.sprite = blob;
            sr.color = new Color(0.08f, 0.05f, 0.03f, 0.85f);
            sr.sortingOrder = -2;
            go.transform.rotation = Quaternion.Euler(90, 0, 0);
            go.AddComponent<FadeOut>().sprite = sr;
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + "/Scorch.prefab");
            Object.DestroyImmediate(go);
            return prefab.GetComponent<SpriteRenderer>();
        }

        static void MakeFireflies(Transform follow)
        {
            var go = new GameObject("Fireflies");
            go.transform.SetParent(follow, false);
            go.transform.localPosition = new Vector3(0, 2, 4);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 6;
            main.startSpeed = 0;
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
            main.startColor = new ParticleSystem.MinMaxGradient(MissionGreen, GameConfig.Hex(0xfff0b0));
            main.maxParticles = 200;
            var emission = ps.emission;
            emission.rateOverTime = 6;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(44, 4, 30);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
            vel.y = new ParticleSystem.MinMaxCurve(-0.2f, 0.6f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                         new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(0.6f, 0.3f), new GradientAlphaKey(0, 1) });
            col.color = grad;
            var pr = go.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = sparkMat;
            pr.shadowCastingMode = ShadowCastingMode.Off;
        }

        // Greenfang HUD: objective line in place of the wave, capture bar, strike boxes,
        // waypoint arrow; arena-only parts (wave, XP, bomb chip) are hidden.
        static MissionHud BuildMissionHud(Hud hud)
        {
            var t = hud.transform;
            foreach (var name in new[] { "WaveLabel", "Wave", "XpBar", "Level", "BombChip" })
                t.Find(name).gameObject.SetActive(false);
            hud.bossBar.transform.Find("Name").GetComponent<Text>().text = "JUNGLE WARLORD";
            hud.touch.bombButton.Find("Label").GetComponent<Text>().text = "STRIKE";

            var mh = BuildObjectiveHud(hud, -18, "SECURING...", MissionGreen);

            // Strike boxes above the dash chip, plus the key hint.
            mh.boxes = new MissionHud.StrikeBox[2];
            for (int i = 0; i < 2; i++)
            {
                var border = Panel(t, StrikeSystem.Labels[i], Vector2.zero, new Vector2(18 + i * 150, 164), new Vector2(138, 56), StrikeSystem.Colors[i]);
                border.pivot = new Vector2(0.5f, 0.5f);
                border.anchoredPosition += new Vector2(69, -28);
                var inner = Panel(border, "Inner", new Vector2(0, 1), new Vector2(2, -2), new Vector2(134, 52), new Color(0.078f, 0.106f, 0.188f, 0.95f));
                Text(inner, "Label", 10, 8, 120, 18, 13, GameConfig.TextBright, StrikeSystem.Labels[i], FontStyle.Bold);
                var status = Text(inner, "Status", 10, 30, 120, 16, 11, GameConfig.TextDim, "READY", FontStyle.Bold);
                var shade = Panel(inner, "Shade", Vector2.zero, Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0.55f));
                shade.anchorMin = Vector2.zero;
                shade.anchorMax = Vector2.one;
                shade.pivot = Vector2.zero;
                shade.offsetMin = shade.offsetMax = Vector2.zero;
                mh.boxes[i] = new MissionHud.StrikeBox
                {
                    root = border,
                    border = border.GetComponent<Image>(),
                    status = status,
                    shade = shade,
                    group = border.gameObject.AddComponent<CanvasGroup>(),
                };
            }
            var hint = Label(t, "StrikeHint", Vector2.zero, new Vector2(18, 30), new Vector2(400, 16), 11, TextAnchor.LowerLeft, GameConfig.TextDim);
            hint.text = "Q  SWITCH   ·   RIGHT-CLICK  CALL";
            hud.touch.desktopOnly = new[] { hud.touch.desktopOnly[0], hint.gameObject };
            return mh;
        }

        // Objective line (y below the top edge), a capture bar at two thirds of the screen
        // height and a waypoint arrow at the screen edge. Shared by Greenfang, the arena's
        // extraction and the jungle.
        static MissionHud BuildObjectiveHud(Hud hud, float y, string captureLabel, Color color)
        {
            var t = hud.transform;
            var mh = hud.gameObject.AddComponent<MissionHud>();
            mh.canvasRect = (RectTransform)t;
            mh.boxes = new MissionHud.StrikeBox[0];
            mh.objective = Label(t, "Objective", new Vector2(0.5f, 1), new Vector2(-300, y), new Vector2(600, 24), 15, TextAnchor.UpperCenter, GameConfig.TextBright);
            mh.objective.fontStyle = FontStyle.Bold;

            var capture = new GameObject("Capture", typeof(RectTransform));
            var crt = (RectTransform)capture.transform;
            crt.SetParent(t, false);
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.pivot = new Vector2(0, 1);
            crt.anchoredPosition = new Vector2(-131, -100);
            crt.sizeDelta = new Vector2(262, 34);
            Label(crt, "Label", new Vector2(0, 1), Vector2.zero, new Vector2(262, 16), 13, TextAnchor.UpperCenter, color).text = captureLabel;
            mh.captureFill = Meter(crt, "Bar", new Vector2(0, 1), new Vector2(0, -20), new Vector2(260, 10), GameConfig.PanelEdge, color);
            mh.capture = capture;
            capture.SetActive(false);

            var arrow = Panel(t, "Waypoint", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34, 34), color);
            arrow.pivot = new Vector2(0.5f, 0.5f);
            arrow.GetComponent<Image>().sprite = LoadSprite(GenDir + "/arrow.png");
            arrow.gameObject.SetActive(false);
            mh.arrow = arrow;
            return mh;
        }

        // Jungle ground (mottled greens), the air-strike strip and the waypoint arrow.
        static void WriteJungleTextures()
        {
            const int S = 256;
            var ground = new Texture2D(S, S, TextureFormat.RGBA32, false);
            Color dark = GameConfig.Hex(0x0f2614), light = GameConfig.Hex(0x1f4426);
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                // Tileable noise: sample on a torus by wrapping the coordinates.
                float u = x / (float)S * Mathf.PI * 2, v = y / (float)S * Mathf.PI * 2;
                float n = Mathf.PerlinNoise(3 + Mathf.Cos(u) * 1.6f, 3 + Mathf.Sin(u) * 1.6f + Mathf.Cos(v) * 1.6f) * 0.6f
                        + Mathf.PerlinNoise(9 + Mathf.Sin(v) * 4f, 9 + Mathf.Cos(u) * 4f) * 0.4f;
                var c = Color.Lerp(dark, light, Mathf.Clamp01((n - 0.3f) * 1.6f));
                c = Color.Lerp(c, GroundBase, 0.35f);
                ground.SetPixel(x, y, c);
            }
            File.WriteAllBytes(GenDir + "/jungle.png", ground.EncodeToPNG());

            var strip = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float across = (x + 0.5f) / 64 - 0.5f, along = (y + 0.5f) / 64;
                float a = Mathf.Exp(-across * across / (2 * 0.2f * 0.2f)) * Mathf.SmoothStep(0, 1, Mathf.Min(along, 1 - along) / 0.1f);
                strip.SetPixel(x, y, new Color(1, 1, 1, a));
            }
            File.WriteAllBytes(GenDir + "/strip.png", strip.EncodeToPNG());

            // An upward-pointing chevron; the HUD rotates it toward the objective.
            var arrow = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float fx = Mathf.Abs(x - 31.5f) / 32, fy = y / 63f;
                bool inside = fy > 0.1f && fy < 0.95f && fx < (0.95f - fy) * 0.9f && !(fy < 0.4f && fx < (0.45f - fy) * 0.9f);
                arrow.SetPixel(x, y, new Color(1, 1, 1, inside ? 1 : 0));
            }
            File.WriteAllBytes(GenDir + "/arrow.png", arrow.EncodeToPNG());

            foreach (var f in new[] { "/jungle.png", "/strip.png", "/arrow.png" })
                AssetDatabase.ImportAsset(GenDir + f, ImportAssetOptions.ForceUpdate);
        }
    }
}
