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

        // M3's Tripo art: the VehicleArt prefab (and everything it pulls in) in its own group,
        // loaded after the jungle (VehicleArtLoader).
        public const string VehiclesGroup = "Vehicles", SharedGroup = "Shared";
        public const string LitShader = "Packages/com.unity.render-pipelines.universal/Shaders/Lit.shader";

        public static void ConfigureVehicles(string prefab)
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            var vehicles = Group(settings, VehiclesGroup, BundledAssetGroupSchema.BundlePackingMode.PackTogether);
            var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(prefab), vehicles);
            entry.address = VehicleArtLoader.Address;
            // No Shared group for the Lit shader: splitting it out of the jungle bundle raised the
            // jungle's load peak over the Low heap step (286 -> 343 MB, measured). The Vehicles
            // bundle carries its own copy of the variants it uses instead.
            var stale = settings.FindGroup(SharedGroup);
            if (stale != null) settings.RemoveGroup(stale);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        static AddressableAssetGroup Group(AddressableAssetSettings settings, string name, BundledAssetGroupSchema.BundlePackingMode packing)
        {
            var group = settings.FindGroup(name) ?? settings.CreateGroup(name, false, false, true, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var schema = group.GetSchema<BundledAssetGroupSchema>();
            schema.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
            schema.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
            schema.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
            schema.BundleMode = packing;
            schema.UseAssetBundleCache = true;
            schema.IncludeInBuild = true;
            EditorUtility.SetDirty(group);
            return group;
        }

        // Fails the build if the jungle scene would pull in the Vehicles art, or if an asset not
        // in any group is used by both (it would be copied into both bundles). The one allowed
        // copy is the URP Lit shader (each bundle has the variants its own materials use).
        public static void CheckBundles(string jungleScene, string vehiclePrefab)
        {
            // Without the art the jeep would ship as greybox only: stop the build instead.
            if (!System.IO.File.Exists(vehiclePrefab))
                throw new System.Exception("[Addressables] the Vehicles prefab is missing: " + vehiclePrefab + " (run SceneBuilder.Build)");
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
            var entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(vehiclePrefab));
            if (entry == null || entry.address != VehicleArtLoader.Address)
                throw new System.Exception($"[Addressables] {vehiclePrefab} is not in the Vehicles group at address \"{VehicleArtLoader.Address}\"");
            var jungle = Packed(jungleScene);
            var vehicles = Packed(vehiclePrefab);
            foreach (var d in vehicles)
            {
                if (d == vehiclePrefab || !jungle.Contains(d) || d.EndsWith(".cs") || d == LitShader) continue;
                if (settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(d)) != null) continue;
                throw new System.Exception("[Addressables] in both the jungle and Vehicles bundles: " + d);
            }
            if (jungle.Contains(vehiclePrefab)) throw new System.Exception("[Addressables] the jungle scene references the Vehicles art directly");
            Debug.Log($"[Addressables] bundle check: the jungle and Vehicles share nothing outside the groups ({vehicles.Count} Vehicles dependencies)");
        }

        // What a bundle would pack for an asset: its dependencies, followed through everything but
        // model files (an FBX's import-time default material isn't packed with its meshes).
        static System.Collections.Generic.HashSet<string> Packed(string root)
        {
            var seen = new System.Collections.Generic.HashSet<string>();
            var todo = new System.Collections.Generic.Stack<string>();
            todo.Push(root);
            while (todo.Count > 0)
            {
                var a = todo.Pop();
                if (!seen.Add(a) || a.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) continue;
                foreach (var d in AssetDatabase.GetDependencies(a, false)) todo.Push(d);
            }
            return seen;
        }

        // Writes the bundles and catalog to Library/com.unity.addressables/aa/WebGL; the
        // player build copies them into StreamingAssets/aa.
        public static bool BuildContent()
        {
            CheckBundles("Assets/Scenes/Jungle.unity", "Assets/Prefabs/Vehicles/VehicleArt.prefab");
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
