using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace WardenZero.EditorTools
{
    // M3's Tripo art (ArtSource/M3Vehicles/README.md) as one prefab, VehicleArt, in the Vehicles
    // Addressables group: the jungle scene never references it; VehicleArtLoader loads it by
    // address after the jungle is in and copies its parts under the jeep's pivots and onto the
    // ride's targets and cover. Each child is a holder whose local transform is the part's place
    // in its pivot's space (game metres), with the FBX under it as imported (its 270-degree X
    // root kept), so a copy made with that pivot as parent lands in place.
    //
    // Jeep space (README) is the body FBX's pivot space; JeepToRig turns it into the rig's (the
    // chassis origin on the ground between the axles) at the game scale K = 1.187 (a 3.1 m
    // wheelbase for the 2.65 m Warden).
    public static partial class SceneBuilder
    {
        public const float JeepK = 1.187f;
        static readonly Vector3 JeepOrigin = new Vector3(0, 1.115f, -0.262f); // the rig's origin in jeep space, negated
        public const float RiserLift = 0.35f; // jeep space: the gun raised on a riser so the grips sit at chest height

        public static Vector3 JeepToRig(Vector3 p) => (p + JeepOrigin) * JeepK;

        const string VehicleArtPath = "Assets/Prefabs/Vehicles/VehicleArt.prefab";
        const string VehicleGen = CampaignImport.Dir + "/Vehicles/Generated";

        // The steering pivot's turn in the rig (README: LookRotation(left, up tilted back)); the
        // wheel turns about its local X.
        static readonly Quaternion SteeringRot = Quaternion.LookRotation(Vector3.left, new Vector3(0, 0.917f, 0.398f));

        // Places (rig space) the builder's jeep pivots use, from the README's jeep space.
        public static Vector3 SteeringPivot => JeepToRig(new Vector3(-0.46f, 0.092f, 0.445f));
        public static Vector3 YokePivot => JeepToRig(new Vector3(0.007f, 0.890f + RiserLift, -1.361f));
        // The trunnion relative to the yoke pivot; the gun's holder, muzzle and grips relative to it.
        public static Vector3 TrunnionInYoke => new Vector3(0.010f, 0.188f, -0.022f) * JeepK;
        public static Vector3 MuzzleInGun => new Vector3(-0.012f, 0, 1.487f) * JeepK;
        public static Vector3 DriverCushion => JeepToRig(new Vector3(-0.452f, -0.265f, -0.04f));
        public static Vector3 GunnerFloor => JeepToRig(new Vector3(0, -0.15f, -1.95f));
        public const float BodyHalfWidth = 1.092f * JeepK;

        // A Tripo part's material: colour and normals; metallic flat (README: near zero, and AO is
        // constant), smoothness from the roughness map where it varies (a generated
        // metallic-smoothness map) or a constant where it is flat; cyan trim glowing where asked.
        static Material VehicleMaterial(string prop, float smoothness, float glow)
        {
            Directory.CreateDirectory(VehicleGen);
            string tex = CampaignImport.VehicleTextures(prop);
            var mat = SaveMaterial("Vehicle_" + prop, Shader.Find("Universal Render Pipeline/Lit"), Color.white);
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex + "/tripo_model_basecolor.JPEG"));
            mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex + "/tripo_model_normal.PNG"));
            mat.EnableKeyword("_NORMALMAP");
            mat.SetFloat("_Metallic", 0);
            mat.SetFloat("_EnvironmentReflections", 0);
            mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            if (smoothness < 0)
            {
                // URP Lit: metallic in R, smoothness in A.
                string ms = VehicleGen + "/" + prop + "_metallic_smoothness.png";
                var rough = ReadImage(tex + "/tripo_model_roughness.JPEG");
                var small = Downsample(rough, 256);
                var px = small.GetPixels();
                for (int i = 0; i < px.Length; i++) px[i] = new Color(0, 0, 0, 1 - px[i].r);
                small.SetPixels(px);
                File.WriteAllBytes(ms, small.EncodeToPNG());
                AssetDatabase.ImportAsset(ms, ImportAssetOptions.ForceUpdate);
                mat.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ms));
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                mat.SetFloat("_Smoothness", 1);
            }
            else
            {
                mat.SetTexture("_MetallicGlossMap", null);
                mat.DisableKeyword("_METALLICSPECGLOSSMAP");
                mat.SetFloat("_Smoothness", smoothness);
            }
            if (glow > 0)
            {
                string em = VehicleGen + "/" + prop + "_emission.png";
                var b = Downsample(ReadImage(tex + "/tripo_model_basecolor.JPEG"), 256);
                var px = b.GetPixels();
                for (int i = 0; i < px.Length; i++)
                {
                    var c = px[i];
                    float cyan = Mathf.Clamp01((Mathf.Min(c.g, c.b) - c.r - 0.15f) * 4);
                    px[i] = c * cyan;
                }
                b.SetPixels(px);
                File.WriteAllBytes(em, b.EncodeToPNG());
                AssetDatabase.ImportAsset(em, ImportAssetOptions.ForceUpdate);
                mat.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(em));
                Emissive(mat, Hdr(Color.white, glow));
            }
            else
            {
                mat.SetTexture("_EmissionMap", null);
                mat.DisableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Texture2D Downsample(Texture2D src, int size)
        {
            var rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Graphics.Blit(src, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            t.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            t.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return t;
        }

        // Smoothness per part: a map where the roughness varies, else the README's constant.
        static readonly Dictionary<string, float> VehicleSmoothness = new Dictionary<string, float>
        {
            { "watchtower", 0.25f }, { "fuel_trailer", 0.56f }, { "roadblock", 0.17f }, { "hesco_wall", 0.07f }, { "jeep_steering", 0.52f },
        };

        // The FBX as imported under a holder; the holder takes the place.
        static Transform ArtHolder(string name, Transform parent, string prop, Material mat, Vector3 pos, Quaternion rot, float scale, Mesh mesh = null)
        {
            var holder = new GameObject(name).transform;
            holder.SetParent(parent, false);
            holder.localPosition = pos;
            holder.localRotation = rot;
            holder.localScale = Vector3.one * scale;
            GameObject model;
            if (mesh == null)
            {
                model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CampaignImport.VehicleFbx(prop)));
                PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                model.transform.SetParent(holder, false);
            }
            else
            {
                // A mesh made here (the cut body), placed as the FBX's root would be.
                model = new GameObject(prop);
                model.transform.SetParent(holder, false);
                model.transform.localRotation = Quaternion.Euler(270, 0, 0);
                model.AddComponent<MeshFilter>().sharedMesh = mesh;
                model.AddComponent<MeshRenderer>();
            }
            foreach (var r in model.GetComponentsInChildren<MeshRenderer>())
            {
                // Every slot (a mesh with sub-meshes would keep URP's default material otherwise).
                int slots = Mathf.Max(1, r.GetComponent<MeshFilter>().sharedMesh.subMeshCount);
                r.sharedMaterials = Enumerable.Repeat(mat, slots).ToArray();
                r.shadowCastingMode = ShadowCastingMode.On;
            }
            return holder;
        }

        // The jeep body without the small steering wheel Tripo fused onto the dash: every triangle
        // with all three corners inside the README's box (pivot space) goes.
        static Mesh CutJeepBody()
        {
            var src = CampaignImport.VehicleMeshes("jeep_body");
            var mesh = Object.Instantiate(src);
            mesh.name = "jeep_body_cut";
            var root = Quaternion.Euler(270, 0, 0);
            var v = mesh.vertices;
            var box = new Bounds();
            box.SetMinMax(new Vector3(-0.63f, -0.02f, 0.22f), new Vector3(-0.29f, 0.30f, 0.455f));
            bool In(int i) => box.Contains(root * v[i]);
            int cut = 0;
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                var tris = mesh.GetTriangles(sub);
                var keep = new List<int>(tris.Length);
                for (int t = 0; t < tris.Length; t += 3)
                {
                    if (In(tris[t]) && In(tris[t + 1]) && In(tris[t + 2]))
                    {
                        cut++;
                        continue;
                    }
                    keep.Add(tris[t]);
                    keep.Add(tris[t + 1]);
                    keep.Add(tris[t + 2]);
                }
                mesh.SetTriangles(keep, sub);
            }
            Debug.Log($"[SceneBuilder] jeep body: cut {cut} dash-wheel triangles");
            Directory.CreateDirectory(VehicleGen);
            string path = VehicleGen + "/jeep_body_cut.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        // Writes VehicleArt.prefab. Children (each placed in its pivot's space):
        //   Body (under the jeep's Body), Riser (the gun's pedestal extension, on the Body),
        //   Wheel (under a hub; the right side turns it 180), Steering, Yoke (under GunYoke),
        //   Gun (under the trunnion pivot); Drum, Plate (its hinge at the pivot), Trailer, Nest,
        //   Tower, Roadblock, Hesco (on the ground, facing +Z, the road).
        static void BuildVehicleArt()
        {
            if (!CampaignImport.Vehicles.All(v => File.Exists(CampaignImport.VehicleFbx(v))))
            {
                Debug.LogWarning("[SceneBuilder] Vehicle art missing: the jeep and targets stay greybox");
                return;
            }
            Material M(string prop, float glow = 0) => VehicleMaterial(prop, VehicleSmoothness.TryGetValue(prop, out float s) ? s : -1, glow);
            var root = new GameObject("VehicleArt").transform;
            float k = JeepK;
            ArtHolder("Body", root, "jeep_body", M("jeep_body", 1.5f), JeepOrigin * k, Quaternion.identity, k, CutJeepBody());
            // The riser: a gunmetal post from the pedestal's plate up to the raised yoke.
            var riserMat = SaveMaterial("Vehicle_riser", Shader.Find("Universal Render Pipeline/Lit"), new Color(0.16f, 0.17f, 0.18f));
            riserMat.SetFloat("_Smoothness", 0.4f);
            var riser = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(riser.GetComponent<Collider>());
            riser.name = "Riser";
            riser.transform.SetParent(root, false);
            riser.transform.localPosition = JeepToRig(new Vector3(0.007f, 0.640f + RiserLift / 2, -1.361f));
            riser.transform.localScale = new Vector3(0.13f, RiserLift / 2, 0.13f) * k;
            riser.GetComponent<MeshRenderer>().sharedMaterial = riserMat;
            ArtHolder("Wheel", root, "jeep_wheel", M("jeep_wheel"), Vector3.zero, Quaternion.identity, 1.354f * k);
            ArtHolder("Steering", root, "jeep_steering", M("jeep_steering", 1.5f), Vector3.zero, Quaternion.identity, 1.26f * k);
            ArtHolder("Yoke", root, "gun_yoke", M("gun_yoke"), Vector3.zero, Quaternion.Euler(0, 90, 0), k);
            ArtHolder("Gun", root, "gun_hmg", M("gun_hmg", 2f), new Vector3(0.042f, -0.081f, 0.514f) * k, Quaternion.Euler(0, 180, 0), 0.707f * k);

            // Targets and cover: Tripo's front (-X) turned to +Z (the road); bases on the ground.
            var front = Quaternion.Euler(0, 90, 0);
            ArtHolder("Drum", root, "fuel_drum", M("fuel_drum"), new Vector3(0, 0.325f * 1.9f, 0), front, 1.9f);
            // The plate's hinge pin (pivot space (-0.003, -0.571, 0), along Z) on the pivot, along X.
            ArtHolder("Plate", root, "target_plate", M("target_plate"), -(front * new Vector3(-0.003f, -0.571f, 0)) * 1.4f, front, 1.4f);
            ArtHolder("Trailer", root, "fuel_trailer", M("fuel_trailer"), new Vector3(0, 0.75f * 1.4f, 0), front, 1.4f);
            ArtHolder("Nest", root, "sandbag_nest", M("sandbag_nest"), new Vector3(0, 0.6f * NestScale, 0), front, NestScale);
            ArtHolder("Tower", root, "watchtower", M("watchtower"), new Vector3(0, 3.0f * 1.4f, 0), front, 1.4f);
            ArtHolder("Roadblock", root, "roadblock", M("roadblock"), new Vector3(0, 0.6f * RoadblockScale, 0), front, RoadblockScale);
            // The hesco lies at a diagonal: -41.2 degrees puts its length on X (centre offset 0.012).
            ArtHolder("Hesco", root, "hesco_wall", M("hesco_wall"), new Vector3(0, 0.3f * HescoScale, 0.012f * HescoScale), Quaternion.Euler(0, -41.2f, 0), HescoScale);

            Directory.CreateDirectory(Path.GetDirectoryName(VehicleArtPath));
            PrefabUtility.SaveAsPrefabAsset(root.gameObject, VehicleArtPath);
            Object.DestroyImmediate(root.gameObject);
            AddressablesSetup.ConfigureVehicles(VehicleArtPath);
        }

        public const float NestScale = 1.2f, RoadblockScale = 2.2f, HescoScale = 2.8f;
    }
}
