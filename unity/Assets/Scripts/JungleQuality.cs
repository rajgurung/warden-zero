using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace WardenZero
{
    // The jungle's share of the quality tiers: ground cover density and reach, tree draw
    // distance, terrain detail, and the heavier post effects. Re-applied when the tier changes.
    public class JungleQuality : MonoBehaviour
    {
        public Terrain patch;
        public Terrain landscape;
        public Volume volume;
        public Camera cam;

        void OnEnable()
        {
            Quality.Changed += Apply;
            Apply();
        }

        void OnDisable()
        {
            Quality.Changed -= Apply;
        }

        public void Apply()
        {
            bool high = Quality.Current == QualityTier.High;
            patch.detailObjectDensity = high ? 1 : 0.4f;
            patch.detailObjectDistance = high ? 70 : 35;
            patch.treeDistance = high ? 400 : 180;
            patch.heightmapPixelError = high ? 4 : 10;
            patch.basemapDistance = high ? 120 : 50;
            landscape.treeDistance = high ? 600 : 250;
            landscape.heightmapPixelError = high ? 6 : 14;
            // volume.profile is this scene's own copy, so the asset is left alone.
            if (volume.profile.TryGet<Bloom>(out var bloom)) bloom.active = high;
            cam.farClipPlane = high ? 4500 : 3000;
        }
    }
}
