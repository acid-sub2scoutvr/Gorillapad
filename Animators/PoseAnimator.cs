
using GorillaLocomotion;
using UnityEngine;
using UnityEngine.InputSystem;
using Gorillapad.Rigging;

namespace Gorillapad.Animators
{
    public class PoseAnimator : AnimatorBase
    {
        Vector3 offsetLeft, lookAtLeft = Vector3.forward;
        Vector3 offsetRight, lookAtRight = Vector3.forward;

        float zRotationLeft, zRotationRight;

        HandDriver main, secondary;

        static Gamepad CurrentGamepad => Gamepad.current;

        Vector2 RightStick => CurrentGamepad != null ? Vector2.ClampMagnitude(CurrentGamepad.rightStick.ReadValue(), 1f) : Vector2.zero;

        public override void Animate()
        {
            rig.headDriver.turn = false;

            AnimateBody();
            AnimateHands();
        }

        void Update()
        {
            if (CurrentGamepad == null || main == null)
                return;

            // Y / Triangle switches selected hands.
            if (CurrentGamepad.buttonNorth.wasPressedThisFrame)
            {
                var temp = main;
                main = secondary;
                secondary = temp;
            }

            // LB / RB rotate the selected hand.
            float rotationInput = 0f;

            if (CurrentGamepad.leftShoulder.isPressed)
                rotationInput -= 1f;

            if (CurrentGamepad.rightShoulder.isPressed)
                rotationInput += 1f;

            if (rotationInput != 0f)
                RotateHand(rotationInput);
            else
                PositionHand();

            Vector3 lookAt = main.isLeft ? lookAtLeft : lookAtRight;
            float zRotation = main.isLeft ? zRotationLeft : zRotationRight;
            float flip = main.isLeft ? -1 : 1;

            main.up =
                Quaternion.AngleAxis(zRotation * flip, lookAt) * head.up;

            // LT = trigger, RT = grip.
            main.trigger = CurrentGamepad.leftTrigger.isPressed;
            main.grip = CurrentGamepad.rightTrigger.isPressed;

            // D-Pad left/right = primary/secondary.
            main.primary = CurrentGamepad.dpad.left.isPressed;
            main.secondary = CurrentGamepad.dpad.right.isPressed;
        }

        const float StickReachSpeed = .01f;
        const float RotationSpeed = 100f;

        Vector3 eulerAngles;

        void RotateHand(float direction)
        {
            eulerAngles.z += direction * RotationSpeed * Time.deltaTime;

            if (main.isLeft)
            {
                lookAtLeft = Quaternion.Euler(eulerAngles) * head.forward;

                zRotationLeft = eulerAngles.z;
            }
            else
            {
                lookAtRight = Quaternion.Euler(eulerAngles) * head.forward;

                zRotationRight = eulerAngles.z;
            }
        }

        void PositionHand()
        {
            Vector3 offset = main.isLeft ? offsetLeft : offsetRight;

            Vector2 stick = RightStick;

            // Right stick moves the hand horizontally and vertically.
            offset.x += stick.x * StickReachSpeed;
            offset.y += stick.y * StickReachSpeed;

            // D-Pad up/down moves the hand forward/back.
            if (CurrentGamepad.dpad.up.wasPressedThisFrame)
                offset.z += .1f;

            if (CurrentGamepad.dpad.down.wasPressedThisFrame)
                offset.z -= .1f;

            offset.x = Mathf.Clamp(offset.x, -.5f, .5f);
            offset.y = Mathf.Clamp(offset.y, -.5f, .5f);
            offset.z = Mathf.Clamp(offset.z, -.25f, .75f);

            if (main.isLeft)
                offsetLeft = offset;
            else
                offsetRight = offset;
        }

        void AnimateBody()
        {
            rig.active = true;
            rig.useGravity = false;
            rig.targetPosition = body.position;
        }

        void AnimateHands()
        {
            if (main == null)
                return;

            float flip = main.isLeft ? -1 : 1;

            Vector3 offset = main.isLeft ? offsetLeft : offsetRight;

            Vector3 lookAt = main.isLeft ? lookAtLeft : lookAtRight;

            main.targetPosition = body.TransformPoint(new Vector3(flip * .2f, 0.1f, .3f) + offset);

            main.lookAt = main.targetPosition + lookAt;
            main.hideControllerTransform = false;
        }

        public override void Setup()
        {
            base.Start();

            HeadDriver.Instance.LockCursor = true;

            main = rightHand;
            secondary = leftHand;

            offsetLeft = Vector3.zero;
            lookAtLeft = head.forward;

            offsetRight = Vector3.zero;
            lookAtRight = head.forward;

            eulerAngles = Vector3.zero;

            secondary.targetPosition = secondary.DefaultPosition + Vector3.up * .2f * GTPlayer.Instance.scale;

            secondary.lookAt = secondary.targetPosition + head.forward;

            secondary.up = body.right * (main.isLeft ? -1 : 1);
        }
    }
}
