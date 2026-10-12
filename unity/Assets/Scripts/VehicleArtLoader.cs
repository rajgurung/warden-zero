using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace WardenZero
{
    // Brings in M3's Tripo art (the VehicleArt prefab, Addressables group Vehicles) once the
    // jungle is in and he is on the ground, and dresses the jeep, the ride's targets and cover
    // and the outpost with it. The jungle scene never references the art. Until it arrives those
    // stay hidden (their greybox is only the fallback if the load fails, so a failed or slow load
    // never holds anything up). The handle is let go with the scene.
    public class VehicleArtLoader : MonoBehaviour
    {
        public const string Address = "VehicleArt";
        // Test hook: the address to load (a bad one makes it fail).
        public static string LoadAddress = Address;

        public VehicleRig rig;
        public RoadStage road;
        public Transform[] covers; // the ride's cover, named by kind (Nest, Tower, Roadblock)
        public Transform[] outpost; // hesco walls at B

        public bool Loading { get; private set; }
        public bool Dressed { get; private set; } // the art is on
        public bool Failed { get; private set; } // the greybox is

        AsyncOperationHandle<GameObject> handle;

        void Awake()
        {
            // Nothing shows until the art (or the fallback) does.
            ShowGreybox(false);
        }

        public void Load()
        {
            if (Loading || Dressed || Failed) return;
            Loading = true;
            handle = Addressables.LoadAssetAsync<GameObject>(LoadAddress);
            handle.Completed += Loaded;
        }

        void Loaded(AsyncOperationHandle<GameObject> h)
        {
            Loading = false;
            if (this == null) return;
            if (h.Status != AsyncOperationStatus.Succeeded || h.Result == null)
            {
                Debug.LogWarning("[WZ] the vehicle art did not load: greybox instead");
                Failed = true;
                ShowGreybox(true);
                return;
            }
            Dress(h.Result.transform);
            Dressed = true;
        }

        // Copies of the art's parts under their pivots, each keeping its holder's place.
        void Dress(Transform art)
        {
            Transform Part(string name) => art.Find(name);
            void Put(string name, Transform pivot, bool turned = false)
            {
                var part = Part(name);
                if (part == null || pivot == null) return;
                var copy = Instantiate(part.gameObject, pivot, false).transform;
                // The right-hand wheels face out the other way.
                if (turned) copy.localRotation = Quaternion.Euler(0, 180, 0) * copy.localRotation;
            }
            Put("Body", rig.body);
            Put("Riser", rig.body);
            for (int i = 0; i < rig.wheels.Length; i++) Put("Wheel", rig.wheels[i], i % 2 == 1);
            Put("Steering", rig.steering);
            Put("Yoke", rig.gunYoke);
            Put("Gun", rig.gun);
            foreach (var t in road.targets)
            {
                string name = t.kind == RoadLayout.TargetKind.Drum ? "Drum" : t.kind == RoadLayout.TargetKind.Plate ? "Plate" : t.kind == RoadLayout.TargetKind.Trailer ? "Trailer" : null;
                if (name != null) Put(name, t.transform);
            }
            foreach (var c in covers) Put(c.name, c);
            foreach (var h in outpost) Put("Hesco", h);
        }

        // The greybox under every dressed pivot (each part's "Greybox" child).
        void ShowGreybox(bool on)
        {
            foreach (var g in Greyboxes())
                if (g != null) g.SetActive(on);
        }

        System.Collections.Generic.IEnumerable<GameObject> Greyboxes()
        {
            foreach (var t in new[] { rig.body, rig.steering, rig.gunYoke, rig.gun })
                if (t.Find("Greybox") != null) yield return t.Find("Greybox").gameObject;
            foreach (var w in rig.wheels)
                if (w.Find("Greybox") != null) yield return w.Find("Greybox").gameObject;
            foreach (var t in road.targets)
                if (t.transform.Find("Greybox") != null) yield return t.transform.Find("Greybox").gameObject;
            foreach (var c in covers)
                if (c.Find("Greybox") != null) yield return c.Find("Greybox").gameObject;
            foreach (var h in outpost)
                if (h.Find("Greybox") != null) yield return h.Find("Greybox").gameObject;
        }

        void OnDestroy()
        {
            if (handle.IsValid()) Addressables.Release(handle);
        }
    }
}
