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
                    if (RoadDistance(p.x, p.y) < 10) continue;
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
            void Edge(Vector2 from, Vector2 to)
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
                    // Faces both ways: either terrain may be the one that dips.
                    tris.AddRange(new[] { k - 2, k, k - 1, k - 1, k, k + 1 });
                    tris.AddRange(new[] { k - 2, k - 1, k, k - 1, k + 1, k });
                }
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
    }
}
