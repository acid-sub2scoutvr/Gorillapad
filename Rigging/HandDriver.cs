
using GorillaLocomotion;
using System;
using System.Collections;
using UnityEngine;
using Gorillapad.Patches;
using Gorillapad.Tools;

namespace Gorillapad.Rigging
{
    public class HandDriver : MonoBehaviour
    {
        public bool grip, trigger, primary, secondary;
        public bool isLeft, grounded;
        public bool hideControllerTransform = true;

        public Vector3 targetPosition, lookAt, up;
        public float followRate = .1f;

        // Smooth relative to the body instead of the world.
        public bool followInBodySpace;

        bool wasFollowingInBodySpace;
        Vector3 bodySpacePosition;

        public Vector3 hit, lastSnap, normal;

        VRMap handMap;
        Vector3 defaultOffset;

        Transform head, body, controller;

        public void Init(bool isLeft)
        {
            this.isLeft = isLeft;

            defaultOffset = new Vector3(
                isLeft ? -.25f : .25f,
                -.45f,
                .2f
            );

            head = Rig.Instance.head;
            body = Rig.Instance.body;

            controller = GTPlayer.Instance.GetControllerTransform(isLeft);

            handMap = isLeft
                ? GorillaTagger.Instance.offlineVRRig.leftHand
                : GorillaTagger.Instance.offlineVRRig.rightHand;

            transform.position = DefaultPosition;
            targetPosition = DefaultPosition;
            lastSnap = DefaultPosition;
            hit = DefaultPosition;
            up = Vector3.up;
        }

        void FixedUpdate()
        {
            if (!body || !controller)
                return;

            if (followInBodySpace)
            {
                if (!wasFollowingInBodySpace)
                {
                    bodySpacePosition =
                        body.InverseTransformPoint(transform.position);
                }

                bodySpacePosition = Vector3.Lerp(
                    bodySpacePosition,
                    body.InverseTransformPoint(targetPosition),
                    Extensions.Smoothing(followRate)
                );

                transform.position =
                    body.TransformPoint(bodySpacePosition);
            }
            else
            {
                transform.position = Vector3.Lerp(
                    transform.position,
                    targetPosition,
                    Extensions.Smoothing(followRate)
                );
            }

            wasFollowingInBodySpace = followInBodySpace;

            transform.LookAt(lookAt, up);

            if (hideControllerTransform)
                controller.position = body.position;
            else
                controller.position = transform.position;

            // Apply the forced finger states to the correct hand.
            if (isLeft)
            {
                FingerPatch.forceLeftGrip = grip;
                FingerPatch.forceLeftPrimary = primary;
                FingerPatch.forceLeftSecondary = secondary;
                FingerPatch.forceLeftTrigger = trigger;
            }
            else
            {
                FingerPatch.forceRightGrip = grip;
                FingerPatch.forceRightPrimary = primary;
                FingerPatch.forceRightSecondary = secondary;
                FingerPatch.forceRightTrigger = trigger;
            }
        }

        void OnEnable()
        {
            if (!body || Rig.Instance.Animator == null)
                return;

            transform.position = DefaultPosition;
            targetPosition = DefaultPosition;

            try
            {
                handMap.overrideTarget = transform;
            }
            catch (Exception e)
            {
                Logging.Exception(e);
            }
        }

        // Return control of the hand to the normal VR controller.
        void OnDisable()
        {
            try
            {
                if (handMap != null &&
                    handMap.overrideTarget == transform)
                {
                    handMap.overrideTarget = null;
                }
            }
            catch (Exception e)
            {
                Logging.Exception(e);
            }

            ResetButtonStates();

            followInBodySpace = false;
            wasFollowingInBodySpace = false;
        }

        void ResetButtonStates()
        {
            grip = false;
            trigger = false;
            primary = false;
            secondary = false;

            if (isLeft)
            {
                FingerPatch.forceLeftGrip = false;
                FingerPatch.forceLeftTrigger = false;
                FingerPatch.forceLeftPrimary = false;
                FingerPatch.forceLeftSecondary = false;
            }
            else
            {
                FingerPatch.forceRightGrip = false;
                FingerPatch.forceRightTrigger = false;
                FingerPatch.forceRightPrimary = false;
                FingerPatch.forceRightSecondary = false;
            }
        }

        IEnumerator Disable(Action<HandDriver> onDisable)
        {
            transform.position = DefaultPosition;

            yield return new WaitForSeconds(.1f);

            handMap.overrideTarget = null;
            enabled = false;

            onDisable?.Invoke(this);
        }

        // Places the hand flat against a surface.
        // Palm faces -normal, with knuckles along forwardHint.
        public void Place(
            Vector3 position,
            Vector3 normal,
            Vector3 forwardHint,
            float curl = 0
        )
        {
            targetPosition = position;

            Vector3 forward =
                Vector3.ProjectOnPlane(forwardHint, normal).normalized;

            if (forward == Vector3.zero)
                forward = body.forward;

            Vector3 right = Vector3.Cross(normal, forward);

            forward =
                Quaternion.AngleAxis(curl, right) * forward;

            lookAt = position + forward;

            up = isLeft ? right : -right;
        }

        public void Reset()
        {
            followInBodySpace = false;
            wasFollowingInBodySpace = false;

            ResetButtonStates();

            targetPosition = DefaultPosition;
            lookAt = targetPosition + body.forward;

            up = isLeft ? body.right : -body.right;

            hideControllerTransform = true;
            grounded = false;
        }

        public Vector3 DefaultPosition
        {
            get
            {
                return body.TransformPoint(defaultOffset);
            }
        }
    }
}
