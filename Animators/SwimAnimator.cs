
using GorillaLocomotion;
using GorillaLocomotion.Swimming;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Gorillapad;
using Gorillapad.Animators;
using Gorillapad.Rigging;
using Gorillapad.Tools;

namespace Gorillapad.Animators
{
    /// <summary>
    /// Swimming. Entered automatically from walking when the body is submerged,
    /// and returns to the walk you were using when you leave the water.
    /// Gamepad controls:
    /// - Left stick: swim
    /// - A / Cross: swim up and leap out at the surface
    /// - B / Circle: swim down
    /// - L3: swim faster
    /// </summary>
    public class SwimAnimator : AnimatorBase
    {
        // ---- Tuning ----
        const float SwimSpeed = 1.4f, SprintMultiplier = 2f;
        const float WaterDrag = .05f;
        const float FloatDepth = .2f;
        const float Buoyancy = 1.5f;
        const float EnterDepth = .3f;
        const float ExitDepth = .15f;
        const float LeapDepth = .45f;
        const float LeapSpeed = 5f;
        const float StrokeRate = .9f;

        public AnimatorBase returnTo;
        Vector3 velocity;
        float strokePhase, strokeWeight;
        static float lastLeap;

        static float Scale => GTPlayer.Instance.scale;

        // Gamepad input
        static Gamepad CurrentGamepad => Gamepad.current;

        Vector2 LeftStickInput => CurrentGamepad != null ? Vector2.ClampMagnitude(CurrentGamepad.leftStick.ReadValue(), 1f) : Vector2.zero;

        bool SwimUp => CurrentGamepad != null && CurrentGamepad.buttonSouth.isPressed;

        bool SwimDown => CurrentGamepad != null && CurrentGamepad.buttonEast.isPressed;

        bool IsSprinting => CurrentGamepad != null && CurrentGamepad.leftStickButton.isPressed;

        // How far the point is below the water surface
        static bool DepthBelowSurface(Vector3 point, out float depth) => DepthBelowSurface(point, out depth, out _);

        static readonly Collider[] overlaps = new Collider[8];
        static WaterVolume currentWater;

        static GTPlayer.LiquidType Liquid => currentWater ? currentWater.LiquidType : GTPlayer.LiquidType.Water;

        static bool InAir => Liquid == GTPlayer.LiquidType.SwimInAir;

        static readonly AccessTools.FieldRef<GTPlayer, List<WaterCurrent>> activeCurrentsRef =
            AccessTools.FieldRefAccess<GTPlayer, List<WaterCurrent>>("activeWaterCurrents");

        readonly List<WaterCurrent> currents = new List<WaterCurrent>();

        static bool DepthBelowSurface(Vector3 point, out float depth, out string source)
        {
            depth = 0;
            source = "not in water";

            int waterLayer = GTPlayer.Instance.waterLayer.value;
            if (waterLayer == 0)
                waterLayer = LayerMask.GetMask("Water");

            int count = Physics.OverlapSphereNonAlloc(point, .05f * Scale, overlaps, waterLayer, QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                var collider = overlaps[i];
                var water = collider.GetComponentInParent<WaterVolume>();

                if (!water)
                    continue;

                currentWater = water;

                if (water.GetSurfaceQueryForPoint(point, out WaterVolume.SurfaceQuery query, false))
                {
                    depth = query.surfacePoint.y - point.y;
                    source = "volume surface";
                }
                else if (collider.Raycast(new Ray(point + Vector3.up * 50, Vector3.down), out RaycastHit hit, 100))
                {
                    depth = hit.point.y - point.y;
                    source = "collider top";
                }
                else
                {
                    depth = collider.bounds.max.y - point.y;
                    source = "collider bounds";
                }

                return true;
            }

            return false;
        }

        static bool wasInWater;

        public static void CheckEnter()
        {
            var plugin = Plugin.Instance;
            var rig = Rig.Instance;

            if (!rig)
                return;

            bool inWater = DepthBelowSurface(rig.body.position, out float d,out string source);

            if (inWater != wasInWater)
            {
                wasInWater = inWater;

                Logging.Info(inWater ? "Entered water:" : "Left water", inWater ? $"depth {d:0.00} ({source}), on ground: {rig.onGround}, walking: {plugin.IsWalking}, game thinks in water: {GTPlayer.Instance.InWater}" : "");
            }

            if (!plugin.IsWalking || Time.time - lastLeap < .6f)
                return;

            if (!DepthBelowSurface(rig.body.position, out float depth) ||
                Liquid == GTPlayer.LiquidType.Lava)
                return;

            if (InAir ? !rig.onGround : (depth > EnterDepth * Scale || !rig.onGround))
            {
                var swim = plugin.swimAnimator as SwimAnimator;
                swim.returnTo = Rig.Instance.Animator;
                Rig.Instance.Animator = swim;
            }
        }

        void Leave()
        {
            Rig.Instance.Animator = returnTo ? returnTo : Plugin.Instance.walkAnimator;
        }

        public override void Setup()
        {
            HeadDriver.Instance.LockCursor = true;
            HeadDriver.Instance.turn = true;

            velocity = rigidbody ? rigidbody.linearVelocity : Vector3.zero;

            strokeWeight = 0;
        }

        public override void Cleanup()
        {
            base.Cleanup();
            leftHand.followRate = rightHand.followRate = .1f;
        }

