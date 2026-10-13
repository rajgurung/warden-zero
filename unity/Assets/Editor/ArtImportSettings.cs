using UnityEditor;
using UnityEngine;

namespace WardenZero.EditorTools
{
    // Import settings for everything under Assets/Art, kept in code so they are reproducible.
    // Character art becomes sprites with the pivot at the feet; sizes are set through
    // pixels-per-unit so the Warden is ~2.8 m tall and a grunt ~2.1 m.
    public class ArtImportSettings : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Art/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.mipmapEnabled = true;
            ti.filterMode = FilterMode.Bilinear;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;

            if (assetPath.Contains("/Warden3D/"))
            {
                // Tripo model textures (512 px): normal maps flagged, the generated
                // metallic/smoothness map kept linear, everything DXT-compressed with mips.
                ti.textureType = assetPath.Contains("normal") ? TextureImporterType.NormalMap : TextureImporterType.Default;
                ti.sRGBTexture = !assetPath.Contains("metallic") && !assetPath.Contains("roughness");
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.maxTextureSize = 512;
                ti.anisoLevel = 2;
                return;
            }

            if (assetPath.Contains("/Campaign/Traversal/"))
            {
                // The course's props (2048 px from Tripo) ship in the jungle bundle, kept small:
                // colour 512, normals and the generated metallic-smoothness and emission maps
                // 128; Low halves them. Measured: at colour 1024 for the wall and normals 256 the
                // bundle crossed a wasm heap growth step (286 -> 344 MB in the jungle); at these
                // sizes it stays at 286 MB and looks the same at game distance.
                bool normal = assetPath.Contains("normal");
                bool generated = assetPath.Contains("/Generated/");
                ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                ti.sRGBTexture = !assetPath.Contains("metallic") && !assetPath.Contains("roughness");
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.maxTextureSize = generated || normal ? 128 : 512;
                ti.anisoLevel = 2;
                return;
            }

            if (assetPath.Contains("/Campaign/Vehicles/"))
            {
                // M3's jeep, gun and ride targets (2048 px from Tripo), in the Vehicles group (M3
                // section 2.4): colour 1024 for the jeep body, 512 for the jeep's parts and the big
                // props, 256 for the drum, plate and hesco; normals 256 for the body, else 128; the
                // generated metallic-smoothness and emission maps 128. Low halves them.
                bool normal = assetPath.Contains("normal");
                bool generated = assetPath.Contains("/Generated/");
                ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                ti.sRGBTexture = !assetPath.Contains("metallic") && !assetPath.Contains("roughness");
                ti.wrapMode = TextureWrapMode.Clamp;
                bool body = assetPath.Contains("/jeep_body");
                bool small = assetPath.Contains("/fuel_drum") || assetPath.Contains("/target_plate") || assetPath.Contains("/hesco_wall");
                ti.maxTextureSize = generated ? 128 : normal ? (body ? 256 : 128) : body ? 1024 : small ? 256 : 512;
                ti.anisoLevel = 2;
                return;
            }

            if (assetPath.Contains("/Campaign/"))
            {
                // Tripo props: the chopper fills the screen in the set pieces, so it keeps 1024.
                ti.textureType = assetPath.Contains("normal") ? TextureImporterType.NormalMap : TextureImporterType.Default;
                ti.sRGBTexture = !assetPath.Contains("metallic") && !assetPath.Contains("roughness");
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.maxTextureSize = assetPath.Contains("/Chopper/") ? 1024 : 512;
                ti.anisoLevel = 2;
                return;
            }

            if (assetPath.Contains("/Jungle/"))
            {
                JungleImport.ConfigureTexture(ti, assetPath);
                return;
            }

            if (assetPath.EndsWith("/deck.png") || assetPath.EndsWith("/jungle.png"))
            {
                ti.textureType = TextureImporterType.Default;
                ti.wrapMode = TextureWrapMode.Repeat;
                ti.anisoLevel = 4;
                ti.maxTextureSize = 256;
                return;
            }

            if (assetPath.Contains("/Pixel/"))
            {
                // 16 px Kenney monsters: crisp nearest-neighbour, no mips, no compression,
                // feet at the bottom, 2.1 m tall like a grunt.
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = false;
                ti.filterMode = FilterMode.Point;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                var ps = new TextureImporterSettings();
                ti.ReadTextureSettings(ps);
                ps.spriteMeshType = SpriteMeshType.FullRect;
                ps.spriteAlignment = (int)SpriteAlignment.BottomCenter;
                ps.spritePixelsPerUnit = 16 / 2.1f;
                ti.SetTextureSettings(ps);
                return;
            }

            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.maxTextureSize = 512;

            var s = new TextureImporterSettings();
            ti.ReadTextureSettings(s);
            s.spriteMeshType = SpriteMeshType.FullRect;
            if (assetPath.Contains("/Hero/"))
            {
                s.spriteAlignment = (int)SpriteAlignment.BottomCenter;
                s.spritePixelsPerUnit = 512 / 2.8f;
            }
            else if (assetPath.Contains("/Enemies/"))
            {
                s.spriteAlignment = (int)SpriteAlignment.BottomCenter;
                s.spritePixelsPerUnit = 256 / 2.1f;
            }
            else
            {
                // Generated decals (shadow blob, rings): 1 m across at scale 1.
                s.spriteAlignment = (int)SpriteAlignment.Center;
                s.spritePixelsPerUnit = 128;
            }
            ti.SetTextureSettings(s);

            // DXT5 keeps alpha and works in every desktop WebGL browser. WebGL only compresses
            // power-of-two textures here, so the character PNGs are padded to square POT sizes
            // (otherwise each 384x512 hero frame ships as 1 MB of raw RGBA).
            ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "WebGL",
                overridden = true,
                maxTextureSize = 512,
                format = TextureImporterFormat.DXT5,
            });
        }
    }
}
