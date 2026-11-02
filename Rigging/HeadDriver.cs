
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using GorillaLocomotion;
using Gorillapad.Tools;

using Thirpy = Unity.Cinemachine.Cinemachine3rdPersonFollow;

namespace Gorillapad.Rigging
{
    public class HeadDriver : MonoBehaviour
    {
        public static HeadDriver Instance;

        public Transform thirpyTarget, head;

        Vector3 offset = Vector3.zero;

        public bool turn = true;

        bool _lockCursor;

        // Gamepad camera sensitivity, in degrees per second.
        const float HorizontalLookSensitivity = 180f;
        const float VerticalLookSensitivity = 120f;

        static Gamepad CurrentGamepad => Gamepad.current;

        static Vector2 LookInput =>
            CurrentGamepad != null
                ? CurrentGamepad.rightStick.ReadValue()
                : Vector2.zero;

        static bool FirstPersonPressed =>
            CurrentGamepad != null &&
            CurrentGamepad.buttonWest.wasPressedThisFrame;

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

        // Find the transform followed by the shoulder camera.
        Transform FindFollowTarget()
        {
            var thirpy =
                FindAnyObjectByType<Thirpy>(
                    FindObjectsInactive.Include
                );

            if (thirpy && thirpy.FollowTarget)
                return thirpy.FollowTarget;

            var follow =
                FindAnyObjectByType<GorillaCameraFollow>(
                    FindObjectsInactive.Include
                );

            if (follow && follow.cinemachineCamera)
                return follow.cinemachineCamera.Follow;

            Logging.Warning(
                "Couldn't find the shoulder camera's follow target; " +
                "vertical look won't move the monitor view"
            );

            return null;
        }

        void LateUpdate()
        {
            if (!head)
                return;

            AlignCameraWithGravity();

            // X toggles first-person and third-person.
            if (FirstPersonPressed)
                ToggleCam();

            // First person: use the game's camera from the head.
            // Leave the camera alone while the game has an overlay open.
            if (firstPerson && !GTPlayer.Instance.inOverlay)
            {
                ShoulderCameraControl.Take(
                    this,
                    FirstPersonFieldOfView
                );

                ShoulderCameraControl.Place(
                    GTPlayer.Instance.headCollider.transform
                        .TransformPoint(offset),
                    Quaternion.LookRotation(head.forward, Gravity.Up)
                );
            }
            else
            {
                ShoulderCameraControl.Release(this);
            }

            // Gamepad camera movement.
            if (turn)
            {
                Vector2 look = LookInput;
                float dt = Time.deltaTime;

                // Horizontal look.
                if (Mathf.Abs(look.x) > .01f)
                {
                    GTPlayer.Instance.Turn(
                        look.x * HorizontalLookSensitivity * dt
                    );
                }

                // Vertical look.
                pitch = Mathf.Clamp(
                    pitch - look.y * VerticalLookSensitivity * dt,
                    -80f,
                    85f
                );

                // Build the camera rotation relative to gravity.
                Vector3 up = Gravity.Up;

                Vector3 facing = Vector3.ProjectOnPlane(
                    GTPlayer.Instance.bodyCollider.transform.forward,
                    up
                );

                if (facing.sqrMagnitude < .0001f)
                {
                    facing = Vector3.ProjectOnPlane(
                        head.forward,
                        up
                    );
                }

                if (facing.sqrMagnitude > .0001f)
                {
                    Quaternion level =
                        Quaternion.LookRotation(facing, up);

                    GorillaTagger.Instance.offlineVRRig
                        .headConstraint.rotation =
                        level * Quaternion.Euler(pitch, 0, 0);

                    // Limit the third-person camera pitch to prevent
                    // the shoulder camera orbit from flipping.
                    if (thirpyTarget)
                    {
                        thirpyTarget.rotation =
                            level * Quaternion.Euler(
                                Mathf.Clamp(
                                    pitch,
                                    -ThirdPersonPitchLimit,
                                    ThirdPersonPitchLimit
                                ),
                                0,
                                0
                            );
                    }
                }

                GTPlayer.Instance.headCollider.transform.localEulerAngles =
                    new Vector3(pitch, 0, 0);
            }
        }

        void OverrideHeadMovement()
        {
            head = GorillaTagger.Instance.offlineVRRig
                .head.rigTarget;
        }

        bool firstPerson;
        float pitch;

        const float ThirdPersonPitchLimit = 60f;

        // Keep the shoulder camera aligned with the current gravity direction.
        CinemachineBrain brain;
        Transform gravityUp, previousWorldUp;

        void AlignCameraWithGravity()
        {
            if (!brain)
            {
                var camera = ShoulderCameraControl.Camera;

                brain = camera
                    ? camera.GetComponentInChildren<CinemachineBrain>(true)
                    : null;

                if (!brain)
                    return;

                previousWorldUp = brain.WorldUpOverride;
            }

            if (!gravityUp)
            {
                gravityUp =
                    new GameObject("WalkSim Gravity Up").transform;
            }

            gravityUp.rotation =
                Quaternion.FromToRotation(Vector3.up, Gravity.Up);

            brain.WorldUpOverride = gravityUp;
        }

        const float FirstPersonFieldOfView = 90f;

        internal void ToggleCam()
        {
            firstPerson = !firstPerson;
        }

        public bool FirstPerson
        {
            get
            {
                return firstPerson;
            }
            set
            {
                firstPerson = value;
            }
        }

        void OnEnable()
        {
            Logging.Debug("Enabled");

            if (Rig.Instance.Animator == null)
                return;

            LockCursor = true;
            OverrideHeadMovement();
        }

        void OnDisable()
        {
            ShoulderCameraControl.Release(this);

            if (brain && brain.WorldUpOverride == gravityUp)
                brain.WorldUpOverride = previousWorldUp;

            if (!head)
                return;

            LockCursor = false;

            GorillaTagger.Instance.offlineVRRig
                .head.rigTarget = head;
        }
    }
}