        void Update()
        {
            if (!Plugin.Instance.Enabled || rig == null)
                return;

            if (!DepthBelowSurface(body.position, out float depth) || (rig.onGround && (InAir || depth < ExitDepth * Scale)))
            {
                Leave();
                return;
            }

            // A / Cross leaps out at the surface
            if (!InAir && CurrentGamepad != null && CurrentGamepad.buttonSouth.wasPressedThisFrame && depth < LeapDepth * Scale)
            {
                lastLeap = Time.time;

                Vector3 horizontal = Vector3.ProjectOnPlane(rigidbody.linearVelocity, Vector3.up);

                Leave();

                rigidbody.linearVelocity = horizontal + Vector3.up * LeapSpeed * Scale;
            }
        }

        public override void Animate()
        {
            if (rig == null)
                return;

            rig.active = true;
            rig.useGravity = false;

            DepthBelowSurface(body.position, out float depth);

            // Left stick controls horizontal swimming
            Vector2 stick = LeftStickInput;

            // A swims up, B swims down
            float verticalInput = 0f;

            if (SwimUp)
                verticalInput += 1f;

            if (SwimDown)
                verticalInput -= 1f;

            Vector3 input = new Vector3(stick.x,verticalInput, stick.y);

            bool sprinting = IsSprinting;

            float speed = SwimSpeed * (sprinting ? SprintMultiplier : 1f) * Scale;

            Vector3 desired = (head.forward * input.z + body.right * input.x + Vector3.up * input.y) * speed;

            // Float back to the surface when not swimming up or down
            if (!InAir && Mathf.Approximately(input.y, 0))
            {
                float floatError = depth - FloatDepth * Scale;
                float bob = Mathf.Sin(Time.time * 1.5f) * .05f * Scale;

                if (!(input.z > 0 && head.forward.y < -.3f))
                {
                    desired.y += Mathf.Clamp((floatError + bob) * Buoyancy, -1, 1) * Scale;
                }
            }

            // Flowing water carries you
            Vector3 flow = Vector3.zero;
            Vector3 push = Vector3.zero;

            foreach (var current in Currents())
            {
                if (current.GetCurrentAtPoint(body.position, velocity, Time.deltaTime, out Vector3 currentVelocity, out Vector3 velocityChange))
                {
                    flow += currentVelocity;
                    push += velocityChange;
                }
            }

            desired += flow;

            velocity = Vector3.Lerp(velocity, desired, Extensions.Smoothing(WaterDrag)) + push;

            rig.targetPosition = body.position + velocity / 4f;

            AnimateHands(input.sqrMagnitude > .01f, sprinting);
        }

        List<WaterCurrent> Currents()
        {
            currents.Clear();

            if (currentWater && currentWater.Current)
                currents.Add(currentWater.Current);

            try
            {
                foreach (var current in activeCurrentsRef(GTPlayer.Instance))
                {
                    if (current && !currents.Contains(current))
                        currents.Add(current);
                }
            }
            catch { }

            return currents;
        }

        void AnimateHands(bool moving, bool sprinting)
        {
            strokeWeight = Mathf.Lerp(strokeWeight, moving ? 1 : 0, Extensions.Smoothing(.08f));

            strokePhase = Mathf.Repeat(strokePhase + Time.deltaTime * StrokeRate * (sprinting ? SprintMultiplier : 1), 1);

            foreach (var hand in new[] { leftHand, rightHand })
            {
                float side = hand.isLeft ? -1 : 1;

                float t = Time.time * 1.5f + (hand.isLeft ? 0 : Mathf.PI);

                var tread = new Vector3(side * (.3f + .05f * Mathf.Cos(t)), -.1f + .06f * Mathf.Sin(t), .15f + .05f * Mathf.Cos(t));

                Breaststroke(strokePhase, side,out Vector3 stroke, out Vector3 strokeNormal);

                Vector3 position = body.TransformPoint(Vector3.Lerp(tread, stroke, strokeWeight));

                Vector3 normal = Vector3.Slerp(body.up, body.TransformDirection(strokeNormal), strokeWeight);

                hand.followInBodySpace = true;
                hand.followRate = .3f;
                hand.Place(position, normal, body.forward);
            }
        }

        static void Breaststroke(float phase, float side, out Vector3 position, out Vector3 normal)
        {
            var chest = new Vector3(side * .1f, -.12f, .15f);
            var reach = new Vector3(side * .08f, -.05f, .55f);
            var sweep = new Vector3(side * .45f, -.15f, .2f);

            Vector3 palmsDown = Vector3.up;
            Vector3 palmsOut = new Vector3(-side, .3f, 0).normalized;

            if (phase < .35f)
            {
                float t = Mathf.SmoothStep(0, 1, phase / .35f);

                position = Vector3.Lerp(chest, reach, t);
                normal = Vector3.Slerp(palmsDown, palmsOut, t);
            }
            else if (phase < .75f)
            {
                float t = Mathf.SmoothStep(0, 1, (phase - .35f) / .4f);

                position = Vector3.Lerp(reach, sweep, t) + new Vector3(side * .1f, 0, 0) * Mathf.Sin(t * Mathf.PI);

                normal = palmsOut;
            }
            else
            {
                float t = Mathf.SmoothStep(0, 1, (phase - .75f) / .25f);

                position = Vector3.Lerp(sweep, chest, t);
                normal = Vector3.Slerp(palmsOut, palmsDown, t);
            }
        }
    }
}