using GorillaLocomotion;
using GorillaNetworking;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using WalkSimulator.Animators;
using WalkSimulator.Rigging;
using WalkSimulator.Tools;
using WalkSimulatorClassic;

namespace WalkSimulator.Menus
{
    public class ComputerGUI : MonoBehaviour
    {
        public static ComputerGUI Instance;
        List<TMP_Text> screens = new List<TMP_Text>();
        AnimatorBase cachedAnimator;
        TMP_Text currentScreen;
        bool inUse;
        const float ViewFieldOfView = 90;

        public bool IsInUse => inUse;

        // The text objects that show the computer screen: one per terminal, plus the main computer's
        void RefreshScreens()
        {
            screens.Clear();
            foreach (var terminal in FindObjectsByType<GorillaComputerTerminal>(FindObjectsSortMode.None))
                if (terminal.myScreenText)
                    screens.Add(terminal.myScreenText);

            var screenText = GorillaComputer.instance.screenText;
            try
            {
                // The main computer pushes its text to whatever is hooked up to this event
                var callback = Traverse.Create(screenText).Field("updateTextCallback").GetValue<UnityEventBase>();
                for (int i = 0; callback != null && i < callback.GetPersistentEventCount(); i++)
                    if (callback.GetPersistentTarget(i) is TMP_Text tmp && !screens.Contains(tmp))
                        screens.Add(tmp);
            }
            catch (Exception e) { Logging.Exception(e); }

            // Fallback: anything currently displaying the computer's screen contents
            if (!string.IsNullOrEmpty(screenText.currentText))
                foreach (var tmp in FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
                    if (tmp.text == screenText.currentText && !screens.Contains(tmp))
                        screens.Add(tmp);
        }

        static Vector3 ScreenCenter(TMP_Text screen)
        {
            var rect = screen.rectTransform;
            return rect.TransformPoint(rect.rect.center);
        }

        bool inRange;
        bool IsInRange()
        {
            Vector3 headPos = GTPlayer.Instance.headCollider.transform.position;
            Transform player = GTPlayer.Instance.transform, rig = GorillaTagger.Instance.offlineVRRig.transform;
            TMP_Text closest = null;
            float closestDistance = 2;
            foreach (var screen in screens)
            {
                if (!screen || !screen.isActiveAndEnabled) continue;
                // Skip anything carried by the player
                if (screen.transform.IsChildOf(player) || screen.transform.IsChildOf(rig)) continue;
                Vector3 center = ScreenCenter(screen);
                // Text is read from its -forward side; don't trigger from behind the monitor
                if (Vector3.Dot(-screen.transform.forward, headPos - center) <= 0) continue;
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
            return closest;
        }


        void Awake()
        {
            Instance = this;

        }

        void Start()
        {
            BuildButtonMap();
            RefreshScreens();
        }

        // How often to recheck which screen (if any) you're in front of. Done on a timer: tying it to
        // frame numbers inside FixedUpdate could skip checks for a long time, leaving a stale result
        const float RangeCheckInterval = .25f, ScreenRefreshInterval = 10f;
        float nextRangeCheck, nextScreenRefresh;

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
                }
            }

            // A / Cross: Use computer
            if (gamepad.buttonSouth.wasPressedThisFrame &&
                !inUse && (inRange = IsInRange()))
            {
                inUse = true;
                cachedAnimator = Rig.Instance.Animator;
                Rig.Instance.Animator = null;
                return;
            }

            if (inUse)
            {
                // Y / Triangle: Exit computer
                if (!inRange || !currentScreen || gamepad.buttonNorth.wasPressedThisFrame)
                {
                    Exit();
                    return;
                }

                foreach (var pair in buttonMapping)
                {
                    try
                    {
                        ButtonControl control = GetControllerButton(gamepad, pair.Key);

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
        }

        // Look at the screen through the game's own camera: square up with it and back off just far
        // enough to fit all of it
        void LateUpdate()
        {
            if (!inUse || !currentScreen) return;
            var rect = currentScreen.rectTransform;
            Vector3 scale = rect.lossyScale;
            float halfWidth = rect.rect.width * Mathf.Abs(scale.x) / 2;
            float halfHeight = rect.rect.height * Mathf.Abs(scale.y) / 2;
            float tanHalfFov = Mathf.Tan(ViewFieldOfView * Mathf.Deg2Rad / 2);
            var camera = ShoulderCameraControl.Camera;
            float aspect = camera ? camera.aspect : 16f / 9f;
            float distance = Mathf.Max(halfHeight, halfWidth / aspect) / tanHalfFov * 1.15f;
            ShoulderCameraControl.Take(this, ViewFieldOfView);
            ShoulderCameraControl.Place(ScreenCenter(currentScreen) - rect.forward * distance, Quaternion.LookRotation(rect.forward, rect.up));
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
            GUI.Label(new Rect(20, 20, 200, 200), text, new GUIStyle()
            {
                fontSize = 20,
                normal = new GUIStyleState()
                {
                    textColor = Color.white
                }
            });
            return;

        }

        Dictionary<GamepadButton, GorillaKeyboardButton> buttonMapping = new Dictionary<GamepadButton, GorillaKeyboardButton>();

        Dictionary<string, GamepadButton> controllerMapping = new Dictionary<string, GamepadButton>()
        {
            // Computer controls
            {"enter", GamepadButton.South},
            {"delete", GamepadButton.East},
            
            // Navigation
            {"up", GamepadButton.DpadNorth},
            {"down", GamepadButton.DpadSouth},
            
            // Computer options
            {"option1", GamepadButton.LeftShoulder},
            {"option2", GamepadButton.RightShoulder},
        };

        void BuildButtonMap()
        {
            buttonMapping.Clear();

            foreach (var button in FindObjectsByType<GorillaKeyboardButton>(FindObjectsSortMode.None))
            {
                if (button.characterString == null)
                    continue;

                if (!controllerMapping.TryGetValue(button.characterString, out GamepadButton gamepadButton))
                    continue;

                if (!buttonMapping.ContainsKey(gamepadButton))
                    buttonMapping.Add(gamepadButton, button);
            }
        }

        Dictionary<string, GamepadButton> controllerMapping = new Dictionary<string, GamepadButton>()
        {
            // Main buttons
            {"A", GamepadButton.South},
            {"B", GamepadButton.East},
            {"X", GamepadButton.West},
            {"Y", GamepadButton.North},
            
            // Shoulder buttons
            {"LB", GamepadButton.LeftShoulder},
            {"RB", GamepadButton.RightShoulder},
            
            // Triggers (analog controls; handle separately)
     
            // Stick clicks
    {"L3", GamepadButton.LeftStick},
    {"R3", GamepadButton.RightStick},

    // Menu buttons
    {"Start", GamepadButton.Start},
    {"Select", GamepadButton.Select},

    // D-Pad
    {"up", GamepadButton.DpadNorth},
    {"down", GamepadButton.DpadSouth},
    {"left", GamepadButton.DpadWest},
    {"right", GamepadButton.DpadEast},
};
    }
}
