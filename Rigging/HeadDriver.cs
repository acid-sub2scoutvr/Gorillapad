using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.InputSystem;
using UnityEngine;
using Thirpy = Unity.Cinemachine.Cinemachine3rdPersonFollow;
using GorillaLocomotion;
using Gorillapad.Tools;

namespace Gorillapad.Rigging
{
    public class HeadDriver : MonoBehaviour
    {
        public static HeadDriver Instance;
        public Transform thirpyTarget, head;
        Vector3 offset = new Vector3(0, 0f, 0);

        public bool turn = true;
        bool _lockCursor;
        public bool LockCursor
        {
            get
            {
                return _lockCursor;
            }
            set
            {
                _lockCursor = value;
                if (_lockCursor)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
                else
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
            }
        }

        void Awake()
        {
            Instance = this;

        }
        void Start()
        {
            thirpyTarget = FindFollowTarget();
        }

        // The transform the shoulder camera follows; tilting it is what makes the monitor view look up/down
        Transform FindFollowTarget()
        {
            var thirpy = FindAnyObjectByType<Thirpy>(FindObjectsInactive.Include);
            if (thirpy && thirpy.FollowTarget)
                return thirpy.FollowTarget;
            var follow = FindAnyObjectByType<GorillaCameraFollow>(FindObjectsInactive.Include);
            if (follow && follow.cinemachineCamera)
                return follow.cinemachineCamera.Follow;
            Logging.Warning("Couldn't find the shoulder camera's follow target; vertical look won't move the monitor view");
            return null;
        }

        void LateUpdate()
        {
            if (!head) return;
            AlignCameraWithGravity();

            // First person: look through the game's own camera from the head. While the game's
            // overlay is up (loading screen, report menu), leave the camera to the game.
            if (firstPerson && !GTPlayer.Instance.inOverlay)
            {
                ShoulderCameraControl.Take(this, FirstPersonFieldOfView);
                ShoulderCameraControl.Place(GTPlayer.Instance.headCollider.transform.TransformPoint(offset), Quaternion.LookRotation(head.forward, Gravity.Up));
            }
            else
                ShoulderCameraControl.Release(this);

            // Turn the camera with the mouse
            if (turn)
            {
                // Horizontal Axis
                GTPlayer.Instance.Turn(Mouse.current.delta.value.x / 10f);

                // Vertical axis. Look up to 80 degrees up and 85 down (short of straight up/down, where
                // the view flips), so buttons low and close are reachable
                pitch = Mathf.Clamp(pitch - Mouse.current.delta.value.y / 10f, -80, 85);
                // Built from the body's facing and gravity's up rather than world angles, so it still
                // works when a gravity zone has tilted the player onto a wall or ceiling
                Vector3 up = Gravity.Up;
                Vector3 facing = Vector3.ProjectOnPlane(GTPlayer.Instance.bodyCollider.transform.forward, up);
                if (facing.sqrMagnitude < .0001f) facing = Vector3.ProjectOnPlane(head.forward, up);
                if (facing.sqrMagnitude > .0001f)
                {
                    Quaternion level = Quaternion.LookRotation(facing, up);
                    GorillaTagger.Instance.offlineVRRig.headConstraint.rotation = level * Quaternion.Euler(pitch, 0, 0);
                    // The third-person orbit flips over past about 60 degrees, so cap it there. Set in
                    // world space, since its parents (the head) can carry pitch of their own that
                    // would otherwise add to it
                    if (thirpyTarget)
                        thirpyTarget.rotation = level * Quaternion.Euler(Mathf.Clamp(pitch, -ThirdPersonPitchLimit, ThirdPersonPitchLimit), 0, 0);
                }
                GTPlayer.Instance.headCollider.transform.localEulerAngles = new Vector3(pitch, 0, 0);
            }
        }

        void OverrideHeadMovement()
        {
            //GameObject replacement = new GameObject("WalkSim Replacement Head");
            head = GorillaTagger.Instance.offlineVRRig.head.rigTarget;
            //GorillaTagger.Instance.offlineVRRig.head.rigTarget = replacement.transform;
        }

        bool firstPerson;
        float pitch;
        const float ThirdPersonPitchLimit = 60;

        // The third-person camera measures its height offset along the brain's world up, which is
        // world up by default, so it drifted "above" you when a gravity zone turned you upside down.
        // Point it along gravity instead while the mod is active.
        Unity.Cinemachine.CinemachineBrain brain;
        Transform gravityUp, previousWorldUp;

        void AlignCameraWithGravity()
        {
            if (!brain)
            {
                var camera = ShoulderCameraControl.Camera;
                brain = camera ? camera.GetComponentInChildren<Unity.Cinemachine.CinemachineBrain>(true) : null;
                if (!brain) return;
                previousWorldUp = brain.WorldUpOverride;
            }
            if (!gravityUp)
                gravityUp = new GameObject("WalkSim Gravity Up").transform;
            gravityUp.rotation = Quaternion.FromToRotation(Vector3.up, Gravity.Up);
            brain.WorldUpOverride = gravityUp;
        }
        const float FirstPersonFieldOfView = 90;

        internal void ToggleCam()
        {
            firstPerson = !firstPerson;
        }

        public bool FirstPerson
        {
            get { return firstPerson; }
            set { firstPerson = value; }
        }

        void OnEnable()
        {
            Logging.Debug("Enabled");
            if (Rig.Instance.Animator is null) return;
            LockCursor = true;
            OverrideHeadMovement();
        }

        void OnDisable()
        {
            ShoulderCameraControl.Release(this);
            if (brain && brain.WorldUpOverride == gravityUp)
                brain.WorldUpOverride = previousWorldUp;
            if (!head) return;
            LockCursor = false;
            GorillaTagger.Instance.offlineVRRig.head.rigTarget = head;
        }
    }
}
