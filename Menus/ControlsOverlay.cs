
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Gorillapad.Animators;
using Gorillapad.Rigging;

namespace Gorillapad.Menus
{
    /// <summary>
    /// Lists the controls for the current mode in the bottom-left corner.
    /// Start toggles the overlay.
    /// </summary>
    public class ControlsOverlay : MonoBehaviour
    {
        GUIStyle boxStyle, textStyle;

        void Update()
        {
            Gamepad gamepad = Gamepad.current;

            if (gamepad != null && gamepad.startButton.wasPressedThisFrame && (ComputerGUI.Instance == null || !ComputerGUI.Instance.IsInUse))
            {
                Plugin.ShowControls.Value = !Plugin.ShowControls.Value;
            }
        }

        List<(string key, string action)> CurrentControls()
        {
            var lines = new List<(string, string)>();

            var plugin = Plugin.Instance;
            var animator = Rig.Instance ? Rig.Instance.Animator : null;

            if (ComputerGUI.Instance && ComputerGUI.Instance.IsInUse)
            {
                lines.Add(("Computer", ""));
                lines.Add(("D-Pad", "Navigate"));
                lines.Add(("A / Cross", "Select / Enter"));
                lines.Add(("B / Circle", "Delete / Back"));
                lines.Add(("LB / RB", "Computer options"));
                lines.Add(("Y / Triangle", "Leave computer"));
                return lines;
            }

            if (plugin.radialMenu && plugin.radialMenu.isActiveAndEnabled)
            {
                lines.Add(("Mode Menu", ""));
                lines.Add(("D-Pad", "Navigate modes"));
                lines.Add(("A / Cross", "Select mode"));
                lines.Add(("B / Circle", "Back"));
                return lines;
            }

            if (!animator)
            {
                lines.Add(("Vr Mode", ""));
                lines.Add(("Start", "Show / hide controls"));
                return lines;
            }

            if (animator == plugin.walkAnimator)
            {
                lines.Add(("Walk", ""));
                lines.Add(("Left Stick", "Move"));
                lines.Add(("Right Stick", "Look"));
                lines.Add(("L3", "Sprint"));
                lines.Add(("A / Cross", "Jump"));
                lines.Add(("A + toward wall", "Wall jump"));
                lines.Add(("B / Circle", "Crouch"));
                lines.Add(("LT / RT", "Grab left / right"));
            }
            else if (animator == plugin.flyAnimator)
            {
                var fly = plugin.flyAnimator as FlyAnimator;

                lines.Add(("Fly", ""));
                lines.Add(("Left Stick", "Move"));
                lines.Add(("A / Cross", "Fly up"));
                lines.Add(("B / Circle", "Fly down"));
                lines.Add(("L3", "Fly faster"));
                lines.Add(("D-Pad Up / Down", "Flight speed"));
                lines.Add(("Y / Triangle", "Toggle noclip: " +
                    (fly != null && fly.NoClip ? "On" : "Off")));
            }
            else if (animator == plugin.swimAnimator)
            {
                lines.Add(("Swim", ""));
                lines.Add(("Left Stick", "Swim"));
                lines.Add(("A / Cross", "Swim up"));
                lines.Add(("B / Circle", "Swim down"));
                lines.Add(("L3", "Swim faster"));
                lines.Add(("A at surface", "Leap out"));
            }
            else if (animator == plugin.ziplineAnimator)
            {
                lines.Add(("Zipline", ""));
                lines.Add(("Left Stick", "Pump / swing"));
                lines.Add(("A / Cross", "Jump off"));
                lines.Add(("Release LT / RT", "Let go"));
            }
            else if (animator == plugin.ropeAnimator)
            {
                lines.Add(("Rope", ""));
                lines.Add(("Left Stick", "Pump / swing"));
                lines.Add(("A / Cross", "Jump off"));
                lines.Add(("Release LT / RT", "Let go"));
            }
            else if (animator == plugin.gliderAnimator)
            {
                lines.Add(("Glider", ""));
                lines.Add(("Left Stick", "Roll / dive / climb"));
                lines.Add(("Right Stick", "Look / turn"));
                lines.Add(("LT / RT", "Hold glider / release"));
            }
            else if (animator == plugin.broomAnimator)
            {
                lines.Add(("Broomstick", ""));
                lines.Add(("Left Stick", "Pump / swing"));
                lines.Add(("A / Cross", "Jump off"));
                lines.Add(("Release LT / RT", "Let go"));
            }
            else if (animator == plugin.hoverboardAnimator)
            {
                lines.Add(("Hoverboard", ""));
                lines.Add(("Right Stick", "Steer"));
                lines.Add(("Left Stick", "Carve / paddle"));
                lines.Add(("A / Cross", "Hop"));
                lines.Add(("Release LT / RT", "Let go"));
            }
            else if (animator == plugin.emoteAnimator)
            {
                lines.Add(("Emote", ""));
                lines.Add(("D-Pad", "Select emote"));
                lines.Add(("Repeat / move", "Stop emote"));
            }
            else if (animator == plugin.handAnimator)
            {
                lines.Add(("Pose", ""));
                lines.Add(("Right Stick", "Move hand"));
                lines.Add(("D-Pad Up / Down", "Hand forward / back"));
                lines.Add(("LB / RB", "Rotate hand"));
                lines.Add(("Y / Triangle", "Switch hands"));
                lines.Add(("LT / RT", "Trigger / grip"));
                lines.Add(("D-Pad Left / Right", "Thumb buttons"));
            }
            else if (animator == plugin.grabAnimator)
            {
                lines.Add(("Interact", ""));
                lines.Add(("LT / RT", "Press with left / right hand"));
                lines.Add(("Left Stick Up / Down", "Drag slider"));
            }

            lines.Add(("", ""));
            lines.Add(("General Controls", ""));
            lines.Add(("Start", "Show / hide controls"));
            lines.Add(("D-Pad", "Navigate menus"));
            lines.Add(("A / Cross", "Confirm / interact"));
            lines.Add(("B / Circle", "Back / cancel"));
            lines.Add(("X / Square", "First / third person"));
            lines.Add(("Y / Triangle", "Gameplay mode"));
            lines.Add(("LB / RB", "Cycle emotes"));
            lines.Add(("View / Select", "Mode menu"));

            return lines;
        }

