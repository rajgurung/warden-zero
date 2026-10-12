using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace WardenZero.EditorTools
{
    // M3's road past checkpoint A (RoadLayout): the JungleRoad terrain tile north of the patch
    // with the drive and the ride painted on it, the corridor forest (trunks close in, cards
    // beyond), roadside rocks at the bends, and a skirt down the tile's free edges that hides
    // any crack where the landscape's coarser terrain meets it. Its ground layers are the
    // patch's forest floor, grass and mud and the landscape's canopy (far from the roads), so
    // it adds no textures; the steep banks have no rock layer, the boulders at the bends do it.
    public static partial class SceneBuilder
    {
        const float RoadTileHeight = 200; // the terrain's vertical range above TerrainBase
        // Resolutions held down for the jungle's heap at Low (M3 decision 0.5): 1.5 m height
        // and paint samples, 3 m ground-cover cells.
        const int RoadHeightmap = 257, RoadAlphamap = 256, RoadDetail = 128;
        const float TrunkClear = 9; // no tree trunks nearer a road's centreline
        const float ShrubClear = 7.5f;
        const float CardsFrom = 25; // mesh trees within this of a road, far cards beyond
        const float CanopyFrom = 40; // the ground fades to the landscape's canopy layer from here

        // Distance from (x, z) to the nearer road's centreline (the ride counts from the start).
        static float RoadDistance(float x, float z)
        {
            var p = new Vector2(x, z);
            return Mathf.Min(RoadLayout.Drive.Nearest(p).Distance, RoadLayout.Ride.Nearest(p).Distance);
        }

        static Terrain BuildRoadTile(JungleFlora.Set flora, List<Vector3> trunkList, Transform rocks)
        {
            var pos = new Vector3(RoadLayout.TileMinX, TerrainBase, RoadLayout.TileMinZ);
            float size = RoadLayout.TileMaxX - RoadLayout.TileMinX;
            var data = NewTerrainData("JungleRoad", RoadHeightmap, new Vector3(size, RoadTileHeight, size));
            FillHeights(data, pos);
            data.baseMapResolution = 512;
            // The patch's forest floor, grass and mud, and the landscape's canopy (no new textures).
            var layers = PatchLayers();
            layers[3] = Layer("CanopyA", "Ground/canopy_far", 140, new Color(1, 1, 1));
            data.terrainLayers = layers;

            // Road distance per alphamap texel, kept for the trees and ground cover below.
            int a = RoadAlphamap;
            data.alphamapResolution = a;
            var dist = new float[a, a];
            var maps = new float[a, a, 4];
            var b = new Vector2(RoadLayout.CheckpointB.x, RoadLayout.CheckpointB.z);
            for (int y = 0; y < a; y++)
            for (int x = 0; x < a; x++)
            {
                float wx = pos.x + (x + 0.5f) / a * size, wz = pos.z + (y + 0.5f) / a * size;
                float d = dist[y, x] = RoadDistance(wx, wz);
                // Mud on the road, two darker wheel ruts, grass on the shoulders, forest floor
                // beyond with grassy patches; far from the roads (and at the tile's edges) the
                // landscape's canopy layer, so from the air the tile reads as the same forest.
                float rut = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.25f, 0.6f, Mathf.Abs(d - GameConfig.Jeep.Track / 2)));
                float road = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(RoadLayout.RoadHalf - 0.5f, RoadLayout.RoadHalf + 0.6f, d));
                float clearing = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(RoadLayout.ClearingRadius - 4, RoadLayout.ClearingRadius + 2, Vector2.Distance(new Vector2(wx, wz), b)));
                float mud = Mathf.Max(road * (0.72f + 0.28f * rut), clearing * 0.55f);
                float shoulder = (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(RoadLayout.ShoulderHalf, RoadLayout.ShoulderHalf + 3, d))) * (1 - road);
                float grassy = Mathf.Max(shoulder, Mathf.Clamp01((Fbm(wx / 14, wz / 14, 2) - 0.5f) * 4) * 0.6f);
                float canopy = Mathf.Max(Mathf.SmoothStep(0, 1, Mathf.InverseLerp(CanopyFrom, CanopyFrom + 20, d)), FreeEdgeWeight(wx, wz)) * (1 - mud);
                float w2 = mud, w3 = canopy, w1 = grassy * (1 - mud) * (1 - canopy);
                float w0 = Mathf.Max(0, 1 - w1 - w2 - w3);
                float sum = w0 + w1 + w2 + w3;
                maps[y, x, 0] = w0 / sum;
                maps[y, x, 1] = w1 / sum;
                maps[y, x, 2] = w2 / sum;
                maps[y, x, 3] = w3 / sum;
            }
            data.SetAlphamaps(0, 0, maps);
            float Dist(float wx, float wz)
            {
                int x = Mathf.Clamp(Mathf.FloorToInt((wx - pos.x) / size * a), 0, a - 1), y = Mathf.Clamp(Mathf.FloorToInt((wz - pos.z) / size * a), 0, a - 1);
                return dist[y, x];
            }

            // The corridor forest: broadleaf trees and palms with trunks from TrunkClear out (most
            // dense 12-18 m, the soft wall), shrubs between, far cards past CardsFrom; none in
            // the clearing at B.
            var protos = new List<GameObject>();
            protos.AddRange(flora.Trees);
            protos.AddRange(flora.Palms);
            protos.AddRange(flora.Shrubs);
            int farBase = protos.Count;
            protos.AddRange(flora.FarTrees);
            data.treePrototypes = protos.Select(p => new TreePrototype { prefab = p }).ToArray();
            var rng = new System.Random(91);
            float Rn(float lo, float hi) => lo + (float)rng.NextDouble() * (hi - lo);
            var trees = new List<TreeInstance>();
            var spacing = new List<Vector3>();
            int trunkCount = 0;
            TreeInstance Tree(int proto, float x, float z, float scale) => new TreeInstance
            {
                prototypeIndex = proto,
                position = new Vector3((x - pos.x) / size, 0, (z - pos.z) / size),
                widthScale = scale,
                heightScale = scale * Rn(0.92f, 1.08f),
                rotation = Rn(0, Mathf.PI * 2),
                color = Color.white,
                lightmapColor = Color.white,
            };
            bool Spaced(float x, float z, float gap)
            {
                foreach (var t in spacing)
                    if ((t.x - x) * (t.x - x) + (t.z - z) * (t.z - z) < gap * gap) return false;
                return true;
            }
            for (int i = 0; i < 26000; i++)
            {
                float x = Rn(pos.x + 2, pos.x + size - 2), z = Rn(pos.z + 2, pos.z + size - 2);
                float d = Dist(x, z);
                if (d < TrunkClear || d > CardsFrom || Vector2.Distance(new Vector2(x, z), b) < RoadLayout.ClearingRadius + 4) continue;
                if (RoadLayout.NearRideSpot(x, z, 4)) continue; // the targets and their cover stay in sight
                // Densest where the jeep would leave the corridor.
                float dense = d >= 12 && d <= 18 ? 1 : 0.55f;
                if (rng.NextDouble() > dense) continue;
                bool broad = rng.Next(10) < 5;
                int proto = broad ? rng.Next(flora.Trees.Count) : flora.Trees.Count + rng.Next(flora.Palms.Count);
                float gap = broad ? 5 : 3;
                if (!Spaced(x, z, gap)) continue;
                float scale = broad ? Rn(0.8f, 1.25f) : Rn(0.85f, 1.2f);
                float trunk = broad ? flora.TreeRadius[proto] * 1.5f * scale : 0.32f * scale;
                spacing.Add(new Vector3(x, trunk, z));
                trees.Add(Tree(proto, x, z, scale));
                trunkList.Add(new Vector3(x, trunk, z));
                trunkCount++;
            }
            int shrubs = 0;
            for (int i = 0; i < 9000; i++)
            {
                float x = Rn(pos.x + 1, pos.x + size - 1), z = Rn(pos.z + 1, pos.z + size - 1);
                float d = Dist(x, z);
                if (d < ShrubClear || d > CardsFrom + 10 || Vector2.Distance(new Vector2(x, z), b) < RoadLayout.ClearingRadius) continue;
                if (RoadLayout.NearRideSpot(x, z, 6)) continue;
                var shrub = Tree(flora.Trees.Count + flora.Palms.Count + rng.Next(flora.Shrubs.Count), x, z, Rn(0.8f, 1.4f));
                shrub.heightScale = Rn(0.8f, 1.4f);
                trees.Add(shrub);
                shrubs++;
            }
            int cards = 0;
            for (int i = 0; i < 4500; i++)
            {
                float x = Rn(pos.x + 2, pos.x + size - 2), z = Rn(pos.z + 2, pos.z + size - 2);
                if (Dist(x, z) < CardsFrom + 3) continue;
                var card = Tree(farBase + rng.Next(flora.FarTrees.Count), x, z, Rn(0.8f, 1.4f));
                card.heightScale = Rn(0.8f, 1.4f);
                trees.Add(card);
                cards++;
            }
            data.SetTreeInstances(trees.ToArray(), true);
            Debug.Log($"[SceneBuilder] road tile trees: {trunkCount} with trunks, {shrubs} shrubs, {cards} far cards");

            // Ground cover off the roads and the clearing.
            data.SetDetailResolution(RoadDetail, 16);
            data.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);
            data.detailPrototypes = DetailPrototypes(flora);
            int dr = RoadDetail;
            int per = Mathf.RoundToInt(size / dr / 1.5f); // instances per cell, as dense as 1.5 m cells
            var cover = new int[4][,];
            for (int l = 0; l < 4; l++) cover[l] = new int[dr, dr];
            for (int y = 0; y < dr; y++)
            for (int x = 0; x < dr; x++)
            {
                float wx = pos.x + (x + 0.5f) / dr * size, wz = pos.z + (y + 0.5f) / dr * size;
                float d = Dist(wx, wz);
                if (d < RoadLayout.ShoulderHalf || Vector2.Distance(new Vector2(wx, wz), b) < RoadLayout.ClearingRadius) continue;
                if (d > 45 && rng.NextDouble() < 0.7f) continue; // sparser where only the air sees it
                if (d < 30 && RoadLayout.NearRideSpot(wx, wz, 3)) continue; // the gun ride's targets stay in sight
                if (Fbm(wx / 9, wz / 9, 2) > 0.56f) cover[0][y, x] = (rng.Next(2) + 1) * per * per;
                if (rng.NextDouble() < 0.14f * per * per) cover[1 + rng.Next(2)][y, x] = 1;
                if (rng.NextDouble() < 0.04f * per * per) cover[3][y, x] = 1;
            }
            for (int l = 0; l < 4; l++) data.SetDetailLayer(0, 0, l, cover[l]);
            data.wavingGrassAmount = 0;

            var terrain = NewTerrain("JungleRoad", data, pos);
            terrain.treeDistance = 400;
            terrain.treeBillboardDistance = 120;
            terrain.detailObjectDistance = 70;
            terrain.detailObjectDensity = 1;
            terrain.heightmapPixelError = 4;
            terrain.basemapDistance = 120;
            EditorUtility.SetDirty(data);

            PlaceRoadsideRocks(rocks, flora);
            BuildRoadSkirt(terrain);
            return terrain;
        }

        // 1 at the tile's edges that meet the landscape, 0 from EdgeBlend in (the south edge west
        // of x = 80 meets the patch, which has no canopy layer).
        static float FreeEdgeWeight(float x, float z)
        {
            float d = Mathf.Min(x - RoadLayout.TileMinX, Mathf.Min(RoadLayout.TileMaxX - x, RoadLayout.TileMaxZ - z));
            var corner = new Vector2(RoadLayout.PatchHalf, RoadLayout.TileMinZ);
            d = Mathf.Min(d, x >= corner.x ? z - corner.y : Vector2.Distance(new Vector2(x, z), corner));
            return 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01(d / RoadLayout.EdgeBlend));
        }

        // The four ground layers the patch uses (no new textures).
        static TerrainLayer[] PatchLayers() => new[]
        {
            Layer("ForestFloor", "Ground/forrest_ground_01", 4, new Color(0.55f, 0.56f, 0.46f)),
            Layer("LeafyGrass", "Ground/leafy_grass", 3, new Color(0.45f, 0.55f, 0.38f)),
            Layer("Mud", "Ground/mud_forest", 3, new Color(0.8f, 0.74f, 0.68f), 0.2f),
            Layer("MossyRock", "Rock/mossy_rock", 5, Color.white),
        };

        // Ground cover meshes: grass in the open, ferns and elephant ears under the trees.
        static DetailPrototype[] DetailPrototypes(JungleFlora.Set flora)
        {
            var detail = new List<GameObject> { flora.Grasses[0], flora.Ferns[0], flora.Ferns[1], flora.BigLeaves[0] };
            return detail.Select(p => new DetailPrototype
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
        }

        // Boulders on the outside of the bends, past the trunks' first row, and the M2 outcrops
        // at the tightest ones (props already in the jungle bundle: no new textures).
        static void PlaceRoadsideRocks(Transform parent, JungleFlora.Set flora)
        {
            var rng = new System.Random(93);
            float Rn(float lo, float hi) => lo + (float)rng.NextDouble() * (hi - lo);
            Material outcrop = File.Exists(CampaignImport.TraversalFbx("rock_outcrop")) ? TraversalMaterial("rock_outcrop") : null;
            int boulders = 0, outcrops = 0;
            foreach (var road in new[] { RoadLayout.Drive, RoadLayout.Ride })
            {
                float lastOutcrop = -100;
                for (float s = 20; s < road.Length - 10; s += 9)
                {
                    var at = road.At(s);
                    // Inside the patch the course and its rocks own the ground.
                    if (RoadLayout.InPatch(at.Position.x, at.Position.y)) continue;
                    float k = Mathf.Abs(at.Curvature);
                    if (k < 1 / 60f) continue;
                    // The outside of the bend: away from the turn's centre.
                    var right = new Vector2(at.Tangent.y, -at.Tangent.x);
                    var outside = at.Curvature > 0 ? right : -right;
                    var p = at.Position + outside * Rn(10.5f, 13);
                    if (RoadDistance(p.x, p.y) < 10 || RoadLayout.NearRideSpot(p.x, p.y, 7)) continue;
                    if (k > 1 / 32f && s - lastOutcrop > 40 && outcrop != null)
                    {
                        var holder = new GameObject("RoadOutcrop").transform;
                        holder.SetParent(parent, false);
                        var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CampaignImport.TraversalFbx("rock_outcrop")));
                        model.transform.SetParent(holder, false);
                        foreach (var r in model.GetComponentsInChildren<MeshRenderer>())
                        {
                            r.sharedMaterial = outcrop;
                            r.shadowCastingMode = ShadowCastingMode.On;
                        }
                        var lb = LocalBounds(model, holder);
                        holder.localScale = Vector3.one * (5.5f / Mathf.Max(lb.size.x, lb.size.z));
                        holder.position = new Vector3(p.x, JungleHeight(p.x, p.y) - 0.8f, p.y);
                        holder.rotation = Quaternion.Euler(0, Rn(0, 360), 0);
                        GameObjectUtility.SetStaticEditorFlags(holder.gameObject, StaticEditorFlags.BatchingStatic);
                        lastOutcrop = s;
                        outcrops++;
                        continue;
                    }
                    var prefab = flora.Rocks[rng.Next(flora.Rocks.Count)];
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                    float sc = Rn(1.6f, 3.2f);
                    go.transform.position = new Vector3(p.x, JungleHeight(p.x, p.y) - sc * 0.25f, p.y);
                    go.transform.rotation = Quaternion.Euler(Rn(-8, 8), Rn(0, 360), Rn(-8, 8));
                    go.transform.localScale = new Vector3(sc * Rn(0.8f, 1.3f), sc * Rn(0.6f, 1), sc * Rn(0.8f, 1.3f));
                    go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.On;
                    GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
                    boulders++;
                }
            }
            Debug.Log($"[SceneBuilder] roadside rocks: {boulders} boulders, {outcrops} outcrops");
        }

        // A strip hanging SkirtDepth metres down from each of the tile's free edges (the south
        // edge east of the patch, west, north, east), following the edge's heights.
        const float SkirtDepth = 16, SkirtStep = 1.5f;

        static void BuildRoadSkirt(Terrain tile)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            // Each edge twice, facing both ways (either terrain may be the one that dips), with
            // its own vertices: shared ones would average the normals to zero (NaN lighting).
            void Strip(Vector2 from, Vector2 to, bool flip)
            {
                int n = Mathf.CeilToInt(Vector2.Distance(from, to) / SkirtStep);
                int first = verts.Count;
                for (int i = 0; i <= n; i++)
                {
                    var p = Vector2.Lerp(from, to, i / (float)n);
                    float h = JungleHeight(p.x, p.y);
                    verts.Add(new Vector3(p.x, h, p.y));
                    verts.Add(new Vector3(p.x, h - SkirtDepth, p.y));
                    uvs.Add(new Vector2(i * SkirtStep / 4, 0));
                    uvs.Add(new Vector2(i * SkirtStep / 4, SkirtDepth / 4));
                    if (i == 0) continue;
                    int k = first + i * 2;
                    if (flip) tris.AddRange(new[] { k - 2, k - 1, k, k - 1, k + 1, k });
                    else tris.AddRange(new[] { k - 2, k, k - 1, k - 1, k, k + 1 });
                }
            }
            void Edge(Vector2 from, Vector2 to)
            {
                Strip(from, to, false);
                Strip(from, to, true);
            }
            float x0 = RoadLayout.TileMinX, x1 = RoadLayout.TileMaxX, z0 = RoadLayout.TileMinZ, z1 = RoadLayout.TileMaxZ;
            Edge(new Vector2(RoadLayout.PatchHalf, z0), new Vector2(x1, z0));
            Edge(new Vector2(x1, z0), new Vector2(x1, z1));
            Edge(new Vector2(x1, z1), new Vector2(x0, z1));
            Edge(new Vector2(x0, z1), new Vector2(x0, z0));
            var mesh = new Mesh { name = "RoadSkirt", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            string path = TerrainDir + "/RoadSkirt.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            var mat = SaveMaterial("RoadSkirt", Shader.Find("Universal Render Pipeline/Lit"), new Color(0.42f, 0.4f, 0.32f));
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{JungleImport.Dir}/Ground/forrest_ground_01_diff_1k.jpg"));
            mat.SetFloat("_Smoothness", 0.05f);
            var go = new GameObject("RoadSkirt");
            go.transform.SetParent(tile.transform, true);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
        }

        // ------------------------------------------------------------------ the jeep (greybox)

        // The jeep's pivots (VehicleRig) with Lit boxes and cylinders under each, sized for the
        // GameConfig.Jeep numbers: 3.1 m wheelbase, 2 m track, 0.53 m wheels, about 4.7 m long.
        // Each part's greybox is a child named "Greybox", so the Tripo part can replace it.
        static VehicleController BuildJeep(Transform parent, JungleStage stage)
        {
            var olive = LitMaterial("JeepOlive", new Color(0.29f, 0.32f, 0.2f), new Color(0.1f, 0.1f, 0.09f), 0.3f);
            var metal = LitMaterial("JeepGunmetal", new Color(0.16f, 0.17f, 0.18f), new Color(0.25f, 0.25f, 0.25f), 0.45f);
            var rubber = LitMaterial("JeepTyre", new Color(0.06f, 0.06f, 0.06f), new Color(0.05f, 0.05f, 0.05f), 0.15f);
            var seat = LitMaterial("JeepSeat", new Color(0.18f, 0.16f, 0.12f), new Color(0.05f, 0.05f, 0.05f), 0.2f);

            var root = new GameObject("Jeep").transform;
            root.SetParent(parent, false);
            Transform Pivot(string name, Transform under, Vector3 at, Quaternion rot = default)
            {
                var t = new GameObject(name).transform;
                t.SetParent(under, false);
                t.localPosition = at;
                t.localRotation = rot == default ? Quaternion.identity : rot;
                return t;
            }
            void Part(Transform under, PrimitiveType type, Material mat, Vector3 at, Vector3 size, Quaternion rot = default)
            {
                var g = under.Find("Greybox") ?? Pivot("Greybox", under, Vector3.zero);
                var go = GameObject.CreatePrimitive(type);
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.transform.SetParent(g, false);
                go.transform.localPosition = at;
                go.transform.localRotation = rot == default ? Quaternion.identity : rot;
                go.transform.localScale = size;
                var mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = ShadowCastingMode.On;
            }
            var rig = root.gameObject.AddComponent<VehicleRig>();
            float L = GameConfig.Jeep.Wheelbase / 2, T = GameConfig.Jeep.Track / 2, R = GameConfig.Jeep.WheelRadius;

            // Wheels: hubs on the chassis (not the sprung body), axles along x.
            var sideways = Quaternion.Euler(0, 0, 90);
            rig.wheels = new Transform[4];
            string[] names = { "Wheel_FL", "Wheel_FR", "Wheel_RL", "Wheel_RR" };
            for (int i = 0; i < 4; i++)
            {
                float x = i % 2 == 0 ? -T : T, z = i < 2 ? L : -L;
                var hub = Pivot(names[i], root, new Vector3(x, R, z));
                Part(hub, PrimitiveType.Cylinder, rubber, Vector3.zero, new Vector3(R * 2, 0.2f, R * 2), sideways);
                Part(hub, PrimitiveType.Cylinder, olive, new Vector3(Mathf.Sign(x) * 0.03f, 0, 0), new Vector3(R * 1.1f, 0.2f, R * 1.1f), sideways);
                rig.wheels[i] = hub;
            }

            // The body: tub, hood, fenders, bumpers, the windshield folded on the hood, seats, the
            // roll bar and the gun pedestal in the bed.
            var body = Pivot("Body", root, Vector3.zero);
            rig.body = body;
            Part(body, PrimitiveType.Cube, olive, new Vector3(0, 1.0f, -0.35f), new Vector3(2.0f, 0.75f, 3.3f));
            Part(body, PrimitiveType.Cube, olive, new Vector3(0, 1.08f, 1.65f), new Vector3(1.7f, 0.6f, 1.3f));
            foreach (float z in new[] { L, -L })
                foreach (float x in new[] { -1, 1 })
                    Part(body, PrimitiveType.Cube, olive, new Vector3(x * 1.02f, 1.2f, z), new Vector3(0.5f, 0.1f, 1.3f));
            Part(body, PrimitiveType.Cube, metal, new Vector3(0, 0.72f, 2.42f), new Vector3(2.2f, 0.3f, 0.22f));
            Part(body, PrimitiveType.Cube, metal, new Vector3(0, 0.72f, -2.08f), new Vector3(2.2f, 0.28f, 0.2f));
            Part(body, PrimitiveType.Cube, metal, new Vector3(0, 1.41f, 1.25f), new Vector3(1.75f, 0.05f, 0.65f));
            foreach (float x in new[] { -0.48f, 0.48f })
            {
                Part(body, PrimitiveType.Cube, seat, new Vector3(x, 1.2f, 0.05f), new Vector3(0.62f, 0.16f, 0.62f));
                Part(body, PrimitiveType.Cube, seat, new Vector3(x, 1.55f, -0.25f), new Vector3(0.62f, 0.62f, 0.12f));
            }
            foreach (float x in new[] { -0.98f, 0.98f })
                Part(body, PrimitiveType.Cube, metal, new Vector3(x, 1.75f, -0.55f), new Vector3(0.09f, 1.1f, 0.09f));
            Part(body, PrimitiveType.Cube, metal, new Vector3(0, 2.28f, -0.55f), new Vector3(2.05f, 0.09f, 0.09f));
            Part(body, PrimitiveType.Cube, metal, new Vector3(0, 1.75f, -1.35f), new Vector3(0.18f, 1.1f, 0.18f));

            // The steering wheel turns about its column, which leans back toward the driver.
            var steer = Pivot("Steering", body, new Vector3(-0.48f, 1.72f, 0.72f), Quaternion.LookRotation(new Vector3(0, 0.6f, -0.8f)));
            Part(steer, PrimitiveType.Cylinder, rubber, Vector3.zero, new Vector3(0.44f, 0.02f, 0.44f), Quaternion.Euler(90, 0, 0));
            Part(steer, PrimitiveType.Cube, metal, Vector3.zero, new Vector3(0.4f, 0.04f, 0.03f));
            rig.steering = steer;

            // The gun: a yoke that yaws on the pedestal, the gun pitching in it, the muzzle ahead.
            var yoke = Pivot("GunYoke", body, new Vector3(0, 2.3f, -1.35f));
            Part(yoke, PrimitiveType.Cylinder, metal, Vector3.zero, new Vector3(0.36f, 0.04f, 0.36f));
            foreach (float x in new[] { -0.2f, 0.2f })
                Part(yoke, PrimitiveType.Cube, metal, new Vector3(x, 0.2f, 0), new Vector3(0.05f, 0.4f, 0.12f));
            var gun = Pivot("Gun", yoke, new Vector3(0, 0.32f, 0));
            Part(gun, PrimitiveType.Cube, metal, new Vector3(0, 0, 0.05f), new Vector3(0.24f, 0.24f, 0.8f));
            Part(gun, PrimitiveType.Cylinder, metal, new Vector3(0, 0.02f, 0.95f), new Vector3(0.09f, 0.55f, 0.09f), Quaternion.Euler(90, 0, 0));
            Part(gun, PrimitiveType.Cube, olive, new Vector3(-0.2f, -0.05f, 0.05f), new Vector3(0.14f, 0.2f, 0.3f));
            foreach (float x in new[] { -0.09f, 0.09f })
                Part(gun, PrimitiveType.Cube, rubber, new Vector3(x, -0.02f, -0.42f), new Vector3(0.04f, 0.16f, 0.05f));
            rig.gunYoke = yoke;
            rig.gun = gun;
            rig.muzzle = Pivot("Muzzle", gun, new Vector3(0, 0.02f, 1.52f));

            // Seats and doors: the driver's cushion; where the gunner stands in the bed; and a
            // spot beside each front seat, clear of the jeep's box, to get in and out.
            rig.driver = Pivot("Driver", body, new Vector3(-0.48f, 1.28f, 0.05f));
            rig.gunner = Pivot("Gunner", body, new Vector3(0, 0.98f, -1.95f));
            float door = GameConfig.Jeep.FootprintRadius + GameConfig.PlayerRadius + 0.25f;
            rig.doorL = Pivot("DoorL", root, new Vector3(-door, 0, 0.05f));
            rig.doorR = Pivot("DoorR", root, new Vector3(door, 0, 0.05f));

            var controller = root.gameObject.AddComponent<VehicleController>();
            controller.rig = rig;
            controller.stage = stage;
            var park = RoadLayout.ParkingA;
            root.SetPositionAndRotation(park.position, park.rotation);
            return controller;
        }

        // Checkpoint B: the Tripo beacon (as at A), its lamp and a capture ring at the outpost.
        static void BuildCheckpointB(RoadStage road, Transform parent)
        {
            var root = new GameObject("CheckpointB").transform;
            root.SetParent(parent, false);
            var b = RoadLayout.CheckpointB;
            // Beside the junction, off both roads.
            var drive = RoadLayout.Drive.At(RoadLayout.Drive.Length - 2);
            var side = new Vector2(drive.Tangent.y, -drive.Tangent.x);
            var at = new Vector2(b.x, b.z) - side * 4.5f;
            root.position = new Vector3(at.x, JungleHeight(at.x, at.y), at.y);
            root.rotation = Quaternion.Euler(0, 160, 0);
            var model = PlaceProp("Beacon", root, beaconMat, 0, 1, 3.4f);
            foreach (var r in model.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.On;
            var light = new GameObject("Lamp").AddComponent<Light>();
            light.transform.SetParent(root, false);
            light.transform.localPosition = new Vector3(0, 3.6f, 0);
            light.type = LightType.Point;
            light.range = 10;
            light.intensity = 4;
            light.shadows = LightShadows.None;
            var ring = GlowQuad("CaptureRingB", parent, GlowMaterial("CheckpointRing", ringTex, Hdr(GameConfig.Hex(0x9bff67), 1.5f), GroundGlowQueue), RoadLayout.CaptureRadiusB * 2);
            ring.transform.position = b + Vector3.up * 0.12f;
            road.beaconB = root;
            road.lightB = light;
            road.ringB = ring.GetComponent<MeshRenderer>();
        }

        // The pause menu's DRIVE ASSIST row, in the jungle only (the arena's menus stay as they
        // are): a copy of the AUTO-VAULT row.
        static void BuildDriveAssistRow(Menus menus)
        {
            var row = Object.Instantiate(menus.vaultButton.gameObject, menus.vaultButton.transform.parent);
            row.name = "DriveAssist";
            menus.assistButton = row.GetComponent<UnityEngine.UI.Button>();
            menus.assistLabel = row.transform.Find("Label").GetComponent<UnityEngine.UI.Text>();
            menus.assistLabel.text = "DRIVE ASSIST";
        }

        // ------------------------------------------------------------------ the gun ride (greybox)

        // The ride's targets (Destructible) and the cover they stand behind, from RoadLayout:
        // drums, plates, crates and the fuel trailer as Lit greybox shapes; a log, sandbag nests,
        // the watchtower and the roadblock; B's gate (the M2 rubble and crates, no new art); and
        // a stand-in for the squadmate at the wheel.
        static void BuildRide(RoadStage road, Transform parent)
        {
            var root = new GameObject("Ride").transform;
            root.SetParent(parent, false);
            var drumMat = LitMaterial("RideDrum", new Color(0.32f, 0.36f, 0.22f), new Color(0.15f, 0.15f, 0.12f), 0.35f);
            var bandMat = LitMaterial("RideDrumBand", new Color(0.62f, 0.12f, 0.08f), new Color(0.1f, 0.1f, 0.1f), 0.3f);
            var plateMat = LitMaterial("RidePlate", new Color(0.36f, 0.38f, 0.3f), new Color(0.3f, 0.3f, 0.3f), 0.5f);
            var markMat = LitMaterial("RidePlateMark", new Color(0.9f, 0.9f, 0.86f), new Color(0.1f, 0.1f, 0.1f), 0.3f);
            var crateMat = LitMaterial("RideCrate", new Color(0.36f, 0.33f, 0.22f), new Color(0.08f, 0.08f, 0.08f), 0.2f);
            var bagMat = LitMaterial("RideSandbag", new Color(0.52f, 0.47f, 0.33f), new Color(0.05f, 0.05f, 0.05f), 0.1f);
            var woodMat = LitMaterial("RideWood", new Color(0.33f, 0.24f, 0.15f), new Color(0.05f, 0.05f, 0.05f), 0.15f);
            var metal = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/JeepGunmetal.mat");
            GameObject Shape(Transform under, PrimitiveType type, Material mat, Vector3 at, Vector3 size, Quaternion rot = default)
            {
                var go = GameObject.CreatePrimitive(type);
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.transform.SetParent(under, false);
                go.transform.localPosition = at;
                go.transform.localRotation = rot == default ? Quaternion.identity : rot;
                go.transform.localScale = size;
                var mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = ShadowCastingMode.On;
                return go;
            }

            var targets = new List<Destructible>();
            foreach (var t in RoadLayout.Targets)
            {
                var p = RoadLayout.RidePoint(t.S, t.Side, out var facing);
                var go = new GameObject(t.Kind.ToString());
                go.transform.SetParent(root, false);
                go.transform.SetPositionAndRotation(p + Vector3.up * t.Lift, facing);
                var d = go.AddComponent<Destructible>();
                d.kind = t.Kind;
                d.popAt = t.PopAt;
                d.roadblock = t.Roadblock;
                var g = go.transform;
                switch (t.Kind)
                {
                    case RoadLayout.TargetKind.Drum:
                        // A 200 l drum at the Warden's scale (1.4x): 0.85 m across, 1.25 m tall.
                        d.size = new Vector3(0.85f, 1.25f, 0.85f);
                        Shape(g, PrimitiveType.Cylinder, drumMat, new Vector3(0, 0.625f, 0), new Vector3(0.85f, 0.625f, 0.85f));
                        Shape(g, PrimitiveType.Cylinder, bandMat, new Vector3(0, 0.8f, 0), new Vector3(0.87f, 0.08f, 0.87f));
                        break;
                    case RoadLayout.TargetKind.Plate:
                        // A soldier silhouette plate, hinged at its foot, white hit mark.
                        d.size = new Vector3(1.0f, 1.9f, 0.3f);
                        Shape(g, PrimitiveType.Cube, plateMat, new Vector3(0, 0.75f, 0), new Vector3(1.0f, 1.5f, 0.06f));
                        Shape(g, PrimitiveType.Cube, plateMat, new Vector3(0, 1.68f, 0), new Vector3(0.45f, 0.45f, 0.06f));
                        Shape(g, PrimitiveType.Cylinder, markMat, new Vector3(0, 1.0f, 0.04f), new Vector3(0.36f, 0.01f, 0.36f), Quaternion.Euler(90, 0, 0));
                        break;
                    case RoadLayout.TargetKind.Crate:
                        d.size = new Vector3(1.4f, 1.4f, 1.4f);
                        Shape(g, PrimitiveType.Cube, crateMat, new Vector3(0, 0.7f, 0), new Vector3(1.4f, 1.4f, 1.4f));
                        break;
                    default:
                        // The fuel trailer: a tank on two wheels, a tow bar.
                        d.size = new Vector3(4.6f, 2.3f, 2.2f);
                        var turned = Quaternion.Euler(0, 90, 0);
                        Shape(g, PrimitiveType.Capsule, drumMat, new Vector3(0, 1.45f, 0), new Vector3(1.8f, 2.1f, 1.8f), Quaternion.Euler(0, 0, 90));
                        Shape(g, PrimitiveType.Cylinder, bandMat, new Vector3(0, 1.45f, 0), new Vector3(1.85f, 0.12f, 1.85f), Quaternion.Euler(0, 0, 90));
                        foreach (float x in new[] { -1.1f, 1.1f })
                            Shape(g, PrimitiveType.Cylinder, metal, new Vector3(0.4f, 0.5f, x), new Vector3(1.0f, 0.15f, 1.0f), Quaternion.Euler(90, 0, 0));
                        Shape(g, PrimitiveType.Cube, metal, new Vector3(-2.7f, 0.55f, 0), new Vector3(1.6f, 0.12f, 0.12f), turned);
                        break;
                }
                // The box is along the target's own x (across) and z (toward the road): the
                // trailer lies along the road.
                if (t.Kind == RoadLayout.TargetKind.Trailer) go.transform.rotation = facing * Quaternion.Euler(0, 90, 0);
                targets.Add(d);
            }
            road.targets = targets.ToArray();

            foreach (var c in RoadLayout.Covers)
            {
                var p = RoadLayout.RidePoint(c.S, c.Side, out var facing);
                var g = new GameObject(c.Kind.ToString()).transform;
                g.SetParent(root, false);
                g.SetPositionAndRotation(p, facing);
                switch (c.Kind)
                {
                    case RoadLayout.CoverKind.Log:
                        Shape(g, PrimitiveType.Cylinder, woodMat, new Vector3(0, 0.45f, 0), new Vector3(0.9f, 2.25f, 0.9f), Quaternion.Euler(0, 0, 90));
                        break;
                    case RoadLayout.CoverKind.Nest:
                        // A U of sandbags open away from the road.
                        Shape(g, PrimitiveType.Cube, bagMat, new Vector3(0, 0.6f, 0), new Vector3(3.2f, 1.2f, 1.1f));
                        foreach (float x in new[] { -1.6f, 1.6f })
                            Shape(g, PrimitiveType.Cube, bagMat, new Vector3(x, 0.5f, -1.1f), new Vector3(0.8f, 1.0f, 2.2f));
                        break;
                    case RoadLayout.CoverKind.Tower:
                        // Four legs, a platform 5 m up with a low wall, a roof.
                        foreach (float x in new[] { -1.3f, 1.3f })
                            foreach (float z in new[] { -1.3f, 1.3f })
                                Shape(g, PrimitiveType.Cube, woodMat, new Vector3(x, 3.1f, z), new Vector3(0.25f, 6.2f, 0.25f));
                        Shape(g, PrimitiveType.Cube, woodMat, new Vector3(0, 5.1f, 0), new Vector3(3.2f, 0.2f, 3.2f));
                        Shape(g, PrimitiveType.Cube, woodMat, new Vector3(0, 5.55f, 1.55f), new Vector3(3.2f, 0.7f, 0.1f));
                        Shape(g, PrimitiveType.Cube, metal, new Vector3(0, 7.6f, 0), new Vector3(3.8f, 0.08f, 3.8f));
                        break;
                    default:
                        // The roadblock: logs on X frames across the road.
                        Shape(g, PrimitiveType.Cylinder, woodMat, new Vector3(0, 0.45f, 0), new Vector3(0.8f, 5.5f, 0.8f), Quaternion.Euler(0, 0, 90));
                        Shape(g, PrimitiveType.Cylinder, woodMat, new Vector3(0, 1.0f, 0.1f), new Vector3(0.7f, 5.2f, 0.7f), Quaternion.Euler(0, 0, 90));
                        foreach (float x in new[] { -4.5f, -1.5f, 1.5f, 4.5f })
                        {
                            Shape(g, PrimitiveType.Cube, metal, new Vector3(x, 0.7f, 0), new Vector3(0.12f, 1.9f, 0.12f), Quaternion.Euler(0, 0, 40));
                            Shape(g, PrimitiveType.Cube, metal, new Vector3(x, 0.7f, 0), new Vector3(0.12f, 1.9f, 0.12f), Quaternion.Euler(0, 0, -40));
                        }
                        break;
                }
            }

            // B's gate: rubble and two crates across the ride road (M2 props, else greybox).
            var gb = RoadLayout.GateBlock(true);
            var gate = new GameObject("GateB").transform;
            gate.SetParent(root, false);
            gate.SetPositionAndRotation(new Vector3(gb.Center.x, gb.Bottom + 0.5f, gb.Center.y), Quaternion.Euler(0, gb.Yaw, 0));
            for (int k = -2; k <= 2; k++)
            {
                bool crate = k == -1 || k == 1;
                string prop = crate ? "supply_crate" : "gate_rubble";
                var at = new Vector3(k * 3.2f, 0, 0);
                if (File.Exists(CampaignImport.TraversalFbx(prop)))
                {
                    var holder = new GameObject(prop).transform;
                    holder.SetParent(gate, false);
                    var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CampaignImport.TraversalFbx(prop)));
                    model.transform.SetParent(holder, false);
                    var mat = TraversalMaterial(prop);
                    foreach (var r in model.GetComponentsInChildren<MeshRenderer>())
                    {
                        r.sharedMaterial = mat;
                        r.shadowCastingMode = ShadowCastingMode.On;
                    }
                    var lb = LocalBounds(model, holder);
                    float scale = (crate ? 1.6f : 3.4f) / Mathf.Max(lb.size.x, lb.size.z);
                    holder.localScale = Vector3.one * scale;
                    holder.localRotation = Quaternion.Euler(0, k * 37, 0);
                    lb = LocalBounds(holder.gameObject, gate);
                    holder.localPosition = new Vector3(at.x - lb.center.x, -0.6f - lb.min.y, at.z - lb.center.z);
                }
                else Shape(gate, PrimitiveType.Cube, crate ? crateMat : bagMat, at + Vector3.up * 0.9f, new Vector3(3, 1.8f, 1.2f));
            }
            road.gate = gate;

            // The squadmate's stand-in at the wheel (the clone of the Warden replaces it).
            var mate = new GameObject("Squadmate").transform;
            mate.SetParent(root, false);
            var olive = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/JeepOlive.mat");
            Shape(mate, PrimitiveType.Capsule, bagMat, new Vector3(0, 1.85f, 0), new Vector3(0.75f, 0.65f, 0.6f));
            Shape(mate, PrimitiveType.Sphere, olive, new Vector3(0, 2.6f, 0), Vector3.one * 0.42f);
            mate.gameObject.SetActive(false);
            road.squadmate = mate;
        }

        // The gun's heat bar (MissionHud.SetHeat places it): a thin bar with a fill.
        static void BuildHeatBar(MissionHud hud, Transform canvas)
        {
            var bar = Panel(canvas, "GunHeat", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120, 6), new Color(0, 0, 0, 0.45f));
            bar.pivot = new Vector2(0.5f, 0.5f);
            var fill = Panel(bar, "Fill", Vector2.zero, Vector2.zero, Vector2.zero, GameConfig.Accent);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0, 1);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            hud.heat = bar;
            hud.heatFill = fill;
            hud.heatImage = fill.GetComponent<UnityEngine.UI.Image>();
            bar.gameObject.SetActive(false);
        }
    }
}
