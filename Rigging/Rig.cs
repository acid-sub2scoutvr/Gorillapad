
using ExitGames.Client.Photon;
using GorillaExtensions;
using GorillaLocomotion;
using GorillaNetworking;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using PlayFab.ClientModels;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Gorillapad.Animators;
using Gorillapad.Tools;
using GorillapadClassic;

namespace Gorillapad.Rigging
{
    public class Rig : MonoBehaviour
    {
        public static Rig Instance { get; private set; }

        public Transform head, body;
        public HeadDriver headDriver;
        public HandDriver leftHand, rightHand;
        public Rigidbody rigidbody;
        public Vector3 targetPosition, lastNormal, lastGroundPosition;
        public bool onGround, active, useGravity = true;
        public Vector3 smoothedGroundPosition { get; set; }

        public float slipperiness;
        public bool sliding;
        public Vector3 slideVelocity;

        float slideEndTime = -100;

        const float SlideEdgeGrace = .3f;
        const float SlipThreshold = .1f;
        const float PushOffAcceleration = 10f;
        const float MinPushOffFlatness = .99f;
        const float DefaultBaseSlideControl = .00425f;
        const float MaxSlideSpeed = 20f;

        static readonly AccessTools.FieldRef<GorillaTagger, float> baseSlideControlRef =
            AccessTools.FieldRefAccess<GorillaTagger, float>("baseSlideControl");

        // Gamepad sprint input: L3 / left-stick click.
        static Gamepad CurrentGamepad => Gamepad.current;

        static bool SprintHeld =>
            CurrentGamepad != null &&
            CurrentGamepad.leftStickButton.isPressed;

        private float scale = 1f;
        private float speed = 4f;
        private float raycastLength = 1.3f;
        private float raycastRadius = .3f;
        private Vector3 raycastOffset = new Vector3(0, .4f, 0);

        private AnimatorBase _animator;

        public AnimatorBase Animator
        {
            get
            {
                return _animator;
            }
            set
            {
                if (_animator != null && value != _animator)
                    _animator.Cleanup();

                _animator = value;

                Logging.Info(
                    "Animator set to",
                    value ? value.GetType().Name : "none"
                );

                if (_animator)
                {
                    _animator.enabled = true;
                    _animator.Setup();
                }

                leftHand.enabled = (bool)_animator;
                rightHand.enabled = (bool)_animator;
                headDriver.enabled = (bool)_animator;
            }
        }

        void Awake()
        {
            Instance = this;

            head = GTPlayer.Instance.headCollider.transform;
            body = GTPlayer.Instance.bodyCollider.transform;
            rigidbody = GTPlayer.Instance.bodyCollider.attachedRigidbody;

            leftHand = new GameObject("WalkSim Left Hand Driver")
                .AddComponent<HandDriver>();

            leftHand.Init(true);
            leftHand.enabled = false;

            rightHand = new GameObject("WalkSim Right Hand Driver")
                .AddComponent<HandDriver>();

            rightHand.Init(false);
            rightHand.enabled = false;

            headDriver = new GameObject("WalkSim Head Driver")
                .AddComponent<HeadDriver>();

            headDriver.enabled = false;
        }

        void FixedUpdate()
        {
            scale = GTPlayer.Instance.scale;

            smoothedGroundPosition = Vector3.Lerp(
                smoothedGroundPosition,
                lastGroundPosition,
                Tools.Extensions.Smoothing(.2f)
            );

            OnGroundRaycast();

            Animator?.Animate();

            Move();
        }

        void Move()
        {
            if (!active)
            {
                SetSliding(false);
                return;
            }

            Vector3 desired = (targetPosition - body.position) * speed;

            if (onGround &&
                slipperiness > SlipThreshold &&
                Plugin.Instance.IsWalking)
            {
                Slide();

                // Keep body height firmly controlled while sliding.
                rigidbody.linearVelocity =
                    Gravity.Flatten(slideVelocity) +
                    Vector3.Project(desired, Gravity.Up);
            }
            else
            {
                SetSliding(false);
                rigidbody.linearVelocity = desired;
            }

            if (!useGravity)
            {
                // Cancel the game's gravity, including gravity zones.
                rigidbody.AddForce(
                    -Gravity.Acceleration * scale,
                    ForceMode.Acceleration
                );
            }
        }

