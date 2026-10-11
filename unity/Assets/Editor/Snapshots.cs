using System.IO;
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
            Debug.Log("[Snapshots] written to " + Path.GetFullPath(OutDir));
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
