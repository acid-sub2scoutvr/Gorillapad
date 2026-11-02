
using GorillaLocomotion;
using GorillaLocomotion.Gameplay;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Gorillapad.Rigging;
using Gorillapad.Tools;

namespace Gorillapad.Animators
{
    /// <summary>
    /// Gamepad zipline riding.
    /// Hold LT or RT near a zipline to grab it.
    /// Release both triggers to drop.
    /// Press A / Cross to jump off.
    /// </summary>
    public class ZiplineAnimator : AnimatorBase
    {
        const float GrabReach = .9f;
        const float HangDistance = .75f;
        const float HandSpacing = .17f;
        const float GripDrop = .07f;
        const float Drag = .1f;
        const float DefaultMaxSpeed = 12f;
        const float JumpOffMultiplier = 1.3f;
        const float RegrabDelay = .4f;

        static readonly AccessTools.FieldRef<GorillaZipline, BezierSpline> splineRef =
            AccessTools.FieldRefAccess<GorillaZipline, BezierSpline>("spline");

        static readonly AccessTools.FieldRef<GorillaZipline, GorillaZiplineSettings> settingsRef =
            AccessTools.FieldRefAccess<GorillaZipline, GorillaZiplineSettings>("settings");

        public AnimatorBase returnTo;

        GorillaZipline zipline;
        BezierSpline spline;

        float t, speed;

        static GorillaZipline[] ziplines = new GorillaZipline[0];
        static float lastZiplineSearch = -100;
        static float lastRelease = -100;

        static float Scale => GTPlayer.Instance.scale;

        // LT or RT holds the zipline.
        static bool GrabHeld
        {
            get
            {
                Gamepad gamepad = Gamepad.current;

                return gamepad != null && (gamepad.leftTrigger.isPressed || gamepad.rightTrigger.isPressed);
            }
        }

        /// <summary>
        /// Called while walking.
        /// Grabs a nearby zipline while LT or RT is held.
        /// </summary>
        public static void CheckGrab()
        {
            var plugin = Plugin.Instance;

            if (!plugin.IsWalking || !GrabHeld || Time.time - lastRelease < RegrabDelay)
                return;

            if (Time.time - lastZiplineSearch > 5)
            {
                ziplines = FindObjectsByType<GorillaZipline>(FindObjectsSortMode.None);

                lastZiplineSearch = Time.time;
            }

            if (ziplines.Length == 0)
                return;

            Vector3 grabPoint = GTPlayer.Instance.headCollider.transform.position + Vector3.up * .3f * Scale;

            float reach = GrabReach * Scale;

            foreach (var candidate in ziplines)
            {
                if (!candidate || !candidate.isActiveAndEnabled)
                    continue;

                var candidateSpline = splineRef(candidate);

                if (!candidateSpline)
                    continue;

                float candidateT = ClosestT(candidateSpline, grabPoint);

                if (candidateSpline.GetPoint(candidateT).Distance(grabPoint) < reach)
                {
                    var ride = plugin.ziplineAnimator as ZiplineAnimator;

                    if (!ride)
                        return;

                    ride.returnTo = Rig.Instance.Animator;
                    ride.zipline = candidate;
                    ride.spline = candidateSpline;
                    ride.t = candidateT;

                    Rig.Instance.Animator = ride;
                    return;
                }
            }
        }

        static float ClosestT(BezierSpline spline, Vector3 point)
        {
            const int samples = 64;

            float bestT = 0;
            float bestDistance = float.MaxValue;

            for (int i = 0; i <= samples; i++)
            {
                float sampleT = i / (float)samples;

                float distance = (spline.GetPoint(sampleT) - point).sqrMagnitude;

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestT = sampleT;
                }
            }

            float step = 1f / samples;

