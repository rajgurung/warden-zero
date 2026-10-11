using System.Collections.Generic;
using UnityEngine;

namespace WardenZero
{
    // Where the touch buttons go, in canvas units (the HUD canvas scales a 1280 x 720 reference
    // by width and height equally), measured from the canvas's bottom-left corner. Pure maths,
    // so EditMode tests check every screen shape.
    // Landscape: JUMP (the context button) big in the bottom-right corner, FIRE big to its left,
    // DROP small above JUMP, DASH and BOMB small in an arc above FIRE and to its left; VIEW and
    // SKIP top right. Portrait: everything at 0.85x in a column up the right edge, DROP left of
    // JUMP. Both keep clear of the screen's safe-area insets.
    public static class TouchLayout
    {
        public const float Base = 74; // a round button's size at scale 1
        public const float Big = 1.35f, Small = 0.8f, PortraitScale = 0.85f;
        public const float Margin = 18, Gap = 14;
        public const float Bottom = 44; // clear of the XP bar along the bottom edge

        public enum Button { Jump, Fire, Drop, Dash, Bomb, View, Skip }

        public struct Placed
        {
            public Rect Rect;
            public float Scale;
        }

        // The canvas size for a screen in pixels (CanvasScaler: 1280 x 720, match 0.5).
        public static Vector2 CanvasSize(float width, float height)
        {
            float k = Mathf.Sqrt(width / 1280f * (height / 720f));
            return new Vector2(width / k, height / k);
        }

        // insets: left, bottom, right, top, in canvas units.
        public static Dictionary<Button, Placed> Compute(Vector2 canvas, Vector4 insets)
        {
            var d = new Dictionary<Button, Placed>();
            bool portrait = canvas.y > canvas.x;
            float k = portrait ? PortraitScale : 1;
            float right = canvas.x - Margin - insets.z, bottom = Bottom + insets.y, top = canvas.y - Margin - insets.w;
            float big = Base * Big * k, small = Base * Small * k, mid = Base * 0.8f;
            void Put(Button b, float x, float y, float size) => d[b] = new Placed { Rect = new Rect(x, y, size, size), Scale = size / Base };

            if (!portrait)
            {
                Put(Button.Jump, right - big, bottom, big);
                Put(Button.Fire, right - big * 2 - Gap, bottom, big);
                Put(Button.Drop, right - big / 2 - small / 2, bottom + big + Gap, small);
                Put(Button.Dash, right - big * 1.5f - Gap - small / 2, bottom + big + Gap, small);
                Put(Button.Bomb, right - big * 2 - Gap * 2 - small, bottom + big * 0.55f, small);
            }
            else
            {
                Put(Button.Jump, right - big, bottom, big);
                Put(Button.Drop, right - big - Gap - small, bottom + (big - small) / 2, small);
                Put(Button.Fire, right - big, bottom + big + Gap, big);
                Put(Button.Dash, right - big / 2 - small / 2, bottom + big * 2 + Gap * 2, small);
                Put(Button.Bomb, right - big / 2 - small / 2, bottom + big * 2 + small + Gap * 3, small);
            }
            Put(Button.View, right - mid, top - mid - 78, mid);
            Put(Button.Skip, right - Base, top - Base - 78, Base);
            return d;
        }

        // Where a touch moves the stick (else, off the buttons, it looks): the left half in
        // landscape, the lower-left 60% (of width and height) in portrait.
        public static bool StickZone(Vector2 screenPos, float width, float height)
        {
            if (height > width) return screenPos.x < width * 0.6f && screenPos.y < height * 0.6f;
            return screenPos.x < width * 0.5f;
        }
    }
}
