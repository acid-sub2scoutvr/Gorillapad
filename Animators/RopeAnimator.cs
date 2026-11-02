using GorillaLocomotion;
using GorillaLocomotion.Gameplay;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using Gorillapad.Rigging;
using Gorillapad.Tools;

namespace Gorillapad.Animators
{
    /// <summary>
    /// Swinging on ropes. While walking, hold either mouse button near a rope to grab it.
    /// Uses the game's own rope attach/detach, so the rope simulation takes your weight and other
    /// players see you on it. WASD pumps the swing, Space launches you off (like ziplines), and
    /// letting go of the mouse button drops you off with the rope's momentum.
    /// </summary>
    public class RopeAnimator : AnimatorBase
    {
        // ---- Tuning ----
        const float GrabReach = .8f;            // rope bone within this distance of the grab point
        const float HangDistance = .55f;        // body hangs this far below the grabbing hand
        const float HangBehind = .22f;          // and this far behind the rope, so it isn't inside it
        const float GripBehind = .04f;          // hands this far behind the rope's center to wrap around it
        const float PumpAcceleration = 1f;      // m/s^2 the held direction adds to the rope's swing
        const float MaxPumpSpeed = 3f;          // pumping stops adding speed past this (m/s)
        const float ReleaseBoost = 1.2f;        // multiplier on the rope's velocity when letting go
        const float JumpOffMultiplier = 1.3f;   // Space launches you this much harder than a normal jump
        const float RegrabDelay = .4f;          // seconds before you can grab again after letting go
        const float HandGap = .16f;             // the upper hand grips this far up the rope from the lower
        const float MaxTrustedSpeed = 15f;      // rope speed readings above this are glitches

        public AnimatorBase returnTo;
        GorillaRopeSwing rope;
        int boneIndex;
        int upStep;                             // which way along the bones is up the rope (-1 or 1)
        Vector3 lastBonePosition, boneVelocity;
        float lastBoneMoveTime;
        bool attached;

        static GorillaRopeSwing[] ropes = new GorillaRopeSwing[0];
        static float lastRopeSearch = -100, lastRelease = -100;

        static float Scale => GTPlayer.Instance.scale;
        static bool GrabHeld => Mouse.current.leftButton.isPressed || Mouse.current.rightButton.isPressed;
        int Bone => Mathf.Clamp(boneIndex, 0, rope.nodes.Length - 1);
        // The same speed a normal jump's impulse gives
        float JumpSpeed => 250 * Scale / rigidbody.mass;
        Transform BoneTransform => rope.nodes[Bone];

        /// <summary>Called while walking; grabs a nearby rope while a mouse button is held.</summary>
        public static void CheckGrab()
        {
            var plugin = Plugin.Instance;
            if (!plugin.IsWalking || !GrabHeld || Time.time - lastRelease < RegrabDelay) return;

            if (Time.time - lastRopeSearch > 5)
            {
                ropes = FindObjectsByType<GorillaRopeSwing>(FindObjectsSortMode.None);
                lastRopeSearch = Time.time;
            }
            if (ropes.Length == 0) return;

            Vector3 grabPoint = GTPlayer.Instance.headCollider.transform.position + Vector3.up * .2f * Scale;
            float bestDistance = GrabReach * Scale;
            GorillaRopeSwing bestRope = null;
            int bestBone = 0;
            foreach (var candidate in ropes)
            {
                if (!candidate || !candidate.isActiveAndEnabled || candidate.nodes == null) continue;
                for (int i = 0; i < candidate.nodes.Length; i++)
                {
                    if (!candidate.nodes[i]) continue;
                    float distance = candidate.nodes[i].position.Distance(grabPoint);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestRope = candidate;
                        bestBone = i;
                    }
                }
            }
            if (!bestRope) return;

            var swing = plugin.ropeAnimator as RopeAnimator;
            swing.returnTo = Rig.Instance.Animator;
            swing.rope = bestRope;
            swing.boneIndex = bestBone;
            var nodes = bestRope.nodes;
            swing.upStep = nodes[0].position.y >= nodes[nodes.Length - 1].position.y ? -1 : 1;
            Rig.Instance.Animator = swing;
        }

        public override void Setup()
        {
            HeadDriver.Instance.LockCursor = true;
            HeadDriver.Instance.turn = true;
            if (!rope) return;
            lastBonePosition = BoneTransform.position;
            lastBoneMoveTime = Time.time;
            boneVelocity = Vector3.zero;
            try
            {
                // Hand the rope our velocity so grabbing it on the run starts it swinging
                rope.AttachLocalPlayer(XRNode.RightHand, BoneTransform, Vector3.zero, rigidbody.linearVelocity);
                attached = true;
            }
            catch (Exception e) { Logging.Exception(e); }
        }