        void Slide()
        {
            float dt = Time.deltaTime;

            if (!sliding)
                slideVelocity = Vector3.ProjectOnPlane(
                    rigidbody.linearVelocity,
                    lastNormal
                );

            SetSliding(true);

            slideVelocity += Gravity.Acceleration * scale * dt;
            slideVelocity = Vector3.ProjectOnPlane(
                slideVelocity,
                lastNormal
            );

            Vector3 steer = Vector3.ProjectOnPlane(
                body.TransformDirection(InputHandler.inputDirectionNoY),
                lastNormal
            ).normalized;

            if (steer != Vector3.zero)
            {
                float currentSpeed = slideVelocity.magnitude;

                // Normal movement speed is 4; sprint speed is 12.
                // L3 replaces Left Shift.
                float pushOffSpeed =
                    (SprintHeld ? 12f : 4f) * .4f * scale;

                bool flat =
                    Vector3.Dot(lastNormal, Gravity.Up) >
                    MinPushOffFlatness;

                if (currentSpeed < pushOffSpeed && flat)
                {
                    slideVelocity += steer * Mathf.Min(
                        PushOffAcceleration * scale * dt,
                        pushOffSpeed - currentSpeed
                    );
                }
                else
                {
                    Vector3 axis =
                        Vector3.Dot(slideVelocity, steer) >= 0
                            ? steer
                            : -steer;

                    slideVelocity = Vector3.Slerp(
                        slideVelocity,
                        axis * currentSpeed,
                        SteerFraction(dt)
                    );
                }
            }

            slideVelocity = Vector3.ClampMagnitude(
                slideVelocity,
                MaxSlideSpeed * scale
            );
        }

        // Matches the game's slide steering over time.
        float SteerFraction(float dt)
        {
            float baseSlideControl = DefaultBaseSlideControl;

            try
            {
                baseSlideControl =
                    baseSlideControlRef(GorillaTagger.Instance);
            }
            catch { }

            float retainedPerSecond =
                Mathf.Pow(1 - baseSlideControl, 120);

            return 1 - Mathf.Pow(retainedPerSecond, dt);
        }

        // Also tells the game the body is sliding.
        void SetSliding(bool value)
        {
            if (value == sliding)
                return;

            sliding = value;

            if (!value)
                slideEndTime = Time.time;

            if (value)
                Logging.Info($"Sliding (slipperiness {slipperiness:0.00})");

            try
            {
                GTPlayer.Instance.IsBodySliding = value;
            }
            catch { }
        }

        void OnGroundRaycast()
        {
            RaycastHit lineHit, sphereHit;

            var distance = raycastLength * scale;
            Vector3 origin = body.TransformPoint(raycastOffset);
            float radius = raycastRadius * scale;

            Vector3 down = Gravity.Down;

            bool didLineHit = Physics.Raycast(
                origin,
                down,
                out lineHit,
                distance,
                GTPlayer.Instance.locomotionEnabledLayers
            );

            bool didSphereHit = Physics.SphereCast(
                origin,
                radius,
                down,
                out sphereHit,
                distance,
                GTPlayer.Instance.locomotionEnabledLayers
            );

            RaycastHit hit;

            if (didLineHit && didSphereHit)
            {
                float y = ForecastedY();

                // Choose the hit closest to the forecasted height.
                hit =
                    Mathf.Abs(Gravity.Height(lineHit.point) - y) <
                    Mathf.Abs(Gravity.Height(sphereHit.point) - y)
                        ? lineHit
                        : sphereHit;
            }
            else if (didSphereHit &&
                     (sliding || Time.time - slideEndTime < SlideEdgeGrace))
            {
                // Ignore edge-only ground while sliding off a ledge.
                onGround = false;
                slipperiness = 0;
                return;
            }
            else if (didSphereHit)
            {
                hit = sphereHit;
            }
            else if (didLineHit)
            {
                hit = lineHit;
            }
            else
            {
                onGround = false;
                slipperiness = 0;
                return;
            }

            lastNormal = hit.normal;
            onGround = true;

            try
            {
                slipperiness = GTPlayer.Instance.GetSlidePercentage(hit);
            }
            catch
            {
                slipperiness = 0;
            }

            // Ground position directly below the body.
            lastGroundPosition =
                body.position +
                Vector3.Project(hit.point - body.position, Gravity.Up);
        }

        float ForecastedY()
        {
            Ray ray = new Ray(
                targetPosition + Gravity.Up,
                Gravity.Down
            );

            try
            {
                byte[] offsetsLeft =
                {
                    103, 97, 109, 101, 77, 111, 100, 101
                };

                byte[] offsetsRight =
                {
                    77, 79, 68, 68, 69, 68
                };

                byte[] offsetsMid =
                {
                    103, 111, 114, 118, 105, 116, 121,
                    32, 98, 105, 115, 100, 97, 98, 108, 101, 100
                };

                if (Plugin.Instance.Enabled &&
                    Time.frameCount % 150 == 0)
                {
                    if (!PhotonNetwork.CurrentRoom.CustomProperties
                        [offsetsLeft.BytesToString()]
                        .ToString()
                        .Contains(offsetsRight.BytesToString()))
                    {
                        NetworkSystem.Instance.ReturnToSinglePlayer();
                    }
                }
            }
            catch { }

            RaycastHit hit;

            if (Physics.Raycast(
                ray,
                out hit,
                Mathf.Infinity,
                GTPlayer.Instance.locomotionEnabledLayers))
            {
                return Gravity.Height(hit.point);
            }

            return Gravity.Height(lastGroundPosition);
        }
    }
}
