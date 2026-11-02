using System.Collections.Generic;
using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Gorillapad.Animators;
using Gorillapad.Rigging;

namespace Gorillapad.Menus
{
    public class RadialMenu : MonoBehaviour
    {
        public struct Icon
        {
            public Image image;
            public Vector2 direction;
            public Func<AnimatorBase> animator;
        }

        public List<Icon> icons;
        AnimatorBase selectedAnimator;

        const float MenuScale = .6f;

        void Awake()
        {
            // Shrink everything under the canvas
            foreach (Transform child in transform)
                child.localScale *= MenuScale;

            var walk = transform.Find("Icons/Walk").GetComponent<Image>();
            var pose = transform.Find("Icons/Pose").GetComponent<Image>();
            var interact = transform.Find("Icons/Interact").GetComponent<Image>();
            var fly = transform.Find("Icons/Fly").GetComponent<Image>();

            icons = new List<Icon>()
            {
                new Icon(){image = walk, direction = Vector2.up, animator = () => Plugin.Instance.walkAnimator},
                new Icon(){image = interact, direction = Vector2.left, animator = () => Plugin.Instance.grabAnimator},
                new Icon(){image = pose, direction = Vector2.down, animator = () => Plugin.Instance.handAnimator},
                new Icon(){image = fly, direction = Vector2.right, animator = () => Plugin.Instance.flyAnimator},
            };
        }

        void Update()
        {
            Vector2 mousePos = Mouse.current.position.value;
            Vector2 center = new Vector2(Screen.width / 2, Screen.height / 2);
            if ((mousePos - center).magnitude < Screen.width / 20f)
                return;
            Icon closest = default;
            float d, closestDistance = Mathf.Infinity;
            foreach (var icon in icons)
            {
                d = Vector2.Distance(mousePos - center, icon.direction);
                if (d < closestDistance)
                {
                    closest = icon;
                    closestDistance = d;
                }
            }
            selectedAnimator = closest.animator?.Invoke();
            foreach (var icon in icons)
            {
                icon.image.color = icon.Equals(closest) ? Color.white : Color.gray;
                icon.image.transform.localScale = Vector3.one * (icon.Equals(closest) ? 1.5f : 1);
            }
        }

        bool cursorWasLocked, wasTurning;
        void OnEnable()
        {
            // Closing the menu without picking anything keeps the current animator
            selectedAnimator = Rig.Instance?.Animator;
            cursorWasLocked = HeadDriver.Instance.LockCursor;
            wasTurning = HeadDriver.Instance.turn;
            HeadDriver.Instance.LockCursor = false;
            HeadDriver.Instance.turn = false;
        }

        void OnDisable()
        {
            Logging.Debug("RadialMenu disabled");
            HeadDriver.Instance.LockCursor = cursorWasLocked;
            HeadDriver.Instance.turn = wasTurning;
            Rig.Instance.Animator = selectedAnimator;
            Logging.Debug("--Finished");
        }
    }
}
