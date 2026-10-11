using UnityEditor;
using UnityEngine;

namespace WardenZero.EditorTools
{
    // Texture import rules for the jungle (Assets/Art/Jungle): CC0 ground, bark, rock and
    // plant textures (see Assets/ThirdParty/CREDITS.md) and the cards SceneBuilder.Jungle
    // composes from them.
    public static class JungleImport
    {
        public const string Dir = "Assets/Art/Jungle";

        public static void ConfigureTexture(TextureImporter ti, string path)
        {
            string file = System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            bool normal = file.Contains("_nor_gl") || file.EndsWith("_normal");
            bool data = file.Contains("_rough") || file.EndsWith("_mask");
            ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            ti.sRGBTexture = !normal && !data;
            ti.mipmapEnabled = true;
            ti.wrapMode = path.Contains("/Cards/") ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            ti.anisoLevel = path.Contains("/Ground/") ? 8 : 2;
            ti.maxTextureSize = path.Contains("/Sky/") ? 2048 : 1024;
            ti.textureCompression = TextureImporterCompression.Compressed;
            // Leaf cards: keep thin alpha coverage in the distant mips.
            bool cutout = path.Contains("/Cards/") && !normal;
            ti.alphaIsTransparency = cutout;
            ti.mipMapsPreserveCoverage = cutout;
            ti.alphaTestReferenceValue = 0.45f;
            if (path.Contains("/Sky/"))
            {
                ti.textureShape = TextureImporterShape.TextureCube;
                ti.sRGBTexture = false;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.mipmapEnabled = true;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
            }
        }
    }
}
