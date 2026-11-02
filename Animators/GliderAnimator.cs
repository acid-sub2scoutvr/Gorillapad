using GorillaLocomotion;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;
using Gorillapad.Patches;
using Gorillapad.Rigging;
using Gorillapad.Tools;

namespace Gorillapad.Animators
{
    /// <summary>
    /// Hang gliders. While walking, hold either mouse button near a glider to grab it.
    /// Rather than faking flight, this lets the game's own glider do the flying: the real hand
    /// (controller) positions are moved onto the handle and grip is squeezed, so the game picks the
    /// glider up normally, and then the hands are held overhead and moved to steer, the way you would
    /// in VR. A/D tilts the hands to roll, W/S moves them forward/back. Let go of the mouse button to drop it.
    /// On the ground you can walk (WASD, Shift) and jump (Space) while holding it.
    /// </summary>
    public class GliderAnimator : AnimatorBase
    {
        // ---- Tuning ----
        const float GrabReach = 1.3f;           // handle within this distance of the head
        const float GrabTimeout = 1.5f;         // give up if the game hasn't picked it up by then
        const float OnHandleDistance = .1f;     // a hand this close to its spot on the handle can squeeze
        const float SqueezePeriod = .2f;        // re-squeeze this often until the game picks it up
        const float SecondHandTimeout = 1f;     // keep trying to get the second hand on for this long
        const float HandSpread = .3f;           // each hand this far to the side while flying
        const float HandHeight = .35f;          // hands this far above the head while flying
        const float RollHeight = .15f;          // A/D raises one hand and lowers the other this much
        // The glider pitches from where your head is relative to the handle (hands further forward
        // raise the nose). Rather than guess the level point, the hands slide forward/back until the
        // nose points where you want: level, or diving with W / climbing with S.
        const float StartHandForward = .3f;     // starting guess, in front of the head
        const float MinHandForward = -.3f, MaxHandForward = .8f;
        const float PitchTarget = .3f;          // sine of the nose-down angle W aims for (S the opposite)
        const float LevelingRate = 1f;          // m/s of hand movement per unit of pitch error
        const float RegrabDelay = .5f;
        const float WalkSpeed = 4f, SprintMultiplier = 3f, RideHeight = .46f;  // same as walking
        const float JumpGrace = .5f;            // after jumping, don't count as grounded for this long

        static readonly AccessTools.FieldRef<GliderHoldable, InteractionPoint> handleRef =
            AccessTools.FieldRefAccess<GliderHoldable, InteractionPoint>("handle");

        public AnimatorBase returnTo;
        GliderHoldable glider;
        float grabStart, firstHeldTime, jumpTime = -100;
        float handForward = StartHandForward;

        bool wasHeld;

        // The rig counts as grounded with ground ~1.3m below, so ignore that right after a jump
        bool OnGround => rig.onGround && Time.time - jumpTime > JumpGrace;
        // The same speed a normal jump's impulse gives
        float JumpSpeed => 250 * Scale / rigidbody.mass;

        static GliderHoldable[] gliders = new GliderHoldable[0];
        static float lastSearch = -100, lastRelease = -100;

        static float Scale => GTPlayer.Instance.scale;
        static bool GrabHeld => Mouse.current.leftButton.isPressed || Mouse.current.rightButton.isPressed;

        bool HoldsWith(HandDriver hand)
        {
            var interactor = EquipmentInteractor.instance;
            if (!interactor || !glider) return false;
            return ReferenceEquals(hand.isLeft ? interactor.leftHandHeldEquipment : interactor.rightHandHeldEquipment, glider);
        }

        bool IsHeld
        {
            get
            {
                var interactor = EquipmentInteractor.instance;
                if (!interactor || !glider) return false;
                return ReferenceEquals(interactor.leftHandHeldEquipment, glider)
                    || ReferenceEquals(interactor.rightHandHeldEquipment, glider);
            }
        }

        /// <summary>Called while walking; grabs a nearby glider while a mouse button is held.</summary>
        public static void CheckGrab()
        {
            var plugin = Plugin.Instance;
            if (!plugin.IsWalking || !GrabHeld || Time.time - lastRelease < RegrabDelay) return;

            if (Time.time - lastSearch > 5)
            {
                gliders = FindObjectsByType<GliderHoldable>(FindObjectsSortMode.None);
                lastSearch = Time.time;
            }
            Vector3 head = GTPlayer.Instance.headCollider.transform.position;
            float bestDistance = GrabReach * Scale;
            GliderHoldable best = null;
            foreach (var candidate in gliders)
            {
                if (!candidate || !candidate.isActiveAndEnabled) continue;
                var handle = handleRef(candidate);
                Vector3 point = handle ? handle.transform.position : candidate.transform.position;
                float distance = point.Distance(head);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }
            if (!best) return;

            var animator = plugin.gliderAnimator as GliderAnimator;
            animator.returnTo = Rig.Instance.Animator;
            animator.glider = best;
            Rig.Instance.Animator = animator;
        }

        public override void Setup()
        {
            HeadDriver.Instance.LockCursor = true;
            HeadDriver.Instance.turn = true;
            grabStart = Time.time;
            wasHeld = false;
        }

