using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace WardenZero.EditorTools
{
    // The jungle's plants and rocks, built as meshes from code: broadleaf trees (buttressed
    // trunk, branches, leaf-cluster cards), palms (curved trunk, drooping V-folded fronds),
    // shrubs, ferns, elephant-ear plants, grass tufts and mossy rocks. Trees get a far LOD:
    // two crossed cards showing a picture of the tree rendered here (an impostor).
    // Card normals point out of the crown, so the foliage shades as one soft mass.
    public static class JungleFlora
    {
        public const string Dir = JungleImport.Dir + "/Flora";

        public static Material Leaf, Palm, Fern, BigLeaf, Grass, Bark, PalmBark, Rock;
        public static Material[] TreeLeaves; // one shade per broadleaf variant

        // A small mesh builder with submeshes (0 = bark/rock, 1 = foliage).
        class MB
        {
            public readonly List<Vector3> V = new List<Vector3>();
            public readonly List<Vector3> N = new List<Vector3>();
            public readonly List<Vector2> UV = new List<Vector2>();
            public readonly List<int>[] T = { new List<int>(), new List<int>() };

            public int Add(Vector3 v, Vector3 n, Vector2 uv)
            {
                V.Add(v);
                N.Add(n.normalized);
                UV.Add(uv);
                return V.Count - 1;
            }

            public void Tri(int sub, int a, int b, int c)
            {
                T[sub].Add(a);
                T[sub].Add(b);
                T[sub].Add(c);
            }

            // A card: centre, right and up half-extents, normal, UV rect.
            public void Card(Vector3 c, Vector3 right, Vector3 up, Vector3 n, Rect uv)
            {
                int a = Add(c - right - up, n, new Vector2(uv.xMin, uv.yMin));
                int b = Add(c + right - up, n, new Vector2(uv.xMax, uv.yMin));
                int d = Add(c + right + up, n, new Vector2(uv.xMax, uv.yMax));
                int e = Add(c - right + up, n, new Vector2(uv.xMin, uv.yMax));
                Tri(1, a, d, b);
                Tri(1, a, e, d);
            }

            // A tapered tube along points with radii; u around, v along (metres / tile).
            public void Tube(IList<Vector3> pts, IList<float> radii, int sides, float vTile, System.Func<int, float, float> shape = null)
            {
                int start = V.Count;
                float v = 0;
                for (int i = 0; i < pts.Count; i++)
                {
                    Vector3 dir = (i < pts.Count - 1 ? pts[i + 1] - pts[i] : pts[i] - pts[i - 1]).normalized;
                    Vector3 side = Vector3.Cross(dir, Mathf.Abs(dir.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
                    Vector3 other = Vector3.Cross(dir, side);
                    if (i > 0) v += Vector3.Distance(pts[i], pts[i - 1]) / vTile;
                    for (int s = 0; s <= sides; s++)
                    {
                        float a = s / (float)sides * Mathf.PI * 2;
                        float r = radii[i] * (shape != null ? shape(i, a) : 1);
                        Vector3 n = side * Mathf.Cos(a) + other * Mathf.Sin(a);
                        Add(pts[i] + n * r, n, new Vector2(s / (float)sides * Mathf.Max(1, Mathf.Round(radii[0] * 4)), v));
                    }
                }
                for (int i = 0; i < pts.Count - 1; i++)
                for (int s = 0; s < sides; s++)
                {
                    int a = start + i * (sides + 1) + s, b = a + sides + 1;
                    Tri(0, a, b, a + 1);
                    Tri(0, a + 1, b, b + 1);
                }
            }

            public Mesh Build(string name)
            {
                var m = new Mesh { name = name };
                if (V.Count > 65000) m.indexFormat = IndexFormat.UInt32;
                m.SetVertices(V);
                m.SetNormals(N);
                m.SetUVs(0, UV);
                int subs = T[0].Count > 0 && T[1].Count > 0 ? 2 : 1;
                m.subMeshCount = subs;
                if (subs == 2)
                {
                    m.SetTriangles(T[0], 0);
                    m.SetTriangles(T[1], 1);
                }
                else
                {
                    m.SetTriangles(T[0].Count > 0 ? T[0] : T[1], 0);
                }
                m.RecalculateBounds();
                m.RecalculateTangents();
                return m;
            }
        }

        static System.Random rng;
        static float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        static readonly Rect[] LeafCells = { new Rect(0, 0, 0.5f, 0.5f), new Rect(0.5f, 0, 0.5f, 0.5f), new Rect(0, 0.5f, 0.5f, 0.5f), new Rect(0.5f, 0.5f, 0.5f, 0.5f) };

        // ------------------------------------------------------------------ materials

        public static void MakeMaterials(System.Func<string, Shader, Color, Material> save)
        {
            Directory.CreateDirectory(Dir);
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            Leaf = Foliage(save, "JungleLeaf", "leaves_broad.png", new Color(0.42f, 0.55f, 0.36f));
            TreeLeaves = new[]
            {
                Foliage(save, "JungleLeafDark", "leaves_broad.png", new Color(0.3f, 0.43f, 0.27f)),
                Foliage(save, "JungleLeafOlive", "leaves_broad.png", new Color(0.48f, 0.54f, 0.3f)),
                Foliage(save, "JungleLeafDeep", "leaves_broad.png", new Color(0.34f, 0.5f, 0.36f)),
            };
            Palm = Foliage(save, "JunglePalm", "palm_frond.png", new Color(0.62f, 0.72f, 0.48f));
            Fern = Foliage(save, "JungleFern", "fern_atlas.png", new Color(0.85f, 0.95f, 0.75f));
            BigLeaf = Foliage(save, "JungleBigLeaf", "big_leaf.png", new Color(0.5f, 0.66f, 0.42f));
            Grass = Foliage(save, "JungleGrass", "grass_tuft.png", new Color(0.8f, 0.95f, 0.62f));
            Bark = Surface(save, lit, "JungleBark", "Bark/tree_bark_03", new Color(0.78f, 0.78f, 0.72f));
            PalmBark = Surface(save, lit, "JunglePalmBark", "Bark/palm_bark", new Color(0.8f, 0.76f, 0.7f));
            Rock = Surface(save, lit, "JungleRock", "Rock/mossy_rock", new Color(0.85f, 0.88f, 0.8f));
        }

        // Alpha-clipped, two-sided foliage that also takes GPU instancing (terrain details).
        // FoliageLit is URP Lit plus the behind view's fade (plants never block the camera).
        static Material Foliage(System.Func<string, Shader, Color, Material> save, string name, string card, Color tint)
        {
            var m = save(name, Shader.Find("WardenZero/FoliageLit"), tint);
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(JungleArt.CardDir + "/" + card));
            m.SetFloat("_AlphaClip", 1);
            m.SetFloat("_Cutoff", 0.45f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.SetFloat("_Cull", 0);
            m.SetFloat("_Smoothness", 0.28f);
            m.SetFloat("_Metallic", 0);
            m.SetFloat("_EnvironmentReflections", 0);
            m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            m.renderQueue = (int)RenderQueue.AlphaTest;
            m.enableInstancing = true;
            m.doubleSidedGI = true;
            return m;
        }

        static Material Surface(System.Func<string, Shader, Color, Material> save, Shader lit, string name, string tex, Color tint)
        {
            var m = save(name, lit, tint);
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(JungleImport.Dir + "/" + tex + "_diff_1k.jpg"));
            m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(JungleImport.Dir + "/" + tex + "_nor_gl_1k.jpg"));
            m.EnableKeyword("_NORMALMAP");
            m.SetFloat("_Smoothness", 0.18f);
            m.SetFloat("_Metallic", 0);
            m.enableInstancing = true;
            return m;
        }

        // ------------------------------------------------------------------ plants

        // A rainforest canopy tree: tall straight trunk with buttress roots, a few heavy limbs
        // near the top, leaf clusters at the limb ends.
        static Mesh BroadleafTree(int seed, out float trunkRadius, out float height)
        {
            rng = new System.Random(seed);
            var mb = new MB();
            float H = R(17, 26);
            float r0 = R(0.42f, 0.62f);
            height = H;
            trunkRadius = r0;
            var pts = new List<Vector3>();
            var radii = new List<float>();
            Vector3 lean = new Vector3(R(-0.4f, 0.4f), 0, R(-0.4f, 0.4f));
            int rings = 12;
            for (int i = 0; i <= rings; i++)
            {
                float t = i / (float)rings;
                pts.Add(new Vector3(0, -0.3f + t * H * 0.82f, 0) + lean * t * t * 2 + new Vector3(Mathf.Sin(t * 7 + seed) * 0.15f, 0, Mathf.Cos(t * 5 + seed) * 0.15f));
                radii.Add(Mathf.Lerp(r0, r0 * 0.45f, t));
            }
            int lobes = rng.Next(4, 6);
            float phase = R(0, 6);
            // Buttress roots: star-shaped flare over the lowest rings.
            mb.Tube(pts, radii, 12, 2.2f, (i, a) =>
            {
                float t = i / (float)rings;
                float flare = Mathf.Clamp01(1 - t * 6);
                return 1 + flare * flare * 2.4f * Mathf.Pow(Mathf.Abs(Mathf.Cos((a + phase) * lobes / 2)), 6);
            });

            Vector3 top = pts[pts.Count - 1];
            int limbs = rng.Next(5, 8);
            var clusters = new List<Vector3>();
            for (int b = 0; b < limbs; b++)
            {
                float t = R(0.62f, 0.95f);
                Vector3 baseP = Vector3.Lerp(pts[0], top, t);
                float ang = b / (float)limbs * 360 + R(-25, 25);
                Vector3 dir = Quaternion.Euler(-R(28, 55), ang, 0) * Vector3.forward;
                float len = R(3.5f, 6.5f) * (1.15f - t * 0.4f);
                var lp = new List<Vector3> { baseP, baseP + dir * len * 0.5f + Vector3.up * 0.4f, baseP + dir * len + Vector3.up * R(0.6f, 1.6f) };
                mb.Tube(lp, new List<float> { r0 * 0.38f, r0 * 0.24f, r0 * 0.1f }, 6, 2.2f);
                clusters.Add(lp[2]);
                clusters.Add(Vector3.Lerp(lp[1], lp[2], 0.4f) + Vector3.up * 0.6f);
            }
            clusters.Add(top + Vector3.up * 0.8f);

            // Leaf clusters: cards in a flattened ball at each limb end.
            Vector3 crown = clusters.Aggregate(Vector3.zero, (a, c) => a + c) / clusters.Count;
            foreach (var c in clusters)
            {
                float rad = R(2.2f, 3.1f);
                int cards = 30;
                for (int k = 0; k < cards; k++)
                {
                    Vector3 off = Random3() * rad;
                    off.y *= 0.55f;
                    Vector3 p = c + off;
                    Vector3 n = ((p - crown).normalized + Vector3.up * 0.8f).normalized;
                    var rot = Quaternion.LookRotation(Random3(), Vector3.up);
                    float size = R(0.85f, 1.3f);
                    mb.Card(p, rot * Vector3.right * size, rot * Vector3.up * size, n, LeafCells[rng.Next(4)]);
                }
            }
            return mb.Build("Broadleaf" + seed);
        }

        // A coconut-style palm: a curved trunk and a crown of drooping, V-folded fronds.
        static Mesh PalmTree(int seed, out float height)
        {
            rng = new System.Random(seed);
            var mb = new MB();
            float H = R(8, 13);
            height = H;
            Vector3 bend = new Vector3(R(-1, 1), 0, R(-1, 1)).normalized * R(1, 2.5f);
            var pts = new List<Vector3>();
            var radii = new List<float>();
            for (int i = 0; i <= 10; i++)
            {
                float t = i / 10f;
                pts.Add(new Vector3(0, -0.2f + t * H, 0) + bend * t * t);
                radii.Add(Mathf.Lerp(0.26f, 0.16f, t) * (i == 0 ? 1.3f : 1));
            }
            mb.Tube(pts, radii, 8, 1.2f);
            Vector3 top = pts[pts.Count - 1];
            int fronds = rng.Next(11, 15);
            for (int f = 0; f < fronds; f++)
            {
                float yaw = f / (float)fronds * 360 + R(-12, 12);
                float lift = R(-10, 45);
                float len = R(3.8f, 5.2f);
                var q = Quaternion.Euler(-lift, yaw, 0);
                const int segs = 7;
                Vector3 p = top, d = q * Vector3.forward;
                int prev = -1;
                for (int s = 0; s <= segs; s++)
                {
                    float t = s / (float)segs;
                    float w = Mathf.Lerp(0.55f, 0.75f, Mathf.Sin(t * Mathf.PI)) * (t > 0.85f ? Mathf.Lerp(1, 0.3f, (t - 0.85f) / 0.15f) : 1);
                    Vector3 right = Vector3.Cross(Vector3.up, d).normalized;
                    Vector3 up = Vector3.Cross(d, right).normalized;
                    Vector3 n = (up + Vector3.up * 0.5f).normalized;
                    // Three vertices across: the leaflets sag either side of the raised midrib.
                    int l = mb.Add(p - right * w - up * 0.12f, n, new Vector2(0, t));
                    int m = mb.Add(p, n, new Vector2(0.5f, t));
                    int r = mb.Add(p + right * w - up * 0.12f, n, new Vector2(1, t));
                    if (prev >= 0)
                    {
                        mb.Tri(1, prev, l, prev + 1);
                        mb.Tri(1, prev + 1, l, m);
                        mb.Tri(1, prev + 1, m, prev + 2);
                        mb.Tri(1, prev + 2, m, r);
                    }
                    prev = l;
                    p += d * len / segs;
                    d = (d + Vector3.down * 0.22f).normalized; // droops toward the tip
                }
            }
            return mb.Build("Palm" + seed);
        }

        // Understory shrub: a dome of leaf-cluster cards.
        static Mesh Shrub(int seed)
        {
            rng = new System.Random(seed);
            var mb = new MB();
            float rad = R(1.1f, 1.8f);
            for (int k = 0; k < 26; k++)
            {
                Vector3 off = Random3() * rad;
                off.y = Mathf.Abs(off.y) * 0.8f;
                Vector3 p = off + Vector3.up * 0.5f;
                Vector3 n = (off.normalized + Vector3.up).normalized;
                var rot = Quaternion.LookRotation(Random3(), Vector3.up);
                float size = R(0.55f, 0.85f);
                mb.Card(p, rot * Vector3.right * size, rot * Vector3.up * size, n, LeafCells[rng.Next(4)]);
            }
            return mb.Build("Shrub" + seed);
        }

        // Fern clump: fronds from the scanned fern atlas, arching out from the centre.
        static Mesh FernClump(int seed, List<Rect> fronds)
        {
            rng = new System.Random(seed);
            var mb = new MB();
            int count = rng.Next(8, 12);
            for (int f = 0; f < count; f++)
            {
                var uv = fronds[rng.Next(fronds.Count)];
                float yaw = f / (float)count * 360 + R(-15, 15);
                float len = R(0.9f, 1.5f);
                float w = len * uv.width / uv.height * 1.1f;
                Arch(mb, Vector3.zero, yaw, R(45, 70), len, w, uv, 0.32f);
            }
            return mb.Build("Fern" + seed);
        }

        // Elephant-ear plant: big heart leaves on long stalks.
        static Mesh BigLeafPlant(int seed)
        {
            rng = new System.Random(seed);
            var mb = new MB();
            int count = rng.Next(4, 7);
            for (int f = 0; f < count; f++)
            {
                float yaw = f / (float)count * 360 + R(-20, 20);
                Arch(mb, Vector3.up * R(0.5f, 1.1f), yaw, R(10, 40), R(0.9f, 1.3f), R(0.75f, 1f), new Rect(0, 0, 1, 1), 0.25f);
            }
            return mb.Build("BigLeaf" + seed);
        }

        // A card bent into an arch: starts at `root` rising at `rise` degrees, droops along.
        static void Arch(MB mb, Vector3 root, float yaw, float rise, float len, float width, Rect uv, float droop)
        {
            const int segs = 4;
            var q = Quaternion.Euler(-rise, yaw, 0);
            Vector3 d = q * Vector3.forward, p = root;
            Vector3 right = Quaternion.Euler(0, yaw, 0) * Vector3.right;
            int prev = -1;
            for (int s = 0; s <= segs; s++)
            {
                float t = s / (float)segs;
                Vector3 n = (Vector3.Cross(right, d) * -1 + Vector3.up).normalized;
                if (Vector3.Dot(n, Vector3.up) < 0) n = -n;
                int a = mb.Add(p - right * width / 2, n, new Vector2(uv.xMin, Mathf.Lerp(uv.yMin, uv.yMax, t)));
                int b = mb.Add(p + right * width / 2, n, new Vector2(uv.xMax, Mathf.Lerp(uv.yMin, uv.yMax, t)));
                if (prev >= 0)
                {
                    mb.Tri(1, prev, a, prev + 1);
                    mb.Tri(1, prev + 1, a, b);
                }
                prev = a;
                p += d * len / segs;
                d = (d + Vector3.down * droop).normalized;
            }
        }

        // Three crossed blades of the grass-tuft card.
        static Mesh GrassTuft(int seed)
        {
            rng = new System.Random(seed);
            var mb = new MB();
            for (int k = 0; k < 3; k++)
            {
                var rot = Quaternion.Euler(R(-8, 8), k * 60 + R(-10, 10), 0);
                float h = R(0.45f, 0.7f), w = h;
                var cell = new Rect(rng.Next(2) * 0.5f, 0, 0.5f, 1);
                mb.Card(Vector3.up * h * 0.5f, rot * Vector3.right * w * 0.5f, rot * Vector3.up * h * 0.5f, Vector3.up, cell);
            }
            return mb.Build("Grass" + seed);
        }

        // A mossy boulder: a noisy, squashed sphere with box-projected UVs.
        static Mesh Boulder(int seed)
        {
            rng = new System.Random(seed);
            var src = Resources.GetBuiltinResource<Mesh>("New-Sphere.fbx");
            var verts = src.vertices;
            var tris = src.triangles;
            float ox = R(0, 100), oz = R(0, 100);
            var outV = new Vector3[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                var v = verts[i] * 2;
                float n = Mathf.PerlinNoise(ox + v.x * 1.3f + v.y * 0.7f, oz + v.z * 1.3f - v.y * 0.5f);
                float n2 = Mathf.PerlinNoise(oz + v.x * 3.1f, ox + v.z * 3.1f + v.y * 2f);
                v *= 0.75f + n * 0.5f + n2 * 0.15f;
                v.y = v.y > 0 ? v.y * 0.65f : v.y * 0.3f;
                outV[i] = v;
            }
            var m = new Mesh { name = "Boulder" + seed };
            m.vertices = outV;
            m.triangles = tris;
            m.RecalculateNormals();
            var uv = new Vector2[outV.Length];
            var nn = m.normals;
            for (int i = 0; i < outV.Length; i++)
            {
                var a = new Vector3(Mathf.Abs(nn[i].x), Mathf.Abs(nn[i].y), Mathf.Abs(nn[i].z));
                uv[i] = a.y >= a.x && a.y >= a.z ? new Vector2(outV[i].x, outV[i].z) : a.x >= a.z ? new Vector2(outV[i].z, outV[i].y) : new Vector2(outV[i].x, outV[i].y);
                uv[i] *= 0.35f;
            }
            m.uv = uv;
            m.RecalculateBounds();
            m.RecalculateTangents();
            return m;
        }

        static Vector3 Random3()
        {
            Vector3 v;
            do v = new Vector3(R(-1, 1), R(-1, 1), R(-1, 1)); while (v.sqrMagnitude > 1 || v.sqrMagnitude < 0.01f);
            return v;
        }

        // ------------------------------------------------------------------ prefabs

        public class Set
        {
            public List<GameObject> Trees = new List<GameObject>(); // broadleaf, with impostor LOD
            public List<float> TreeRadius = new List<float>();
            public List<GameObject> Palms = new List<GameObject>();
            public List<GameObject> Shrubs = new List<GameObject>();
            public List<GameObject> Ferns = new List<GameObject>();
            public List<GameObject> BigLeaves = new List<GameObject>();
            public List<GameObject> Grasses = new List<GameObject>();
            public List<GameObject> Rocks = new List<GameObject>();
            public List<GameObject> FarTrees = new List<GameObject>(); // impostor only, for the landscape
        }

        public static Set Build()
        {
            var set = new Set();
            var fernImg = LoadCard("fern_atlas.png");
            var fern = JungleArt.Cut(fernImg, 0, 2000);
            float fw = fernImg.W, fh = fernImg.H;
            var fernRects = fern.Select(p => new Rect(p.Rect.x / fw, p.Rect.y / fh, p.Rect.width / fw, p.Rect.height / fh)).ToList();
            Debug.Log($"[JungleFlora] fern fronds: {fernRects.Count}");

            for (int i = 0; i < 3; i++)
            {
                var mesh = BroadleafTree(100 + i, out float r, out float h);
                var tree = Prefab("Broadleaf" + i, mesh, new[] { Bark, TreeLeaves[i % TreeLeaves.Length] }, true, h);
                set.Trees.Add(tree.full);
                set.FarTrees.Add(tree.far);
                set.TreeRadius.Add(r);
            }
            for (int i = 0; i < 2; i++)
            {
                var mesh = PalmTree(200 + i, out float h);
                var palm = Prefab("Palm" + i, mesh, new[] { PalmBark, Palm }, true, h);
                set.Palms.Add(palm.full);
                set.FarTrees.Add(palm.far);
            }
            for (int i = 0; i < 2; i++) set.Shrubs.Add(Prefab("Shrub" + i, Shrub(300 + i), new[] { Leaf }, false, 0).full);
            for (int i = 0; i < 2; i++) set.Ferns.Add(Prefab("Fern" + i, FernClump(400 + i, fernRects), new[] { Fern }, false, 0).full);
            set.BigLeaves.Add(Prefab("BigLeaf0", BigLeafPlant(500), new[] { BigLeaf }, false, 0).full);
            set.Grasses.Add(Prefab("Grass0", GrassTuft(600), new[] { Grass }, false, 0).full);
            for (int i = 0; i < 3; i++) set.Rocks.Add(Prefab("Rock" + i, Boulder(700 + i), new[] { Rock }, false, 0).full);
            return set;
        }

        static JungleArt.Img LoadCard(string name)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            t.LoadImage(File.ReadAllBytes(JungleArt.CardDir + "/" + name));
            var img = new JungleArt.Img(t.width, t.height) { Px = t.GetPixels() };
            Object.DestroyImmediate(t);
            return img;
        }

        // Saves the mesh and a prefab. Trees also get an impostor: LOD0 the mesh, LOD1 the
        // crossed cards; `far` is a second prefab with only the cards (for the landscape).
        static (GameObject full, GameObject far) Prefab(string name, Mesh mesh, Material[] mats, bool impostor, float height)
        {
            string meshPath = $"{Dir}/{name}.asset";
            AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(mesh, meshPath);
            var go = new GameObject(name);
            // Trees keep the mesh on a child (LOD group); small plants on the root, which
            // terrain detail meshes require.
            var body = go;
            if (impostor)
            {
                body = new GameObject("LOD0");
                body.transform.SetParent(go.transform, false);
            }
            body.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = body.AddComponent<MeshRenderer>();
            mr.sharedMaterials = mats;
            mr.shadowCastingMode = impostor ? ShadowCastingMode.On : ShadowCastingMode.Off;

            GameObject far = null;
            if (impostor)
            {
                var card = ImpostorCard(name, body, height);
                var lod1 = Object.Instantiate(card, go.transform);
                lod1.name = "LOD1";
                var group = go.AddComponent<LODGroup>();
                group.SetLODs(new[]
                {
                    new LOD(0.2f, new Renderer[] { mr }),
                    new LOD(0.004f, new Renderer[] { lod1.GetComponent<Renderer>() }),
                });
                group.fadeMode = LODFadeMode.None;
                group.RecalculateBounds();
                var farGo = new GameObject(name + "Far");
                var farCard = Object.Instantiate(card, farGo.transform);
                farCard.name = "LOD0";
                var farGroup = farGo.AddComponent<LODGroup>();
                farGroup.SetLODs(new[] { new LOD(0.003f, new Renderer[] { farCard.GetComponent<Renderer>() }) });
                far = PrefabUtility.SaveAsPrefabAsset(farGo, $"{Dir}/{name}Far.prefab");
                Object.DestroyImmediate(farGo);
                Object.DestroyImmediate(card);
            }
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{Dir}/{name}.prefab");
            Object.DestroyImmediate(go);
            return (prefab, far);
        }

        // Renders the plant from two sides into a 512x512 picture (two 256x512 halves) and
        // builds two crossed cards that show it.
        static GameObject ImpostorCard(string name, GameObject body, float height)
        {
            var b = body.GetComponent<Renderer>().bounds;
            float halfW = Mathf.Max(b.extents.x, b.extents.z) * 1.05f;
            float top = b.max.y * 1.02f;
            const int W = 256, Hp = 512;
            var rt = new RenderTexture(W, Hp, 24, RenderTextureFormat.ARGB32);
            var atlas = new Texture2D(W * 2, Hp, TextureFormat.RGBA32, false);
            var camGo = new GameObject("ImpostorCam");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0, 0, 0, 0);
            cam.targetTexture = rt;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200;
            cam.allowHDR = false;
            cam.allowMSAA = false;
            var urp = camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            urp.renderPostProcessing = false;
            urp.renderShadows = false;
            var lightGo = new GameObject("ImpostorSun");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.6f;
            light.color = new Color(1, 0.96f, 0.88f);
            lightGo.transform.rotation = Quaternion.Euler(50, 30, 0);
            var ambient = RenderSettings.ambientLight;
            var mode = RenderSettings.ambientMode;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.5f, 0.45f);
            // Place the plant far from anything else in the scene.
            var holder = new GameObject("ImpostorSubject");
            holder.transform.position = new Vector3(0, -2000, 0);
            var copy = Object.Instantiate(body, holder.transform, false);
            for (int view = 0; view < 2; view++)
            {
                float yaw = view * 90;
                var dir = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
                cam.aspect = W / (float)Hp;
                cam.orthographicSize = Mathf.Max(top / 2, halfW / cam.aspect);
                camGo.transform.position = holder.transform.position + Vector3.up * cam.orthographicSize - dir * 60;
                camGo.transform.rotation = Quaternion.LookRotation(dir);
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(W, Hp, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, W, Hp), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                atlas.SetPixels(view * W, 0, W, Hp, tex.GetPixels());
                Object.DestroyImmediate(tex);
            }
            float size = cam.orthographicSize; // half height of each view, in metres
            RenderSettings.ambientMode = mode;
            RenderSettings.ambientLight = ambient;
            Object.DestroyImmediate(holder);
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(lightGo);
            rt.Release();

            string texPath = $"{Dir}/{name}_impostor.png";
            File.WriteAllBytes(texPath, atlas.EncodeToPNG());
            Object.DestroyImmediate(atlas);
            AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceUpdate);
            var ti = (TextureImporter)AssetImporter.GetAtPath(texPath);
            ti.alphaIsTransparency = true;
            ti.mipMapsPreserveCoverage = true;
            ti.alphaTestReferenceValue = 0.4f;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.maxTextureSize = 512;
            ti.SaveAndReimport();

            var mat = new Material(Leaf) { name = name + "Impostor" };
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            mat.SetColor("_BaseColor", Color.white);
            string matPath = $"{Dir}/{name}_impostor.mat";
            AssetDatabase.DeleteAsset(matPath);
            AssetDatabase.CreateAsset(mat, matPath);

            var mb = new MB();
            float hw = size * W / (float)Hp, hh = size;
            mb.Card(new Vector3(0, hh, 0), Vector3.right * hw, Vector3.up * hh, Vector3.back, new Rect(0, 0, 0.5f, 1));
            mb.Card(new Vector3(0, hh, 0), Vector3.forward * hw, Vector3.up * hh, Vector3.left, new Rect(0.5f, 0, 0.5f, 1));
            // Soft normals: up-ish, so both cards shade alike whatever the sun.
            for (int i = 0; i < mb.N.Count; i++) mb.N[i] = (mb.N[i] * 0.3f + Vector3.up).normalized;
            var mesh = mb.Build(name + "Impostor");
            string meshPath = $"{Dir}/{name}_impostor.asset";
            AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(mesh, meshPath);
            var go = new GameObject("Impostor");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }
    }
}
