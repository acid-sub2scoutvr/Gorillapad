
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
    /// Swinging on ropes using a gamepad.
    /// Hold LT or RT near a rope to grab it.
    /// Left stick pumps the swing, A jumps off, and releasing
    /// both triggers drops you with the rope's momentum.
    /// </summary>
    public class RopeAnimator : AnimatorBase
    {
        // ---- Tuning ----
        const float GrabReach = .8f;
        const float HangDistance = .55f;
        const float HangBehind = .22f;
        const float GripBehind = .04f;
        const float PumpAcceleration = 1f;
        const float MaxPumpSpeed = 3f;
        const float ReleaseBoost = 1.2f;
        const float JumpOffMultiplier = 1.3f;
        const float RegrabDelay = .4f;
        const float HandGap = .16f;
        const float MaxTrustedSpeed = 15f;

        public AnimatorBase returnTo;
        GorillaRopeSwing rope;
        int boneIndex;
        int upStep;
        Vector3 lastBonePosition, boneVelocity;
        float lastBoneMoveTime;
        bool attached;

        static GorillaRopeSwing[] ropes = new GorillaRopeSwing[0];
        static float lastRopeSearch = -100, lastRelease = -100;

        static Gamepad CurrentGamepad => Gamepad.current;

        static bool GrabHeld => CurrentGamepad != null && (CurrentGamepad.leftTrigger.isPressed || CurrentGamepad.rightTrigger.isPressed);

        static bool JumpPressed => CurrentGamepad != null && CurrentGamepad.buttonSouth.wasPressedThisFrame;

        Vector2 LeftStickInput => CurrentGamepad != null ? Vector2.ClampMagnitude(CurrentGamepad.leftStick.ReadValue(), 1f) : Vector2.zero;

        static float Scale => GTPlayer.Instance.scale;

        int Bone => Mathf.Clamp(boneIndex, 0, rope.nodes.Length - 1);
        float JumpSpeed => 250 * Scale / rigidbody.mass;
        Transform BoneTransform => rope.nodes[Bone];

        /// <summary>Called while walking; grabs a nearby rope while a trigger is held.</summary>
        public static void CheckGrab()
        {
            var plugin = Plugin.Instance;

            if (!plugin.IsWalking || !GrabHeld || Time.time - lastRelease < RegrabDelay)
                return;

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
                if (!candidate || !candidate.isActiveAndEnabled || candidate.nodes == null)
                    continue;

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
            if (swing == null) return;

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
                rope.AttachLocalPlayer(XRNode.RightHand, BoneTransform, Vector3.zero, rigidbody.linearVelocity);

                attached = true;
            }
            catch (Exception e)
            {
                Logging.Exception(e);
            }
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

            try
            {
                if (rope) rope.DetachLocalPlayer();
            }
            catch (Exception e)
            {
                Logging.Exception(e);
            }
        }

        void Release(bool jump = false)
        {
            Vector3 velocity = boneVelocity * ReleaseBoost;

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

            if (JumpPressed)
            {
                Release(jump: true);
                return;
            }

            TrackBoneVelocity();
        }

        void TrackBoneVelocity()
        {
            Vector3 position = BoneTransform.position;
            float elapsed = Time.time - lastBoneMoveTime;

            if (position == lastBonePosition || elapsed <= 0)
                return;

            Vector3 measured = (position - lastBonePosition) / elapsed;

            lastBonePosition = position;
            lastBoneMoveTime = Time.time;

            if (measured.magnitude > MaxTrustedSpeed * Scale)
                return;

            boneVelocity = Vector3.Lerp(boneVelocity, measured, .4f);
        }

        public override void Animate()
        {
            if (rig == null || !rope) return;

            float dt = Time.deltaTime;
            float s = Scale;

            Vector2 stick = LeftStickInput;
            Vector3 input = new Vector3(stick.x, 0f, stick.y);

            Vector3 bonePosition = BoneTransform.position;

            // Pump the swing with the left stick.
            Vector3 push = body.TransformDirection(new Vector3(input.x, 0, input.z));

            push.y = 0;

            if (push != Vector3.zero && Vector3.Dot(boneVelocity, push.normalized) < MaxPumpSpeed * s)
            {
                try
                {
                    rope.SetVelocity(Bone, boneVelocity + push.normalized * PumpAcceleration * s * dt, true, default);
                }
                catch (Exception e)
                {
                    Logging.Exception(e);
                }
            }

            // Hang below the hand and slightly behind the rope.
            Vector3 facing = Vector3.ProjectOnPlane(body.forward, Vector3.up).normalized;

            if (facing == Vector3.zero)
                facing = Vector3.forward;

            Vector3 hang = bonePosition + (Vector3.down * HangDistance - facing * HangBehind) * s;

            rig.active = false;
            rigidbody.linearVelocity = boneVelocity + (hang - body.position) * 10f;

            // Position both hands along the rope.
            int upperBone = Mathf.Clamp(Bone + upStep, 0, rope.nodes.Length - 1);

            Vector3 ropeUp = rope.nodes[upperBone].position - bonePosition;

            if (ropeUp.sqrMagnitude < .0001f)
                ropeUp = Vector3.up;

            ropeUp.Normalize();

            Vector3 lower = bonePosition;
            Vector3 upper = bonePosition + ropeUp * HandGap * s;

            foreach (var hand in new[] { leftHand, rightHand })
            {
                Vector3 grip = (hand.isLeft ? upper : lower) - facing * GripBehind * s;

                Vector3 across = hand.isLeft ? body.right : -body.right;

                hand.followInBodySpace = true;
                hand.followRate = .5f;
                hand.Place(grip, -facing, across);
            }
        }
    }
}
