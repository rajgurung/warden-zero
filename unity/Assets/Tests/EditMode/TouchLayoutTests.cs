using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace WardenZero.Tests
{
    // The touch layout on phone and desktop shapes: every button on screen, clear of the
    // safe-area insets, and none on top of another.
    public class TouchLayoutTests
    {
        static readonly (string name, float w, float h)[] Screens =
        {
            ("16:9", 1920, 1080), ("19.5:9", 2532, 1170), ("9:19.5", 1170, 2532),
        };

        [Test]
        public void EveryButton_OnScreen_NoneOverlapping()
        {
            foreach (var (name, w, h) in Screens)
                foreach (var notch in new[] { 0f, 47f })
                {
                    Vector2 canvas = TouchLayout.CanvasSize(w, h);
                    bool portrait = h > w;
                    // A notch on the long side, a home bar at the bottom.
                    var insets = portrait ? new Vector4(0, notch * 0.7f, 0, notch) : new Vector4(notch, notch * 0.4f, notch, 0);
                    var places = TouchLayout.Compute(canvas, insets);
                    var screen = new Rect(insets.x, insets.y, canvas.x - insets.x - insets.z, canvas.y - insets.y - insets.w);
                    foreach (var p in places)
                    {
                        var r = p.Value.Rect;
                        Assert.IsTrue(r.xMin >= screen.xMin && r.yMin >= screen.yMin && r.xMax <= screen.xMax && r.yMax <= screen.yMax,
                            $"{name} notch {notch}: {p.Key} {r} outside {screen}");
                    }
                    var list = places.ToList();
                    for (int i = 0; i < list.Count; i++)
                        for (int k = i + 1; k < list.Count; k++)
                        {
                            var a = list[i];
                            var b = list[k];
                            // VIEW and SKIP share a corner: SKIP only in the flight, VIEW only in play.
                            if (a.Key == TouchLayout.Button.View && b.Key == TouchLayout.Button.Skip) continue;
                            Assert.IsFalse(a.Value.Rect.Overlaps(b.Value.Rect), $"{name}: {a.Key} overlaps {b.Key}");
                        }
                    Assert.AreEqual(portrait ? TouchLayout.PortraitScale * TouchLayout.Big : TouchLayout.Big, places[TouchLayout.Button.Jump].Scale, 1e-4f, "JUMP big");
                }
        }

        [Test]
        public void StickZone_LeftHalf_OrLowerLeftInPortrait()
        {
            Assert.IsTrue(TouchLayout.StickZone(new Vector2(400, 900), 1920, 1080));
            Assert.IsFalse(TouchLayout.StickZone(new Vector2(1100, 300), 1920, 1080));
            Assert.IsTrue(TouchLayout.StickZone(new Vector2(600, 800), 1170, 2532));
            Assert.IsFalse(TouchLayout.StickZone(new Vector2(600, 2000), 1170, 2532), "the top: look");
            Assert.IsFalse(TouchLayout.StickZone(new Vector2(1000, 800), 1170, 2532), "the right: look");
        }
    }
}
