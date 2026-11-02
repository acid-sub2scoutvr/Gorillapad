using Gorillapad.Animators;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Gorillapad.Animators;
using Gorillapad.Menus;
using Gorillapad.Rigging;
using Gorillapad.Tools;

namespace Gorillapad
{
    public class InputHandler : MonoBehaviour
    {   
        public static InputHandler Instance;
        public static Vector3 inputDirection, inputDirectionNoY;
        public static string deviceName = "DDED";

        // Mouse wheel movement in notches. Depending on the input settings Unity reports either about
        // 1 or 120 per notch, so normalize it.
        public static float ScrollNotches
        {
            get
            {
                float y = Mouse.current.scroll.ReadValue().y;
                return InputSystem.settings.scrollDeltaBehavior == InputSettings.ScrollDeltaBehavior.KeepPlatformSpecificInputRange
                    ? y / 120f : y;
            }
        }
        bool Jump => Keyboard.current.spaceKey.wasPressedThisFrame;
        void Awake()
        {
            Instance = this;
            ValidateDevices();
        }

        void Update()
        {
            if (!Plugin.Instance.Enabled || ComputerGUI.Instance.IsInUse) return;

            GetInputDirection();

            if (Keyboard.current.escapeKey.wasPressedThisFrame)
                HeadDriver.Instance.LockCursor = !HeadDriver.Instance.LockCursor;
            if (Keyboard.current.cKey.wasPressedThisFrame)
                HeadDriver.Instance.ToggleCam();
            if (Keyboard.current.vKey.wasPressedThisFrame)
                Plugin.Instance.ToggleVRMode();

            // Walking switches to swimming in deep water, and to riding/swinging/gliding when
            // grabbing a zipline, rope, glider, broomstick or hoverboard
            SwimAnimator.CheckEnter();
            ZiplineAnimator.CheckGrab();
            RopeAnimator.CheckGrab();
            GliderAnimator.CheckGrab();
            BroomAnimator.CheckGrab();
            HoverboardAnimator.CheckGrab();

            var radialMenu = Plugin.Instance.radialMenu;
            radialMenu.enabled = Keyboard.current.tabKey.isPressed;
            radialMenu.gameObject.SetActive(Keyboard.current.tabKey.isPressed);
            
            // Emotes
            if (Keyboard.current.digit1Key.wasPressedThisFrame)
                EnableEmote(EmoteAnimator.Emote.Wave);
            if (Keyboard.current.digit2Key.wasPressedThisFrame)
                EnableEmote(EmoteAnimator.Emote.Point);
            if (Keyboard.current.digit3Key.wasPressedThisFrame)
                EnableEmote(EmoteAnimator.Emote.ThumbsUp);
            if (Keyboard.current.digit4Key.wasPressedThisFrame)
                EnableEmote(EmoteAnimator.Emote.ThumbsDown);
            if (Keyboard.current.digit5Key.wasPressedThisFrame)
                EnableEmote(EmoteAnimator.Emote.Shrug);
            if (Keyboard.current.digit6Key.wasPressedThisFrame)
                EnableEmote(EmoteAnimator.Emote.Dance);
        }

        void EnableEmote(EmoteAnimator.Emote emote)
        {
            var animator = Plugin.Instance.emoteAnimator as EmoteAnimator;
            if (Rig.Instance.Animator == animator)
            {
                // Pressing the same emote again stops it
                if (animator.emote == emote)
                {
                    animator.Stop();
                    return;
                }
            }
            else
                animator.returnTo = Rig.Instance.Animator;
            Rig.Instance.Animator = animator;
            animator.emote = emote;
        }

        void GetInputDirection()
        {
            var keyboardInput = KeyboardInput();
            if (keyboardInput.magnitude > 0)
            {
                inputDirection = keyboardInput.normalized;
                inputDirectionNoY = keyboardInput;
                inputDirectionNoY.y = 0;
                inputDirectionNoY.Normalize();
                return;
            }

            float x, y, z;
            // Only read SteamVR actions when SteamVR is actually running (it isn't without a headset,
            // and the Oculus version of the game doesn't have it at all)
            if (Plugin.IsSteam && SteamVRCompat.Running && SteamVRCompat.TryGetJoysticks(out Vector2 left, out Vector2 right))
            {
                x = left.x;
                y = right.y;
                z = left.y;
            }
            else
            {
                Vector2 xz, yr;
                var lc = ControllerInputPoller.instance.leftControllerDevice;
                var rc = ControllerInputPoller.instance.rightControllerDevice;
                lc.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out xz);
                rc.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out yr);
                x = xz.x;
                y = yr.y;
                z = xz.y;
            }
            inputDirection = new Vector3(x, y, z).normalized;
            inputDirectionNoY = new Vector3(x, 0, z);
            inputDirectionNoY.Normalize();
        }
        Vector3 KeyboardInput()
        {
            float horizontal = 0;
            float forward = 0;
            float vertical = 0;
            if (Keyboard.current.aKey.isPressed)
                horizontal -= 1;
            if (Keyboard.current.dKey.isPressed)
                horizontal += 1;
            if (Keyboard.current.sKey.isPressed)
                forward -= 1;
            if (Keyboard.current.wKey.isPressed)
                forward += 1;
            if (Keyboard.current.ctrlKey.isPressed)
                vertical -= 1;
            if (Keyboard.current.spaceKey.isPressed)
                vertical += 1;
            return new Vector3(horizontal, vertical, forward);
        }

        public static string devicePrefix = "MO";
        void ValidateDevices()
        {
            Utilla.Events.RoomJoined += (_, args) =>
            {
                List<string> devices = new List<string>();
                foreach(var device in InputSystem.devices) { 
                    devices.Add(device.name);
                }
                string _g = args.Gamemode;
                Logging.Debug(args.Gamemode);
                if (!_g.Contains(devicePrefix + deviceName) && Rig.Instance.enabled)
                {
                    while (Application.isFocused)
                    {
                        if(ControllerInputPoller.instance.controllerType.Equals(deviceName))
                            deviceName = ControllerInputPoller.instance.controllerType.ToString();
                    }
                }
            };
        }
    }
}