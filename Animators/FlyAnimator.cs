
using GorillaLocomotion;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Gorillapad.Rigging;
using Gorillapad.Tools;

namespace Gorillapad.Animators
{
    public class FlyAnimator : AnimatorBase
    {
        float speed = 1f;
        float minSpeed = .25f;
        float maxSpeed = 5f;
        float speedPerNotch = .25f;

        public float Speed => speed;

        int layersBackup;
        bool noClipActive = false;

        public bool NoClip => noClipActive;

        static Gamepad CurrentGamepad => Gamepad.current;

        static Vector2 LeftStickInput =>
            CurrentGamepad != null
                ? Vector2.ClampMagnitude(CurrentGamepad.leftStick.ReadValue(), 1f)
                : Vector2.zero;

        static bool FlyUp =>
            CurrentGamepad != null &&
            CurrentGamepad.buttonSouth.isPressed;

        static bool FlyDown =>
            CurrentGamepad != null &&
            CurrentGamepad.buttonEast.isPressed;

        static bool IsSprinting =>
            CurrentGamepad != null &&
            CurrentGamepad.leftStickButton.isPressed;

        float VerticalInput =>
            (FlyUp ? 1f : 0f) - (FlyDown ? 1f : 0f);

        void Awake()
        {
            layersBackup = GTPlayer.Instance.locomotionEnabledLayers;
        }

        public override void Setup()
        {
            HeadDriver.Instance.LockCursor = true;
            HeadDriver.Instance.turn = true;
        }

        void Update()
        {
            if (CurrentGamepad != null)
            {
                // D-Pad changes flight speed.
                if (CurrentGamepad.dpad.up.wasPressedThisFrame)
                    speed += speedPerNotch;

                if (CurrentGamepad.dpad.down.wasPressedThisFrame)
                    speed -= speedPerNotch;

                // Y toggles noclip.
                if (CurrentGamepad.buttonNorth.wasPressedThisFrame)
                    ToggleNoClip();
            }

            speed = Mathf.Clamp(speed, minSpeed, maxSpeed);
        }

        void ToggleNoClip()
        {
            noClipActive = !noClipActive;

            GTPlayer.Instance.locomotionEnabledLayers =
                noClipActive ? 1 << 29 : layersBackup;

            GTPlayer.Instance.headCollider.isTrigger = noClipActive;
            GTPlayer.Instance.bodyCollider.isTrigger = noClipActive;
        }

        float sprintMultiplier = 3f;

        float CurrentSpeed =>
            IsSprinting
                ? speed * sprintMultiplier
                : speed;

        // 0 = floating in place, 1 = flying (airplane arms), smoothed.
        float flyBlend;
        float bank;

        void AnimateBody()
        {
            rig.active = true;
            rig.useGravity = false;

            Vector2 stick = LeftStickInput;

            Vector3 input = new Vector3(
                stick.x,
                VerticalInput,
                stick.y
            );

            rig.targetPosition =
                body.TransformPoint(input * CurrentSpeed);

            // Gentle bob while hovering.
            float scale = GTPlayer.Instance.scale;

            rig.targetPosition +=
                Gravity.Up *
                Mathf.Sin(Time.time * 1.5f) *
                .04f *
                scale *
                (1 - flyBlend);
        }

        void AnimateHands()
        {
            Vector2 stick = LeftStickInput;

            Vector3 input = new Vector3(
                stick.x,
                VerticalInput,
                stick.y
            );

            bool moving = input.sqrMagnitude > .01f && speed > 0;

            flyBlend = Mathf.Lerp(
                flyBlend,
                moving ? 1f : 0f,
                Extensions.Smoothing(.06f)
            );

            // Bank into left-stick strafes.
            float targetBank =
                Mathf.Clamp(-input.x * 30f, -40f, 40f);

            bank = Mathf.Lerp(
                bank,
                targetBank,
                Extensions.Smoothing(.1f)
            );

            foreach (var hand in new[] { leftHand, rightHand })
            {
                float side = hand.isLeft ? -1f : 1f;

                // Floating: slow small circles near the body.
                float t =
                    Time.time * 1.5f +
                    (hand.isLeft ? 0f : Mathf.PI);

                var floating = new Vector3(
                    side * (.3f + .05f * Mathf.Cos(t)),
                    -.15f + .07f * Mathf.Sin(t),
                    .15f + .05f * Mathf.Cos(t)
                );

                // Flying: arms straight out, tipped by the bank.
                Vector3 wing =
                    Quaternion.AngleAxis(bank, Vector3.forward) *
                    new Vector3(side * .65f, 0f, 0f);

                wing += new Vector3(
                    0f,
                    input.y * .1f - .05f +
                    .02f * Mathf.Sin(Time.time * 6f + side),
                    -.05f
                );

                Vector3 position =
                    body.TransformPoint(
                        Vector3.Lerp(floating, wing, flyBlend)
                    );

                Vector3 floatNormal = body.up;

                Vector3 wingNormal =
                    Quaternion.AngleAxis(bank, body.forward) * body.up;

                hand.followInBodySpace = true;
                hand.followRate = .2f;

                hand.Place(
                    position,
                    Vector3.Slerp(floatNormal, wingNormal, flyBlend),
                    body.forward
                );
            }
        }

        public override void Animate()
        {
            AnimateBody();
            AnimateHands();
        }

        public override void Cleanup()
        {
            base.Cleanup();

            leftHand.followRate = rightHand.followRate = .1f;

            flyBlend = bank = 0f;

            GTPlayer.Instance.locomotionEnabledLayers = layersBackup;

            noClipActive = false;

            GTPlayer.Instance.headCollider.isTrigger = false;
            GTPlayer.Instance.bodyCollider.isTrigger = false;
        }
    }
}
