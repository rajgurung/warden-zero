using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

namespace WardenZero.EditorTools
{
    // The Warden's two views: the rigged Tripo model (default) and the 2D sprite (fallback).
    public static partial class SceneBuilder
    {
        const string W3D = Warden3DImport.Dir;
        // The Warden draws after the enemy sprites (which don't write depth), so a crowd never
        // covers him; walls still do. The outline hull goes just before him.
        const int WardenQueue = 3010;

        static Material wardenBodyMat;
        static Material wardenOutlineMat;
        static Material xrayMat;
        static Material rimMat3D;
        static Material rifleMat;
        static AnimatorController wardenController;

        // Import the FBXs, build PBR materials (metallic/smoothness and a cyan emission mask
        // generated from Tripo's maps) and the animator controller.
        static void PrepareWarden3D()
        {
            Warden3DImport.Configure();
            Directory.CreateDirectory(W3D + "/Generated");
            wardenBodyMat = ModelMaterial("WardenBody", W3D + "/Textures", "warden");
            rifleMat = ModelMaterial("WardenRifle", W3D + "/Textures/Rifle", "rifle");
            // A thin cyan edge that separates him from the dark floor (kept below the bloom threshold).
            wardenOutlineMat = SaveMaterial("WardenOutline3D", Shader.Find("WardenZero/Outline3D"), Hdr(GameConfig.Accent, 0.55f));
            wardenOutlineMat.SetFloat("_Width", 0.012f);
            wardenOutlineMat.renderQueue = WardenQueue - 1;
            // Silhouette where scenery hides him; drawn just before the body (see shader).
            xrayMat = SaveMaterial("WardenXRay", Shader.Find("WardenZero/XRaySilhouette"), new Color(GameConfig.Accent.r, GameConfig.Accent.g, GameConfig.Accent.b, 0.45f)); // #4fd1ff, below the bloom threshold
            xrayMat.renderQueue = WardenQueue - 2;
            // Cyan rim on his silhouette edges, drawn just after the body.
            rimMat3D = SaveMaterial("WardenRim", Shader.Find("WardenZero/RimGlow"), GameConfig.Accent * 0.3f); // thin and dim: separates him from the floor without washing out his colours
            rimMat3D.SetFloat("_Power", 4.5f);
            rimMat3D.renderQueue = WardenQueue + 1;
            wardenController = BuildWardenController();
        }

