using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace WardenZero.EditorTools
{
    // Stage 2 streams in: the jungle scene (and everything it pulls in: terrain, vegetation,
    // textures, the Tripo props) is one Addressables group, built into the WebGL output's
    // StreamingAssets/aa. On WebGL that folder is fetched over HTTP from the page's own URL,
    // so the bundles are downloaded only when StageLoader asks for them, from the same site.
    // In the editor and in tests the group loads straight from the AssetDatabase.
    public static class AddressablesSetup
    {
        public const string GroupName = "Jungle";

        public static void Configure(string jungleScene)
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            settings.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer; // WebGLBuilder builds it first
            settings.BuildRemoteCatalog = false;
            for (int i = 0; i < settings.DataBuilders.Count; i++)
                if (settings.DataBuilders[i].GetType().Name.Contains("FastMode")) settings.ActivePlayModeDataBuilderIndex = i;

            var group = settings.FindGroup(GroupName) ?? settings.CreateGroup(GroupName, false, false, true, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var schema = group.GetSchema<BundledAssetGroupSchema>();
            schema.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
            schema.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
            // LZ4: WebGL players cannot decompress LZMA bundles ("format 1 not supported").
            schema.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
            schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
            schema.UseAssetBundleCache = true;
            schema.IncludeInBuild = true;

            var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(jungleScene), group);
            entry.address = StageLoader.JungleAddress;
            EditorUtility.SetDirty(group);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        // Writes the bundles and catalog to Library/com.unity.addressables/aa/WebGL; the
        // player build copies them into StreamingAssets/aa.
        public static bool BuildContent()
        {
            AddressableAssetSettings.CleanPlayerContent();
            AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
            if (!string.IsNullOrEmpty(result.Error))
            {
                Debug.LogError("[Addressables] build failed: " + result.Error);
                return false;
            }
            long bytes = 0;
            foreach (var b in result.AssetBundleBuildResults)
            {
                long size = new System.IO.FileInfo(b.FilePath).Length;
                bytes += size;
                Debug.Log($"[Addressables] {System.IO.Path.GetFileName(b.FilePath)} {size / 1024f / 1024f:0.00} MB");
            }
            Debug.Log($"[Addressables] content built: {result.AssetBundleBuildResults.Count} bundles, {bytes / 1024f / 1024f:0.00} MB");
            return true;
        }
    }
}
