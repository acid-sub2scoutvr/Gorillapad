
using GorillaLocomotion;
using GorillaNetworking;
using HarmonyLib;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Gorillapad.Animators;
using Gorillapad.Rigging;
using Gorillapad.Tools;
using GorillapadClassic;

namespace Gorillapad.Menus
{
    public class ComputerGUI : MonoBehaviour
    {
        public static ComputerGUI Instance;

        List<TMP_Text> screens = new List<TMP_Text>();
        AnimatorBase cachedAnimator;
        TMP_Text currentScreen;

        bool inUse;
        bool inRange;

        const float ViewFieldOfView = 90;
        const float RangeCheckInterval = .25f;
        const float ScreenRefreshInterval = 10f;

        float nextRangeCheck;
        float nextScreenRefresh;

        public bool IsInUse => inUse;

        // Maps gamepad buttons to the game's computer buttons.
        Dictionary<GamepadButton, GorillaKeyboardButton> buttonMapping = new Dictionary<GamepadButton, GorillaKeyboardButton>();

        // Maps GorillaKeyboardButton character strings to controller buttons.
        Dictionary<string, GamepadButton> controllerMapping = new Dictionary<string, GamepadButton>()
        {
            { "enter", GamepadButton.South },          // A / Cross
            { "delete", GamepadButton.East },          // B / Circle

            { "up", GamepadButton.DpadUp },
            { "down", GamepadButton.DpadDown },
            { "left", GamepadButton.DpadLeft },
            { "right", GamepadButton.DpadRight },

            { "option1", GamepadButton.LeftShoulder }, // LB
            { "option2", GamepadButton.RightShoulder } // RB
        };

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            BuildButtonMap();
            RefreshScreens();
        }

        void RefreshScreens()
        {
            screens.Clear();

            foreach (var terminal in FindObjectsByType<GorillaComputerTerminal>(FindObjectsSortMode.None))
            {
                if (terminal.myScreenText)
                    screens.Add(terminal.myScreenText);
            }

            var screenText = GorillaComputer.instance.screenText;

            try
            {
                var callback = Traverse.Create(screenText).Field("updateTextCallback").GetValue<UnityEventBase>();

                for (int i = 0;
                     callback != null && i < callback.GetPersistentEventCount();
                     i++)
                {
                    if (callback.GetPersistentTarget(i) is TMP_Text tmp && !screens.Contains(tmp))
                    {
                        screens.Add(tmp);
                    }
                }
            }
            catch (Exception e)
            {
                Logging.Exception(e);
            }

            if (!string.IsNullOrEmpty(screenText.currentText))
            {
                foreach (var tmp in FindObjectsByType<TMP_Text>(
                    FindObjectsSortMode.None))
                {
                    if (tmp.text == screenText.currentText &&
                        !screens.Contains(tmp))
                    {
                        screens.Add(tmp);
                    }
                }
            }
        }

        static Vector3 ScreenCenter(TMP_Text screen)
        {
            var rect = screen.rectTransform;
            return rect.TransformPoint(rect.rect.center);
        }

        bool IsInRange()
        {
            Vector3 headPos = GTPlayer.Instance.headCollider.transform.position;

            Transform player = GTPlayer.Instance.transform;
            Transform rig = GorillaTagger.Instance.offlineVRRig.transform;

            TMP_Text closest = null;
            float closestDistance = 2f;

            foreach (var screen in screens)
            {
                if (!screen || !screen.isActiveAndEnabled)
                    continue;

                // Don't activate computers carried by the player.
                if (screen.transform.IsChildOf(player) || screen.transform.IsChildOf(rig))
                    continue;

                Vector3 center = ScreenCenter(screen);

                // Don't activate the computer from behind its screen.
                if (Vector3.Dot(-screen.transform.forward, headPos - center) <= 0)
                    continue;

                float distance = headPos.Distance(center);

                if (distance < closestDistance)
                {
                    closest = screen;
                    closestDistance = distance;
                }
            }

            if (closest && closest != currentScreen)
                Logging.Info("Computer screen in range:", closest.transform.GetPath());

            currentScreen = closest;

            return closest != null;
        }

