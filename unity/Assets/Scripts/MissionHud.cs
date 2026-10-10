using UnityEngine;
using UnityEngine.UI;

namespace WardenZero
{
    // Greenfang's extra HUD (e3bf116:src/ui/JungleHud.ts): objective callout, beacon capture
    // bar, the two strike boxes (armed highlight, cooldown shade, air charges) and a
    // waypoint arrow at the screen edge when the objective is off screen.
    public class MissionHud : MonoBehaviour
    {
        [System.Serializable]
        public class StrikeBox
        {
            public RectTransform root;
            public Image border;
            public Text status;
            public RectTransform shade;
            public CanvasGroup group;
        }

        public Text objective;
        public GameObject capture;
        public RectTransform captureFill;
        public StrikeBox[] boxes; // artillery, air
        public RectTransform arrow;
        public RectTransform canvasRect;

        public void SetObjective(string text) => objective.text = text;

        // null hides the bar.
        public void SetCapture(float? progress)
        {
            capture.SetActive(progress.HasValue);
            if (progress.HasValue) captureFill.anchorMax = new Vector2(Mathf.Clamp01(progress.Value), 1);
        }

        public void SetStrikes(StrikeSystem s)
        {
            for (int i = 0; i < boxes.Length; i++)
            {
                var type = (StrikeType)i;
                var b = boxes[i];
                bool armed = s.Armed == type;
                float progress = s.Cooldown(type);
                bool reloading = progress < 1;
                bool noCharges = type == StrikeType.Air && s.AirCharges <= 0;
                string text = noCharges ? "NO CHARGES" : reloading ? "RELOADING" : armed ? "ARMED" : "READY";
                if (type == StrikeType.Air) text += $"  ×{s.AirCharges}";
                b.status.text = text;
                b.status.color = noCharges || reloading ? GameConfig.TextDim : armed ? GameConfig.Gold : GameConfig.TextBright;
                b.root.localScale = Vector3.one * (armed ? 1.04f : 1);
                b.group.alpha = armed ? 1 : 0.7f;
                var c = StrikeSystem.Colors[i];
                b.border.color = new Color(c.r, c.g, c.b, armed ? 1 : 0.6f);
                // Dark shade over the part still reloading (all of it when out of charges).
                float fill = noCharges ? 0 : progress;
                b.shade.gameObject.SetActive(noCharges || reloading);
                b.shade.anchorMax = new Vector2(1, 1 - fill);
            }
        }

        // Point an arrow at the objective from the screen edge when it's off screen.
        public void PointAt(Vector3? target, Camera cam)
        {
            if (!target.HasValue)
            {
                arrow.gameObject.SetActive(false);
                return;
            }
            Vector3 sp = cam.WorldToScreenPoint(target.Value);
            float margin = 46 * canvasRect.lossyScale.x;
            bool onScreen = sp.z > 0 && sp.x > margin && sp.x < Screen.width - margin && sp.y > margin && sp.y < Screen.height - margin;
            arrow.gameObject.SetActive(!onScreen);
            if (onScreen) return;
            Vector2 centre = new Vector2(Screen.width, Screen.height) / 2;
            Vector2 d = (Vector2)sp - centre;
            if (sp.z < 0) d = -d;
            float scale = Mathf.Min((Screen.width / 2f - margin) / Mathf.Max(1e-3f, Mathf.Abs(d.x)), (Screen.height / 2f - margin) / Mathf.Max(1e-3f, Mathf.Abs(d.y)));
            Vector2 edge = centre + d * scale;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, edge, null, out var local);
            arrow.anchoredPosition = local;
            arrow.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg - 90);
        }
    }
}
