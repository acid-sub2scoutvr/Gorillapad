using GorillaLocomotion;
using Photon.Voice;
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
        public override void Animate()
        {
            rig.headDriver.turn = false;
            AnimateBody();
            AnimateHands();
        }

        void Update()
        {
            if (Keyboard.current.qKey.wasPressedThisFrame)
            {
                var temp = main;
                main = secondary;
                secondary = temp;
            }

            if (Keyboard.current.rKey.isPressed)
                RotateHand();
            else
                PositionHand();

            Vector3 lookAt = main.isLeft ? lookAtLeft : lookAtRight;
            float zRotation = main.isLeft ? zRotationLeft : zRotationRight;
            float flip = main.isLeft ? -1 : 1;
            main.up = Quaternion.AngleAxis(zRotation * flip, lookAt) * head.up;

            main.trigger = Mouse.current.leftButton.isPressed;
            main.grip = Mouse.current.rightButton.isPressed;
            main.primary = Mouse.current.backButton.isPressed || Keyboard.current.leftBracketKey.isPressed;
            main.secondary = Mouse.current.forwardButton.isPressed || Keyboard.current.rightBracketKey.isPressed;
        }

        // How far one unit of scroll moves the hand forward/back, and rolls it while rotating
        const float ScrollReachSpeed = .008f, ScrollRollSpeed = 1.6f;

        Vector3 eulerAngles;
        void RotateHand()
        {
            eulerAngles.x = eulerAngles.x - Mouse.current.delta.value.y / 10f;
            if (eulerAngles.x > 180)
                eulerAngles.x -= 360;
            //clamp rotation between -85 and 85 
            eulerAngles.x = Mathf.Clamp(eulerAngles.x, -85, 85);

            eulerAngles.y = eulerAngles.y + Mouse.current.delta.value.x / 10f;
            if (eulerAngles.y > 180)
                eulerAngles.y -= 360;
            //clamp rotation between -85 and 85
            eulerAngles.y = Mathf.Clamp(eulerAngles.y, -85, 85);

            if (main.isLeft)
            {
                lookAtLeft = Quaternion.Euler(eulerAngles) * head.forward;
                zRotationLeft += Mouse.current.scroll.ReadValue().y * ScrollRollSpeed;
            }
            else
            {
                lookAtRight = Quaternion.Euler(eulerAngles) * head.forward;
                zRotationRight += Mouse.current.scroll.ReadValue().y * ScrollRollSpeed;
            }
        }

        void PositionHand()
        {
            Vector3 offset = main.isLeft ? offsetLeft : offsetRight;
            offset.z += Mouse.current.scroll.ReadValue().y * ScrollReachSpeed;
            if (Keyboard.current.upArrowKey.wasPressedThisFrame)
                offset.z += .1f;
            if (Keyboard.current.downArrowKey.wasPressedThisFrame)
                offset.z -= .1f;
            offset.z = Mathf.Clamp(offset.z, -.25f, .75f);

            offset.x += Mouse.current.delta.ReadValue().x / 1000f;
            offset.x = Mathf.Clamp(offset.x, -.5f, .5f);
            offset.y += Mouse.current.delta.ReadValue().y / 1000f;
            offset.y = Mathf.Clamp(offset.y, -.5f, .5f);
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
            float flip = main.isLeft ? -1 : 1;
            Vector3 offset = main.isLeft ? offsetLeft : offsetRight;
            Vector3 lookAt = main.isLeft ? lookAtLeft : lookAtRight;
            main.targetPosition = body.TransformPoint(new Vector3(flip * .2f, 0.1f, .3f) + offset);
            main.lookAt = main.targetPosition + lookAt;
            //main.up = body.up;
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

            secondary.targetPosition = secondary.DefaultPosition + Vector3.up * .2f * GTPlayer.Instance.scale;
            secondary.lookAt = secondary.targetPosition + head.forward;
            secondary.up = body.right * (main.isLeft ? -1 : 1);
        }
    }
}
