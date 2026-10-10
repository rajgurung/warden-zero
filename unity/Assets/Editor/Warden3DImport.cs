using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace WardenZero.EditorTools
{
    // Import settings for the Tripo Warden (branch warden-3d, assets3d/warden/README.md),
    // applied from code so they are reproducible:
    // - warden_rigged.fbx: Humanoid, avatar created from this model, textures extracted.
    // - warden_anims*.fbx: Humanoid copying that avatar; root rotation and position baked
    //   into the pose (removes Tripo's start offsets and the run's climb); idle/walk/run/fire loop.
    // - rifle.fbx: plain mesh.
    public static class Warden3DImport
    {
        public const string Dir = "Assets/Art/Warden3D";
        public const string Rigged = Dir + "/warden_rigged.fbx";
        public const string Rifle = Dir + "/rifle.fbx";
        public static readonly string[] AnimFiles = { Dir + "/warden_anims.fbx", Dir + "/warden_anims_extra.fbx" };
        static readonly string[] Looping = { "idle", "walk", "run", "fire" };

        public static void Configure()
        {
            Directory.CreateDirectory(Dir + "/Textures");

            var rig = (ModelImporter)AssetImporter.GetAtPath(Rigged);
            rig.animationType = ModelImporterAnimationType.Human;
            rig.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            rig.importAnimation = false;
            rig.materialImportMode = ModelImporterMaterialImportMode.None;
            rig.useFileScale = true;
            rig.SaveAndReimport();
            rig.ExtractTextures(Dir + "/Textures");
            AssetDatabase.Refresh();
            var avatar = AssetDatabase.LoadAllAssetsAtPath(Rigged).OfType<Avatar>().First();

            foreach (var path in AnimFiles)
            {
                var im = (ModelImporter)AssetImporter.GetAtPath(path);
                im.animationType = ModelImporterAnimationType.Human;
                im.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                im.sourceAvatar = avatar;
                im.importAnimation = true;
                im.materialImportMode = ModelImporterMaterialImportMode.None;
                im.SaveAndReimport();
                var clips = im.defaultClipAnimations;
                foreach (var c in clips)
                {
                    // "Armature|run" -> "run"
                    c.name = c.takeName.Contains("|") ? c.takeName.Substring(c.takeName.LastIndexOf('|') + 1) : c.takeName;
                    bool loop = Looping.Contains(c.name);
                    c.loopTime = loop;
                    c.loopPose = loop;
                    c.lockRootRotation = true; // Root Rotation: Bake Into Pose
                    c.keepOriginalOrientation = false; // based on body orientation
                    c.lockRootHeightY = true; // Root Position Y: Bake Into Pose
                    c.keepOriginalPositionY = false;
                    c.heightFromFeet = true; // based on feet
                    c.lockRootPositionXZ = true; // Root Position XZ: Bake Into Pose
                    c.keepOriginalPositionXZ = false; // based on centre of mass
                }
                im.clipAnimations = clips;
                im.SaveAndReimport();
            }

            var rifle = (ModelImporter)AssetImporter.GetAtPath(Rifle);
            rifle.animationType = ModelImporterAnimationType.None;
            rifle.importAnimation = false;
            rifle.materialImportMode = ModelImporterMaterialImportMode.None;
            rifle.SaveAndReimport();
            rifle.ExtractTextures(Dir + "/Textures/Rifle");
            AssetDatabase.Refresh();
        }

        // Batch-mode check of what Unity made of the files.
        public static void Diagnose()
        {
            Configure();
            var avatar = AssetDatabase.LoadAllAssetsAtPath(Rigged).OfType<Avatar>().First();
            Debug.Log($"[W3D] avatar valid={avatar.isValid} human={avatar.isHuman}");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Rigged);
            foreach (var r in model.GetComponentsInChildren<Renderer>())
                Debug.Log($"[W3D] renderer {r.name} bounds={r.bounds} mats={r.sharedMaterials.Length}");
            var go = (GameObject)Object.Instantiate(model);
            var anim = go.GetComponent<Animator>();
            foreach (var b in new[] { HumanBodyBones.Hips, HumanBodyBones.Head, HumanBodyBones.LeftFoot, HumanBodyBones.RightHand, HumanBodyBones.LeftHand, HumanBodyBones.LeftToes })
            {
                var t = anim.GetBoneTransform(b);
                Debug.Log($"[W3D] bone {b} {(t ? t.name + " " + t.position.ToString("F3") + " rot " + t.rotation.eulerAngles.ToString("F0") : "missing")}");
            }
            Object.DestroyImmediate(go);
            foreach (var path in AnimFiles)
                foreach (var c in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")))
                    Debug.Log($"[W3D] clip {path.Substring(path.LastIndexOf('/') + 1)}:{c.name} len={c.length:F2} loop={c.isLooping} human={c.isHumanMotion}");
            foreach (var t in AssetDatabase.FindAssets("t:Texture2D", new[] { Dir }))
            {
                var p = AssetDatabase.GUIDToAssetPath(t);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
                Debug.Log($"[W3D] texture {p} {tex.width}x{tex.height}");
            }
            var rifle = AssetDatabase.LoadAssetAtPath<GameObject>(Rifle);
            foreach (var r in rifle.GetComponentsInChildren<Renderer>())
                Debug.Log($"[W3D] rifle {r.name} bounds={r.bounds} local={r.transform.localPosition} rot={r.transform.localEulerAngles} scale={r.transform.localScale}");
        }
    }
}
