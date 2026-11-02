using GorillaLocomotion;
using GorillaLocomotion.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;
using Gorillapad.Rigging;
using Gorillapad.Tools;

namespace Gorillapad.Animators
{
    /// <summary>
    /// Hanging onto the flying broomsticks. While walking (or jumping), hold either mouse button when a
    /// broomstick is close to grab it; you hang below it with both hands on the stick and get carried
    /// along its path. Let go of the mouse button to fly off with its momentum, or press Space to
    /// jump off. The game grabs these with its VR hand-grabber system, so this rides them directly.
    /// </summary>
    public class BroomAnimator : AnimatorBase
    {
        // ---- Tuning ----
        const float GrabReach = 1.2f;           // broom within this distance of the head
        const float HangDistance = .6f;         // body hangs this far below the stick
        const float HandSpacing = .17f;         // each hand this far along the stick from its center
        const float GripDrop = .07f;            // hands sit this far below the stick so fingers wrap over it
        const float JumpOffMultiplier = 1.3f;   // Space launches you this much harder than a normal jump
        const float RegrabDelay = .4f;

        public AnimatorBase returnTo;
        NoncontrollableBroomstick broom;
        Vector3 lastBroomPosition, broomVelocity;

        static NoncontrollableBroomstick[] brooms = new NoncontrollableBroomstick[0];
        static float lastSearch = -100, lastRelease = -100;

        static float Scale => GTPlayer.Instance.scale;
        static bool GrabHeld => Mouse.current.leftButton.isPressed || Mouse.current.rightButton.isPressed;
        // The same speed a normal jump's impulse gives
        float JumpSpeed => 250 * Scale / rigidbody.mass;

        /// <summary>Called while walking; grabs a nearby broomstick while a mouse button is held.</summary>
        public static void CheckGrab()
        {
            var plugin = Plugin.Instance;
            if (!plugin.IsWalking || !GrabHeld || Time.time - lastRelease < RegrabDelay) return;

            if (Time.time - lastSearch > 5)
            {
                brooms = FindObjectsByType<NoncontrollableBroomstick>(FindObjectsSortMode.None);
                lastSearch = Time.time;
            }
            Vector3 head = GTPlayer.Instance.headCollider.transform.position;
            float bestDistance = GrabReach * Scale;
            NoncontrollableBroomstick best = null;
            foreach (var candidate in brooms)
            {
                if (!candidate || !candidate.isActiveAndEnabled) continue;
                float distance = candidate.transform.position.Distance(head);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }
            if (!best) return;

            var ride = plugin.broomAnimator as BroomAnimator;
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
            broomVelocity = rigidbody ? rigidbody.linearVelocity : Vector3.zero;
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
            // Jumping off cancels any downward speed, then launches upward
            if (jump)
                velocity.y = Mathf.Max(velocity.y, 0) + JumpSpeed * JumpOffMultiplier;
            Rig.Instance.Animator = returnTo ? returnTo : Plugin.Instance.walkAnimator;
            rigidbody.linearVelocity = velocity;
        }

        void Update()
        {
            if (!Plugin.Instance.Enabled || rig == null) return;
            if (!broom || !broom.isActiveAndEnabled || !GrabHeld)
                Release();
            else if (Keyboard.current.spaceKey.wasPressedThisFrame)
                Release(jump: true);
        }

        public override void Animate()
        {
            if (rig == null || !broom) return;
            float dt = Time.deltaTime;
            float s = Scale;

            // The broom moves itself along its path in FixedUpdate, so measure its velocity per tick
            Vector3 broomPosition = broom.transform.position;
            Vector3 measured = (broomPosition - lastBroomPosition) / Mathf.Max(dt, .0001f);
            lastBroomPosition = broomPosition;
            broomVelocity = Vector3.Lerp(broomVelocity, measured, .5f);

            // Hang below the stick; drive the body directly so it stays with the broom
            Vector3 hang = broomPosition + Vector3.down * HangDistance * s;
            rig.active = false;
            rigidbody.linearVelocity = broomVelocity + (hang - body.position) * 10f;

            // Both hands on the stick like a bar: just below it, palms toward it, fingers over the top.
            // When the stick runs front-to-back along the body, turn the rear hand around.
            Vector3 stick = broom.transform.forward;
            Vector3 across = Vector3.Cross(Vector3.up, stick).normalized;
            if (across == Vector3.zero) across = body.right;
            Vector3 palmDirection = Vector3.Dot(across, body.forward) >= 0 ? across : -across;
            float sideSign = Vector3.Dot(stick, body.right) >= 0 ? 1 : -1;
            bool alongBody = Mathf.Abs(Vector3.Dot(stick, body.forward)) > .5f;
            foreach (var hand in new[] { leftHand, rightHand })
            {
                float offset = (hand.isLeft ? -HandSpacing : HandSpacing) * sideSign * s;
                Vector3 grip = broomPosition + stick * offset;
                bool rear = alongBody && Vector3.Dot(stick * offset, body.forward) < 0;
                Vector3 palm = rear ? -palmDirection : palmDirection;
                hand.followInBodySpace = true;
                hand.followRate = .5f;
                hand.Place(grip + (Vector3.down * GripDrop - palm * .03f) * s, -palm, Vector3.up);
            }
        }
    }
}