            for (int i = 0; i < 12; i++)
            {
                step /= 2;

                foreach (float candidate in new[]
                {
                    bestT - step,
                    bestT + step
                })
                {
                    float clamped = Mathf.Clamp01(candidate);

                    float distance = (spline.GetPoint(clamped) - point).sqrMagnitude;

                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestT = clamped;
                    }
                }
            }

            return bestT;
        }

        public override void Setup()
        {
            HeadDriver.Instance.LockCursor = true;
            HeadDriver.Instance.turn = true;

            speed = spline && rigidbody ? Vector3.Dot(rigidbody.linearVelocity, spline.GetDirection(t)) : 0;
        }

        public override void Cleanup()
        {
            base.Cleanup();

            leftHand.followRate = rightHand.followRate = .1f;

            lastRelease = Time.time;
        }

        float JumpSpeed => 250 * Scale / rigidbody.mass;

        void Release(bool jump = false)
        {
            Vector3 velocity = rigidbody.linearVelocity;

            if (jump)
            {
                velocity.y = Mathf.Max(velocity.y, 0) + JumpSpeed * JumpOffMultiplier;
            }

            Rig.Instance.Animator = returnTo ? returnTo : Plugin.Instance.walkAnimator;

            rigidbody.linearVelocity = velocity;
        }

        void Update()
        {
            if (!Plugin.Instance.Enabled || rig == null)
                return;

            Gamepad gamepad = Gamepad.current;

            if (!zipline || !spline || gamepad == null || (!gamepad.leftTrigger.isPressed && !gamepad.rightTrigger.isPressed))
            {
                Release();
                return;
            }

            if (gamepad.buttonSouth.wasPressedThisFrame)
            {
                Release(jump: true);
            }
        }

        public override void Animate()
        {
            if (rig == null || !spline)
                return;

            float dt = Time.deltaTime;
            float s = Scale;

            Vector3 direction = spline.GetDirection(t);

            var settings = settingsRef(zipline);

            float gravityMulti = settings ? settings.gravityMulti : 1;

            float maxSpeed = settings && settings.maxSpeed > 0 ? settings.maxSpeed : DefaultMaxSpeed;

            speed += Vector3.Dot(Physics.gravity, direction) * gravityMulti * dt;

            speed -= speed * Drag * dt;

            speed = Mathf.Clamp(speed, -maxSpeed * s, maxSpeed * s);

            float metersPerT = Mathf.Max(spline.GetVelocity(t).magnitude, .001f);

            t += speed * dt / metersPerT;

            if (t <= 0 || t >= 1)
            {
                t = Mathf.Clamp01(t);
                Release();
                return;
            }

            Vector3 cable = spline.GetPoint(t);

            Vector3 hang = cable + Vector3.down * HangDistance * s;

            rig.active = false;

            rigidbody.linearVelocity = spline.GetDirection(t) * speed + (hang - body.position) * 10f;

            Vector3 across = Vector3.Cross(Vector3.up, direction).normalized;

            if (across == Vector3.zero)
                across = body.right;

            Vector3 palmDirection = Vector3.Dot(across, body.forward) >= 0 ? across : -across;

            float sideSign =Vector3.Dot(direction, body.right) >= 0 ? 1 : -1;

            bool alongBody = Mathf.Abs(Vector3.Dot(direction, body.forward)) > .5f;

            float handT = HandSpacing * s / metersPerT;

            foreach (var hand in new[] { leftHand, rightHand })
            {
                float offset = (hand.isLeft ? -handT : handT) * sideSign;

                Vector3 grip = spline.GetPoint( Mathf.Clamp01(t + offset));

                bool rear = alongBody && Vector3.Dot(grip - body.position, body.forward) < Vector3.Dot(cable - body.position, body.forward);

                Vector3 palm = rear ? -palmDirection : palmDirection;

                Vector3 position = grip + (Vector3.down * GripDrop - palm * .03f) * s;

                hand.followInBodySpace = true;
                hand.followRate = .5f;

                hand.Place(position, -palm, Vector3.up);
            }
        }
    }
}
