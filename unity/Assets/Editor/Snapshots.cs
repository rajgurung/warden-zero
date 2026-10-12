using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace WardenZero.EditorTools
{
    // Still renders of the generated scenes for checking the look without a WebGL build:
    //   -executeMethod WardenZero.EditorTools.Snapshots.Capture
    // Writes PNGs to Logs/Snapshots (git ignores Logs).
    public static class Snapshots
    {
        const string OutDir = "Logs/Snapshots";

        public static void Capture()
        {
            Directory.CreateDirectory(OutDir);
            EditorSceneManager.OpenScene("Assets/Scenes/Arena.unity");
            {
                var ex = Object.FindFirstObjectByType<Extraction>(FindObjectsInactive.Include);
                ex.chopper.gameObject.SetActive(true);
                ex.chopper.transform.SetPositionAndRotation(Extraction.Lz, Quaternion.Euler(0, Extraction.LzHeading, 0));
                var warden = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
                warden.transform.position = ex.chopper.door.position + Vector3.down * ex.chopper.door.position.y + new Vector3(-1, 0, -3);
                Shot("arena_chopper", warden.transform.position + GameConfig.CameraOffset, warden.transform.position + new Vector3(0, 0, GameConfig.CameraLookAhead), GameConfig.CameraFovDegrees);
                Shot("arena_chopper_side", Extraction.Lz + new Vector3(10, 6, -22), Extraction.Lz + Vector3.up * 3, 50);
            }

            EditorSceneManager.OpenScene("Assets/Scenes/Jungle.unity");
            var stage = Object.FindFirstObjectByType<JungleStage>(FindObjectsInactive.Include);
            Vector3 lz = stage.lz, cp = stage.checkpoint;
            Shot("jungle_gameplay", lz + new Vector3(0, 8.5f, -10.5f), lz + new Vector3(0, 0, GameConfig.CameraLookAhead), GameConfig.CameraFovDegrees);
            Shot("jungle_ground", lz + new Vector3(4, 1.7f, 6), cp + Vector3.up * 2, 60);
            Shot("jungle_wide", new Vector3(-70, 30, -75), new Vector3(10, 0, 10), 60);
            Shot("jungle_checkpoint", cp + new Vector3(-6, 3, -9), cp + Vector3.up * 1.5f, 55);
            Shot("jungle_stream", new Vector3(-12, 1.8f, -10), new Vector3(10, -0.5f, 6), 60);
            Shot("jungle_air_600", lz + new Vector3(-200, 600, -260), lz, 62);
            Shot("jungle_air_150", lz + new Vector3(-40, 150, -60), lz, 62);
            Shot("jungle_horizon", lz + new Vector3(-300, 900, -400), lz + new Vector3(600, 300, 800), 60);

            // The traversal course, from the trail before each beat (the Warden standing at it,
            // facing it, for scale), and the gate from above.
            var walker = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include).transform;
            Vector3 home = walker.position;
            Quaternion homeRot = walker.rotation;
            // As in play behind him: no see-through silhouette pass.
            var bodies = walker.GetComponentsInChildren<Renderer>(true).Where(r => r.sharedMaterials.Any(m => m != null && m.shader.name.Contains("XRay"))).ToArray();
            var keep = bodies.Select(r => r.sharedMaterials).ToArray();
            foreach (var r in bodies) r.sharedMaterials = r.sharedMaterials.Where(m => m == null || !m.shader.name.Contains("XRay")).ToArray();
            foreach (var name in new[] { "1_Log", "2_CrateStack", "3_Ford1", "4_Ledge", "5_WallVine", "6_Terrace" })
            {
                var b = CourseLayout.Pieces.First(p => p.Name == name).Block;
                // The terrace stands close behind the gate: nearer for that one.
                Vector3 from = b.ToWorld(new Vector3(-1.5f, 0, -b.Half.y - (name == "6_Terrace" ? 4 : 7)));
                from.y = stage.Ground(from) + 3;
                Vector3 at = b.ToWorld(new Vector3(1.2f, 0, -b.Half.y - 1.4f));
                at.y = stage.Ground(at);
                walker.SetPositionAndRotation(at, Quaternion.LookRotation(-b.Normal(2)));
                Shot("course_" + name, from, new Vector3(b.Center.x, b.Top - 0.5f, b.Center.y), 55);
            }
            walker.SetPositionAndRotation(home, homeRot);
            for (int i = 0; i < bodies.Length; i++) bodies[i].sharedMaterials = keep[i];
            var gate = CourseLayout.Pieces.First(p => p.Name == "5_WallVine").Block;
            Shot("course_gate_above", gate.ToWorld(new Vector3(-4, 0, -16)) + Vector3.up * 22, new Vector3(gate.Center.x, 0, gate.Center.y), 60);

            // The chopper and the jump rig over the jungle.
            var ch = stage.chopper;
            ch.gameObject.SetActive(true);
            var dropPos = lz + new Vector3(-200, 900, -250);
            ch.transform.SetPositionAndRotation(dropPos, Quaternion.Euler(0, 40, 0));
            Shot("flight_chase", ch.transform.TransformPoint(new Vector3(-20, 6, -30)), dropPos, 50);
            var player = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
            player.transform.SetParent(ch.seat, false);
            player.transform.localPosition = Vector3.zero;
            player.transform.localRotation = Quaternion.identity;
            Shot("flight_door", ch.transform.TransformPoint(new Vector3(-8.5f, 2, 9)), player.transform.position + Vector3.up * 1.3f, 55);
            player.transform.SetParent(null, true);

            var p = lz + new Vector3(-30, 160, -40);
            player.transform.SetPositionAndRotation(p, Quaternion.Euler(0, 40, 0) * Quaternion.Euler(78, 0, 0));
            Shot("freefall", p + Quaternion.Euler(0, 40, 0) * new Vector3(0, 4.7f, -8), p + Quaternion.Euler(0, 40, 0) * new Vector3(0, -6, 10), 62);
            player.transform.SetPositionAndRotation(p, Quaternion.Euler(0, 40, 0));
            stage.parachute.gameObject.SetActive(true);
            stage.parachute.SetPositionAndRotation(p + Vector3.up * 7.2f, Quaternion.Euler(0, 40, 0));
            Shot("canopy", p + Quaternion.Euler(0, 40, 0) * new Vector3(0, 3.4f, -12), p + Quaternion.Euler(0, 40, 0) * new Vector3(0, 3.4f, 10), 60);
            Shot("canopy_front", p + Quaternion.Euler(0, 40, 0) * new Vector3(3, 4, 12), p + Vector3.up * 4, 60);
            RoadShots(stage);
            Debug.Log("[Snapshots] written to " + Path.GetFullPath(OutDir));
        }

        // Just the road (M3): -executeMethod WardenZero.EditorTools.Snapshots.CaptureRoad
        public static void CaptureRoad()
        {
            Directory.CreateDirectory(OutDir);
            EditorSceneManager.OpenScene("Assets/Scenes/Jungle.unity");
            RoadShots(Object.FindFirstObjectByType<JungleStage>(FindObjectsInactive.Include));
            Debug.Log("[Snapshots] road written to " + Path.GetFullPath(OutDir));
        }

        // Along the road at driver's-eye height (behind and above a spot, looking down the road),
        // and the tile from the air on the way down.
        static void RoadShots(JungleStage stage)
        {
            void Along(string name, RoadPath road, float s, float back = 9, float up = 3.5f, float ahead = 30)
            {
                var a = road.At(s - back);
                var t = road.At(s + ahead);
                Vector3 from = new Vector3(a.Position.x, 0, a.Position.y);
                from.y = stage.Ground(from) + up;
                Vector3 at = new Vector3(t.Position.x, 0, t.Position.y);
                at.y = stage.Ground(at) + 1.5f;
                Shot(name, from, at, 60);
            }
            var drive = RoadLayout.Drive;
            Along("road_motorpool", drive, 6, 10);
            Along("road_climb", drive, drive.Nearest(new Vector2(52, 62)).S);
            Along("road_rim", drive, drive.Nearest(new Vector2(56, 80)).S, 12, 4, 40);
            Along("road_straight", drive, drive.Nearest(new Vector2(140, 150)).S, 9, 3.5f, 60);
            Along("road_hairpin", drive, drive.Nearest(new Vector2(232, 180)).S, 9, 3.5f, 25);
            Along("road_to_b", drive, drive.Length - 40, 9, 3.5f, 40);
            Along("road_ride", RoadLayout.Ride, 120, 9, 3.5f, 40);
            var b = RoadLayout.CheckpointB;
            Shot("road_b_above", b + new Vector3(-30, 35, -40), b, 55);
            Shot("road_b", b + new Vector3(-14, 4, -16), b + Vector3.up * 1.5f, 55);

            // The ride's targets from the jeep's gun as it comes up to each (plates raised).
            foreach (var d in Object.FindObjectsByType<Destructible>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (d.kind == RoadLayout.TargetKind.Plate) d.transform.localRotation = d.transform.localRotation;
            void FromRide(string name, float s, float atS, float atSide)
            {
                var a = RoadLayout.Ride.At(s);
                var from = new Vector3(a.Position.x, 0, a.Position.y);
                from.y = stage.Ground(from) + 4;
                var at = RoadLayout.RidePoint(atS, atSide, out _) + Vector3.up * 1.2f;
                Shot(name, from, at, 50);
            }
            FromRide("ride_warmup", 18, 36, 10.5f);
            FromRide("ride_nest", 62, 85, -12.5f);
            FromRide("ride_tower", 100, 124, 12.5f);
            FromRide("ride_fueldump", 150, 170, -12.5f);
            FromRide("ride_roadblock", RoadLayout.RideEnd - 30, RoadLayout.RideEnd + 10, 0);

            // The jeep at the motor pool, the Warden at its door and then in the seat (without
            // his see-through pass, as in play).
            var road = stage.GetComponent<RoadStage>();
            if (road != null)
            {
                var jeep = road.jeep.transform;
                var walker = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include).transform;
                Vector3 home = walker.position;
                var bodies = walker.GetComponentsInChildren<Renderer>(true).Where(r => r.sharedMaterials.Any(m => m != null && m.shader.name.Contains("XRay"))).ToArray();
                var keep = bodies.Select(r => r.sharedMaterials).ToArray();
                foreach (var r in bodies) r.sharedMaterials = r.sharedMaterials.Where(m => m == null || !m.shader.name.Contains("XRay")).ToArray();
                var door = road.jeep.rig.doorL.position;
                walker.position = new Vector3(door.x, stage.Ground(door), door.z);
                Shot("jeep_motorpool", jeep.TransformPoint(new Vector3(-6, 2.6f, 6)), jeep.position + Vector3.up * 1.2f, 50);
                Shot("jeep_side", jeep.TransformPoint(new Vector3(-8, 1.6f, 0)), jeep.position + Vector3.up * 1.2f, 45);
                walker.position = road.jeep.rig.driver.position - Vector3.up * VehicleController.HipHeight;
                Shot("jeep_seated", jeep.TransformPoint(new Vector3(-4, 2.4f, -7)), jeep.position + Vector3.up * 1.6f, 50);
                walker.position = home;
                for (int i = 0; i < bodies.Length; i++) bodies[i].sharedMaterials = keep[i];
            }
            var lz = stage.lz;
            Shot("road_air_600", lz + new Vector3(-200, 600, -260), new Vector3(150, 40, 260), 62);
            Shot("road_air_250", lz + new Vector3(-60, 250, -90), new Vector3(150, 30, 260), 62);
            Shot("road_air_seam", new Vector3(100, 90, 40), new Vector3(60, 10, 120), 60);
            Shot("road_air_edge", new Vector3(380, 160, 300), new Vector3(300, 60, 300), 60);
            Shot("road_top_edge", new Vector3(330, 200, 300.1f), new Vector3(330, 0, 300), 60);
        }

        static void Shot(string name, Vector3 from, Vector3 at, float fov)
        {
            var main = Camera.main;
            var go = new GameObject("SnapshotCam");
            var cam = go.AddComponent<Camera>();
            if (main != null) cam.CopyFrom(main);
            cam.fieldOfView = fov;
            cam.farClipPlane = 5000;
            cam.nearClipPlane = 0.3f;
            go.transform.position = from;
            go.transform.LookAt(at);
            // Same altitude haze as JungleStage at runtime.
            float fog = RenderSettings.fogDensity;
            var stage = Object.FindFirstObjectByType<JungleStage>(FindObjectsInactive.Include);
            if (stage != null)
            {
                float h = from.y - stage.Ground(from);
                RenderSettings.fogDensity = Mathf.Lerp(fog, fog * 0.025f, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(15, 160, h)));
            }
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            RenderTexture.active = null;
            File.WriteAllBytes($"{OutDir}/{name}.png", tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(go);
            RenderSettings.fogDensity = fog;
        }
    }
}
