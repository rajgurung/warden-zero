using UnityEngine;

namespace WardenZero
{
    // Draws the Warden as the rigged Tripo model (Humanoid). The body turns to the aim with
    // Babylon-style easing (a fraction of the remaining angle each frame, so fast but never a
    // snap) through a full 360 degrees. Legs play idle/walk/run by speed; an upper-body
    // layer (Avatar Mask) leans him into the shouldered rifle with the `fire` clip while he
    // fires, and plays the hit flinch; at rest it fades out, so he stands upright in the
    // relaxed idle (breathing, weight shifts), rifle at a low ready, and now and then looks
    // around. Set pieces play the jump's Canopy and Land on the base layer (Play).
    // Death plays defeat_03. The rifle hangs off the body frame along his facing and both
    // hands hold it through IK (WardenHandIK), so the gun turns with him and stays level.
    // He wears the HALO gear (pack, jump helmet, altimeter) from boarding until he unclips it
    // after landing; the pack stays on the ground.
    // Jungle traversal: each traversal state asks for a base-layer state (TraversalAnim);
    // until the art pass adds that state he stays on Locomotion and TraversalPose poses him.
    // While he vaults, hangs or climbs the rifle is slung across his back (Sling) and the
    // hands let go of it; after, they take it back over a moment.
    [DefaultExecutionOrder(50)]
    public class WardenModelView : WardenView
    {
        public const int UpperLayer = 1;
        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int AnimSpeedId = Animator.StringToHash("AnimSpeed");
        static readonly int UpperSpeedId = Animator.StringToHash("UpperSpeed");
        static readonly int AirSpeedId = Animator.StringToHash("AirSpeed");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        public const float LookAroundMin = 20, LookAroundMax = 40; // seconds standing still
        const float LookAroundFor = 8;

        public Transform yaw; // turns to the aim; the model sits under it
        public Animator animator;
        public Transform rightHand;
        public Transform rifle; // child of the model frame at chest height
        public Transform muzzle; // child of the rifle, at the barrel tip
        public WardenHandIK handIK;
        public Renderer[] renderers;
        public Transform[] gear; // holders on his bones: HALO pack, jump helmet, altimeter
        // Babylon eased with dr * min(1, dt * 20); 24 is a touch snappier (about 5 degrees left
        // 0.15 s after a 180-degree flip, against 9 at 20).
        public float turnRate = 24;
        // The run has no strafe clips: moving across his aim, his hips and legs turn up to this
        // far toward the run (away from it when backpedalling), less with the rifle shouldered.
        public const float MaxTwist = 50;
        public const float MaxTwistShouldered = 30;

        public float Yaw { get; private set; }
        public bool Dead { get; private set; }
        public float Twist { get; private set; } // degrees the model turns from the aim toward the run
        public string Action { get; private set; } // a set piece's base-layer state, or null
        public bool LookingAround { get; private set; }
        public float NextLookAround => nextLook; // seconds of standing still before the next
        public bool GearOn { get; private set; }
        public string TraversalAnim { get; private set; } // the wanted base-layer state, null on the ground
        public string BaseState { get; private set; } = "Locomotion"; // what the traversal last played
        public bool Slung { get; private set; }
        public float SlingAmount => sling; // 0 in his hands .. 1 on his back
        public Transform DroppedPack { get; private set; }

        float targetYaw;
        float hitUntil = -1;
        float upperWeight;
        Transform rifleHome;
        Vector3 rifleHomePos;
        Quaternion rifleHomeRot;
        MaterialPropertyBlock block;
        Material[][] allMaterials; // per renderer, with the x-ray pass
        Material[][] noXRay; // per renderer, without it
        Renderer[][] gearRenderers;
        float stillTime, nextLook, lookUntil;
        float sling;
        Transform chest;

        // Queue for the Warden's own materials: after the enemy sprites (3000), so a crowd never
        // covers him, and after his x-ray pass (3008), so that only shows through scenery.
        // Set at runtime because URP resets a Lit material's queue from its surface type.
        public const int BodyQueue = 3010;

