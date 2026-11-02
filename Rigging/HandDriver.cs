using GorillaLocomotion;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
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
        // Smooth relative to the body instead of the world, so the hand keeps up when the body
        // moves fast (falling, flying). Use for poses that hang off the body, not planted hands.
        public bool followInBodySpace;
        bool wasFollowingInBodySpace;
        Vector3 bodySpacePosition;
        public Vector3 hit, lastSnap, normal;

        VRMap handMap;
        Vector3 defaultOffset;
        Transform head, body, controller;

        //void Start()
        //{
            
        //}

        public void Init(bool isLeft)
        {
            this.isLeft = isLeft;
            defaultOffset = new Vector3(isLeft ? -.25f : .25f, -.45f, .2f);
            head = Rig.Instance.head;
            body = Rig.Instance.body;
            controller = GTPlayer.Instance.GetControllerTransform(isLeft);
            handMap = isLeft ?
                GorillaTagger.Instance.offlineVRRig.leftHand :
                GorillaTagger.Instance.offlineVRRig.rightHand;
            transform.position = DefaultPosition;
            targetPosition = DefaultPosition;
            lastSnap = DefaultPosition;
            hit = DefaultPosition;
            up = Vector3.up;
        }

        void FixedUpdate()
        {
            if (followInBodySpace)
            {
                if (!wasFollowingInBodySpace)
                    bodySpacePosition = body.InverseTransformPoint(transform.position);
                bodySpacePosition = Vector3.Lerp(bodySpacePosition, body.InverseTransformPoint(targetPosition), Extensions.Smoothing(followRate));
                transform.position = body.TransformPoint(bodySpacePosition);
            }
            else
                transform.position = Vector3.Lerp(transform.position, targetPosition, Extensions.Smoothing(followRate));
            wasFollowingInBodySpace = followInBodySpace;
            transform.LookAt(lookAt, up);
            if(hideControllerTransform)
                controller.position = body.position;
            else
                controller.position = transform.position;

            //DebugPoint.Get(this.name + "target", Color.red).position = targetPosition;
            //DebugPoint.Get(this.name + "hit", Color.blue).position = hit;
            //DebugPoint.Get(this.name + "snap", Color.green).position = lastSnap;
            //DebugPoint.Get(this.name + "lookAt", lookAt,  Color.magenta);
            //DebugPoint.Get(this.name + "up", targetPosition + up * .1f, Color.cyan);


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
            if (!body || Rig.Instance.Animator is null) return;
            transform.position = DefaultPosition;
            targetPosition = DefaultPosition;
            try
            {
                handMap.overrideTarget = this.transform;
            }
            catch (Exception e) { Logging.Exception(e); }
        }

        // Switching back to VR: give the hand back to the controller and let go of any buttons the
        // mod was holding for it
        void OnDisable()
        {
            try
            {
                if (handMap != null && handMap.overrideTarget == transform)
                    handMap.overrideTarget = null;
            }
            catch (Exception e) { Logging.Exception(e); }
            grip = trigger = primary = secondary = false;
            followInBodySpace = false;
            if (isLeft)
                FingerPatch.forceLeftGrip = FingerPatch.forceLeftTrigger = FingerPatch.forceLeftPrimary = FingerPatch.forceLeftSecondary = false;
            else
                FingerPatch.forceRightGrip = FingerPatch.forceRightTrigger = FingerPatch.forceRightPrimary = FingerPatch.forceRightSecondary = false;
        }

        IEnumerator Disable(Action<HandDriver> onDisable)
        {
            transform.position = DefaultPosition;
            yield return new WaitForSeconds(0.1f);
            handMap.overrideTarget = null;
            this.enabled = false;
            onDisable?.Invoke(this);
        }

        // Places the hand flat against a surface (palm toward -normal) with its knuckles pointing
        // along forwardHint, curled forward by curl degrees
        public void Place(Vector3 position, Vector3 normal, Vector3 forwardHint, float curl = 0)
        {
            targetPosition = position;
            Vector3 forward = Vector3.ProjectOnPlane(forwardHint, normal).normalized;
            if (forward == Vector3.zero) forward = body.forward;
            Vector3 right = Vector3.Cross(normal, forward);
            forward = Quaternion.AngleAxis(curl, right) * forward;
            lookAt = position + forward;
            up = isLeft ? right : -right;
        }

        public void Reset()
        {
            followInBodySpace = false;
            grip = trigger = primary = false;
            targetPosition = DefaultPosition;
            lookAt = targetPosition + body.forward;
            up = isLeft ? body.right : -body.right;
            hideControllerTransform = true;
            grounded = false;
        }
        

        public Vector3 DefaultPosition { get { return body.TransformPoint(defaultOffset); } }


    }
}
