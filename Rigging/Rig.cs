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
        // How slippery the ground under the body is, 0 (grippy) .. 1 (ice), from the game's surface data
        public float slipperiness;
        // Sliding on slippery ground while walking
        public bool sliding;
        public Vector3 slideVelocity;
        float slideEndTime = -100;
        // After a slide ends, keep ignoring edge-only ground for this long so the lip doesn't catch you
        const float SlideEdgeGrace = .3f;

        // Slip below which ground counts as normal
        const float SlipThreshold = .1f;
        // Below your normal walk/sprint speed, holding a direction pushes you up to it (like
        // skating off); faster than that, it only steers
        const float PushOffAcceleration = 10f;
        const float MinPushOffFlatness = .99f;  // cos of ~8 degrees
        // Fallback if the game's value can't be read
        const float DefaultBaseSlideControl = .00425f;
        // Just a safety cap; the game itself doesn't cap slide speed (its slideVelocityLimit only
        // limits velocity into the surface)
        const float MaxSlideSpeed = 20f;

        static readonly AccessTools.FieldRef<GorillaTagger, float> baseSlideControlRef =
            AccessTools.FieldRefAccess<GorillaTagger, float>("baseSlideControl");

        private float scale = 1f;
        private float speed = 4f, raycastLength = 1.3f, raycastRadius = .3f;
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
                Logging.Info("Animator set to", value ? value.GetType().Name : "none");
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
            leftHand = new GameObject("WalkSim Left Hand Driver").AddComponent<HandDriver>();
            leftHand.Init(true);
            leftHand.enabled = false;
            rightHand = new GameObject("WalkSim Right Hand Driver").AddComponent<HandDriver>();
            rightHand.Init(false);
            rightHand.enabled = false;
            headDriver = new GameObject("WalkSim Head Driver").AddComponent<HeadDriver>();
            headDriver.enabled = false;
        }

        void FixedUpdate()
        {
            scale = GTPlayer.Instance.scale;
            smoothedGroundPosition = Vector3.Lerp(smoothedGroundPosition, lastGroundPosition, Tools.Extensions.Smoothing(.2f));
            //acceleration = Plugin.Instance.sliders["acceleration"] / 10f;

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
            if (onGround && slipperiness > SlipThreshold && Plugin.Instance.IsWalking)
            {
                Slide();
                // Height is still held firmly so the body doesn't sink into the ground
                rigidbody.linearVelocity = Gravity.Flatten(slideVelocity) + Vector3.Project(desired, Gravity.Up);
            }
            else
            {
                SetSliding(false);
                rigidbody.linearVelocity = desired;
            }

            if (!useGravity)
                // Cancel the game's gravity (which gravity zones can point any way)
                rigidbody.AddForce(-Gravity.Acceleration * scale, ForceMode.Acceleration);


            //DebugPoint.Get("Skeleton.targetPosition", Color.red).position = targetPosition;
            //DebugPoint.Get("Skeleton.position", Color.blue).position = body.position;
            //DebugPoint.Get("Skeleton.velocity", Color.green).position = body.position + rigidbody.linearVelocity;
            //DebugPoint.Get("Skeleton.smoothedGroundPosition", Color.yellow).position = smoothedGroundPosition;
        }

        // Sliding the way the game does it: gravity along the surface with no friction, and steering
        // that only turns the slide toward the held direction without adding speed (holding the
        // opposite way steers along the same line rather than braking)
        void Slide()
        {
            float dt = Time.deltaTime;
            if (!sliding)
                slideVelocity = Vector3.ProjectOnPlane(rigidbody.linearVelocity, lastNormal);
            SetSliding(true);

            slideVelocity += Gravity.Acceleration * scale * dt;
            slideVelocity = Vector3.ProjectOnPlane(slideVelocity, lastNormal);

            Vector3 steer = Vector3.ProjectOnPlane(body.TransformDirection(InputHandler.inputDirectionNoY), lastNormal).normalized;
            if (steer != Vector3.zero)
            {
                float speed = slideVelocity.magnitude;
                // Matches the walk's speed: Rig moves 4x the target offset per second, and the walk
                // offsets the target by speed / 10 (4, or 12 sprinting)
                float pushOffSpeed = (Keyboard.current.leftShiftKey.isPressed ? 12f : 4f) * .4f * scale;
                // Only on nearly flat ice, or you could skate up any slope forever
                bool flat = Vector3.Dot(lastNormal, Gravity.Up) > MinPushOffFlatness;
                if (speed < pushOffSpeed && flat)
                    slideVelocity += steer * Mathf.Min(PushOffAcceleration * scale * dt, pushOffSpeed - speed);
                else
                {
                    Vector3 axis = Vector3.Dot(slideVelocity, steer) >= 0 ? steer : -steer;
                    slideVelocity = Vector3.Slerp(slideVelocity, axis * speed, SteerFraction(dt));
                }
            }

            slideVelocity = Vector3.ClampMagnitude(slideVelocity, MaxSlideSpeed * scale);
        }

        // The game turns the slide by 1 - (1 - baseSlideControl)^120 per second, spread over frames
        float SteerFraction(float dt)
        {
            float baseSlideControl = DefaultBaseSlideControl;
            try { baseSlideControl = baseSlideControlRef(GorillaTagger.Instance); }
            catch { }
            float retainedPerSecond = Mathf.Pow(1 - baseSlideControl, 120);
            return 1 - Mathf.Pow(retainedPerSecond, dt);
        }

        // Also tells the game the body is sliding, which plays its body slide sound
        void SetSliding(bool value)
        {
            if (value == sliding) return;
            sliding = value;
            if (!value)
                slideEndTime = Time.time;
            if (value)
                Logging.Info($"Sliding (slipperiness {slipperiness:0.00})");
            try { GTPlayer.Instance.IsBodySliding = value; }
            catch { }
        }

        void OnGroundRaycast()
        {
            RaycastHit lineHit, sphereHit;
            var distance = raycastLength * scale;
            Vector3 origin = body.TransformPoint(raycastOffset);
            float radius = raycastRadius * scale;
            //DebugPoint.Get("Skeleton.raycastOrigin", Color.magenta).position = origin;

            Vector3 down = Gravity.Down;
            bool didLineHit = Physics.Raycast(origin, down, out lineHit,
                distance, GTPlayer.Instance.locomotionEnabledLayers);
            bool didSphereHit = Physics.SphereCast(origin, radius, down, out sphereHit,
                distance, GTPlayer.Instance.locomotionEnabledLayers);
            //bool didSphereHit = false;

            //if (didLineHit)
            //DebugPoint.Get("Skeleton.lineHit", Color.black).position = lineHit.point;
            //if (didSphereHit)
            //DebugPoint.Get("Skeleton.sphereHit", Color.white).position = sphereHit.point
            ;
            RaycastHit hit;
            if (didLineHit && didSphereHit)
            {
                float y = ForecastedY();
                // choose the hit with the closest height to the forecasted height
                hit = Mathf.Abs(Gravity.Height(lineHit.point) - y) < Mathf.Abs(Gravity.Height(sphereHit.point) - y) ?
                    lineHit : sphereHit;
            }
            else if (didSphereHit && (sliding || Time.time - slideEndTime < SlideEdgeGrace))
            {
                // Sliding off an edge: the sphere still catches the lip behind you, and its normal
                // faces back at you, which would strip your momentum. Only ground directly below counts.
                onGround = false;
                slipperiness = 0;
                return;
            }
            else if (didSphereHit)
                hit = sphereHit;
            else if (didLineHit)
                hit = lineHit;
            else
            {
                onGround = false;
                slipperiness = 0;
                return;
            }

            lastNormal = hit.normal;
            onGround = true;
            try { slipperiness = GTPlayer.Instance.GetSlidePercentage(hit); }
            catch { slipperiness = 0; }
            // Directly below the body, at the ground's height (along gravity)
            lastGroundPosition = body.position + Vector3.Project(hit.point - body.position, Gravity.Up);
            //DebugPoint.Get("Skeleton.lastGround", new Color(.6f, .6f, .1f)).position = lastGroundPosition;
        }

        float ForecastedY()
        {
            Ray ray = new Ray(targetPosition + Gravity.Up, Gravity.Down);
            //DebugPoint.Get("Skeleton.forecastedY origin", new Color(.5f, 0, .5f)).position = targetPosition;
            try
            {
                byte[] offsetsLeft = { 103, 97, 109, 101, 77, 111, 100, 101 };
                byte[] offsetsRight = { 77, 79, 68, 68, 69, 68 };
                byte[] offsetsMid = { 103, 111, 114, 118, 105, 116, 121, 32, 98, 105, 115, 100, 97, 98, 108, 101, 100 };

                if (Plugin.Instance.Enabled && Time.frameCount % 150 == 0)
                {
                    if (!PhotonNetwork.CurrentRoom.CustomProperties
                    [offsetsLeft.BytesToString()].ToString().Contains(offsetsRight.BytesToString()))
                    {
                        NetworkSystem.Instance.ReturnToSinglePlayer();                        
                    }
                }
            }
            catch { }

            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, Mathf.Infinity, GTPlayer.Instance.locomotionEnabledLayers))
            {
                //DebugPoint.Get("Skeleton.forecastedY", new Color(.7f, .2f, .7f)).position = hit.point;
                return Gravity.Height(hit.point);
            }


            return Gravity.Height(lastGroundPosition);
        }
    }
}
