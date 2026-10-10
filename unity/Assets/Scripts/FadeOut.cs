using UnityEngine;

namespace WardenZero
{
    // Fades a sprite out over `seconds`, then removes it (strike scorch marks).
    public class FadeOut : MonoBehaviour
    {
        public SpriteRenderer sprite;
        public float seconds = 5;

        float t;
        float startAlpha;

        void Start()
        {
            startAlpha = sprite.color.a;
        }

        void Update()
        {
            t += Time.deltaTime;
            var c = sprite.color;
            sprite.color = new Color(c.r, c.g, c.b, startAlpha * Mathf.Clamp01(1 - t / seconds));
            if (t >= seconds) Destroy(gameObject);
        }
    }
}
