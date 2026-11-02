
using GorillaLocomotion;
using GorillaLocomotion.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;
using Gorillapad.Rigging;
using Gorillapad.Tools;

namespace Gorillapad.Animators
{
    /// <summary>
    /// Hanging onto flying broomsticks with a gamepad.
    /// LT/RT grab and hold, A jumps off, and releasing both
    /// triggers lets go while preserving momentum.
    /// </summary>
    public class BroomAnimator : AnimatorBase
    {
        // ---- Tuning ----
        const float GrabReach = 1.2f;
        const float HangDistance = .6f;
        const float HandSpacing = .17f;
        const float GripDrop = .07f;
        const float JumpOffMultiplier = 1.3f;
        const float RegrabDelay = .4f;

        public AnimatorBase returnTo;

        NoncontrollableBroomstick broom;

        Vector3 lastBroomPosition;
        Vector3 broomVelocity;

        static NoncontrollableBroomstick[] brooms =
            new NoncontrollableBroomstick[0];

        static float lastSearch = -100;
        static float lastRelease = -100;

        static Gamepad CurrentGamepad => Gamepad.current;

        static bool GrabHeld =>
            CurrentGamepad != null &&
            (CurrentGamepad.leftTrigger.isPressed ||
             CurrentGamepad.rightTrigger.isPressed);

        static bool JumpPressed =>
            CurrentGamepad != null &&
            CurrentGamepad.buttonSouth.wasPressedThisFrame;

        static float Scale => GTPlayer.Instance.scale;

        // The same speed a normal jump's impulse gives.
        float JumpSpeed => 250 * Scale / rigidbody.mass;

        /// <summary>
        /// Called while walking; grabs a nearby broomstick while
        /// either trigger is held.
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
                brooms = FindObjectsByType<NoncontrollableBroomstick>(
                    FindObjectsSortMode.None
                );

                lastSearch = Time.time;
            }

            Vector3 head =
                GTPlayer.Instance.headCollider.transform.position;

            float bestDistance = GrabReach * Scale;
            NoncontrollableBroomstick best = null;

            foreach (var candidate in brooms)
            {
                if (!candidate || !candidate.isActiveAndEnabled)
                    continue;

                float distance =
                    candidate.transform.position.Distance(head);

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }

            if (!best)
                return;

            var ride = plugin.broomAnimator as BroomAnimator;

            if (!ride)
                return;

            ride.returnTo = Rig.Instance.Animator;
            ride.broom = best;

            Rig.Instance.Animator = ride;
        }

        public override void Setup()
        {
            HeadDriver.Instance.LockCursor = true;
            HeadDriver.Instance.turn = true;

            if (broom)
                lastBroomPosition = broom.transform.position;

            broomVelocity = rigidbody
                ? rigidbody.linearVelocity
                : Vector3.zero;
        }

        public override void Cleanup()
        {
            base.Cleanup();

            leftHand.followRate = rightHand.followRate = .1f;

            lastRelease = Time.time;
        }

        void Release(bool jump = false)
        {
            Vector3 velocity = broomVelocity;

            // Jumping off cancels downward speed, then launches upward.
            if (jump)
            {
                velocity.y =
                    Mathf.Max(velocity.y, 0) +
                    JumpSpeed * JumpOffMultiplier;
            }

            Rig.Instance.Animator =
                returnTo ? returnTo : Plugin.Instance.walkAnimator;

            rigidbody.linearVelocity = velocity;
        }

        void Update()
        {
            if (!Plugin.Instance.Enabled || rig == null)
                return;

            if (!broom || !broom.isActiveAndEnabled || !GrabHeld)
            {
                Release();
                return;
            }

            if (JumpPressed)
                Release(jump: true);
        }

        public override void Animate()
        {
            if (rig == null || !broom)
                return;

            float dt = Time.deltaTime;
            float s = Scale;

            // Measure broomstick velocity as it moves along its path.
            Vector3 broomPosition = broom.transform.position;

            Vector3 measured =
                (broomPosition - lastBroomPosition) /
                Mathf.Max(dt, .0001f);

            lastBroomPosition = broomPosition;

            broomVelocity = Vector3.Lerp(
                broomVelocity,
                measured,
                .5f
            );

            // Hang below the stick and drive the body with it.
            Vector3 hang =
                broomPosition + Vector3.down * HangDistance * s;

            rig.active = false;

            rigidbody.linearVelocity =
                broomVelocity + (hang - body.position) * 10f;

            // Both hands hold the stick.
            Vector3 stick = broom.transform.forward;

            Vector3 across =
                Vector3.Cross(Vector3.up, stick).normalized;

            if (across == Vector3.zero)
                across = body.right;

            Vector3 palmDirection =
                Vector3.Dot(across, body.forward) >= 0
                    ? across
                    : -across;

            float sideSign =
                Vector3.Dot(stick, body.right) >= 0
                    ? 1f
                    : -1f;

            bool alongBody =
                Mathf.Abs(Vector3.Dot(stick, body.forward)) > .5f;

            foreach (var hand in new[] { leftHand, rightHand })
            {
                float offset =
                    (hand.isLeft ? -HandSpacing : HandSpacing) *
                    sideSign * s;

                Vector3 grip = broomPosition + stick * offset;

                bool rear =
                    alongBody &&
                    Vector3.Dot(stick * offset, body.forward) < 0;

                Vector3 palm =
                    rear ? -palmDirection : palmDirection;

                hand.followInBodySpace = true;
                hand.followRate = .5f;

                hand.Place(grip + (Vector3.down * GripDrop - palm * .03f) * s, -palm, Vector3.up);
            }
        }
    }
}
