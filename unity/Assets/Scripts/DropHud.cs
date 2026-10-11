using UnityEngine;
using UnityEngine.UI;

namespace WardenZero
{
    // The jump's HUD: altitude (big), fall speed, an altimeter strip with the deploy bands
    // (green above the prompt, amber to the safe pull, red below), the distance to the LZ
    // and a prompt line for the next action.
    public class DropHud : MonoBehaviour
    {
        public GameObject root;
        public Text altitude;
        public Text speed;
        public Text lz;
        public Text prompt;
        public RectTransform strip; // the altimeter bar; the marker moves along its height
        public RectTransform marker;
        public RectTransform lzIcon; // a ring over the landing zone, on screen
        public RectTransform canvasRect;

        int shownAlt = -1, shownSpeed = -1, shownLz = -1;
        string shownPrompt;

        public void Show(bool on)
        {
            root.SetActive(on);
            shownAlt = shownSpeed = shownLz = -1;
            shownPrompt = null;
        }

        public void Set(float alt, float top, float fallSpeed, float lzDistance)
        {
            int a = Mathf.Max(0, Mathf.RoundToInt(alt));
            if (a != shownAlt)
            {
                shownAlt = a;
                var c = alt > Skydive.DeployPrompt ? "#e6ecff" : alt > Skydive.SafeDeploy ? "#ffd75a" : "#ff4d5e";
                altitude.text = $"<color={c}>{a}</color><size=16><color=#8a96b8> m</color></size>";
            }
            int s = Mathf.RoundToInt(fallSpeed);
            if (s != shownSpeed)
            {
                shownSpeed = s;
                speed.text = $"FALL {s} m/s";
            }
            int d = Mathf.RoundToInt(lzDistance);
            if (d != shownLz)
            {
                shownLz = d;
                lz.text = $"LZ {d} m";
            }
            float k = Mathf.Clamp01(alt / top);
            marker.anchoredPosition = new Vector2(marker.anchoredPosition.x, strip.rect.height * (k - 1));
        }

        // Keep the LZ ring over the landing zone (hidden when it is behind the camera).
        public void Track(Vector3 world, Camera cam)
        {
            Vector3 sp = cam.WorldToScreenPoint(world);
            lzIcon.gameObject.SetActive(sp.z > 0);
            if (sp.z <= 0) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, sp, null, out var local);
            lzIcon.anchoredPosition = local;
        }

        public void Prompt(string text, bool flash = false)
        {
            if (text != shownPrompt)
            {
                shownPrompt = text;
                prompt.text = text;
            }
            prompt.color = new Color(1, 1, 1, flash ? 0.55f + 0.45f * Mathf.Sin(Time.time * 9) : 1);
        }
    }
}
