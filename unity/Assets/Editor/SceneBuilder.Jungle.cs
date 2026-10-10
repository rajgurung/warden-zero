using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace WardenZero.EditorTools
{
    // Stage 2's first patch: a ~150 m jungle valley (the drop zone and checkpoint A), set in a
    // wide low-detail landscape that is seen from the chopper and the freefall. Streamed in as
    // Addressables content (AddressablesSetup), so it is not a build scene.
    //
    // Layout (metres, origin at the patch centre): the valley floor is gently uneven, rises to
    // a 7 m rim at the patch edge, then hills and far mountains. A stream winds west to east,
    // a muddy trail runs from the LZ clearing (south-west) over a ford to checkpoint A (north-east).
    public static partial class SceneBuilder
    {
        const string JunglePath = "Assets/Scenes/Jungle.unity";
        const string TerrainDir = JungleImport.Dir + "/Terrain";
        const float PatchHalf = 80;
        const float Rim = 7;
        const float WaterLevel = -0.6f;
        const float TerrainBase = -20;
        const float LandscapeSize = 8192;
        static readonly Vector3 LzPoint = new Vector3(-14, 0, -30);
        static readonly Vector3 CheckpointPoint = new Vector3(22, 0, 32);
        static readonly Vector2[] Stream = { new Vector2(-58, -6), new Vector2(-34, 4), new Vector2(-12, -3), new Vector2(4, 2), new Vector2(22, 10), new Vector2(40, 4), new Vector2(58, 9) };
        static readonly Vector2[] Trail = { new Vector2(-14, -30), new Vector2(-9, -18), new Vector2(-6, -8), new Vector2(-3, 0), new Vector2(3, 10), new Vector2(12, 20), new Vector2(22, 32) };

        // Just the jungle (faster when working on it). Batch: -executeMethod ...SceneBuilder.RebuildJungle
        public static void RebuildJungle()
        {
            PrepareAssets();
            PrepareCampaign();
            BuildJungleScene();
            AssetDatabase.SaveAssets();
        }

        static void BuildJungleScene()
        {
            JungleArt.WriteAll();
            WriteWaterNormal();
            AssetDatabase.ImportAsset(JungleImport.Dir, ImportAssetOptions.ImportRecursive);
            JungleFlora.MakeMaterials(SaveMaterial);
            var flora = JungleFlora.Build();
            Directory.CreateDirectory(TerrainDir);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var sun = SetupJungleLighting();

            var patch = BuildPatchTerrain(flora, out var trunks);
            var landscape = BuildLandscape(flora);
            BuildStream();
            var rocks = new GameObject("Rocks").transform;
            PlaceRocks(rocks, flora);

            var gm = CreateCore();
            var player = gm.player;
            var cam = player.cam;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.farClipPlane = 4500;
            cam.nearClipPlane = 0.3f;
            // Under the crowns (they start ~12 m up), closer than the arena's high view.
            gm.cameraFollow.offset = new Vector3(0, 8.5f, -10.5f);

            var volume = new GameObject("PostFX").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = BuildJunglePostProfile();

            var go = new GameObject("Stage");
            var stage = go.AddComponent<JungleStage>();
            stage.terrain = patch;
            stage.lz = LzPoint + Vector3.up * JungleHeight(LzPoint.x, LzPoint.z);
            stage.checkpoint = CheckpointPoint + Vector3.up * JungleHeight(CheckpointPoint.x, CheckpointPoint.z);
            stage.trunks = trunks;
            stage.alert = Clip("strike_ready");
            stage.chopper = ((GameObject)PrefabUtility.InstantiatePrefab(chopperPrefab.gameObject)).GetComponent<Chopper>();
            stage.chopper.transform.SetParent(go.transform, false);
            stage.chopper.gameObject.SetActive(false);
            BuildParachute(stage, go.transform);
            BuildLzMarker(stage, go.transform);
            BuildCheckpoint(stage, go.transform);

            // The jump poses ride on the Warden's animator.
            var view = (WardenModelView)player.view;
            stage.groundMarks = new[] { player.transform.Find("Ring").gameObject, player.transform.Find("Shadow").gameObject, player.glow.gameObject };
            var pose = player.gameObject.AddComponent<SkydivePose>();
            pose.animator = view.animator;
            pose.body = player.transform;
            stage.pose = pose;

            BuildJungleCameras(stage, gm, go.transform);
            BuildJungleHud(stage, gm);
            gm.jungle = stage;
            MakeMotes(player.transform);

            var q = go.AddComponent<JungleQuality>();
            q.patch = patch;
            q.landscape = landscape;
            q.volume = volume;
            q.cam = cam;

            EditorSceneManager.SaveScene(scene, JunglePath);
            AddressablesSetup.Configure(JunglePath);
            Debug.Log($"[SceneBuilder] Jungle written: {trunks.Length} trunks, sun {sun.transform.forward}");
        }

        // ------------------------------------------------------------------ the land

        // Height of the ground (metres) anywhere: valley, rim, hills, mountains. The patch and
        // the landscape terrains both sample this, and it is flat at the rim (|x| or |z| from
        // 74 to 86 m), so the two meet without a seam at the patch edge (80 m).
        static float JungleHeight(float x, float z)
        {
            float r = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
            if (r <= 86)
            {
                float floor = (Fbm(x / 30, z / 30, 3) - 0.5f) * 2.4f;
                floor *= Mathf.SmoothStep(0, 1, Mathf.InverseLerp(12, 24, Vector2.Distance(new Vector2(x, z), new Vector2(LzPoint.x, LzPoint.z))));
                floor *= Mathf.SmoothStep(0.3f, 1, Mathf.InverseLerp(6, 14, Vector2.Distance(new Vector2(x, z), new Vector2(CheckpointPoint.x, CheckpointPoint.z))));
                float channel = Mathf.Exp(-Mathf.Pow(DistanceToPath(Stream, x, z) / 2.6f, 2)) * Mathf.Clamp01((62 - Mathf.Abs(x)) / 8);
                floor -= 2.6f * channel;
                return Mathf.Lerp(floor, Rim, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(44, 74, r)));
            }
            float d = new Vector2(x, z).magnitude;
            float rise = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(86, 420, r));
            float hills = Fbm(x / 420 + 7, z / 420 + 3, 4) * 140 + Mathf.Pow(Fbm(x / 1300 + 1, z / 1300 + 9, 3), 2) * 380;
            float mountains = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(2200, 3800, d)) * (500 + Fbm(x / 700, z / 700, 3) * 500);
            return Rim + (hills - 40) * rise + mountains;
        }

        static float Fbm(float x, float z, int octaves)
        {
            float sum = 0, amp = 0.5f, freq = 1, norm = 0;
            for (int i = 0; i < octaves; i++)
            {
                sum += Mathf.PerlinNoise(x * freq + 13.7f * i, z * freq - 7.3f * i) * amp;
                norm += amp;
                amp *= 0.5f;
                freq *= 2.1f;
            }
            return sum / norm;
        }

        static float DistanceToPath(Vector2[] path, float x, float z)
        {
            var p = new Vector2(x, z);
            float best = float.MaxValue;
            for (int i = 0; i < path.Length - 1; i++)
            {
                Vector2 a = path[i], b = path[i + 1];
                float t = Mathf.Clamp01(Vector2.Dot(p - a, b - a) / (b - a).sqrMagnitude);
                best = Mathf.Min(best, Vector2.Distance(p, a + (b - a) * t));
            }
            return best;
        }

        static TerrainLayer Layer(string name, string tex, float tile, Color tint, float smooth = 0.08f)
        {
            string path = $"{TerrainDir}/{name}.terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, path);
            }
            layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{JungleImport.Dir}/{tex}_diff{(tex.Contains("canopy") ? "" : "_1k")}.{(tex.Contains("canopy") ? "png" : "jpg")}");
            layer.normalMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{JungleImport.Dir}/{tex}_nor_gl{(tex.Contains("canopy") ? "" : "_1k")}.{(tex.Contains("canopy") ? "png" : "jpg")}");
            layer.tileSize = new Vector2(tile, tile);
            layer.diffuseRemapMax = new Vector4(tint.r, tint.g, tint.b, 1);
            layer.smoothness = smooth;
            layer.metallic = 0;
            layer.normalScale = 1;
            EditorUtility.SetDirty(layer);
            return layer;
        }

        static TerrainData NewTerrainData(string name, int res, Vector3 size)
        {
            string path = $"{TerrainDir}/{name}.asset";
            AssetDatabase.DeleteAsset(path);
            var data = new TerrainData { heightmapResolution = res };
            data.size = size;
            AssetDatabase.CreateAsset(data, path);
            return data;
        }

        static Terrain NewTerrain(string name, TerrainData data, Vector3 pos)
        {
            var go = Terrain.CreateTerrainGameObject(data);
            go.name = name;
            go.transform.position = pos;
            var t = go.GetComponent<Terrain>();
            t.materialTemplate = SaveMaterial("JungleTerrain", Shader.Find("Universal Render Pipeline/Terrain/Lit"), Color.white);
            t.drawInstanced = true;
            t.shadowCastingMode = ShadowCastingMode.On;
            Object.DestroyImmediate(go.GetComponent<TerrainCollider>());
            return t;
        }

        static void FillHeights(TerrainData data, Vector3 pos)
        {
            int res = data.heightmapResolution;
            var h = new float[res, res];
            for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float wx = pos.x + x / (float)(res - 1) * data.size.x;
                float wz = pos.z + y / (float)(res - 1) * data.size.z;
                h[y, x] = Mathf.Clamp01((JungleHeight(wx, wz) - pos.y) / data.size.y);
            }
            data.SetHeights(0, 0, h);
        }

        // The playable valley: four ground layers, trees, palms, shrubs and ground cover.
        static Terrain BuildPatchTerrain(JungleFlora.Set flora, out Vector3[] trunks)
        {
            var pos = new Vector3(-PatchHalf, TerrainBase, -PatchHalf);
            var data = NewTerrainData("JunglePatch", 257, new Vector3(PatchHalf * 2, 120, PatchHalf * 2));
            FillHeights(data, pos);
            data.terrainLayers = new[]
            {
                Layer("ForestFloor", "Ground/forrest_ground_01", 4, new Color(0.55f, 0.56f, 0.46f)),
                Layer("LeafyGrass", "Ground/leafy_grass", 3, new Color(0.45f, 0.55f, 0.38f)),
                Layer("Mud", "Ground/mud_forest", 3, new Color(0.8f, 0.74f, 0.68f), 0.2f),
                Layer("MossyRock", "Rock/mossy_rock", 5, Color.white),
            };
            int a = 256;
            data.alphamapResolution = a;
            var maps = new float[a, a, 4];
            for (int y = 0; y < a; y++)
            for (int x = 0; x < a; x++)
            {
                float wx = pos.x + (x + 0.5f) / a * data.size.x, wz = pos.z + (y + 0.5f) / a * data.size.z;
                float trail = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1.4f, 2.8f, DistanceToPath(Trail, wx, wz)));
                float bank = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(2.5f, 5f, DistanceToPath(Stream, wx, wz)));
                float clearing = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(10, 18, Vector2.Distance(new Vector2(wx, wz), new Vector2(LzPoint.x, LzPoint.z))));
                float grassy = Mathf.Clamp01((Fbm(wx / 14, wz / 14, 2) - 0.48f) * 4) * 0.7f + clearing;
                float slope = data.GetSteepness((x + 0.5f) / a, (y + 0.5f) / a);
                float rock = Mathf.Clamp01((slope - 24) / 10);
                float mud = Mathf.Max(trail, bank * 0.8f);
                float g = Mathf.Clamp01(grassy) * (1 - mud);
                float w0 = Mathf.Max(0, 1 - g - mud - rock), w1 = g, w2 = mud, w3 = rock;
                float sum = w0 + w1 + w2 + w3;
                maps[y, x, 0] = w0 / sum;
                maps[y, x, 1] = w1 / sum;
                maps[y, x, 2] = w2 / sum;
                maps[y, x, 3] = w3 / sum;
            }
            data.SetAlphamaps(0, 0, maps);

            // Trees: broadleaf giants, palms and shrubs, kept off the clearings, trail and stream.
            var protos = new List<GameObject>();
            protos.AddRange(flora.Trees);
            protos.AddRange(flora.Palms);
            protos.AddRange(flora.Shrubs);
            data.treePrototypes = protos.Select(p => new TreePrototype { prefab = p }).ToArray();
            var rng = new System.Random(31);
            float Rn(float lo, float hi) => lo + (float)rng.NextDouble() * (hi - lo);
            var trees = new List<TreeInstance>();
            var trunkList = new List<Vector3>();
            bool Free(float x, float z, float clear)
            {
                if (DistanceToPath(Trail, x, z) < 3.2f + clear) return false;
                if (DistanceToPath(Stream, x, z) < 3 + clear * 0.5f) return false;
                if (Vector2.Distance(new Vector2(x, z), new Vector2(LzPoint.x, LzPoint.z)) < 17 + clear) return false;
                if (Vector2.Distance(new Vector2(x, z), new Vector2(CheckpointPoint.x, CheckpointPoint.z)) < 9 + clear) return false;
                return true;
            }
            void Plant(int proto, float x, float z, float scale, float spacing, float trunk)
            {
                foreach (var t in trunkList)
                    if (new Vector2(t.x - x, t.z - z).magnitude < spacing) return;
                trees.Add(new TreeInstance
                {
                    prototypeIndex = proto,
                    position = new Vector3((x - pos.x) / data.size.x, 0, (z - pos.z) / data.size.z),
                    widthScale = scale,
                    heightScale = scale * Rn(0.92f, 1.08f),
                    rotation = Rn(0, Mathf.PI * 2),
                    color = Color.white,
                    lightmapColor = Color.white,
                });
                if (trunk > 0) trunkList.Add(new Vector3(x, trunk * scale, z));
            }
            for (int i = 0; i < 5200; i++)
            {
                float x = Rn(-PatchHalf + 2, PatchHalf - 2), z = Rn(-PatchHalf + 2, PatchHalf - 2);
                float r = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
                if (!Free(x, z, 2)) continue;
                // Denser toward the rim, a few giants in the open valley.
                float dense = r > 50 ? 1 : 0.7f;
                if (rng.NextDouble() > dense) continue;
                int kind = rng.Next(10);
                if (kind < 5)
                {
                    int t = rng.Next(flora.Trees.Count);
                    Plant(t, x, z, Rn(0.8f, 1.25f), 6.5f, flora.TreeRadius[t] * 1.5f);
                }
                else if (kind < 9)
                {
                    Plant(flora.Trees.Count + rng.Next(flora.Palms.Count), x, z, Rn(0.85f, 1.2f), 4, 0.32f);
                }
            }
            // Shrubs fill the gaps (they don't block the Warden).
            for (int i = 0; i < 2400; i++)
            {
                float x = Rn(-PatchHalf + 1, PatchHalf - 1), z = Rn(-PatchHalf + 1, PatchHalf - 1);
                if (!Free(x, z, 0)) continue;
                trees.Add(new TreeInstance
                {
                    prototypeIndex = flora.Trees.Count + flora.Palms.Count + rng.Next(flora.Shrubs.Count),
                    position = new Vector3((x - pos.x) / data.size.x, 0, (z - pos.z) / data.size.z),
                    widthScale = Rn(0.8f, 1.4f),
                    heightScale = Rn(0.8f, 1.4f),
                    rotation = Rn(0, Mathf.PI * 2),
                    color = Color.white,
                    lightmapColor = Color.white,
                });
            }
            data.SetTreeInstances(trees.ToArray(), true);
            trunks = trunkList.ToArray();
            Debug.Log($"[SceneBuilder] patch trees: {trees.Count} instances ({trunkList.Count} with trunks)");

            // Ground cover as instanced detail meshes: grass in the open, ferns and
            // elephant ears under the trees.
            var detail = new List<GameObject> { flora.Grasses[0], flora.Ferns[0], flora.Ferns[1], flora.BigLeaves[0] };
            data.SetDetailResolution(256, 16);
            data.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);
            data.detailPrototypes = detail.Select(p => new DetailPrototype
            {
                usePrototypeMesh = true,
                prototype = p,
                renderMode = DetailRenderMode.VertexLit,
                useInstancing = true,
                minWidth = 0.8f,
                maxWidth = 1.3f,
                minHeight = 0.8f,
                maxHeight = 1.4f,
                noiseSpread = 0.3f,
                healthyColor = Color.white,
                dryColor = new Color(0.85f, 0.8f, 0.65f),
                alignToGround = 0.4f,
            }).ToArray();
            int dr = 256;
            var layers = new int[4][,];
            for (int l = 0; l < 4; l++) layers[l] = new int[dr, dr];
            for (int y = 0; y < dr; y++)
            for (int x = 0; x < dr; x++)
            {
                float wx = pos.x + (x + 0.5f) / dr * data.size.x, wz = pos.z + (y + 0.5f) / dr * data.size.z;
                if (DistanceToPath(Trail, wx, wz) < 1.6f) continue;
                if (JungleHeight(wx, wz) < WaterLevel + 0.15f) continue;
                if (Vector2.Distance(new Vector2(wx, wz), new Vector2(CheckpointPoint.x, CheckpointPoint.z)) < 4) continue;
                float n = Fbm(wx / 9, wz / 9, 2);
                float cover = Mathf.Clamp01(Mathf.Max(Mathf.Abs(wx), Mathf.Abs(wz)) / 60);
                float lz = Vector2.Distance(new Vector2(wx, wz), new Vector2(LzPoint.x, LzPoint.z));
                // Grass: thick in the LZ clearing and grassy patches.
                if ((lz < 16 && rng.NextDouble() < 0.55f) || n > 0.58f) layers[0][y, x] = rng.Next(2) + 1;
                // Ferns under the canopy, more toward the rim.
                if (rng.NextDouble() < 0.06f + cover * 0.12f && lz > 9) layers[1 + rng.Next(2)][y, x] = 1;
                if (rng.NextDouble() < 0.02f + cover * 0.03f && lz > 12) layers[3][y, x] = 1;
            }
            for (int l = 0; l < 4; l++) data.SetDetailLayer(0, 0, l, layers[l]);
            data.wavingGrassAmount = 0;

            var terrain = NewTerrain("JunglePatch", data, pos);
            terrain.treeDistance = 400;
            terrain.treeBillboardDistance = 120;
            terrain.detailObjectDistance = 70;
            terrain.detailObjectDensity = 1;
            terrain.heightmapPixelError = 4;
            terrain.basemapDistance = 120;
            EditorUtility.SetDirty(data);
            return terrain;
        }

        // The wider world seen from the air: hills and mountains under a canopy texture, ringed
        // with far trees near the valley, with a hole where the patch sits.
        static Terrain BuildLandscape(JungleFlora.Set flora)
        {
            float half = LandscapeSize / 2;
            var pos = new Vector3(-half, TerrainBase, -half);
            var data = NewTerrainData("JungleLandscape", 513, new Vector3(LandscapeSize, 1600, LandscapeSize));
            FillHeights(data, pos);
            data.terrainLayers = new[]
            {
                Layer("CanopyA", "Ground/canopy_far", 140, new Color(1, 1, 1)),
                Layer("CanopyB", "Ground/canopy_far", 95, new Color(0.75f, 0.85f, 0.7f)),
                Layer("FarRock", "Rock/mossy_rock", 60, new Color(0.75f, 0.75f, 0.7f)),
            };
            int a = 512;
            data.alphamapResolution = a;
            var maps = new float[a, a, 3];
            for (int y = 0; y < a; y++)
            for (int x = 0; x < a; x++)
            {
                float wx = pos.x + (x + 0.5f) / a * LandscapeSize, wz = pos.z + (y + 0.5f) / a * LandscapeSize;
                float b = Mathf.Clamp01((Fbm(wx / 600, wz / 600, 3) - 0.4f) * 3);
                float rock = Mathf.Clamp01((data.GetSteepness((x + 0.5f) / a, (y + 0.5f) / a) - 30) / 10) * Mathf.Clamp01((JungleHeight(wx, wz) - 300) / 200);
                maps[y, x, 0] = (1 - b) * (1 - rock);
                maps[y, x, 1] = b * (1 - rock);
                maps[y, x, 2] = rock;
            }
            data.SetAlphamaps(0, 0, maps);

            // The hole: the patch terrain fills it (16 m cells, so the edge at 80 m lines up).
            int hr = data.holesResolution;
            var holes = new bool[hr, hr];
            for (int y = 0; y < hr; y++)
            for (int x = 0; x < hr; x++)
            {
                float cx = pos.x + (x + 0.5f) / hr * LandscapeSize, cz = pos.z + (y + 0.5f) / hr * LandscapeSize;
                holes[y, x] = !(Mathf.Abs(cx) < PatchHalf && Mathf.Abs(cz) < PatchHalf); // true = solid
            }
            data.SetHoles(0, 0, holes);

            // Far trees in a band around the valley: the cards read as forest from the jump.
            data.treePrototypes = flora.FarTrees.Select(p => new TreePrototype { prefab = p }).ToArray();
            var rng = new System.Random(57);
            var trees = new List<TreeInstance>();
            for (int i = 0; i < 9000; i++)
            {
                float ang = (float)rng.NextDouble() * Mathf.PI * 2;
                float r = 84 + Mathf.Pow((float)rng.NextDouble(), 1.6f) * 520;
                float x = Mathf.Cos(ang) * r, z = Mathf.Sin(ang) * r;
                if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) < PatchHalf + 3) continue;
                trees.Add(new TreeInstance
                {
                    prototypeIndex = rng.Next(flora.FarTrees.Count),
                    position = new Vector3((x - pos.x) / LandscapeSize, 0, (z - pos.z) / LandscapeSize),
                    widthScale = 0.8f + (float)rng.NextDouble() * 0.6f,
                    heightScale = 0.8f + (float)rng.NextDouble() * 0.6f,
                    rotation = (float)rng.NextDouble() * Mathf.PI * 2,
                    color = Color.white,
                    lightmapColor = Color.white,
                });
            }
            data.SetTreeInstances(trees.ToArray(), true);
            Debug.Log($"[SceneBuilder] landscape far trees: {trees.Count}");
            var t = NewTerrain("JungleLandscape", data, pos);
            t.treeDistance = 600;
            t.heightmapPixelError = 6;
            t.basemapDistance = 20000;
            t.shadowCastingMode = ShadowCastingMode.Off;
            EditorUtility.SetDirty(data);
            return t;
        }

        // A water strip along the stream at a fixed level; the banks hide it where the
        // ground is above the water.
        static void BuildStream()
        {
            var mat = SaveMaterial("JungleWater", Shader.Find("Universal Render Pipeline/Lit"), new Color(0.06f, 0.1f, 0.07f, 0.84f));
            mat.SetFloat("_Surface", 1);
            mat.SetFloat("_Blend", 0);
            mat.SetFloat("_ZWrite", 0);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)RenderQueue.Transparent - 10;
            mat.SetFloat("_Smoothness", 0.93f);
            mat.SetFloat("_Metallic", 0);
            mat.SetFloat("_EnvironmentReflections", 1);
            mat.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(JungleImport.Dir + "/Ground/water_nor_gl.png"));
            mat.SetFloat("_BumpScale", 0.35f);
            mat.EnableKeyword("_NORMALMAP");
            mat.SetTextureScale("_BumpMap", new Vector2(1, 6));

            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            float v = 0;
            var pts = new List<Vector2>();
            for (int i = 0; i < Stream.Length - 1; i++)
                for (int s = 0; s < 8; s++) pts.Add(Vector2.Lerp(Stream[i], Stream[i + 1], s / 8f));
            pts.Add(Stream[Stream.Length - 1]);
            for (int i = 0; i < pts.Count; i++)
            {
                Vector2 d = (i < pts.Count - 1 ? pts[i + 1] - pts[i] : pts[i] - pts[i - 1]).normalized;
                Vector2 n = new Vector2(-d.y, d.x) * 4.5f;
                if (i > 0) v += Vector2.Distance(pts[i], pts[i - 1]) / 9;
                verts.Add(new Vector3(pts[i].x - n.x, WaterLevel, pts[i].y - n.y));
                verts.Add(new Vector3(pts[i].x + n.x, WaterLevel, pts[i].y + n.y));
                uvs.Add(new Vector2(0, v));
                uvs.Add(new Vector2(1, v));
                if (i > 0)
                {
                    int a = verts.Count - 4;
                    tris.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 });
                }
            }
            var mesh = new Mesh { name = "Stream" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            string path = TerrainDir + "/Stream.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            var go = new GameObject("Stream");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
        }

        // Boulders along the stream, stepping stones at the ford, a few in the forest.
        static void PlaceRocks(Transform parent, JungleFlora.Set flora)
        {
            var rng = new System.Random(77);
            float Rn(float lo, float hi) => lo + (float)rng.NextDouble() * (hi - lo);
            void Rock(Vector3 p, float s)
            {
                var prefab = flora.Rocks[rng.Next(flora.Rocks.Count)];
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                p.y = JungleHeight(p.x, p.z) - s * 0.25f;
                go.transform.position = p;
                go.transform.rotation = Quaternion.Euler(Rn(-8, 8), Rn(0, 360), Rn(-8, 8));
                go.transform.localScale = new Vector3(s * Rn(0.8f, 1.3f), s * Rn(0.6f, 1), s * Rn(0.8f, 1.3f));
                go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.On;
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            }
            for (int i = 0; i < Stream.Length - 1; i++)
                for (int k = 0; k < 4; k++)
                {
                    var p = Vector2.Lerp(Stream[i], Stream[i + 1], (float)rng.NextDouble());
                    var off = new Vector2(Rn(-1, 1), Rn(-1, 1)).normalized * Rn(2.5f, 4.5f);
                    if (DistanceToPath(Trail, p.x + off.x, p.y + off.y) < 3) continue;
                    Rock(new Vector3(p.x + off.x, 0, p.y + off.y), Rn(0.8f, 2.2f));
                }
            // Stepping stones where the trail crosses.
            for (int k = -2; k <= 2; k++) Rock(new Vector3(-3.6f + k * 0.4f, 0, -1 + k * 1.3f), Rn(0.6f, 0.8f));
            for (int i = 0; i < 26; i++)
            {
                float x = Rn(-70, 70), z = Rn(-70, 70);
                if (DistanceToPath(Trail, x, z) < 4 || Vector2.Distance(new Vector2(x, z), new Vector2(LzPoint.x, LzPoint.z)) < 18) continue;
                Rock(new Vector3(x, 0, z), Rn(1, 3.2f));
            }
        }

        static Light SetupJungleLighting()
        {
            var cube = AssetDatabase.LoadAssetAtPath<Cubemap>(JungleImport.Dir + "/Sky/kloofendal_48d_partly_cloudy_puresky_2k.hdr");
            var sky = SaveMaterial("JungleSky", Shader.Find("Skybox/Cubemap"), Color.white);
            sky.SetTexture("_Tex", cube);
            sky.SetFloat("_Exposure", 1f);
            sky.SetFloat("_Rotation", 110);
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.3f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = cube;
            RenderSettings.reflectionIntensity = 0.6f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.012f;
            RenderSettings.fogColor = new Color(0.58f, 0.67f, 0.68f);

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.93f, 0.8f);
            sun.intensity = 2.1f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            sun.transform.rotation = Quaternion.Euler(48, 125, 0);
            RenderSettings.sun = sun;
            return sun;
        }

        static VolumeProfile BuildJunglePostProfile()
        {
            const string path = "Assets/Settings/JunglePostFX.asset";
            AssetDatabase.DeleteAsset(path);
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(p, path);
            var tone = p.Add<Tonemapping>();
            tone.mode.Override(TonemappingMode.ACES);
            var color = p.Add<ColorAdjustments>();
            color.postExposure.Override(0.15f);
            color.contrast.Override(18);
            color.saturation.Override(-4);
            var wb = p.Add<WhiteBalance>();
            wb.temperature.Override(6);
            wb.tint.Override(-3);
            var bloom = p.Add<Bloom>();
            bloom.threshold.Override(1.05f);
            bloom.intensity.Override(0.55f);
            bloom.scatter.Override(0.7f);
            var smh = p.Add<ShadowsMidtonesHighlights>();
            smh.shadows.Override(new Vector4(0.95f, 1.02f, 1f, 0));
            smh.highlights.Override(new Vector4(1.03f, 1.01f, 0.95f, 0));
            var vignette = p.Add<Vignette>();
            vignette.intensity.Override(0.28f);
            vignette.smoothness.Override(0.5f);
            foreach (var c in p.components)
            {
                c.name = c.GetType().Name;
                c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(c, p);
            }
            EditorUtility.SetDirty(p);
            AssetDatabase.SaveAssets();
            return p;
        }

        // ------------------------------------------------------------------ set pieces

        // The canopy (Tripo) with suspension lines from its underside to his shoulders.
        static void BuildParachute(JungleStage stage, Transform parent)
        {
            var root = new GameObject("Parachute").transform;
            root.SetParent(parent, false);
            // Line the span up with X; the leading edge (the open cells, the thicker end of
            // the chord) faces +Z.
            var probe = PlaceProp("Canopy", root, canopyMat, 0, 1, 1);
            float yaw = 90 - PrincipalHeading(LocalVertices(probe, root));
            Object.DestroyImmediate(probe);
            probe = PlaceProp("Canopy", root, canopyMat, yaw, 0, 1);
            var b = LocalBounds(probe, root);
            var pts = LocalVertices(probe, root);
            float depth = b.size.z;
            float Thick(System.Func<Vector3, bool> sel)
            {
                var s = pts.Where(sel).ToList();
                return s.Count == 0 ? 0 : s.Max(p => p.y) - s.Min(p => p.y);
            }
            if (Thick(p => p.z < b.min.z + depth * 0.15f) > Thick(p => p.z > b.max.z - depth * 0.15f)) yaw += 180;
            Object.DestroyImmediate(probe);
            var model = PlaceProp("Canopy", root, canopyMat, yaw, 0, 9);
            foreach (var r in model.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.On;
            b = LocalBounds(model, root);
            Debug.Log($"[SceneBuilder] canopy {b.size} yaw {yaw}");

            var lineMat = SaveMaterial("ChuteLine", Unlit(), new Color(0.12f, 0.13f, 0.1f));
            var anchors = new List<Transform>();
            var lines = new List<LineRenderer>();
            foreach (float x in new[] { -0.45f, -0.2f, 0.2f, 0.45f })
            foreach (float z in new[] { -0.3f, 0.3f })
            {
                var a = new GameObject("Anchor").transform;
                a.SetParent(root, false);
                a.localPosition = new Vector3(b.size.x * x, b.min.y + 0.1f, b.center.z + b.size.z * z);
                anchors.Add(a);
                var lr = new GameObject("Line").AddComponent<LineRenderer>();
                lr.transform.SetParent(parent, false);
                lr.sharedMaterial = lineMat;
                lr.positionCount = 2;
                lr.widthMultiplier = 0.03f;
                lr.useWorldSpace = true;
                lr.shadowCastingMode = ShadowCastingMode.Off;
                lr.enabled = false;
                lines.Add(lr);
            }
            root.gameObject.SetActive(false);
            stage.parachute = root;
            // The pilot chute: a small canopy that pulls the main one out of the bag.
            var pilot = new GameObject("PilotChute").transform;
            pilot.SetParent(parent, false);
            PlaceProp("Canopy", pilot, canopyMat, yaw, 0, 1.2f);
            pilot.gameObject.SetActive(false);
            stage.pilotChute = pilot;
            stage.lineAnchors = anchors.ToArray();
            stage.lines = lines.ToArray();
        }

        // The LZ: a glowing ring and orange marker smoke you can see from the air.
        static void BuildLzMarker(JungleStage stage, Transform parent)
        {
            var orange = GameConfig.Hex(0xff8a3a);
            var ring = GlowQuad("LzRing", parent, GlowMaterial("JungleLzRing", ringTex, Hdr(orange, 1.4f), GroundGlowQueue), 14);
            ring.transform.position = stage.lz + Vector3.up * 0.15f;
            stage.lzMarker = ring.GetComponent<MeshRenderer>();
            var smoke = new GameObject("LzSmoke");
            smoke.transform.SetParent(ring.transform, false);
            smoke.transform.localPosition = Vector3.zero;
            smoke.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            var ps = smoke.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6, 9);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3, 5);
            main.startSize = new ParticleSystem.MinMaxCurve(2, 4);
            main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            main.startColor = new Color(1, 0.55f, 0.25f, 0.8f);
            main.maxParticles = 220;
            var em = ps.emission;
            em.rateOverTime = 22;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 8;
            shape.radius = 0.4f;
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
            vel.y = new ParticleSystem.MinMaxCurve(0, 0);
            vel.z = new ParticleSystem.MinMaxCurve(0.4f, 1f);
            vel.radial = new ParticleSystem.MinMaxCurve(0, 0);
            vel.orbitalX = vel.orbitalY = vel.orbitalZ = new ParticleSystem.MinMaxCurve(0, 0);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 0.6f, 1, 4f));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(new Color(0.85f, 0.8f, 0.75f), 1) },
                         new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(0.9f, 0.08f), new GradientAlphaKey(0, 1) });
            col.color = grad;
            var pr = smoke.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = dustMat;
            pr.shadowCastingMode = ShadowCastingMode.Off;
        }

        // Checkpoint A: the Tripo beacon, its lamp and a capture ring.
        static void BuildCheckpoint(JungleStage stage, Transform parent)
        {
            var root = new GameObject("CheckpointA").transform;
            root.SetParent(parent, false);
            root.position = stage.checkpoint;
            root.rotation = Quaternion.Euler(0, 200, 0);
            var model = PlaceProp("Beacon", root, beaconMat, 0, 1, 3.4f);
            foreach (var r in model.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.On;
            var light = new GameObject("Lamp").AddComponent<Light>();
            light.transform.SetParent(root, false);
            light.transform.localPosition = new Vector3(0, 3.6f, 0);
            light.type = LightType.Point;
            light.range = 10;
            light.intensity = 4;
            light.shadows = LightShadows.None;
            stage.beacon = root;
            stage.beaconLight = light;
            var ring = GlowQuad("CaptureRing", parent, GlowMaterial("CheckpointRing", ringTex, Hdr(GameConfig.Hex(0x9bff67), 1.5f), GroundGlowQueue), JungleStage.CaptureRadius * 2);
            ring.transform.position = stage.checkpoint + Vector3.up * 0.12f;
            stage.beaconRing = ring.GetComponent<MeshRenderer>();
        }

        static void BuildJungleCameras(JungleStage stage, GameManager gm, Transform parent)
        {
            var cams = new GameObject("Cameras").transform;
            cams.SetParent(parent, false);
            stage.brain = gm.player.cam.GetComponent<CinemachineBrain>();
            var chopper = stage.chopper.transform;

            stage.chaseCam = NewCinemachineCamera("ChaseCam", cams, 10, 50);
            stage.chaseCam.Follow = chopper;
            stage.chaseCam.LookAt = chopper;
            var f = stage.chaseCam.gameObject.AddComponent<CinemachineFollow>();
            f.FollowOffset = new Vector3(-20, 6, -30);
            f.TrackerSettings.BindingMode = Unity.Cinemachine.TargetTracking.BindingMode.LockToTargetWithWorldUp;
            f.TrackerSettings.PositionDamping = new Vector3(0.8f, 0.8f, 0.8f);
            stage.chaseCam.gameObject.AddComponent<CinemachineRotationComposer>().Damping = new Vector2(0.4f, 0.4f);

            stage.passCam = NewCinemachineCamera("PassCam", cams, 10, 40);
            stage.passCam.LookAt = chopper;
            stage.passCam.gameObject.AddComponent<CinemachineRotationComposer>().Damping = new Vector2(0.2f, 0.2f);

            stage.doorCam = NewCinemachineCamera("DoorCam", cams, 10, 55);
            stage.doorCam.Follow = chopper;
            stage.doorCam.LookAt = gm.player.transform;
            var df = stage.doorCam.gameObject.AddComponent<CinemachineFollow>();
            df.FollowOffset = new Vector3(-8.5f, 2, 9);
            df.TrackerSettings.BindingMode = Unity.Cinemachine.TargetTracking.BindingMode.LockToTarget;
            df.TrackerSettings.PositionDamping = Vector3.zero;
            var dc = stage.doorCam.gameObject.AddComponent<CinemachineRotationComposer>();
            dc.TargetOffset = new Vector3(0, 1.3f, 0);
            dc.Damping = new Vector2(0.3f, 0.3f);

            var rig = new GameObject("DiveRig").transform;
            rig.SetParent(parent, false);
            stage.diveRig = rig;
            var ffLook = new GameObject("FreefallLook").transform;
            ffLook.SetParent(rig, false);
            ffLook.localPosition = new Vector3(0, -6, 10);
            var cLook = new GameObject("CanopyLook").transform;
            cLook.SetParent(rig, false);
            cLook.localPosition = new Vector3(0, 3f, 10);

            stage.freefallCam = NewCinemachineCamera("FreefallCam", cams, 10, 62);
            stage.freefallCam.Follow = rig;
            stage.freefallCam.LookAt = ffLook;
            var ff = stage.freefallCam.gameObject.AddComponent<CinemachineFollow>();
            ff.FollowOffset = new Vector3(0, 3.5f, -8);
            ff.TrackerSettings.BindingMode = Unity.Cinemachine.TargetTracking.BindingMode.LockToTargetWithWorldUp;
            ff.TrackerSettings.PositionDamping = new Vector3(0.3f, 0.15f, 0.3f);
            ff.TrackerSettings.RotationDamping = Vector3.one * 0.6f;
            stage.freefallCam.gameObject.AddComponent<CinemachineRotationComposer>().Damping = new Vector2(0.2f, 0.2f);

            stage.canopyCam = NewCinemachineCamera("CanopyCam", cams, 10, 66);
            stage.canopyCam.Follow = rig;
            stage.canopyCam.LookAt = cLook;
            var cf = stage.canopyCam.gameObject.AddComponent<CinemachineFollow>();
            cf.FollowOffset = new Vector3(0, 2.5f, -12);
            cf.TrackerSettings.BindingMode = Unity.Cinemachine.TargetTracking.BindingMode.LockToTargetWithWorldUp;
            cf.TrackerSettings.PositionDamping = new Vector3(0.5f, 0.3f, 0.5f);
            cf.TrackerSettings.RotationDamping = Vector3.one * 1.2f;
            stage.canopyCam.gameObject.AddComponent<CinemachineRotationComposer>().Damping = new Vector2(0.3f, 0.3f);

            stage.settleCam = NewCinemachineCamera("SettleCam", cams, 10, GameConfig.CameraFovDegrees);

            // Handheld noise, driven by speed and the opening jolt (JungleStage).
            var profile = AssetDatabase.LoadAssetAtPath<NoiseSettings>("Packages/com.unity.cinemachine/Presets/Noise/Handheld_normal_mild.asset");
            foreach (var c in new[] { stage.freefallCam, stage.canopyCam, stage.settleCam })
            {
                var n = c.gameObject.AddComponent<CinemachineBasicMultiChannelPerlin>();
                n.NoiseProfile = profile;
                n.AmplitudeGain = 0;
                n.FrequencyGain = 1.4f;
            }

            stage.windStreaks = MakeWindStreaks(rig);
            MakeClouds(parent, stage.lz);
        }

        static void BuildJungleHud(JungleStage stage, GameManager gm)
        {
            var hud = gm.hud;
            var t = hud.transform;
            foreach (var name in new[] { "WaveLabel", "Wave" })
                t.Find(name).gameObject.SetActive(false);
            stage.hud = BuildObjectiveHud(hud, -18, "SECURING...", GameConfig.Hex(0x9bff67));

            var drop = new GameObject("Drop", typeof(RectTransform));
            var rt = (RectTransform)drop.transform;
            rt.SetParent(t, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var dh = hud.gameObject.AddComponent<DropHud>();
            dh.root = drop;
            dh.canvasRect = (RectTransform)t;
            dh.altitude = Label(rt, "Altitude", new Vector2(1, 0.5f), new Vector2(-236, 70), new Vector2(200, 50), 44, TextAnchor.UpperRight, GameConfig.TextBright);
            dh.altitude.fontStyle = FontStyle.Bold;
            Label(rt, "AltLabel", new Vector2(1, 0.5f), new Vector2(-236, 88), new Vector2(200, 16), 11, TextAnchor.UpperRight, GameConfig.TextDim).text = "ALTITUDE";
            dh.speed = Label(rt, "Speed", new Vector2(1, 0.5f), new Vector2(-236, 16), new Vector2(200, 18), 13, TextAnchor.UpperRight, GameConfig.TextDim);
            dh.lz = Label(rt, "Lz", new Vector2(1, 0.5f), new Vector2(-236, -4), new Vector2(200, 18), 13, TextAnchor.UpperRight, GameConfig.Hex(0xff8a3a));
            var strip = Panel(rt, "Altimeter", new Vector2(1, 0.5f), new Vector2(-26, 130), new Vector2(8, 260), GameConfig.Hex(0x9bff67));
            float top = JungleStage.JumpAltitude;
            var amber = Panel(strip, "Amber", new Vector2(0, 0), new Vector2(0, 260 * Skydive.DeployPrompt / top), new Vector2(8, 260 * (Skydive.DeployPrompt - Skydive.SafeDeploy) / top), GameConfig.Gold);
            amber.pivot = new Vector2(0, 1);
            var red = Panel(strip, "Red", new Vector2(0, 0), new Vector2(0, 260 * Skydive.SafeDeploy / top), new Vector2(8, 260 * Skydive.SafeDeploy / top), GameConfig.Health);
            red.pivot = new Vector2(0, 1);
            foreach (var img in new[] { strip, amber, red }) img.GetComponent<Image>().color *= new Color(1, 1, 1, 0.75f);
            dh.strip = strip;
            var marker = Panel(strip, "Marker", new Vector2(0, 1), new Vector2(-6, 0), new Vector2(20, 4), Color.white);
            marker.pivot = new Vector2(0, 0.5f);
            dh.marker = marker;
            dh.prompt = Label(rt, "Prompt", new Vector2(0.5f, 0), new Vector2(-400, 170), new Vector2(800, 30), 20, TextAnchor.UpperCenter, GameConfig.TextBright);
            dh.prompt.fontStyle = FontStyle.Bold;
            var icon = Panel(rt, "LzIcon", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(46, 46), GameConfig.Hex(0xff8a3a));
            icon.pivot = new Vector2(0.5f, 0.5f);
            icon.GetComponent<Image>().sprite = LoadSprite(GenDir + "/ring.png");
            Label(icon, "Tag", new Vector2(0.5f, 1), new Vector2(-30, 18), new Vector2(60, 16), 11, TextAnchor.UpperCenter, GameConfig.Hex(0xff8a3a)).text = "LZ";
            dh.lzIcon = icon;
            drop.SetActive(false);
            stage.dropHud = dh;
        }

        // Streaks of air rushing past in freefall (rate set by JungleStage from his speed).
        static ParticleSystem MakeWindStreaks(Transform rig)
        {
            var go = new GameObject("WindStreaks");
            go.transform.SetParent(rig, false);
            go.transform.localPosition = new Vector3(0, -18, 0);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = 0.55f;
            main.startSpeed = 0;
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            main.startColor = new Color(1, 1, 1, 0.3f);
            main.maxParticles = 200;
            var em = ps.emission;
            em.rateOverTime = 0;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(26, 2, 26);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.x = new ParticleSystem.MinMaxCurve(0, 0);
            vel.y = new ParticleSystem.MinMaxCurve(60, 75);
            vel.z = new ParticleSystem.MinMaxCurve(0, 0);
            vel.radial = new ParticleSystem.MinMaxCurve(0, 0);
            vel.orbitalX = vel.orbitalY = vel.orbitalZ = new ParticleSystem.MinMaxCurve(0, 0);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                         new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(0.35f, 0.3f), new GradientAlphaKey(0, 1) });
            col.color = grad;
            var pr = go.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = dustMat;
            pr.renderMode = ParticleSystemRenderMode.Stretch;
            pr.velocityScale = 0.06f;
            pr.lengthScale = 1;
            pr.shadowCastingMode = ShadowCastingMode.Off;
            return ps;
        }

        // A thin layer of cloud the drop falls through, around 560 m above the valley.
        static void MakeClouds(Transform parent, Vector3 lz)
        {
            var go = new GameObject("Clouds");
            go.transform.SetParent(parent, false);
            go.transform.position = lz + new Vector3(-120, 560, -150);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = true;
            main.duration = 1;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 100000;
            main.startSpeed = 0;
            main.startSize = new ParticleSystem.MinMaxCurve(50, 120);
            main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            main.startColor = new Color(1, 1, 1, 0.55f);
            main.maxParticles = 90;
            var em = ps.emission;
            em.rateOverTime = 0;
            em.SetBursts(new[] { new ParticleSystem.Burst(0, 90) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(900, 50, 900);
            var pr = go.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = dustMat;
            pr.shadowCastingMode = ShadowCastingMode.Off;
            pr.maxParticleSize = 4;
        }

        // Specks of dust and pollen drifting in the light around the Warden.
        static void MakeMotes(Transform follow)
        {
            var go = new GameObject("Motes");
            go.transform.SetParent(follow, false);
            go.transform.localPosition = new Vector3(0, 2.5f, 3);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 8;
            main.startSpeed = 0;
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
            main.startColor = new Color(0.25f, 0.24f, 0.2f, 1); // additive: kept dim, under the bloom threshold
            main.maxParticles = 300;
            var em = ps.emission;
            em.rateOverTime = 30;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(30, 6, 30);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.15f, 0.25f);
            vel.y = new ParticleSystem.MinMaxCurve(-0.05f, 0.1f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
            vel.radial = new ParticleSystem.MinMaxCurve(0, 0);
            vel.orbitalX = vel.orbitalY = vel.orbitalZ = new ParticleSystem.MinMaxCurve(0, 0);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                         new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(0.5f, 0.3f), new GradientAlphaKey(0, 1) });
            col.color = grad;
            var pr = go.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = sparkMat;
            pr.shadowCastingMode = ShadowCastingMode.Off;
        }

        // Soft ripples for the stream (tileable).
        static void WriteWaterNormal()
        {
            const int S = 256;
            var h = new float[S * S];
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float u = x / (float)S * Mathf.PI * 2, v = y / (float)S * Mathf.PI * 2;
                h[y * S + x] = Mathf.PerlinNoise(Mathf.Cos(u) * 3 + 10, Mathf.Sin(u) * 3 + Mathf.Cos(v) * 3) + 0.5f * Mathf.PerlinNoise(Mathf.Sin(v) * 7 + 3, Mathf.Cos(u) * 7);
            }
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = h[y * S + (x + 1) % S] - h[y * S + (x - 1 + S) % S];
                float dy = h[((y + 1) % S) * S + x] - h[((y - 1 + S) % S) * S + x];
                var n = new Vector3(-dx * 3, -dy * 3, 1).normalized;
                tex.SetPixel(x, y, new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1));
            }
            Directory.CreateDirectory(JungleImport.Dir + "/Ground");
            File.WriteAllBytes(JungleImport.Dir + "/Ground/water_nor_gl.png", tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
    }
}
