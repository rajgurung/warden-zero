using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Unity.Collections;
using UnityEngine;

namespace WardenZero
{
    // Debug URLs for measuring the wasm heap:
    // - ?mem shows the heap size at the top left, under the health bar;
    // - ?memprobe=N allocates N MB of native memory, 1 MB at a time, 3 s after the jungle is
    //   playing, and logs whether the heap grew. The largest N that does not grow it is the
    //   headroom left before the next growth step (M3 decision 0.5).
    public class MemoryProbe : MonoBehaviour
    {
        public const float ProbeDelay = 3;

        public static bool ShowHeap(string url) => Regex.IsMatch(url ?? "", @"[?&]mem(?=$|[&#])");

        // Megabytes to allocate (0 when absent).
        public static int ProbeMegabytes(string url)
        {
            var m = Regex.Match(url ?? "", @"[?&]memprobe=(\d+)");
            return m.Success ? Mathf.Min(int.Parse(m.Groups[1].Value), 1024) : 0;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        static extern int WZ_HeapMB();
        public static int HeapMB => WZ_HeapMB();
#else
        public static int HeapMB => (int)(UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong() / (1024 * 1024));
#endif

        readonly List<NativeArray<byte>> held = new List<NativeArray<byte>>();
        bool show;
        int probe;
        float playing;
        string label = "";
        float nextLabel;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            string url = Application.absoluteURL;
            bool show = ShowHeap(url);
            int probe = ProbeMegabytes(url);
            if (!show && probe == 0) return;
            var go = new GameObject("MemoryProbe");
            DontDestroyOnLoad(go);
            var me = go.AddComponent<MemoryProbe>();
            me.show = show;
            me.probe = probe;
        }

        void Update()
        {
            if (Time.unscaledTime >= nextLabel)
            {
                nextLabel = Time.unscaledTime + 0.5f;
                label = $"heap {HeapMB} MB";
            }
            if (probe <= 0) return;
            var gm = GameManager.Instance;
            if (gm == null || gm.jungle == null || !gm.IsPlaying) return;
            playing += Time.unscaledDeltaTime;
            if (playing < ProbeDelay) return;
            int before = HeapMB;
            for (int i = 0; i < probe; i++) held.Add(new NativeArray<byte>(1024 * 1024, Allocator.Persistent));
            int after = HeapMB;
            Debug.Log($"[WZ] memprobe {probe} MB: heap {before} -> {after} MB, {(after > before ? "GREW" : "no growth")}");
            probe = 0;
        }

        void OnGUI()
        {
            if (!show) return;
            GUI.color = Color.black;
            GUI.Label(new Rect(19, 59, 240, 24), label);
            GUI.color = Color.white;
            GUI.Label(new Rect(18, 58, 240, 24), label);
        }

        void OnDestroy()
        {
            foreach (var a in held)
                if (a.IsCreated) a.Dispose();
            held.Clear();
        }
    }
}
