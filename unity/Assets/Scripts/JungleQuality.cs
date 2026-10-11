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
            // Ground cover: a third as dense and half the reach on phones.
            patch.detailObjectDensity = high ? 1 : 0.3f;
            patch.detailObjectDistance = high ? 70 : 30;
            // Trees are LOD groups (full mesh, then a two-card impostor): phones switch to the
            // impostor at half the distance and stop drawing trees sooner.
            patch.treeDistance = high ? 400 : 120;
            patch.treeLODBiasMultiplier = high ? 1 : 0.5f;
            patch.treeBillboardDistance = high ? 120 : 60;
            patch.heightmapPixelError = high ? 4 : 10;
            patch.basemapDistance = high ? 120 : 50;
            landscape.treeDistance = high ? 600 : 220;
            landscape.treeLODBiasMultiplier = high ? 1 : 0.5f;
            landscape.heightmapPixelError = high ? 6 : 14;
            // volume.profile is this scene's own copy, so the asset is left alone.
            if (volume.profile.TryGet<Bloom>(out var bloom)) bloom.active = high;
            cam.farClipPlane = high ? 4500 : 3000;
        }
    }
}
