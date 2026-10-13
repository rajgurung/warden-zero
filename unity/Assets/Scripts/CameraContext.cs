using UnityEngine;

namespace WardenZero
{
    // Picks the jungle's gameplay view from what is going on (plain C#, ticked by CameraFollow
    // during play only; set pieces disable CameraFollow and keep their own shots):
    // - Combat: a live enemy within CombatEnter, or he is in a combat zone. It lasts until
    //   CombatHold seconds pass with no live enemy within CombatExit and outside every zone
    //   (hysteresis). High view.
    // - Platforming: on a move, or an edge near (CameraFollow frames for it). Behind view.
    // - Explore: otherwise. Behind view.
    // Automatic switches wait CombatDwell seconds after the last one and never happen while he
    // is off the ground or on a block. V pins the other view for the current context; the pin
    // clears when the context changes between combat and the rest (platforming and exploring
    // count as one, so a pinned view isn't undone at the next obstacle). With Auto off it never
    // switches by itself (the M1 behaviour: V only).
    // Seated in the jeep (M3) the context is Driving or Gunner: getting in or out is a context
    // change (any pin clears), nothing switches by itself while seated, and getting out goes
    // back to the behind view (Auto).
    public class CameraContext
    {
        public enum Context { Combat, Platforming, Explore, Driving, Gunner }

        public const float CombatEnter = 18, CombatExit = 25, CombatHold = 4, Dwell = 2;

        // One frame's facts.
        public struct Inputs
        {
            public float NearestEnemy; // metres to the nearest live enemy (infinity: none)
            public bool InZone; // inside a combat zone
            public bool Settled; // on the ground and not on a block (a switch may happen)
            public bool Platforming;
            public VehicleController.Seat Seat; // in the jeep, and where
        }

        public bool Auto = true;
        public Context Current { get; private set; } = Context.Explore;
        public CameraFollow.View View { get; private set; }
        public bool Pinned { get; private set; }

        bool combat;
        float calmSince = -1;
        float lastSwitch = float.NegativeInfinity;
        bool pinnedCombat;
        VehicleController.Seat seat;

        public CameraContext(CameraFollow.View start)
        {
            View = start;
        }

        public static CameraFollow.View ViewFor(Context c) => c == Context.Combat ? CameraFollow.View.High : CameraFollow.View.Behind;

        // Updates the context; returns the view to switch to now, or null.
        public CameraFollow.View? Tick(Inputs i, float time)
        {
            if (i.Seat != seat)
            {
                bool gotOut = i.Seat == VehicleController.Seat.None;
                seat = i.Seat;
                Pinned = false;
                combat = false;
                calmSince = -1;
                if (gotOut)
                {
                    Current = Context.Explore;
                    if (Auto && View != CameraFollow.View.Behind)
                    {
                        View = CameraFollow.View.Behind;
                        lastSwitch = time;
                        return View;
                    }
                }
            }
            if (seat != VehicleController.Seat.None)
            {
                Current = seat == VehicleController.Seat.Gunner ? Context.Gunner : Context.Driving;
                return null;
            }
            bool enter = i.NearestEnemy <= CombatEnter || i.InZone;
            bool calm = i.NearestEnemy > CombatExit && !i.InZone;
            if (enter) combat = true;
            if (combat)
            {
                if (!calm) calmSince = -1;
                else if (calmSince < 0) calmSince = time;
                if (calm && time - calmSince >= CombatHold)
                {
                    combat = false;
                    calmSince = -1;
                }
            }
            Current = combat ? Context.Combat : i.Platforming ? Context.Platforming : Context.Explore;
            // A pin lasts until the context crosses between combat and the rest.
            if (Pinned && pinnedCombat != combat) Pinned = false;
            if (!Auto || Pinned || !i.Settled || time - lastSwitch < Dwell) return null;
            var want = ViewFor(Current);
            if (want == View) return null;
            View = want;
            lastSwitch = time;
            return want;
        }

        // V / VIEW: the other view, pinned for this context. Returns it.
        public CameraFollow.View Override()
        {
            View = View == CameraFollow.View.High ? CameraFollow.View.Behind : CameraFollow.View.High;
            Pinned = Auto;
            pinnedCombat = combat;
            return View;
        }

        // A new start (a run, a set piece's end): the view this context wants, unpinned.
        public void Reset(CameraFollow.View view)
        {
            View = view;
            Pinned = false;
            combat = false;
            calmSince = -1;
            lastSwitch = float.NegativeInfinity;
            Current = Context.Explore;
            seat = VehicleController.Seat.None;
        }
    }
}
