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

            if (assetPath.EndsWith("/deck.png"))
            {
                ti.textureType = TextureImporterType.Default;
                ti.wrapMode = TextureWrapMode.Repeat;
                ti.anisoLevel = 4;
                ti.maxTextureSize = 256;
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