        public override void Cleanup()
        {
            base.Cleanup();
            Detach();
            leftHand.followRate = rightHand.followRate = .1f;
            lastRelease = Time.time;
        }

        void Detach()
        {
            if (!attached) return;
            attached = false;
            try { if (rope) rope.DetachLocalPlayer(); }
            catch (Exception e) { Logging.Exception(e); }
        }

        void Release(bool jump = false)
        {
            Vector3 velocity = boneVelocity * ReleaseBoost;
            // Jumping off cancels any downward speed, then launches upward
            if (jump)
                velocity.y = Mathf.Max(velocity.y, 0) + JumpSpeed * JumpOffMultiplier;
            Rig.Instance.Animator = returnTo ? returnTo : Plugin.Instance.walkAnimator;
            rigidbody.linearVelocity = velocity;
        }

        void Update()
        {
            if (!Plugin.Instance.Enabled || rig == null) return;
            if (!rope || !rope.isActiveAndEnabled || !GrabHeld)
            {
                Release();
                return;
            }
            if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                Release(jump: true);
                return;
            }
            TrackBoneVelocity();
        }

        // The rope simulation moves its bones on frames, not physics ticks, so measuring per tick
        // reads zero on some ticks and double on others. Measure only when the bone actually moved,
        // over the real time since it last moved, smoothed, and ignore glitchy spikes. (Writing a bad
        // reading back into the rope is what caused occasional hard kicks.)
        void TrackBoneVelocity()
        {
            Vector3 position = BoneTransform.position;
            float elapsed = Time.time - lastBoneMoveTime;
            if (position == lastBonePosition || elapsed <= 0) return;
            Vector3 measured = (position - lastBonePosition) / elapsed;
            lastBonePosition = position;
            lastBoneMoveTime = Time.time;
            if (measured.magnitude > MaxTrustedSpeed * Scale) return;
            boneVelocity = Vector3.Lerp(boneVelocity, measured, .4f);
        }

        public override void Animate()
        {
            if (rig == null || !rope) return;
            float dt = Time.deltaTime;
            float s = Scale;
            Vector3 input = InputHandler.inputDirection;

            Vector3 bonePosition = BoneTransform.position;

            // Pump the swing in the held direction, up to a speed limit
            Vector3 push = body.TransformDirection(new Vector3(input.x, 0, input.z));
            push.y = 0;
            if (push != Vector3.zero && Vector3.Dot(boneVelocity, push.normalized) < MaxPumpSpeed * s)
            {
                try
                {
                    rope.SetVelocity(Bone, boneVelocity + push.normalized * PumpAcceleration * s * dt, true, default);
                }
                catch (Exception e) { Logging.Exception(e); }
            }

            // Hang below the hand and a little behind the rope (facing it); drive the body directly
            // so it stays with the rope
            Vector3 facing = Vector3.ProjectOnPlane(body.forward, Vector3.up).normalized;
            if (facing == Vector3.zero) facing = Vector3.forward;
            Vector3 hang = bonePosition + (Vector3.down * HangDistance - facing * HangBehind) * s;
            rig.active = false;
            rigidbody.linearVelocity = boneVelocity + (hang - body.position) * 10f;

            // Right hand on the grabbed bone, left hand a hand's width higher; knuckles facing out.
            // (Bones can be far apart, so measure the gap along the rope instead of using the next bone.)
            int upperBone = Mathf.Clamp(Bone + upStep, 0, rope.nodes.Length - 1);
            Vector3 ropeUp = (rope.nodes[upperBone].position - bonePosition);
            if (ropeUp.sqrMagnitude < .0001f) ropeUp = Vector3.up;
            ropeUp.Normalize();
            Vector3 lower = bonePosition, upper = bonePosition + ropeUp * HandGap * s;
            foreach (var hand in new[] { leftHand, rightHand })
            {
                // Grip the rope from behind like a pole: palm toward the rope, fingers wrapping around
                // it sideways (left hand's fingers point right, right hand's point left)
                Vector3 grip = (hand.isLeft ? upper : lower) - facing * GripBehind * s;
                Vector3 across = hand.isLeft ? body.right : -body.right;
                hand.followInBodySpace = true;
                hand.followRate = .5f;
                hand.Place(grip, -facing, across);
            }
        }
    }
}
