using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace WardenZero.EditorTools
{
    // Composes the jungle's foliage cards from CC0 photo atlases (unity/ArtSource/Jungle,
    // see Assets/ThirdParty/CREDITS.md): single scanned leaves become leaf clusters, grass
    // blades become tufts, long blades become palm fronds. Also paints the canopy texture
    // seen from the air. Everything is seeded, so a rebuild gives the same pictures.
    public static class JungleArt
    {
        public const string SourceDir = "ArtSource/Jungle"; // relative to the project folder
        public const string CardDir = JungleImport.Dir + "/Cards";
        public const string GroundDir = JungleImport.Dir + "/Ground";

        // A CPU image: linear-free RGBA floats, (0,0) bottom left like Texture2D.
        public class Img
        {
            public int W, H;
            public Color[] Px;

            public Img(int w, int h)
            {
                W = w;
                H = h;
                Px = new Color[w * h];
            }

            public Color Get(int x, int y) => Px[Mathf.Clamp(y, 0, H - 1) * W + Mathf.Clamp(x, 0, W - 1)];

            public Color Sample(float x, float y)
            {
                if (x < 0 || y < 0 || x > W - 1 || y > H - 1) return Color.clear;
                int x0 = (int)x, y0 = (int)y;
                float fx = x - x0, fy = y - y0;
                var a = Color.Lerp(Get(x0, y0), Get(x0 + 1, y0), fx);
                var b = Color.Lerp(Get(x0, y0 + 1), Get(x0 + 1, y0 + 1), fx);
                return Color.Lerp(a, b, fy);
            }

            // Alpha-over at (x, y).
            public void Over(int x, int y, Color c)
            {
                if (x < 0 || y < 0 || x >= W || y >= H || c.a <= 0) return;
                int i = y * W + x;
                var d = Px[i];
                float a = c.a + d.a * (1 - c.a);
                if (a <= 0) return;
                Px[i] = new Color((c.r * c.a + d.r * d.a * (1 - c.a)) / a, (c.g * c.a + d.g * d.a * (1 - c.a)) / a, (c.b * c.a + d.b * d.a * (1 - c.a)) / a, a);
            }

            public void Save(string assetPath)
            {
                var t = new Texture2D(W, H, TextureFormat.RGBA32, false);
                t.SetPixels(Px);
                File.WriteAllBytes(assetPath, t.EncodeToPNG());
                Object.DestroyImmediate(t);
            }
        }

        // A piece cut from an atlas: the leaf or blade with its stem at the bottom (y = 0).
        public class Piece
        {
            public Img Img;
            public RectInt Rect; // where it came from in the source atlas
        }

        static Img Load(string file)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            t.LoadImage(File.ReadAllBytes(Path.Combine(SourceDir, file)));
            var img = new Img(t.width, t.height) { Px = t.GetPixels() };
            Object.DestroyImmediate(t);
            return img;
        }

        // Colour plus a separate opacity map (its red channel).
        public static Img LoadRgba(string color, string opacity)
        {
            var c = Load(color);
            var o = Load(opacity);
            for (int i = 0; i < c.Px.Length; i++) c.Px[i].a = o.Px[i].r;
            return c;
        }

        // Cut every separate shape out of an atlas (connected opacity on a coarse grid).
        // turns: quarter turns (counter-clockwise) that bring the stem to the bottom.
        public static List<Piece> Cut(Img atlas, int turns, int minArea = 400)
        {
            const int G = 4; // grid step in pixels
            int gw = atlas.W / G, gh = atlas.H / G;
            var label = new int[gw * gh];
            var pieces = new List<Piece>();
            var stack = new Stack<int>();
            int next = 1;
            for (int s = 0; s < label.Length; s++)
            {
                if (label[s] != 0 || atlas.Px[(s / gw) * G * atlas.W + (s % gw) * G].a < 0.5f) continue;
                int minX = int.MaxValue, minY = int.MaxValue, maxX = 0, maxY = 0, area = 0;
                label[s] = next;
                stack.Push(s);
                while (stack.Count > 0)
                {
                    int c = stack.Pop();
                    int cx = c % gw, cy = c / gw;
                    area++;
                    minX = Mathf.Min(minX, cx); maxX = Mathf.Max(maxX, cx);
                    minY = Mathf.Min(minY, cy); maxY = Mathf.Max(maxY, cy);
                    for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = cx + dx, ny = cy + dy;
                        if (nx < 0 || ny < 0 || nx >= gw || ny >= gh) continue;
                        int n = ny * gw + nx;
                        if (label[n] != 0 || atlas.Px[ny * G * atlas.W + nx * G].a < 0.5f) continue;
                        label[n] = next;
                        stack.Push(n);
                    }
                }
                next++;
                if (area * G * G < minArea) continue;
                var rect = new RectInt(Mathf.Max(0, minX * G - 6), Mathf.Max(0, minY * G - 6), 0, 0);
                rect.width = Mathf.Min(atlas.W, maxX * G + G + 6) - rect.x;
                rect.height = Mathf.Min(atlas.H, maxY * G + G + 6) - rect.y;
                var img = new Img(rect.width, rect.height);
                for (int y = 0; y < rect.height; y++)
                for (int x = 0; x < rect.width; x++)
                    img.Px[y * rect.width + x] = atlas.Get(rect.x + x, rect.y + y);
                for (int t = 0; t < ((turns % 4) + 4) % 4; t++) img = Turn(img);
                pieces.Add(new Piece { Img = img, Rect = rect });
            }
            return pieces;
        }

        static Img Turn(Img a)
        {
            var b = new Img(a.H, a.W);
            for (int y = 0; y < a.H; y++)
            for (int x = 0; x < a.W; x++)
                b.Px[x * b.W + (a.H - 1 - y)] = a.Px[y * a.W + x];
            return b;
        }

        // Draw `p` with its stem at (x, y), pointing at `angle` (degrees, 0 = up, positive =
        // counter-clockwise), `length` pixels from stem to tip, its colour scaled by `tint`.
        public static void Stamp(Img dst, Piece p, float x, float y, float angle, float length, Color tint, float squash = 1)
        {
            float s = length / p.Img.H;
            float a = angle * Mathf.Deg2Rad;
            Vector2 up = new Vector2(-Mathf.Sin(a), Mathf.Cos(a)), right = new Vector2(up.y, -up.x);
            float hw = p.Img.W * s * squash / 2, h = p.Img.H * s;
            // Destination bounds of the rotated rectangle.
            Vector2 o = new Vector2(x, y);
            Vector2[] corners = { o - right * hw, o + right * hw, o - right * hw + up * h, o + right * hw + up * h };
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (var c in corners)
            {
                minX = Mathf.Min(minX, c.x); maxX = Mathf.Max(maxX, c.x);
                minY = Mathf.Min(minY, c.y); maxY = Mathf.Max(maxY, c.y);
            }
            for (int py = Mathf.FloorToInt(minY); py <= Mathf.CeilToInt(maxY); py++)
            for (int px = Mathf.FloorToInt(minX); px <= Mathf.CeilToInt(maxX); px++)
            {
                Vector2 d = new Vector2(px, py) - o;
                float u = Vector2.Dot(d, right) / (s * squash) + p.Img.W / 2f;
                float v = Vector2.Dot(d, up) / s;
                var c = p.Img.Sample(u, v);
                if (c.a <= 0.01f) continue;
                dst.Over(px, py, new Color(c.r * tint.r, c.g * tint.g, c.b * tint.b, c.a));
            }
        }

        // ------------------------------------------------------------------ cards

        public static void WriteAll()
        {
            Directory.CreateDirectory(CardDir);
            var rng = new System.Random(21);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            // Broadleaf clusters: a 2x2 atlas of leafy clumps radiating from a hidden twig.
            var broad = new List<Piece>();
            broad.AddRange(Cut(LoadRgba("LeafSet022_Color.png", "LeafSet022_Opacity.png"), 0));
            broad.AddRange(Cut(LoadRgba("LeafSet023_Color.png", "LeafSet023_Opacity.png"), 3));
            broad.AddRange(Cut(LoadRgba("LeafSet024_Color.png", "LeafSet024_Opacity.png"), 0));
            Debug.Log($"[JungleArt] broad leaves: {broad.Count}");
            var leaves = new Img(1024, 1024);
            for (int cell = 0; cell < 4; cell++)
            {
                int ox = (cell % 2) * 512, oy = (cell / 2) * 512;
                var sub = new Img(512, 512);
                int n = 34;
                for (int i = 0; i < n; i++)
                {
                    var p = broad[rng.Next(broad.Count)];
                    // Back leaves first (darker), front leaves last (lighter).
                    float depth = i / (float)n;
                    float ang = R(0, 360);
                    float r = R(10, 110);
                    float cx = 256 + Mathf.Sin(ang * Mathf.Deg2Rad) * r * 0.5f, cy = 256 - Mathf.Cos(ang * Mathf.Deg2Rad) * r * 0.5f;
                    float shade = Mathf.Lerp(0.45f, 1.05f, depth) * R(0.85f, 1.1f);
                    var tint = new Color(shade * R(0.85f, 1.05f), shade, shade * R(0.75f, 0.95f));
                    Stamp(sub, p, cx, cy, -ang, R(170, 230), tint, R(0.85f, 1.1f));
                }
                for (int y = 0; y < 512; y++)
                for (int x = 0; x < 512; x++)
                    leaves.Px[(oy + y) * 1024 + ox + x] = sub.Px[y * 512 + x];
            }
            leaves.Save(CardDir + "/leaves_broad.png");

            // Palm frond: leaflets either side of a midrib, swept toward the tip.
            var blades = Cut(LoadRgba("Foliage008_Color.png", "Foliage008_Opacity.png"), 1, 300);
            Debug.Log($"[JungleArt] palm leaflets: {blades.Count}");
            var frond = new Img(512, 1024);
            for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i < 46; i++)
            {
                float t = 0.04f + i / 46f * 0.94f;
                float y = t * 1000;
                float x = 256 + Mathf.Sin(t * 2.2f) * 12;
                float len = Mathf.Lerp(150, 250, Mathf.Sin(t * Mathf.PI * 0.9f + 0.15f)) * R(0.9f, 1.05f);
                if (t > 0.85f) len *= Mathf.Lerp(1, 0.4f, (t - 0.85f) / 0.15f);
                float shade = R(0.75f, 1.05f);
                Stamp(frond, blades[rng.Next(blades.Count)], x, y, side * R(52, 64), len, new Color(shade * 0.95f, shade, shade * 0.8f), 0.7f);
            }
            var rib = new Color(0.35f, 0.38f, 0.18f, 1);
            for (int y = 0; y < 1000; y++)
            {
                float t = y / 1000f;
                int x = Mathf.RoundToInt(256 + Mathf.Sin(t * 2.2f) * 12);
                int w = Mathf.RoundToInt(Mathf.Lerp(6, 1, t));
                for (int dx = -w; dx <= w; dx++) frond.Over(x + dx, y, rib);
            }
            frond.Save(CardDir + "/palm_frond.png");

            // Grass tufts: a 2x1 atlas of fans of blades.
            var grass = Cut(LoadRgba("Foliage001_Color.png", "Foliage001_Opacity.png"), 0, 300);
            Debug.Log($"[JungleArt] grass blades: {grass.Count}");
            var tuft = new Img(1024, 512);
            for (int cell = 0; cell < 2; cell++)
            for (int i = 0; i < 40; i++)
            {
                float shade = R(0.6f, 1.05f);
                Stamp(tuft, grass[rng.Next(grass.Count)], cell * 512 + 256 + R(-60, 60), 0, R(-28, 28), R(260, 480),
                    new Color(shade * R(0.9f, 1.1f), shade, shade * 0.8f), R(0.8f, 1.3f));
            }
            tuft.Save(CardDir + "/grass_tuft.png");

            // Plants from Poly Haven's scanned atlases, alpha merged in.
            LoadRgba("fern_02_diff_1k.jpg", "fern_02_alpha_1k.png").Save(CardDir + "/fern_atlas.png");
            // One big heart-shaped leaf for the elephant-ear plants, stem at the bottom.
            var hearts = Cut(LoadRgba("LeafSet023_Color.png", "LeafSet023_Opacity.png"), 3);
            var big = new Img(512, 512);
            Stamp(big, hearts[0], 256, 4, 0, 500, Color.white, 1);
            big.Save(CardDir + "/big_leaf.png");

            WriteCanopy();
            AssetDatabase.Refresh();
        }

        // The jungle seen from the air: packed tree crowns (domes of leafy noise), with a normal
        // map from their height. Tiles every 128 m on the landscape terrain.
        static void WriteCanopy()
        {
            const int S = 1024;
            var rng = new System.Random(4);
            var height = new float[S * S];
            var tone = new float[S * S];
            var dome = new float[S * S];
            // Crowns: random discs on a torus (so the tile repeats seamlessly).
            for (int i = 0; i < 520; i++)
            {
                float cx = (float)rng.NextDouble() * S, cy = (float)rng.NextDouble() * S;
                float r = 28 + (float)rng.NextDouble() * 60;
                float t = (float)rng.NextDouble();
                int r0 = Mathf.CeilToInt(r);
                for (int dy = -r0; dy <= r0; dy++)
                for (int dx = -r0; dx <= r0; dx++)
                {
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / r;
                    if (d >= 1) continue;
                    int x = ((int)cx + dx + S) % S, y = ((int)cy + dy + S) % S;
                    float h = Mathf.Sqrt(1 - d * d) * r * 0.5f + r * 0.2f;
                    int k = y * S + x;
                    if (h > height[k])
                    {
                        height[k] = h;
                        tone[k] = t;
                        dome[k] = Mathf.Sqrt(1 - d * d);
                    }
                }
            }
            var col = new Img(S, S);
            var nor = new Img(S, S);
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                int k = y * S + x;
                // Leafy grain on top of the crowns.
                float u = x / (float)S * Mathf.PI * 2, v = y / (float)S * Mathf.PI * 2;
                float leaf = Mathf.PerlinNoise(Mathf.Cos(u) * 40 + 100, Mathf.Sin(u) * 40 + Mathf.Cos(v) * 40) * 0.5f
                           + Mathf.PerlinNoise(Mathf.Sin(v) * 90 + 50, Mathf.Cos(u) * 90 + 7) * 0.5f;
                height[k] += leaf * 6;
            }
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                int k = y * S + x;
                float hx = height[y * S + (x + 1) % S] - height[y * S + (x - 1 + S) % S];
                float hy = height[((y + 1) % S) * S + x] - height[((y - 1 + S) % S) * S + x];
                var n = new Vector3(-hx * 0.25f, -hy * 0.25f, 1).normalized;
                nor.Px[k] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1);
                // Gaps between crowns are deep shade; crowns vary from dark to yellow-green.
                float lit = Mathf.Clamp01(height[k] / 40f);
                var dark = new Color(0.05f, 0.09f, 0.04f);
                var c1 = Color.Lerp(new Color(0.12f, 0.22f, 0.07f), new Color(0.24f, 0.33f, 0.1f), tone[k]);
                var c = Color.Lerp(dark, c1, Mathf.SmoothStep(0, 1, lit * 1.6f)) * Mathf.Lerp(0.45f, 1.1f, dome[k]);
                c.a = 1;
                col.Px[k] = c;
            }
            col.Save(GroundDir + "/canopy_far_diff.png");
            nor.Save(GroundDir + "/canopy_far_nor_gl.png");
        }
    }
}