        void Awake()
        {
            block = new MaterialPropertyBlock();
            foreach (var r in renderers)
            {
                var mats = r.materials; // instances, so the shared assets stay untouched
                mats[0].renderQueue = BodyQueue;
                r.materials = mats;
            }
            allMaterials = new Material[renderers.Length][];
            noXRay = new Material[renderers.Length][];
            for (int i = 0; i < renderers.Length; i++)
            {
                allMaterials[i] = renderers[i].materials;
                noXRay[i] = System.Array.FindAll(allMaterials[i], m => !m.shader.name.Contains("XRay"));
            }
            gearRenderers = System.Array.ConvertAll(gear ?? new Transform[0], g => g.GetComponentsInChildren<Renderer>(true));
            nextLook = Random.Range(LookAroundMin, LookAroundMax);
            chest = animator.GetBoneTransform(HumanBodyBones.UpperChest) ?? animator.GetBoneTransform(HumanBodyBones.Chest);
            rifleHome = rifle.parent;
            rifleHomePos = rifle.localPosition;
            rifleHomeRot = rifle.localRotation;
        }

        public override void UpdateFacing(Vector3 aim)
        {
            if (aim.sqrMagnitude > 1e-4f) targetYaw = Mathf.Atan2(aim.x, aim.z) * Mathf.Rad2Deg;
        }

        public override Vector3 RifleTip() => muzzle.position;

        // The see-through silhouette helps in a crowd; set pieces turn it off (in the chopper's
        // cabin he would show through the hull).
        public void SetXRay(bool on)
        {
            if (allMaterials == null || XRay == on) return;
            XRay = on;
            for (int i = 0; i < renderers.Length; i++) renderers[i].materials = on ? allMaterials[i] : noXRay[i];
        }

        public bool XRay { get; private set; } = true;

        // Face a heading at once (after a set piece), without the eased turn.
        public void SnapYaw(float degrees)
        {
            Yaw = targetYaw = Mathf.Repeat(degrees, 360);
            yaw.localRotation = Quaternion.Euler(0, Yaw, 0);
        }

