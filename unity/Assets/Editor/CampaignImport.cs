using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace WardenZero.EditorTools
{
    // Import settings for the campaign's Tripo props (Assets/Art/Campaign, see TRIPO_LOG.md):
    // plain meshes, no animation, textures extracted next to each FBX. Tripo normalises props
    // to about one unit with the origin at the centre; SceneBuilder scales and places them.
    public static class CampaignImport
    {
        public const string Dir = "Assets/Art/Campaign";
        public static readonly string[] Props = { "Chopper", "Rotor", "Canopy", "Beacon" };

        // HALO gear worn by the Warden (unity/ArtSource/M1Polish/README.md has sizes and attach bones).
        // Unlike the props above these keep Tripo's real size in metres.
        public static readonly string[] Gear = { "halo_pack", "halo_helmet", "altimeter" };

        public static string Fbx(string prop) => $"{Dir}/{prop}/{prop.ToLowerInvariant()}.fbx";
        public static string Textures(string prop) => $"{Dir}/{prop}/Textures";
        public static string GearFbx(string gear) => $"{Dir}/Gear/{gear}.fbx";
        public static string GearTextures(string gear) => $"{Dir}/Gear/Textures/{gear}";

        public static void Configure()
        {
            foreach (var (path, textures) in Props.Select(p => (Fbx(p), Textures(p))).Concat(Gear.Select(g => (GearFbx(g), GearTextures(g)))))
            {
                if (!File.Exists(path)) throw new System.Exception("Campaign prop missing: " + path);
                Directory.CreateDirectory(textures);
                var im = (ModelImporter)AssetImporter.GetAtPath(path);
                im.animationType = ModelImporterAnimationType.None;
                im.importAnimation = false;
                im.materialImportMode = ModelImporterMaterialImportMode.None;
                im.importCameras = false;
                im.importLights = false;
                im.isReadable = false; // the editor can still measure it (rotor hubs, size)
                im.SaveAndReimport();
                if (!Directory.EnumerateFiles(textures).Any(f => !f.EndsWith(".meta")))
                {
                    im.ExtractTextures(textures);
                    AssetDatabase.Refresh();
                }
            }
        }

        // Batch-mode check of what Unity made of the files.
        public static void Diagnose()
        {
            Configure();
            foreach (var (prop, path) in Props.Select(p => (p, Fbx(p))).Concat(Gear.Select(g => (g, GearFbx(g)))))
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                foreach (var t in model.GetComponentsInChildren<Transform>())
                {
                    var mf = t.GetComponent<MeshFilter>();
                    Debug.Log($"[Campaign] {prop} {t.name} pos={t.localPosition} rot={t.localEulerAngles} scale={t.localScale}" +
                              (mf ? $" mesh bounds={mf.sharedMesh.bounds} verts={mf.sharedMesh.vertexCount} tris={mf.sharedMesh.triangles.Length / 3}" : ""));
                }
            }
            foreach (var t in AssetDatabase.FindAssets("t:Texture2D", new[] { Dir }))
            {
                var p = AssetDatabase.GUIDToAssetPath(t);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
                Debug.Log($"[Campaign] texture {p} {tex.width}x{tex.height}");
            }
        }

        public static Mesh LoadMesh(string prop) =>
            AssetDatabase.LoadAllAssetsAtPath(Fbx(prop)).OfType<Mesh>().First();
    }
}
