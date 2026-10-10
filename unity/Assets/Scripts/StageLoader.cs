using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WardenZero
{
    // Streams Stage 2. The jungle scene and its heavy assets are an Addressables group whose
    // bundles sit next to the game on the same site (StreamingAssets/aa, a relative URL), so
    // the first load stays small. Preload starts the download in the background during
    // Stage 1; LoadJungle fades to black and shows the progress until the scene is in.
    // Also owns the full-screen fade used between scenes (it survives scene loads).
    public class StageLoader : MonoBehaviour
    {
        public const string JungleAddress = "Jungle";

        static StageLoader instance;
        static AsyncOperationHandle download;
        static bool downloaded;
        static AsyncOperationHandle<UnityEngine.ResourceManagement.ResourceProviders.SceneInstance> load;

        Image black;
        Text status;
        float alpha;
        float target;
        float fadeSpeed = 2;

        public static bool Loading => load.IsValid() && !load.IsDone;
        public static float Fade => instance != null ? instance.alpha : 0;

        // 0..1 of the background download.
        public static float DownloadProgress =>
            downloaded ? 1 : download.IsValid() ? download.GetDownloadStatus().Percent : 0;

        static StageLoader Get()
        {
            if (instance != null) return instance;
            var go = new GameObject("StageLoader");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<StageLoader>();
            instance.BuildOverlay();
            return instance;
        }

        // Start fetching the jungle's bundles without loading the scene.
        public static void Preload()
        {
            Get();
            if (downloaded || download.IsValid()) return;
            download = Addressables.DownloadDependenciesAsync(JungleAddress);
        }

        // Fade out, then load the jungle (single mode) once its bundles are in.
        public static void LoadJungle()
        {
            var me = Get();
            if (Loading) return;
            me.FadeTo(1, 2.5f);
            me.StartCoroutine(me.LoadWhenBlack());
        }

        public static void FadeOut(float speed = 2) => Get().FadeTo(1, speed);
        public static void FadeIn(float speed = 1.2f) => Get().FadeTo(0, speed);

        System.Collections.IEnumerator LoadWhenBlack()
        {
            while (alpha < 0.99f) yield return null;
            Preload();
            Time.timeScale = 1;
            load = Addressables.LoadSceneAsync(JungleAddress, LoadSceneMode.Single);
            while (!load.IsDone)
            {
                float p = load.GetDownloadStatus().Percent;
                status.text = p < 1 ? $"DROP ZONE  ·  DOWNLOADING {Mathf.RoundToInt(p * 100)}%" : "DROP ZONE  ·  LOADING";
                yield return null;
            }
            status.text = "";
            if (load.Status != AsyncOperationStatus.Succeeded)
            {
                status.text = "COULD NOT LOAD THE JUNGLE  ·  RELOAD THE PAGE";
                Debug.LogError("[StageLoader] Jungle failed to load: " + load.OperationException);
            }
        }

        void FadeTo(float a, float speed)
        {
            target = a;
            fadeSpeed = speed;
        }

        void Update()
        {
            // The handle keeps the bundles loaded; once they are cached, let them go until needed.
            if (download.IsValid() && download.IsDone)
            {
                downloaded = download.Status == AsyncOperationStatus.Succeeded;
                Addressables.Release(download);
                download = default;
            }
            alpha = Mathf.MoveTowards(alpha, target, Time.unscaledDeltaTime * fadeSpeed);
            black.color = new Color(0, 0, 0, alpha);
            black.enabled = alpha > 0.001f;
            status.enabled = alpha > 0.5f && status.text.Length > 0;
        }

        // Built here rather than in a scene, because it has to outlive scene loads.
        void BuildOverlay()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;

            black = new GameObject("Black", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            var rt = black.rectTransform;
            rt.SetParent(transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            black.color = Color.clear;
            black.raycastTarget = false;

            status = new GameObject("Status", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            var st = status.rectTransform;
            st.SetParent(transform, false);
            st.anchorMin = st.anchorMax = new Vector2(0.5f, 0.5f);
            st.sizeDelta = new Vector2(900, 40);
            status.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            status.fontSize = 16;
            status.fontStyle = FontStyle.Bold;
            status.alignment = TextAnchor.MiddleCenter;
            status.color = GameConfig.TextDim;
            status.raycastTarget = false;
            status.text = "";
        }
    }
}