        public override void Show(WardenPose p)
        {
            float dt = Time.deltaTime;
            // Hurt: flash red rather than blinking out, so he stays readable in a crowd.
            float tint = Mathf.Max(p.HurtTint, p.Hidden ? 0.6f : 0);
            block.SetColor(BaseColorId, Color.Lerp(Color.white, new Color(1, 0.35f, 0.35f), tint));
            // Index 0 only: a whole-renderer block would also override the x-ray and rim colours.
            foreach (var r in renderers) r.SetPropertyBlock(block, 0);
            for (int i = 0; i < gearRenderers.Length; i++)
                if (gear[i].gameObject.activeSelf && gear[i] != DroppedPack)
                    foreach (var r in gearRenderers[i]) r.SetPropertyBlock(block, 0);

            if (p.Dead)
            {
                if (!Dead)
                {
                    Dead = true;
                    animator.CrossFadeInFixedTime("Death", 0.15f, 0);
                    animator.SetLayerWeight(UpperLayer, 0);
                    // Let go: the rifle falls with his right hand.
                    handIK.weight = 0;
                    rifle.SetParent(rightHand, true);
                }
                return;
            }
            if (Dead)
            {
                // A new run after death.
                Dead = false;
                handIK.weight = 1;
                rifle.SetParent(rifleHome, false);
                rifle.localPosition = rifleHomePos;
                rifle.localRotation = rifleHomeRot;
                animator.Play("Locomotion", 0, 0);
                animator.Play("Aim", UpperLayer, 0);
                BaseState = "Locomotion";
                TraversalAnim = null;
                Slung = false;
                sling = 0;
                upperWeight = 0;
                Action = null;
                LookingAround = false;
            }
            // Land hands back to the idle by itself.
            if (Action == "Land" && animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion") && !animator.IsInTransition(0))
            {
                Action = null;
                BaseState = "Locomotion";
            }
            IdleVariant(p, dt);

            Traverse(p);
            UpdateFacing(p.Facing.sqrMagnitude > 1e-4f ? p.Facing : p.Aim);
            Yaw += Mathf.DeltaAngle(Yaw, targetYaw) * Mathf.Min(1, dt * turnRate);
            Yaw = Mathf.Repeat(Yaw, 360);
            yaw.localRotation = Quaternion.Euler(0, Yaw, 0);

            // Legs: run when moving (there is no strafe set, so the run plays backwards
            // when he backpedals against his aim); faster during a dash.
            bool moving = p.Moving || p.Dashing;
            animator.SetFloat(SpeedId, moving ? 1 : 0, 0.08f, dt);
            bool backpedal = p.Moving && Vector3.Dot(p.MoveDir, p.Aim) < -0.25f;
            animator.SetFloat(AnimSpeedId, p.Dashing ? 1.8f : backpedal ? -1 : 1);
            // Legs toward the run. The rifle hangs off the aim frame (yaw), so it stays on the
            // aim and the hands follow it by IK.
            float twist = 0;
            if (p.Moving && !p.Dashing && p.MoveDir.sqrMagnitude > 0.01f)
            {
                float limit = Mathf.Lerp(MaxTwist, MaxTwistShouldered, handIK.Shouldered);
                Vector3 legs = backpedal ? -p.MoveDir : p.MoveDir;
                twist = Mathf.Clamp(Vector3.SignedAngle(new Vector3(p.Aim.x, 0, p.Aim.z), new Vector3(legs.x, 0, legs.z), Vector3.up), -limit, limit);
            }
            Twist += (twist - Twist) * Mathf.Min(1, dt * 10);
            animator.transform.localRotation = Quaternion.Euler(0, Twist, 0);
            // Upper body: the fire clip is held on its first frame, a forward-leaning combat
            // crouch; it is weighted in with the shouldering (and for a flinch) only. At full
            // weight while resting it hunched him over like he was kneeling. The stance itself
            // (shouldering, recoil, both hands) is procedural in WardenHandIK.
            animator.SetFloat(UpperSpeedId, 0);
            handIK.WantShouldered = p.Shooting;
            handIK.AimPitch = p.AimPitch;
            if (hitUntil >= 0 && Time.time >= hitUntil)
            {
                hitUntil = -1;
                animator.CrossFadeInFixedTime("Aim", 0.2f, UpperLayer);
            }
            float upper = hitUntil >= 0 ? 1 : Mathf.SmoothStep(0, 1, handIK.Shouldered);
            upperWeight = Mathf.MoveTowards(upperWeight, upper, dt / 0.15f);
            animator.SetLayerWeight(UpperLayer, upperWeight);
        }

        // The base-layer state for a traversal state (the art pass's names; see Warden3DImport).
        public static string AnimFor(TraversalState state, float verticalSpeed)
        {
            switch (state)
            {
                case TraversalState.Air: return verticalSpeed > 0 ? "JumpUp" : "JumpAir";
                case TraversalState.Vault: return "Vault";
                case TraversalState.Mantle:
                case TraversalState.ClimbUp: return "ClimbUp";
                case TraversalState.Hang:
                case TraversalState.Lower: return "Hang";
                case TraversalState.WallClimb: return "WallClimb";
                default: return null;
            }
        }

        bool Attached(TraversalState s) => s != TraversalState.Grounded && s != TraversalState.Air;

        void Traverse(WardenPose p)
        {
            Sling(Attached(p.State));
            if (Action != null) return;
            string want = AnimFor(p.State, p.VerticalSpeed);
            if (want == TraversalAnim) return;
            TraversalAnim = want;
            string play = want != null && animator.HasState(0, Animator.StringToHash(want)) ? want : "Locomotion";
            if (play == BaseState) return;
            BaseState = play;
            animator.CrossFadeInFixedTime(play, 0.15f, 0);
        }

        // Rifle onto his back (hands off it fast) or back into his hands (over a moment).
        public void Sling(bool on)
        {
            if (on == Slung) return;
            Slung = on;
            handIK.Grip(!on, on ? 0.12f : 0.35f);
        }

        // The slung rifle rides diagonally across his back, muzzle over his left shoulder,
        // blending from and back to wherever the hands' IK put it.
        void LateUpdate()
        {
            sling = Mathf.MoveTowards(sling, Slung ? 1 : 0, Time.deltaTime / 0.25f);
            if (sling <= 0 || Dead || chest == null) return;
            var m = animator.transform;
            Vector3 U = m.up, F = m.forward, R = m.right;
            Vector3 barrel = (U * 0.8f - R * 0.6f).normalized;
            Vector3 middle = chest.position - F * 0.36f + U * 0.02f;
            Vector3 pos = middle - barrel * 0.25f;
            Quaternion rot = Quaternion.LookRotation(barrel, -F);
            float k = Mathf.SmoothStep(0, 1, sling);
            rifle.SetPositionAndRotation(Vector3.Lerp(rifle.position, pos, k), Quaternion.Slerp(rifle.rotation, rot, k));
        }

        // A set piece's pose on the base layer: "Canopy", "Land"; null for Locomotion.
        public void Play(string state, float fade = 0.25f, float speed = 1)
        {
            Action = state;
            BaseState = state ?? "Locomotion";
            TraversalAnim = null;
            LookingAround = false;
            stillTime = 0;
            animator.SetFloat(AirSpeedId, speed);
            animator.CrossFadeInFixedTime(state ?? "Locomotion", fade, 0);
        }

        // Standing still with the rifle held, every 20-40 s he looks around for a few seconds;
        // moving, firing or a hit brings him straight back.
        void IdleVariant(WardenPose p, float dt)
        {
            bool resting = !p.Moving && !p.Dashing && !p.Shooting && hitUntil < 0 && Action == null && handIK.weight > 0 && p.State == TraversalState.Grounded;
            if (LookingAround)
            {
                if (resting && Time.time < lookUntil) return;
                LookingAround = false;
                animator.CrossFadeInFixedTime("Locomotion", 0.5f, 0);
                stillTime = 0;
                nextLook = Random.Range(LookAroundMin, LookAroundMax);
                return;
            }
            stillTime = resting ? stillTime + dt : 0;
            if (stillTime < nextLook) return;
            LookingAround = true;
            lookUntil = Time.time + LookAroundFor;
            animator.CrossFadeInFixedTime("LookAround", 0.6f, 0);
        }

        // Test hook: the next still moment starts the look-around.
        public void LookAroundSoon() => nextLook = stillTime;

        // Wear the HALO gear (boarding) or take it all off at once (a new stage start).
        public void SetGear(bool on)
        {
            GearOn = on;
            if (gear == null) return;
            foreach (var g in gear)
                if (g != DroppedPack) g.gameObject.SetActive(on);
        }

        // After landing: the pack unclips and drops behind him onto the ground (left there,
        // under `ground`), the jump helmet comes off and the altimeter goes.
        public void Unclip(Transform ground, System.Func<Vector3, float> height)
        {
            if (!GearOn || gear == null || gear.Length < 3) return;
            GearOn = false;
            StartCoroutine(UnclipRoutine(ground, height));
        }

        System.Collections.IEnumerator UnclipRoutine(Transform ground, System.Func<Vector3, float> height)
        {
            var pack = gear[0];
            var helmet = gear[1];
            gear[2].gameObject.SetActive(false);
            DroppedPack = pack;
            pack.SetParent(ground, true);
            Vector3 from = pack.position;
            Quaternion fromRot = pack.rotation;
            Vector3 back = -yaw.forward;
            Vector3 to = from + back * 0.45f + yaw.right * 0.2f;
            to.y = height(to) + 0.28f; // lying on its back, about half its depth up
            Quaternion toRot = Quaternion.AngleAxis(-82, yaw.right) * fromRot;
            Vector3 helmetFrom = helmet.localPosition, helmetScale = helmet.localScale;
            for (float t = 0; t < 1; t += Time.deltaTime / 0.5f)
            {
                float fall = t * t; // drops, speeding up
                pack.SetPositionAndRotation(Vector3.Lerp(from, to, fall), Quaternion.Slerp(fromRot, toRot, fall));
                float off = Mathf.Clamp01(t * 1.4f);
                helmet.localPosition = helmetFrom + Vector3.up * 0.25f * off;
                helmet.localScale = helmetScale * (1 - off);
                yield return null;
            }
            pack.SetPositionAndRotation(to, toRot);
            helmet.gameObject.SetActive(false);
            helmet.localPosition = helmetFrom;
            helmet.localScale = helmetScale;
        }

        public override void OnFire()
        {
            if (!Dead) handIK.Kick();
        }

        public override void OnHurt()
        {
            if (Dead) return;
            // A short flinch on the upper body; the clip starts in a guard, so blend in and out.
            animator.CrossFadeInFixedTime("Hit", 0.08f, UpperLayer, 0.15f);
            hitUntil = Time.time + 0.4f;
        }
    }
}
