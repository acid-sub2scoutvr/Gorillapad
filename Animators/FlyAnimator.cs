using GorillaLocomotion;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using Gorillapad.Rigging;
using Gorillapad.Tools;

namespace Gorillapad.Animators
{
    public class FlyAnimator : AnimatorBase
    {
        float speed = 1;
        float minSpeed = .25f;                  // never zero, or you couldn't move at all
        float maxSpeed = 5;
        float speedPerNotch = .25f;             // how much each scroll-wheel notch changes speed
        public float Speed => speed;
        int layersBackup;
        bool noClipActive = false;
        public bool NoClip => noClipActive;

        void Awake()
        {
            layersBackup = GTPlayer.Instance.locomotionEnabledLayers;
        }

        public override void Animate()
        {
            AnimateBody();
            AnimateHands();
        }

        void Update()
        {
            speed += InputHandler.ScrollNotches * speedPerNotch;
            speed = Mathf.Clamp(speed, minSpeed, maxSpeed);
            if(Keyboard.current.nKey.wasPressedThisFrame)
            {
                noClipActive = !noClipActive;
                GTPlayer.Instance.locomotionEnabledLayers = noClipActive ? 1 << 29 : layersBackup;
                GTPlayer.Instance.headCollider.isTrigger = noClipActive;
                GTPlayer.Instance.bodyCollider.isTrigger = noClipActive;
            }
        }

        float sprintMultiplier = 3;
        float CurrentSpeed => Keyboard.current.leftShiftKey.isPressed ? speed * sprintMultiplier : speed;

        // 0 = floating in place, 1 = flying (airplane arms), smoothed
        float flyBlend;
        float bank;

        void AnimateBody()
        {
            rig.active = true;
            rig.useGravity = false;
            rig.targetPosition = body.TransformPoint(InputHandler.inputDirection * CurrentSpeed);
            // Gentle bob while hovering, like floating in water
            float scale = GTPlayer.Instance.scale;
            rig.targetPosition += Gravity.Up * Mathf.Sin(Time.time * 1.5f) * .04f * scale * (1 - flyBlend);
        }

        void AnimateHands()
        {
            Vector3 input = InputHandler.inputDirection;
            bool moving = input != Vector3.zero && speed > 0;
            flyBlend = Mathf.Lerp(flyBlend, moving ? 1 : 0, Extensions.Smoothing(.06f));

            // Bank into strafes and mouse turns (negative tips the right wing down)
            float turn = HeadDriver.Instance.turn ? Mouse.current.delta.value.x : 0;
            float targetBank = Mathf.Clamp(-input.x * 30f - turn * .5f, -40, 40);
            bank = Mathf.Lerp(bank, targetBank, Extensions.Smoothing(.1f));

            foreach (var hand in new[] { leftHand, rightHand })
            {
                float side = hand.isLeft ? -1 : 1;

                // Floating: slow small circles near the body, like treading water (hands out of phase)
                float t = Time.time * 1.5f + (hand.isLeft ? 0 : Mathf.PI);
                var floating = new Vector3(
                    side * (.3f + .05f * Mathf.Cos(t)),
                    -.15f + .07f * Mathf.Sin(t),
                    .15f + .05f * Mathf.Cos(t));

                // Flying: arms straight out, tipped by the bank, lifted when climbing, slight flutter
                Vector3 wing = Quaternion.AngleAxis(bank, Vector3.forward) * new Vector3(side * .65f, 0, 0);
                wing += new Vector3(0, input.y * .1f - .05f + .02f * Mathf.Sin(Time.time * 6 + side), -.05f);

                Vector3 position = body.TransformPoint(Vector3.Lerp(floating, wing, flyBlend));
                Vector3 floatNormal = body.up;
                Vector3 wingNormal = Quaternion.AngleAxis(bank, body.forward) * body.up;
                hand.followInBodySpace = true;
                hand.followRate = .2f;
                hand.Place(position, Vector3.Slerp(floatNormal, wingNormal, flyBlend), body.forward);
            }
        }

        public override void Cleanup()
        {
            base.Cleanup();
            leftHand.followRate = rightHand.followRate = .1f;
            flyBlend = bank = 0;
            GTPlayer.Instance.locomotionEnabledLayers = layersBackup;
            // Leaving noclip on would leave the colliders as triggers, which (among other things)
            // water ignores
            noClipActive = false;
            GTPlayer.Instance.headCollider.isTrigger = false;
            GTPlayer.Instance.bodyCollider.isTrigger = false;
        }

        public override void Setup()
        {
            HeadDriver.Instance.LockCursor = true;
            HeadDriver.Instance.turn = true;
        }
    }
}
