using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace WardenZero.EditorTools
{
    // The campaign's shared pieces: the Tripo props (chopper with spinning rotors, canopy,
    // checkpoint beacon) and the arena's extraction set piece.
    public static partial class SceneBuilder
    {
        const string CampDir = CampaignImport.Dir;
        const float ChopperLength = 17; // nose to tail, metres (a UH-60 is 19.8 with rotors)
        const float MainRotorRadius = 7.6f;
        const float TailRotorRadius = 1.5f;

        static Material chopperMat;
        static Material rotorMat;
        static Material canopyMat;
        static Material beaconMat;
        static Material discMat;
        static Material dustMat;
        static Texture2D smokeTex;
        static Chopper chopperPrefab;

        static void PrepareCampaign()
        {
            CampaignImport.Configure();
            WriteCampaignTextures();
            chopperMat = PropMaterial("Chopper", 2f);
            rotorMat = PropMaterial("Rotor", 1.5f);
            canopyMat = PropMaterial("Canopy", 1.5f);
            beaconMat = PropMaterial("Beacon", 3f);
            canopyMat.SetFloat("_Cull", 0); // the canopy is seen from below, inside the cells
            canopyMat.doubleSidedGI = true;
            // Daylight through the fabric: the underside would otherwise be near black.
            canopyMat.SetTexture("_EmissionMap", canopyMat.GetTexture("_BaseMap"));
            canopyMat.SetColor("_EmissionColor", new Color(0.9f, 0.9f, 0.85f));

            // Rotor blur: a soft disc that fades in at speed.
            discMat = SaveMaterial("RotorDisc", Unlit(), new Color(1, 1, 1, 0.3f));
            MakeTransparent(discMat);
            discMat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(CampDir + "/Generated/rotor_disc.png"));
            dustMat = SaveMaterial("RotorDust", Shader.Find("Universal Render Pipeline/Particles/Unlit"), new Color(0.75f, 0.7f, 0.6f, 0.35f));
            MakeTransparent(dustMat);
            dustMat.SetTexture("_BaseMap", smokeTex);
            AssetDatabase.SaveAssets();
            chopperPrefab = MakeChopperPrefab();
        }

        static Material PropMaterial(string prop, float glow)
        {
            string gen = $"{CampDir}/{prop}/Generated";
            Directory.CreateDirectory(gen);
            return ModelMaterial("Prop" + prop, CampaignImport.Textures(prop), prop.ToLowerInvariant(), gen, (int)RenderQueue.Geometry, glow);
        }

        static void MakeTransparent(Material mat)
        {
            mat.SetFloat("_Surface", 1);
            mat.SetFloat("_Blend", 0);
            mat.SetFloat("_ZWrite", 0);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_Cull", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)RenderQueue.Transparent;
        }

        // ------------------------------------------------------------------ props

        // A Tripo prop as a child of `parent`: rotated by `yaw`, scaled so its largest size
        // along `measure` (0 = x, 1 = y, 2 = z, after the yaw) is `size`, and moved so its
        // bounding box bottom centre sits at the parent's origin.
        // The FBX keeps its own root rotation (Tripo files are Z-up) inside a holder that
        // carries the yaw, scale and offset.
        static GameObject PlaceProp(string prop, Transform parent, Material mat, float yaw, int measure, float size)
        {
            var holder = new GameObject(prop + "Model").transform;
            holder.SetParent(parent, false);
            holder.localRotation = Quaternion.Euler(0, yaw, 0);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(CampaignImport.Fbx(prop));
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
            go.transform.SetParent(holder, false);
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mat;
            var b = LocalBounds(holder.gameObject, parent);
            holder.localScale = Vector3.one * (size / b.size[measure]);
            b = LocalBounds(holder.gameObject, parent);
            holder.localPosition -= new Vector3(b.center.x, b.min.y, b.center.z);
            return holder.gameObject;
        }

        // Heading (degrees from +Z toward +X) of the longest horizontal axis of a point cloud.
        static float PrincipalHeading(List<Vector3> pts)
        {
            float mx = pts.Average(p => p.x), mz = pts.Average(p => p.z);
            float sxx = 0, szz = 0, sxz = 0;
            foreach (var p in pts)
            {
                float dx = p.x - mx, dz = p.z - mz;
                sxx += dx * dx;
                szz += dz * dz;
                sxz += dx * dz;
            }
            // Angle from +X toward +Z of the major axis, turned into a heading.
            float theta = 0.5f * Mathf.Atan2(2 * sxz, sxx - szz);
            return Mathf.Atan2(Mathf.Cos(theta), Mathf.Sin(theta)) * Mathf.Rad2Deg;
        }

        // Bounds of every mesh vertex under `go`, in `space`'s local coordinates.
        static Bounds LocalBounds(GameObject go, Transform space)
        {
            var pts = LocalVertices(go, space);
            var b = new Bounds(pts[0], Vector3.zero);
            foreach (var p in pts) b.Encapsulate(p);
            return b;
        }

        static List<Vector3> LocalVertices(GameObject go, Transform space)
        {
            var pts = new List<Vector3>();
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            {
                var m = space.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                foreach (var v in mf.sharedMesh.vertices) pts.Add(m.MultiplyPoint3x4(v));
            }
            return pts;
        }

        // Root (Chopper: position and heading) > Body (pitch and bank) > model, rotors, door, seat.
        // The model is turned so the nose points +Z (the tail end is the one with the tall fin)
        // and its wheels sit on y = 0. The rotor hubs are found from the mesh: the highest
        // point over the middle of the fuselage and the top of the tail fin.
        static Chopper MakeChopperPrefab()
        {
            var root = new GameObject("Chopper");
            var chopper = root.AddComponent<Chopper>();
            var body = new GameObject("Body").transform;
            body.SetParent(root.transform, false);
            chopper.body = body;

            // Tripo built it from a three-quarter view, so the fuselage can lie at any angle:
            // line it up with +Z, then turn it round if the tall fin ended up in front.
            var probe = PlaceProp("Chopper", body, chopperMat, 0, 1, 1);
            float yaw = -PrincipalHeading(LocalVertices(probe, body));
            Object.DestroyImmediate(probe);
            probe = PlaceProp("Chopper", body, chopperMat, yaw, 2, 1);
            var b = LocalBounds(probe, body);
            var pts = LocalVertices(probe, body);
            float len = b.size.z;
            float topBack = pts.Where(p => p.z < b.min.z + len * 0.12f).Max(p => p.y);
            float topFront = pts.Where(p => p.z > b.max.z - len * 0.12f).Max(p => p.y);
            if (topFront > topBack) yaw += 180;
            Object.DestroyImmediate(probe);
            var model = PlaceProp("Chopper", body, chopperMat, yaw, 2, ChopperLength);
            foreach (var r in model.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.On;

            pts = LocalVertices(model, body);
            b = LocalBounds(model, body);
            float L = b.size.z, H = b.size.y;
            // Main hub: the top of the mast over the middle third.
            var mid = pts.Where(p => Mathf.Abs(p.z - b.center.z) < L * 0.2f).ToList();
            float mastTop = mid.Max(p => p.y);
            var hubPts = mid.Where(p => p.y > mastTop - H * 0.04f).ToList();
            var hub = new Vector3(hubPts.Average(p => p.x), mastTop, hubPts.Average(p => p.z));
            // Tail hub: the top of the fin, rear 12%.
            var rear = pts.Where(p => p.z < b.min.z + L * 0.12f).ToList();
            float finTop = rear.Max(p => p.y);
            var finPts = rear.Where(p => p.y > finTop - H * 0.12f).ToList();
            var tailHub = new Vector3(finPts.Max(p => p.x) + 0.15f, finPts.Average(p => p.y), finPts.Average(p => p.z));

            var main = new GameObject("MainRotor").transform;
            main.SetParent(body, false);
            main.localPosition = hub;
            PlaceRotor(main, MainRotorRadius, 0.15f);
            chopper.mainRotor = main;
            chopper.mainDisc = RotorDisc(body, hub + Vector3.up * 0.2f, Quaternion.Euler(90, 0, 0), MainRotorRadius);

            var tail = new GameObject("TailRotor").transform;
            tail.SetParent(body, false);
            tail.localPosition = tailHub;
            // The tail rotor turns about the sideways axis: its blades lie in the fin's plane.
            var tailBlades = new GameObject("Blades").transform;
            tailBlades.SetParent(tail, false);
            tailBlades.localRotation = Quaternion.Euler(0, 0, 90);
            PlaceRotor(tailBlades, TailRotorRadius, 0);
            chopper.tailRotor = tail;
            chopper.tailDisc = RotorDisc(body, tailHub + Vector3.right * 0.1f, Quaternion.Euler(0, 90, 0), TailRotorRadius);

            // The cabin door is on the left (-X), just behind the cockpit; floor at ~0.25 of the height.
            float doorZ = b.center.z + L * 0.12f;
            var side = pts.Where(p => Mathf.Abs(p.z - doorZ) < 0.6f && p.y > H * 0.2f && p.y < H * 0.5f).ToList();
            float leftX = side.Count > 0 ? side.Min(p => p.x) : b.min.x;
            chopper.door = new GameObject("Door").transform;
            chopper.door.SetParent(body, false);
            chopper.door.localPosition = new Vector3(leftX - 0.4f, H * 0.22f, doorZ);
            chopper.seat = new GameObject("Seat").transform;
            chopper.seat.SetParent(body, false);
            chopper.seat.localPosition = new Vector3(leftX + 0.9f, H * 0.2f, doorZ);
            chopper.seat.localRotation = Quaternion.Euler(0, -90, 0); // facing out of the door

            chopper.wash = MakeRotorWash(root.transform);
            root.AddComponent<ChopperAudio>();
            Debug.Log($"[SceneBuilder] chopper {b.size} hub {hub} tail {tailHub} door {chopper.door.localPosition}");
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "/Chopper.prefab");
            Object.DestroyImmediate(root);
            return prefab.GetComponent<Chopper>();
        }

        // The Tripo rotor scaled to `radius` (centre on the hub). Its own mesh can be any size.
        static void PlaceRotor(Transform hub, float radius, float lift)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(CampaignImport.Fbx("Rotor"));
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
            go.transform.SetParent(hub, false);
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                r.sharedMaterial = rotorMat;
                r.shadowCastingMode = ShadowCastingMode.On;
            }
            var pts = LocalVertices(go, hub);
            var b = LocalBounds(go, hub);
            float reach = pts.Max(p => new Vector2(p.x - b.center.x, p.z - b.center.z).magnitude);
            float s = radius / reach;
            go.transform.localScale = Vector3.one * s;
            b = LocalBounds(go, hub);
            go.transform.localPosition -= new Vector3(b.center.x, b.min.y - lift, b.center.z);
        }

        static Renderer RotorDisc(Transform parent, Vector3 pos, Quaternion rot, float radius)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = "Disc";
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = Vector3.one * radius * 2;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = discMat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return mr;
        }

        // Dust kicked up under the rotor: soft puffs blown outward along the ground.
        static ParticleSystem MakeRotorWash(Transform parent)
        {
            var go = new GameObject("RotorWash");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(6, 12);
            main.startSize = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            main.maxParticles = 300;
            var emission = ps.emission;
            emission.rateOverTime = 0;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 3;
            shape.radiusThickness = 0.3f;
            shape.rotation = new Vector3(-90, 0, 0);
            shape.alignToDirection = false;
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.radial = new ParticleSystem.MinMaxCurve(4, 4);
            vel.y = new ParticleSystem.MinMaxCurve(0.4f, 1.2f);
            vel.x = new ParticleSystem.MinMaxCurve(0, 0);
            vel.z = new ParticleSystem.MinMaxCurve(0, 0);
            vel.orbitalX = new ParticleSystem.MinMaxCurve(0, 0);
            vel.orbitalY = new ParticleSystem.MinMaxCurve(0, 0);
            vel.orbitalZ = new ParticleSystem.MinMaxCurve(0, 0);
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.dampen = 0.15f;
            limit.limit = 3;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 0.6f, 1, 1.8f));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                         new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(0.6f, 0.15f), new GradientAlphaKey(0, 1) });
            col.color = grad;
            var pr = go.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = dustMat;
            pr.shadowCastingMode = ShadowCastingMode.Off;
            pr.receiveShadows = false;
            pr.sortingFudge = 10;
            return ps;
        }

        // ------------------------------------------------------------------ arena extraction

        static void BuildExtraction(GameManager gm)
        {
            var go = new GameObject("Extraction");
            var ex = go.AddComponent<Extraction>();
            ex.chopper = ((GameObject)PrefabUtility.InstantiatePrefab(chopperPrefab.gameObject)).GetComponent<Chopper>();
            ex.chopper.transform.SetParent(go.transform, false);
            ex.hud = BuildObjectiveHud(gm.hud, -92, "HOLDING THE LZ", GameConfig.Gold);
            ex.alert = Clip("strike_ready");
            var lzMat = GlowMaterial("LzRing", ringTex, Hdr(GameConfig.Gold, 1.6f), GroundGlowQueue);
            ex.lzRing = GlowQuad("LzRing", go.transform, lzMat, 1).GetComponent<MeshRenderer>();
            var doorMat = GlowMaterial("DoorRing", ringTex, Hdr(GameConfig.Accent, 2f), GroundGlowQueue);
            ex.doorMarker = GlowQuad("DoorMarker", go.transform, doorMat, Extraction.BoardRadius * 2).GetComponent<MeshRenderer>();

            ex.brain = gm.player.cam.GetComponent<CinemachineBrain>();
            ex.snapCam = NewCinemachineCamera("SnapCam", go.transform, 10);
            ex.liftCam = NewCinemachineCamera("LiftCam", go.transform, 20);
            ex.liftCam.Follow = ex.chopper.transform;
            ex.liftCam.LookAt = ex.chopper.transform;
            var follow = ex.liftCam.gameObject.AddComponent<CinemachineFollow>();
            follow.FollowOffset = new Vector3(14, 9, -24);
            follow.TrackerSettings.BindingMode = Unity.Cinemachine.TargetTracking.BindingMode.WorldSpace;
            follow.TrackerSettings.PositionDamping = new Vector3(1.2f, 1.2f, 1.2f);
            var aim = ex.liftCam.gameObject.AddComponent<CinemachineRotationComposer>();
            aim.Damping = new Vector2(0.6f, 0.6f);
            gm.extraction = ex;
        }

        static CinemachineCamera NewCinemachineCamera(string name, Transform parent, int priority, float fov = 50)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var cam = go.AddComponent<CinemachineCamera>();
            cam.Priority = priority;
            var lens = LensSettings.Default;
            lens.FieldOfView = fov;
            lens.NearClipPlane = 0.3f;
            lens.FarClipPlane = 4000;
            cam.Lens = lens;
            go.SetActive(false);
            return cam;
        }

        // Rotor blur disc and a soft smoke puff.
        static void WriteCampaignTextures()
        {
            Directory.CreateDirectory(CampDir + "/Generated");
            const int S = 256;
            var disc = new Texture2D(S, S, TextureFormat.RGBA32, false);
            var smoke = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            var tint = new Color(0.16f, 0.18f, 0.12f);
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = x - S / 2 + 0.5f, dy = y - S / 2 + 0.5f;
                float r = Mathf.Sqrt(dx * dx + dy * dy) / (S / 2);
                // Denser toward the tips (blades sweep more area per turn near the hub, but
                // the eye reads the outer ring), a faint cyan band where the tip stripes run.
                float a = r > 1 ? 0 : Mathf.SmoothStep(0, 1, (r - 0.08f) / 0.2f) * Mathf.Lerp(0.55f, 0.9f, r) * Mathf.Clamp01((1 - r) / 0.03f);
                var c = Color.Lerp(tint, GameConfig.Accent, Mathf.Clamp01(1 - Mathf.Abs(r - 0.9f) / 0.025f) * 0.6f);
                disc.SetPixel(x, y, new Color(c.r, c.g, c.b, a));
            }
            var rng = new System.Random(5);
            float ox = (float)rng.NextDouble() * 50, oy = (float)rng.NextDouble() * 50;
            for (int y = 0; y < 128; y++)
            for (int x = 0; x < 128; x++)
            {
                float d = new Vector2(x - 63.5f, y - 63.5f).magnitude / 64;
                float n = Mathf.PerlinNoise(ox + x * 0.06f, oy + y * 0.06f) * 0.6f + Mathf.PerlinNoise(ox + x * 0.15f, oy + y * 0.15f) * 0.4f;
                float a = Mathf.Clamp01(1 - d) * Mathf.Clamp01(n * 1.6f - 0.25f);
                smoke.SetPixel(x, y, new Color(1, 1, 1, a * a));
            }
            File.WriteAllBytes(CampDir + "/Generated/rotor_disc.png", disc.EncodeToPNG());
            File.WriteAllBytes(CampDir + "/Generated/smoke.png", smoke.EncodeToPNG());
            AssetDatabase.ImportAsset(CampDir + "/Generated/rotor_disc.png", ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(CampDir + "/Generated/smoke.png", ImportAssetOptions.ForceUpdate);
            smokeTex = AssetDatabase.LoadAssetAtPath<Texture2D>(CampDir + "/Generated/smoke.png");
        }
    }
}
