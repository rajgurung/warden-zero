using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WardenZero
{
    // Streams Stage 2. The jungle scene and its heavy assets are an Addressables group whose
    // bundles sit next to the game on the same site (StreamingAssets/aa, a relative URL), so
    // the first load stays small. Preload starts the download in the background during
    // Stage 1; LoadJungle fades to black and shows the progress until the scene is in.
    // Also owns the full-screen fade used between scenes (it survives scene loads).
    //
    // While a load is under way (Busy) the overlay swallows clicks and the game refuses to
    // pause, so nothing can start a second load or leave mid-way. A failed load offers
    // RETRY and MENU.
    //
    // Memory: the preloaded bundles are let go as soon as they are downloaded; the scene
    // load fetches them again from the browser's cache (a 304 revalidation, measured). Even
    // holding the download's handle did not stop that second request, so holding only cost
    // memory. Campaign.End releases a download still under way.
    public class StageLoader : MonoBehaviour
    {
        public const string JungleAddress = "Jungle";

        // Test hook: the address to load (a bad one makes the load fail).
        public static string Address = JungleAddress;

        static StageLoader instance;
        static AsyncOperationHandle download;
        static bool downloaded;
        static AsyncOperationHandle<SceneInstance> load;

        Image black;
        Text status;
        GameObject failPanel;
        float alpha;
        float target;
        float fadeSpeed = 2;
        Coroutine loading;

        // From LoadJungle until the scene is in (or the player gives up after a failure).
        public static bool Busy { get; private set; }
        public static bool Failed { get; private set; }
        public static bool Loading => load.IsValid() && !load.IsDone;
        public static float Fade => instance != null ? instance.alpha : 0;
        public static bool HoldingBundles => download.IsValid();

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
            download = Addressables.DownloadDependenciesAsync(Address);
        }

        // Let go of preloaded bundles (the campaign was left), even mid-download; not while a
        // load needs them.
        public static void ReleasePreload()
        {
            if (Busy || !download.IsValid()) return;
            Addressables.Release(download);
            download = default;
            downloaded = false;
        }

        // Fade out, then load the jungle (single mode) once its bundles are in. A second
        // call while one is under way does nothing.
        public static void LoadJungle()
        {
            var me = Get();
            if (Busy) return;
            Busy = true;
            Failed = false;
            me.failPanel.SetActive(false);
            me.FadeTo(1, 2.5f);
            me.loading = me.StartCoroutine(me.LoadWhenBlack());
        }

        // Stop a load that has not reached the scene yet (leaving to the menu) and fade back.
        // Returns false when the scene is already loading: that can't be stopped.
        public static bool Cancel()
        {
            if (!Busy || instance == null) return true;
            if (Loading) return false;
            if (instance.loading != null) instance.StopCoroutine(instance.loading);
            instance.loading = null;
            Busy = false;
            Failed = false;
            instance.failPanel.SetActive(false);
            instance.status.text = "";
            instance.FadeTo(0, 2);
            return true;
        }

        public static void FadeOut(float speed = 2) => Get().FadeTo(1, speed);
        public static void FadeIn(float speed = 1.2f) => Get().FadeTo(0, speed);

        System.Collections.IEnumerator LoadWhenBlack()
        {
            while (alpha < 0.99f) yield return null;
            Preload();
            // Let a background download finish rather than fetch the bundles twice.
            while (download.IsValid() && !download.IsDone)
            {
                status.text = $"DROP ZONE  ·  DOWNLOADING {Mathf.RoundToInt(download.GetDownloadStatus().Percent * 100)}%";
                yield return null;
            }
            if (download.IsValid() && download.Status != AsyncOperationStatus.Succeeded)
            {
                Fail(download.OperationException);
                yield break;
            }
            load = Addressables.LoadSceneAsync(Address, LoadSceneMode.Single);
            while (!load.IsDone)
            {
                float p = load.GetDownloadStatus().Percent;
                status.text = p < 1 ? $"DROP ZONE  ·  DOWNLOADING {Mathf.RoundToInt(p * 100)}%" : "DROP ZONE  ·  LOADING";
                yield return null;
            }
            status.text = "";
            // The scene holds the bundles now; the preload's own handle can go.
            if (download.IsValid())
            {
                Addressables.Release(download);
                download = default;
            }
            downloaded = false;
            if (load.Status != AsyncOperationStatus.Succeeded)
            {
                Fail(load.OperationException);
                yield break;
            }
            loading = null;
            Busy = false;
        }

        void Fail(System.Exception e)
        {
            Debug.LogError("[StageLoader] Jungle failed to load: " + e);
            if (download.IsValid())
            {
                Addressables.Release(download);
                download = default;
            }
            if (load.IsValid())
            {
                Addressables.Release(load);
                load = default;
            }
            downloaded = false;
            loading = null;
            Failed = true;
            status.text = "COULD NOT LOAD THE JUNGLE  ·  CHECK THE CONNECTION";
            failPanel.SetActive(true);
        }

        // RETRY: try the same load again.
        public static void Retry()
        {
            if (!Failed) return;
            Busy = false;
            LoadJungle();
        }

        // MENU: give up and go back to the arena's menu.
        public static void GiveUp()
        {
            if (!Failed) return;
            Failed = false;
            Busy = false;
            instance.failPanel.SetActive(false);
            instance.status.text = "";
            instance.FadeTo(0, 1.5f);
            Campaign.End();
            Time.timeScale = 1;
            SceneManager.LoadScene("Arena");
        }

        void FadeTo(float a, float speed)
        {
            target = a;
            fadeSpeed = speed;
        }

        void Update()
        {
            if (download.IsValid() && download.IsDone)
            {
                downloaded = download.Status == AsyncOperationStatus.Succeeded;
                // In the browser's cache now: no need to keep them in memory.
                if (!Busy)
                {
                    Addressables.Release(download);
                    download = default;
                }
            }
            alpha = Mathf.MoveTowards(alpha, target, Time.unscaledDeltaTime * fadeSpeed);
            black.color = new Color(0, 0, 0, alpha);
            // Clicks don't reach the menus while it's dark or a load is under way.
            black.enabled = alpha > 0.001f || Busy;
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
            gameObject.AddComponent<GraphicRaycaster>();

            black = new GameObject("Black", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            var rt = black.rectTransform;
            rt.SetParent(transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            black.color = Color.clear;
            black.raycastTarget = true;

            status = Label(transform, "Status", Vector2.zero, new Vector2(900, 40), 16, GameConfig.TextDim);

            failPanel = new GameObject("Failed", typeof(RectTransform));
            failPanel.transform.SetParent(transform, false);
            Button(failPanel.transform, "RETRY", new Vector2(-95, -50), true, Retry);
            Button(failPanel.transform, "MAIN MENU", new Vector2(95, -50), false, GiveUp);
            failPanel.SetActive(false);
        }

        static Text Label(Transform parent, string name, Vector2 pos, Vector2 size, int fontSize, Color color)
        {
            var t = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            var r = t.rectTransform;
            r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = pos;
            r.sizeDelta = size;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = fontSize;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = color;
            t.raycastTarget = false;
            t.text = "";
            return t;
        }

        static void Button(Transform parent, string text, Vector2 pos, bool primary, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(text, typeof(RectTransform), typeof(Image), typeof(Button));
            var r = (RectTransform)go.transform;
            r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = pos;
            r.sizeDelta = new Vector2(170, 46);
            var img = go.GetComponent<Image>();
            img.color = primary ? GameConfig.Accent : GameConfig.Panel;
            go.GetComponent<Button>().onClick.AddListener(onClick);
            Label(r, "Label", Vector2.zero, r.sizeDelta, 15, primary ? GameConfig.Hex(0x05101a) : GameConfig.TextBright).text = text;
        }
    }
}