        void Update()
        {
            Gamepad gamepad = Gamepad.current;

            if (gamepad == null)
            {
                if (inUse)
                    Exit();

                return;
            }

            if (Time.time >= nextRangeCheck)
            {
                nextRangeCheck = Time.time + RangeCheckInterval;
                inRange = IsInRange();

                if (!inRange && Time.time >= nextScreenRefresh)
                {
                    nextScreenRefresh = Time.time + ScreenRefreshInterval;

                    RefreshScreens();
                    BuildButtonMap();
                }
            }

            if (gamepad.buttonSouth.wasPressedThisFrame && !inUse && (inRange = IsInRange()))
            {
                inUse = true;

                cachedAnimator = Rig.Instance.Animator;
                Rig.Instance.Animator = null;

                return;
            }

            if (!inUse)
                return;

            if (!inRange || !currentScreen ||gamepad.buttonNorth.wasPressedThisFrame)
            {
                Exit();
                return;
            }

            foreach (var pair in buttonMapping)
            {
                try
                {
                    ButtonControl control =GetControllerButton(gamepad, pair.Key);

                    if (control != null && control.wasPressedThisFrame)
                    {
                        GorillaComputer.instance.PressButton(pair.Value.Binding);

                        Sounds.Play(66, .5f);
                    }
                }
                catch (Exception e)
                {
                    Logging.Exception(e);
                }
            }
        }

        ButtonControl GetControllerButton(Gamepad gamepad, GamepadButton button)
        {
            switch (button)
            {
                case GamepadButton.South:
                    return gamepad.buttonSouth;

                case GamepadButton.East:
                    return gamepad.buttonEast;

                case GamepadButton.West:
                    return gamepad.buttonWest;

                case GamepadButton.North:
                    return gamepad.buttonNorth;

                case GamepadButton.LeftShoulder:
                    return gamepad.leftShoulder;

                case GamepadButton.RightShoulder:
                    return gamepad.rightShoulder;

                case GamepadButton.LeftStick:
                    return gamepad.leftStickButton;

                case GamepadButton.RightStick:
                    return gamepad.rightStickButton;

                case GamepadButton.Start:
                    return gamepad.startButton;

                case GamepadButton.Select:
                    return gamepad.selectButton;

                case GamepadButton.DpadUp:
                    return gamepad.dpad.up;

                case GamepadButton.DpadDown:
                    return gamepad.dpad.down;

                case GamepadButton.DpadLeft:
                    return gamepad.dpad.left;

                case GamepadButton.DpadRight:
                    return gamepad.dpad.right;

                default:
                    return null;
            }
        }

        void BuildButtonMap()
        {
            buttonMapping.Clear();

            foreach (var button in FindObjectsByType<GorillaKeyboardButton>(FindObjectsSortMode.None))
            {
                if (button.characterString == null)
                    continue;

                if (!controllerMapping.TryGetValue(button.characterString,out GamepadButton gamepadButton))
                {
                    continue;
                }

                if (!buttonMapping.ContainsKey(gamepadButton))
                {
                    buttonMapping.Add(gamepadButton, button);
                }
            }
        }

        void LateUpdate()
        {
            if (!inUse || !currentScreen)
                return;

            var rect = currentScreen.rectTransform;

            Vector3 scale = rect.lossyScale;

            float halfWidth = rect.rect.width * Mathf.Abs(scale.x) / 2;

            float halfHeight = rect.rect.height * Mathf.Abs(scale.y) / 2;

            float tanHalfFov = Mathf.Tan(ViewFieldOfView * Mathf.Deg2Rad / 2);

            var camera = ShoulderCameraControl.Camera;

            float aspect = camera ? camera.aspect : 16f / 9f;

            float distance = Mathf.Max(halfHeight, halfWidth / aspect) / tanHalfFov * 1.15f;

            ShoulderCameraControl.Take(this, ViewFieldOfView);

            ShoulderCameraControl.Place(ScreenCenter(currentScreen) - rect.forward * distance,Quaternion.LookRotation(rect.forward, rect.up));
        }

        void Exit()
        {
            inUse = false;

            ShoulderCameraControl.Release(this);

            Rig.Instance.Animator = cachedAnimator;
        }

        void OnGUI()
        {
            string text = "";

            if (inRange)
            {
                text = inUse ? "Press [Y / TRIANGLE] to exit" : "Press [A / CROSS] to use computer";
            }

            GUI.Label(new Rect(20, 20, 300, 200), text, new GUIStyle()
                {
                    fontSize = 20,
                    normal = new GUIStyleState()
                    {
                        textColor = Color.white
                    }
                });
        }
    }
}
