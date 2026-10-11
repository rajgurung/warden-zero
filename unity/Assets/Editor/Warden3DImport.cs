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
    // - Anims/warden_anims_m1_*.fbx: milestone 1 polish clips on the same rig, listed in M1Clips
    //   (unity/ArtSource/M1Polish/README.md). Takes not listed there are rejected presets and
    //   are not imported.
    public static class Warden3DImport
    {
        public const string Dir = "Assets/Art/Warden3D";
        public const string Rigged = Dir + "/warden_rigged.fbx";
        public const string Rifle = Dir + "/rifle.fbx";
        public static readonly string[] AnimFiles = { Dir + "/warden_anims.fbx", Dir + "/warden_anims_extra.fbx" };
        static readonly string[] Looping = { "idle", "walk", "run", "fire" };

        public const string AnimDir = Dir + "/Anims";
        // file, Tripo take, clip name, loop, trim start/end in seconds (end < 0 = to the end),
        // height based on feet (ground clips) or on the original hips (air clips).
        public static readonly (string file, string take, string clip, bool loop, float start, float end, bool feet)[] M1Clips =
        {
            ("warden_anims_m1_idle_land.fbx", "standing_relax", "idle_relaxed", true, 0, -1, true),
            ("warden_anims_m1_idle_land.fbx", "wait", "idle_hands_on_hips", true, 0, -1, true),
            ("warden_anims_m1_idle_land.fbx", "jump_down", "land", false, 2.2f, -1, true), // from touchdown
            ("warden_anims_m1_fall_look.fbx", "look_around", "idle_look_around", true, 0, -1, true),
            ("warden_anims_m1_fall_look.fbx", "swim", "fall_loop", true, 0, -1, false),
            ("warden_anims_m1_canopy.fbx", "victory_celebration", "canopy_hold", true, 1.3f, 3.8f, false), // arms-up hold
        };

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
                var im = HumanoidAnimImporter(path, avatar);
                var clips = im.defaultClipAnimations;
                foreach (var c in clips)
                {
                    c.name = TakeShortName(c.takeName);
                    bool loop = Looping.Contains(c.name);
                    c.loopTime = loop;
                    c.loopPose = loop;
                    BakeRoot(c, true);
                }
                im.clipAnimations = clips;
                im.SaveAndReimport();
            }

            foreach (var file in M1Clips.Select(m => m.file).Distinct())
            {
                var im = HumanoidAnimImporter(AnimDir + "/" + file, avatar);
                var takes = im.defaultClipAnimations;
                var clips = new System.Collections.Generic.List<ModelImporterClipAnimation>();
                foreach (var m in M1Clips.Where(m => m.file == file))
                {
                    var c = takes.First(t => TakeShortName(t.takeName) == m.take);
                    float rate = im.importedTakeInfos.First(t => t.name == c.takeName).sampleRate;
                    float first = c.firstFrame;
                    c.name = m.clip;
                    c.firstFrame = first + m.start * rate;
                    if (m.end >= 0) c.lastFrame = first + m.end * rate;
                    c.loopTime = m.loop;
                    c.loopPose = m.loop;
                    BakeRoot(c, m.feet);
                    clips.Add(c);
                }
                im.clipAnimations = clips.ToArray();
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

        static ModelImporter HumanoidAnimImporter(string path, Avatar avatar)
        {
            var im = (ModelImporter)AssetImporter.GetAtPath(path);
            im.animationType = ModelImporterAnimationType.Human;
            im.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            im.sourceAvatar = avatar;
            im.importAnimation = true;
            im.materialImportMode = ModelImporterMaterialImportMode.None;
            im.SaveAndReimport();
            return im;
        }

        // "Armature|run" -> "run"
        static string TakeShortName(string take) => take.Contains("|") ? take.Substring(take.LastIndexOf('|') + 1) : take;

        // Bake root rotation and position into the pose (removes Tripo's start offsets and drift).
        static void BakeRoot(ModelImporterClipAnimation c, bool heightFromFeet)
        {
            c.lockRootRotation = true; // Root Rotation: Bake Into Pose
            c.keepOriginalOrientation = false; // based on body orientation
            c.lockRootHeightY = true; // Root Position Y: Bake Into Pose
            c.keepOriginalPositionY = !heightFromFeet; // based on original (air clips)
            c.heightFromFeet = heightFromFeet; // based on feet (ground clips)
            c.lockRootPositionXZ = true; // Root Position XZ: Bake Into Pose
            c.keepOriginalPositionXZ = false; // based on centre of mass
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
            foreach (var b in new[] { HumanBodyBones.Hips, HumanBodyBones.Chest, HumanBodyBones.UpperChest, HumanBodyBones.Head, HumanBodyBones.LeftFoot,
                HumanBodyBones.RightShoulder, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
                HumanBodyBones.LeftShoulder, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand })
            {
                var t = anim.GetBoneTransform(b);
                Debug.Log($"[W3D] bone {b} {(t ? t.name + " " + t.position.ToString("F3") + " rot " + t.rotation.eulerAngles.ToString("F0") : "missing")}");
            }
            Object.DestroyImmediate(go);
            foreach (var path in AnimFiles.Concat(M1Clips.Select(m => AnimDir + "/" + m.file).Distinct()))
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