        public override void Cleanup()
        {
            // base.Cleanup resets the hands: grip off (which lets go) and controllers hidden again
            base.Cleanup();
            leftHand.followRate = rightHand.followRate = .1f;
            lastRelease = Time.time;
            EquipmentInteractorPatch.injectLeft = EquipmentInteractorPatch.injectRight = null;
            try
            {
                if (IsHeld)
                    EquipmentInteractor.instance.ForceDropEquipment(glider);
            }
            catch (System.Exception e) { Logging.Exception(e); }
        }

        void Leave()
        {
            Rig.Instance.Animator = returnTo ? returnTo : Plugin.Instance.walkAnimator;
        }

        void Update()
        {
            if (!Plugin.Instance.Enabled || rig == null) return;
            if (!glider || !GrabHeld)
            {
                Leave();
                return;
            }
            if (IsHeld && OnGround && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                jumpTime = Time.time;
                Vector3 velocity = rigidbody.linearVelocity;
                velocity.y = Mathf.Max(velocity.y, 0) + JumpSpeed;
                rigidbody.linearVelocity = velocity;
            }
        }

        public override void Animate()
        {
            if (rig == null || !glider) return;
            float s = Scale;
            bool held = IsHeld;
            if (held && !wasHeld)
            {
                Logging.Info("Grabbed glider");
                firstHeldTime = Time.time;
            }
            if (!held && wasHeld)
            {
                // The game let go of it (respawned, taken by someone, ...)
                Leave();
                return;
            }
            wasHeld = held;

            foreach (var hand in new[] { leftHand, rightHand })
            {
                // Real hand positions matter here: the game grabs and steers with them
                hand.hideControllerTransform = false;
                hand.followInBodySpace = true;
                hand.followRate = .5f;
            }

            if (!held && Time.time - grabStart > GrabTimeout)
            {
                Logging.Info("Couldn't grab the glider");
                Leave();
                return;
            }

            // Get both hands on it (the glider flies two-handed); a hand that's already holding grips
            // with its whole hand, the other keeps reaching and squeezing
            bool bothHolding = HoldsWith(leftHand) && HoldsWith(rightHand);
            if (!held || (!bothHolding && Time.time - firstHeldTime < SecondHandTimeout))
            {
                var handle = handleRef(glider);
                Transform handleTransform = handle ? handle.transform : glider.transform;
                // The game only grabs at the moment grip is pressed with the hand on the handle, so keep
                // the hand open while reaching, then squeeze, letting go briefly every so often to retry
                bool squeezePhase = Mathf.Repeat(Time.time - grabStart, SqueezePeriod) > SqueezePeriod * .3f;
                foreach (var hand in new[] { leftHand, rightHand })
                {
                    float side = hand.isLeft ? -1 : 1;
                    Vector3 point = handleTransform.position + handleTransform.right * side * HandSpread * .5f * s;
                    hand.Place(point, -body.forward, Vector3.up);
                    bool holding = HoldsWith(hand);
                    // The game only grabs handles its hand collider is touching, which doesn't always
                    // register (e.g. from the ground), so also put the handle in its overlap list
                    var inject = holding ? null : handle;
                    if (hand.isLeft) EquipmentInteractorPatch.injectLeft = inject; else EquipmentInteractorPatch.injectRight = inject;
                    if (holding)
                        hand.grip = hand.trigger = true;
                    else
                    {
                        bool onHandle = hand.transform.position.Distance(point) < OnHandleDistance * s;
                        hand.grip = onHandle && squeezePhase;
                        hand.trigger = false;
                    }
                }
                return;
            }
            EquipmentInteractorPatch.injectLeft = EquipmentInteractorPatch.injectRight = null;

            // Full grip with every hand that's holding it
            foreach (var hand in new[] { leftHand, rightHand })
                hand.grip = hand.trigger = HoldsWith(hand);

            Vector3 input = InputHandler.inputDirection;
            if (OnGround)
            {
                // On the ground: walk around holding it, the same way walking moves the body
                // (Rig moves 4x the target offset per second)
                rig.active = true;
                rig.useGravity = false;
                Vector3 direction = body.TransformDirection(InputHandler.inputDirectionNoY);
                direction.y = 0;
                float speed = Keyboard.current.leftShiftKey.isPressed ? WalkSpeed * SprintMultiplier : WalkSpeed;
                rig.targetPosition = rig.lastGroundPosition + Vector3.up * RideHeight * s + direction * s * speed / 10f;
                input = Vector3.zero; // hold the glider level while walking
            }
            else
            {
                // Flying: the glider moves the body, so leave the body alone
                rig.active = false;
                rig.useGravity = true;

                // Slide the hands forward/back until the nose points where you want it
                float noseDown = -glider.transform.forward.y;
                float wanted = input.z * PitchTarget;
                handForward = Mathf.Clamp(handForward + (noseDown - wanted) * LevelingRate * Time.deltaTime,
                    MinHandForward, MaxHandForward);
            }

            // Steer the way you'd hold it in VR: hands overhead and apart; A/D tilts them to roll
            Vector3 center = head.position + (Vector3.up * HandHeight + body.forward * handForward) * s;
            foreach (var hand in new[] { leftHand, rightHand })
            {
                float side = hand.isLeft ? -1 : 1;
                Vector3 position = center + (body.right * side * HandSpread + Vector3.up * -side * input.x * RollHeight) * s;
                hand.Place(position, -body.forward, Vector3.up);
            }
        }
    }
}