        void OnGUI()
        {
            if (!Plugin.ShowControls.Value ||
                !Plugin.Instance.Enabled)
                return;

            if (boxStyle == null)
            {
                var background = new Texture2D(1, 1);
                background.SetPixel(0, 0, new Color(0, 0, 0, .55f));
                background.Apply();

                boxStyle = new GUIStyle()
                {
                    normal = new GUIStyleState()
                    {
                        background = background
                    }
                };

                textStyle = new GUIStyle()
                {
                    fontSize = 16,
                    richText = true,
                    normal = new GUIStyleState()
                    {
                        textColor = Color.white
                    }
                };
            }

            var lines = CurrentControls();

            const float lineHeight = 22;
            const float padding = 10;
            const float keyWidth = 190;
            const float width = 490;

            float height = lines.Count * lineHeight + padding * 2;

            var area = new Rect(
                20,
                Screen.height - height - 20,
                width,
                height
            );

            GUI.Box(area, GUIContent.none, boxStyle);

            float y = area.y + padding;

            foreach (var (key, action) in lines)
            {
                if (action == "" && key != "")
                {
                    GUI.Label(
                        new Rect(area.x + padding, y, width, lineHeight),
                        $"<b><size=18>{key}</size></b>",
                        textStyle
                    );
                }
                else
                {
                    GUI.Label(new Rect(area.x + padding, y, keyWidth, lineHeight), $"<b>{key}</b>", textStyle);
                    GUI.Label(new Rect(area.x + padding + keyWidth,y, width - keyWidth, lineHeight),action, textStyle);
                }

                y += lineHeight;
            }
        }
    }
}