        static Material ModelMaterial(string name, string texDir, string prefix)
        {
            string ms = W3D + "/Generated/" + prefix + "_metallic_smoothness.png";
            string em = W3D + "/Generated/" + prefix + "_emission.png";
            var baseTex = ReadImage(texDir + "/tripo_model_basecolor.JPEG");
            var metal = ReadImage(texDir + "/tripo_model_metallic.JPEG");
            var rough = ReadImage(texDir + "/tripo_model_roughness.JPEG");
            int w = baseTex.width, h = baseTex.height;
            var msTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var emTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var bp = baseTex.GetPixels();
            var mp = metal.GetPixels();
            var rp = rough.GetPixels();
            var msp = new Color[bp.Length];
            var emp = new Color[bp.Length];
            for (int i = 0; i < bp.Length; i++)
            {
                // URP Lit: metallic in R, smoothness in A (glTF keeps roughness separately).
                msp[i] = new Color(mp[i].r, 0, 0, 1 - rp[i].r);
                // The cyan visor, chevrons and rails glow: keep cyan pixels, black elsewhere.
                var c = bp[i];
                float cyan = Mathf.Clamp01((Mathf.Min(c.g, c.b) - c.r - 0.15f) * 4);
                emp[i] = c * cyan;
            }
            msTex.SetPixels(msp);
            emTex.SetPixels(emp);
            File.WriteAllBytes(ms, msTex.EncodeToPNG());
            File.WriteAllBytes(em, emTex.EncodeToPNG());
            AssetDatabase.ImportAsset(ms, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(em, ImportAssetOptions.ForceUpdate);

            // Albedo lifted a little: the olive armour is very dark under the arena's moody light.
            var mat = SaveMaterial(name, Shader.Find("Universal Render Pipeline/Lit"), Color.white);
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texDir + "/tripo_model_basecolor.JPEG"));
            mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texDir + "/tripo_model_normal.PNG"));
            mat.EnableKeyword("_NORMALMAP");
            mat.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ms));
            mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            mat.SetFloat("_Smoothness", 1);
            mat.SetFloat("_EnvironmentReflections", 0);
            mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            mat.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(em));
            Emissive(mat, Hdr(Color.white, 2.5f)); // visor and chevrons bloom, armour does not
            mat.renderQueue = WardenQueue;
            return mat;
        }

        // Read a texture's file directly (the imported asset isn't CPU-readable).
        static Texture2D ReadImage(string path)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            t.LoadImage(File.ReadAllBytes(path));
            return t;
        }

        static AnimationClip Clip3D(string file, string name)
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath(W3D + "/" + file).OfType<AnimationClip>().FirstOrDefault(c => c.name == name);
            if (clip == null) throw new System.Exception("Warden clip missing: " + name);
            return clip;
        }

        // Base layer: Locomotion (idle/walk/run by Speed, AnimSpeed plays it backwards for
        // backpedalling) and Death. Upper layer (arms, spine, head): Aim (the fire clip,
        // slowed by UpperSpeed when not shooting) and Hit.
        static AnimatorController BuildWardenController()
        {
            string path = W3D + "/Warden.controller";
            string maskPath = W3D + "/UpperBody.mask";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.DeleteAsset(maskPath);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter(new AnimatorControllerParameter { name = "AnimSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1 });
            ctrl.AddParameter(new AnimatorControllerParameter { name = "UpperSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1 });

            var sm = ctrl.layers[0].stateMachine;
            var loco = ctrl.CreateBlendTreeInController("Locomotion", out var tree, 0);
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(Clip3D("warden_anims.fbx", "idle"), 0);
            tree.AddChild(Clip3D("warden_anims.fbx", "walk"), 0.35f);
            tree.AddChild(Clip3D("warden_anims.fbx", "run"), 1);
            loco.speedParameterActive = true;
            loco.speedParameter = "AnimSpeed";
            var death = sm.AddState("Death");
            death.motion = Clip3D("warden_anims_extra.fbx", "defeat_03");
            sm.defaultState = loco;

            var mask = new AvatarMask();
            for (var part = AvatarMaskBodyPart.Root; part < AvatarMaskBodyPart.LastBodyPart; part++)
            {
                bool upper = part == AvatarMaskBodyPart.Body || part == AvatarMaskBodyPart.Head
                    || part == AvatarMaskBodyPart.LeftArm || part == AvatarMaskBodyPart.RightArm
                    || part == AvatarMaskBodyPart.LeftFingers || part == AvatarMaskBodyPart.RightFingers;
                mask.SetHumanoidBodyPartActive(part, upper);
            }
            AssetDatabase.CreateAsset(mask, maskPath);

            var upperSm = new AnimatorStateMachine { name = "Upper", hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(upperSm, ctrl);
            var aim = upperSm.AddState("Aim");
            aim.motion = Clip3D("warden_anims_extra.fbx", "fire");
            aim.speedParameterActive = true;
            aim.speedParameter = "UpperSpeed";
            var hit = upperSm.AddState("Hit");
            hit.motion = Clip3D("warden_anims.fbx", "hit_to_body_01");
            upperSm.defaultState = aim;
            ctrl.AddLayer(new AnimatorControllerLayer
            {
                name = "Upper",
                defaultWeight = 1,
                avatarMask = mask,
                blendingMode = AnimatorLayerBlendingMode.Override,
                iKPass = true, // WardenHandIK puts both hands on the rifle
                stateMachine = upperSm,
            });
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            return ctrl;
        }

        static WardenModelView BuildModelView(Transform warden)
        {
            var view = warden.gameObject.AddComponent<WardenModelView>();
            var yaw = new GameObject("Yaw").transform;
            yaw.SetParent(warden, false);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Warden3DImport.Rigged), yaw);
            model.name = "Model";
            model.transform.localRotation = Quaternion.Euler(0, ModelYawFix, 0);
            model.transform.localScale = Vector3.one * ModelScale;
            var animator = model.GetComponent<Animator>();
            animator.runtimeAnimatorController = wardenController;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var body = model.GetComponentInChildren<SkinnedMeshRenderer>();
            // The inverted-hull outline (wardenOutlineMat) drew seam lines across the body on this
            // mesh, so it's left off; the draw order and the ground ring keep him readable.
            body.sharedMaterials = new[] { wardenBodyMat, xrayMat, rimMat3D };
            // Above the enemy sprites (order 0) so a crowd never covers him.
            body.sortingOrder = 2;
            body.updateWhenOffscreen = true;

            // Rifle held in front of the chest along the facing (model units, before the 1.4x
            // scale): pistol grip right of centre at 1.32 m, 0.2 m forward. Arms reach 0.66 from
            // shoulders at (+-0.25, 1.58, 0), so both grips sit inside reach. The mesh is scaled
            // to 1.05 m (Tripo's 1.3 m reads oversized against these arms).
            var hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            var rifle = new GameObject("Rifle").transform;
            rifle.SetParent(model.transform, false);
            rifle.localPosition = new Vector3(0.05f, 1.32f, 0.20f);
            var rifleModel = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Warden3DImport.Rifle), rifle);
            // rifle.fbx is 1 m long and centred; this offset puts the grip at the origin and the
            // muzzle on +Z (assets3d/warden/README.md gives (0, 0.13, 0.338) at scale 1.3).
            float k = RifleScale / 1.3f;
            rifleModel.transform.localPosition = new Vector3(0, 0.13f, 0.338f) * k;
            rifleModel.transform.localScale = Vector3.one * RifleScale;
            foreach (var r in rifleModel.GetComponentsInChildren<MeshRenderer>())
            {
                r.sharedMaterials = new[] { rifleMat, xrayMat };
                r.sortingOrder = 2;
            }
            var muzzle = Empty("Muzzle", rifle, new Vector3(0, 0.125f, 0.99f * k));
            // Vertical foregrip ("attachment grip") under the handguard, in the rifle's dark finish.
            var foregrip = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(foregrip.GetComponent<Collider>());
            foregrip.name = "Foregrip";
            foregrip.transform.SetParent(rifle, false);
            foregrip.transform.localPosition = new Vector3(0, 0.03f, 0.28f);
            foregrip.transform.localRotation = Quaternion.Euler(-8, 0, 0); // raked slightly forward
            foregrip.transform.localScale = new Vector3(0.045f, 0.06f, 0.045f);
            var fgr = foregrip.GetComponent<MeshRenderer>();
            fgr.sharedMaterials = new[] { LitMaterial("Foregrip", GameConfig.Hex(0x15181c), new Color(0.2f, 0.2f, 0.22f), 0.45f), xrayMat };
            fgr.sortingOrder = 2;
            var ik = model.AddComponent<WardenHandIK>();
            ik.yaw = yaw;
            ik.rifle = rifle;
            ik.rightGrip = Empty("RightHandGrip", rifle, new Vector3(0, -0.02f, 0));
            ik.leftGrip = Empty("LeftHandGrip", rifle, new Vector3(0, 0.03f, 0.28f));
            ik.stock = Empty("Stock", rifle, new Vector3(0, 0.1f, -0.31f * k));
            view.handIK = ik;

            view.yaw = yaw;
            view.animator = animator;
            view.rightHand = hand;
            view.rifle = rifle;
            view.muzzle = muzzle;
            view.renderers = model.GetComponentsInChildren<Renderer>(true);
            return view;
        }

        // Rotation of the model under the yaw pivot so the animated body faces the pivot's +Z
        // (Humanoid playback already aligns him; checked by AnimatedBody_FacesWhereHeAims).
        const float ModelYawFix = 0;
        // Tripo's Warden is 1.9 m; 1.4x (about 2.65 m) matches the sprite Warden and reads
        // next to the 2.1 m grunts. The rifle scales with him.
        public const float ModelScale = 1.4f;

        public const float RifleScale = 1.05f;

        static Transform Empty(string name, Transform parent, Vector3 localPos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            return t;
        }

        static WardenSpriteView BuildSpriteView(Transform warden, Camera cam)
        {
            var view = warden.gameObject.AddComponent<WardenSpriteView>();
            view.cam = cam;
            // Order 1 keeps the Warden readable when a crowd overlaps him.
            view.body = MakeSprite("Body", warden, wardenMat, LoadSprite("Assets/Art/Hero/idle.png"), 1);
            view.body.gameObject.AddComponent<Billboard>();
            view.idle = LoadSprite("Assets/Art/Hero/idle.png");
            view.shoot = LoadSprite("Assets/Art/Hero/shoot.png");
            view.shootUp = LoadSprite("Assets/Art/Hero/shoot_up.png");
            view.shootDown = LoadSprite("Assets/Art/Hero/shoot_down.png");
            view.dash = LoadSprite("Assets/Art/Hero/dash.png");
            view.death = LoadSprite("Assets/Art/Hero/death.png");
            view.runDown = Frames("Assets/Art/Hero/run_down_", 6);
            view.runSide = Frames("Assets/Art/Hero/run_side_", 6);
            view.runUp = Frames("Assets/Art/Hero/run_up_", 6);
            return view;
        }
    }
}
