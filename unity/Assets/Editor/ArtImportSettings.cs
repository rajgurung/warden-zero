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
