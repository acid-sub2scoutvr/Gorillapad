
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
    /// Hang gliders controlled with a gamepad.
    /// LT/RT grab and hold, left stick steers, A jumps,
    /// L3 sprints on the ground, and releasing both triggers drops it.
    /// </summary>
    public class GliderAnimator : AnimatorBase
    {
        // ---- Tuning ----
        const float GrabReach = 1.3f;
        const float GrabTimeout = 1.5f;
        const float OnHandleDistance = .1f;
        const float SqueezePeriod = .2f;
        const float SecondHandTimeout = 1f;
        const float HandSpread = .3f;
        const float HandHeight = .35f;
        const float RollHeight = .15f;
        const float StartHandForward = .3f;
        const float MinHandForward = -.3f;
        const float MaxHandForward = .8f;
        const float PitchTarget = .3f;
        const float LevelingRate = 1f;
        const float RegrabDelay = .5f;
        const float WalkSpeed = 4f;
        const float SprintMultiplier = 3f;
        const float RideHeight = .46f;
        const float JumpGrace = .5f;

        static readonly AccessTools.FieldRef<GliderHoldable, InteractionPoint> handleRef =
            AccessTools.FieldRefAccess<GliderHoldable, InteractionPoint>("handle");

        public AnimatorBase returnTo;
        GliderHoldable glider;

        float grabStart;
        float firstHeldTime;
        float jumpTime = -100;
        float handForward = StartHandForward;

        bool wasHeld;

        static GliderHoldable[] gliders = new GliderHoldable[0];
        static float lastSearch = -100;
        static float lastRelease = -100;

        static Gamepad CurrentGamepad => Gamepad.current;

        static Vector2 LeftStickInput =>
            CurrentGamepad != null
                ? Vector2.ClampMagnitude(CurrentGamepad.leftStick.ReadValue(), 1f)
                : Vector2.zero;

        static bool GrabHeld =>
            CurrentGamepad != null &&
            (CurrentGamepad.leftTrigger.isPressed ||
             CurrentGamepad.rightTrigger.isPressed);

        static bool JumpPressed =>
            CurrentGamepad != null &&
            CurrentGamepad.buttonSouth.wasPressedThisFrame;

        static bool IsSprinting =>
            CurrentGamepad != null &&
            CurrentGamepad.leftStickButton.isPressed;

        static float Scale => GTPlayer.Instance.scale;

        // The rig counts as grounded with ground ~1.3m below,
        // so ignore that right after a jump.
        bool OnGround => rig.onGround && Time.time - jumpTime > JumpGrace;

        float JumpSpeed => 250 * Scale / rigidbody.mass;

        bool HoldsWith(HandDriver hand)
        {
            var interactor = EquipmentInteractor.instance;

            if (!interactor || !glider)
                return false;

            return ReferenceEquals(
                hand.isLeft
                    ? interactor.leftHandHeldEquipment
                    : interactor.rightHandHeldEquipment,
                glider
            );
        }

        bool IsHeld
        {
            get
            {
                var interactor = EquipmentInteractor.instance;

                if (!interactor || !glider)
                    return false;

                return ReferenceEquals(interactor.leftHandHeldEquipment, glider)
                    || ReferenceEquals(interactor.rightHandHeldEquipment, glider);
            }
        }

        /// <summary>
        /// Called while walking; grabs a nearby glider while either trigger is held.
        /// </summary>
        public static void CheckGrab()
        {
            var plugin = Plugin.Instance;

            if (!plugin.IsWalking ||
                !GrabHeld ||
                Time.time - lastRelease < RegrabDelay)
                return;

            if (Time.time - lastSearch > 5)
            {
                gliders = FindObjectsByType<GliderHoldable>(
                    FindObjectsSortMode.None
                );

                lastSearch = Time.time;
            }

            Vector3 head = GTPlayer.Instance.headCollider.transform.position;
            float bestDistance = GrabReach * Scale;
            GliderHoldable best = null;

            foreach (var candidate in gliders)
            {
                if (!candidate || !candidate.isActiveAndEnabled)
                    continue;

                var handle = handleRef(candidate);

                Vector3 point = handle
                    ? handle.transform.position
                    : candidate.transform.position;

                float distance = point.Distance(head);

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }

            if (!best)
                return;

            var animator = plugin.gliderAnimator as GliderAnimator;

            if (!animator)
                return;

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
            handForward = StartHandForward;
        }

        public override void Cleanup()
        {
            // Resets the hands and releases the glider.
            base.Cleanup();

            leftHand.followRate = rightHand.followRate = .1f;

            lastRelease = Time.time;

            EquipmentInteractorPatch.injectLeft =
                EquipmentInteractorPatch.injectRight = null;

            try
            {
                if (IsHeld)
                    EquipmentInteractor.instance.ForceDropEquipment(glider);
            }
            catch (System.Exception e)
            {
                Logging.Exception(e);
            }
        }

        void Leave()
        {
            Rig.Instance.Animator =
                returnTo ? returnTo : Plugin.Instance.walkAnimator;
        }

        void Update()
        {
            if (!Plugin.Instance.Enabled || rig == null)
                return;

            if (!glider || !GrabHeld)
            {
                Leave();
                return;
            }

            if (IsHeld && OnGround && JumpPressed)
            {
                jumpTime = Time.time;

                Vector3 velocity = rigidbody.linearVelocity;
                velocity.y = Mathf.Max(velocity.y, 0) + JumpSpeed;

                rigidbody.linearVelocity = velocity;
            }
        }

        public override void Animate()
        {
            if (rig == null || !glider)
                return;

            float s = Scale;
            bool held = IsHeld;

            if (held && !wasHeld)
            {
                Logging.Info("Grabbed glider");
                firstHeldTime = Time.time;
            }

            if (!held && wasHeld)
            {
                Leave();
                return;
            }

            wasHeld = held;

            foreach (var hand in new[] { leftHand, rightHand })
            {
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

            // Get both hands onto the glider.
            bool bothHolding =
                HoldsWith(leftHand) && HoldsWith(rightHand);

            if (!held ||
                (!bothHolding &&
                 Time.time - firstHeldTime < SecondHandTimeout))
            {
                var handle = handleRef(glider);

                Transform handleTransform = handle
                    ? handle.transform
                    : glider.transform;

                bool squeezePhase =
                    Mathf.Repeat(Time.time - grabStart, SqueezePeriod)
                    > SqueezePeriod * .3f;

                foreach (var hand in new[] { leftHand, rightHand })
                {
                    float side = hand.isLeft ? -1 : 1;

                    Vector3 point =
                        handleTransform.position +
                        handleTransform.right * side * HandSpread * .5f * s;

                    hand.Place(point, -body.forward, Vector3.up);

                    bool holding = HoldsWith(hand);

                    var inject = holding ? null : handle;

                    if (hand.isLeft)
                        EquipmentInteractorPatch.injectLeft = inject;
                    else
                        EquipmentInteractorPatch.injectRight = inject;

                    if (holding)
                    {
                        hand.grip = hand.trigger = true;
                    }
                    else
                    {
                        bool onHandle =
                            hand.transform.position.Distance(point) <
                            OnHandleDistance * s;

                        hand.grip = onHandle && squeezePhase;
                        hand.trigger = false;
                    }
                }

                return;
            }

            EquipmentInteractorPatch.injectLeft =
                EquipmentInteractorPatch.injectRight = null;

            foreach (var hand in new[] { leftHand, rightHand })
                hand.grip = hand.trigger = HoldsWith(hand);

            Vector2 stick = LeftStickInput;

            Vector3 input = new Vector3(
                stick.x,
                0f,
                stick.y
            );

            if (OnGround)
            {
                // Walk around while holding the glider.
                rig.active = true;
                rig.useGravity = false;

                Vector3 direction =
                    body.TransformDirection(
                        new Vector3(stick.x, 0f, stick.y)
                    );

                direction.y = 0;

                float speed = IsSprinting
                    ? WalkSpeed * SprintMultiplier
                    : WalkSpeed;

                rig.targetPosition =
                    rig.lastGroundPosition +
                    Vector3.up * RideHeight * s +
                    direction * s * speed / 10f;

                // Keep the glider level while walking.
                input = Vector3.zero;
            }
            else
            {
                // Flying: let the glider move the body.
                rig.active = false;
                rig.useGravity = true;

                float noseDown = -glider.transform.forward.y;
                float wanted = input.z * PitchTarget;

                handForward = Mathf.Clamp(
                    handForward +
                    (noseDown - wanted) * LevelingRate * Time.deltaTime,
                    MinHandForward,
                    MaxHandForward
                );
            }

            // Hands overhead and apart.
            // Left stick X rolls; left stick Y controls pitch.
            Vector3 center =
                head.position +
                (Vector3.up * HandHeight +
                 body.forward * handForward) * s;

            foreach (var hand in new[] { leftHand, rightHand })
            {
                float side = hand.isLeft ? -1 : 1;

                Vector3 position =
                    center +
                    (body.right * side * HandSpread +
                     Vector3.up * -side * input.x * RollHeight) * s;

                hand.Place(position, -body.forward, Vector3.up);
            }
        }
    }
}
