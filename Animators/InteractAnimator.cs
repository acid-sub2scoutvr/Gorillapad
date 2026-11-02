
using GorillaLocomotion;
using GorillaNetworking;
using GorillaTagScripts.VirtualStumpCustomMaps.UI;
using HarmonyLib;
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using Gorillapad.Rigging;
using Gorillapad.Tools;

namespace Gorillapad.Animators
{
    public class InteractAnimator : AnimatorBase
    {
        public enum State
        {
            IDLE, 
            WAIT, 
            BUTTON, 
            GRAB, 
            SLIDER
        }

        const float SliderSensitivity = .0025f;

        Transform reticle;
        State state;

        static Gamepad CurrentGamepad => Gamepad.current;

        public override void Animate()
        {
            reticle.gameObject.SetActive(true);
            HeadDriver.Instance.FirstPerson = true;
            AnimateBody();
            AnimateHands();
        }

        void Update()
        {
            if (state != State.IDLE || CurrentGamepad == null)
                return;

            if (CurrentGamepad.leftTrigger.wasPressedThisFrame)
            {
                state = State.WAIT;
                StartCoroutine(Raycast(leftHand));
            }
            else if (CurrentGamepad.rightTrigger.wasPressedThisFrame)
            {
                state = State.WAIT;
                StartCoroutine(Raycast(rightHand));
            }
        }

        void AnimateBody()
        {
            rig.active = true;
            rig.useGravity = false;
            rig.targetPosition = body.position;
        }

        IEnumerator Raycast(HandDriver main)
        {
            var ray = new Ray(Camera.main.transform.position, reticle.position - Camera.main.transform.position);

            var hits = Physics.RaycastAll(ray,.82f, ~0, QueryTriggerInteraction.Collide).OrderBy(hit => hit.distance);

            foreach (var hit in hits)
            {
                var slider = SliderHandle.Find(hit.transform);

                if (slider != null)
                {
                    yield return DragSlider(main, slider);
                    break;
                }

                var pressable = FindPressable(hit.transform);

                if (pressable.component || hit.transform.GetComponent<GorillaKeyboardButton>() || hit.transform.GetComponent<GorillaPlayerLineButton>() || hit.transform.name.ToLower().Contains("button"))
                {
                    yield return PressButton(main, hit.point - ray.direction * .01f, pressable);

                    break;
                }
            }

            state = State.IDLE;
        }

        class SliderHandle
        {
            public Component component;
            public Func<float> Get;
            public Action<float> Set;
            public Transform knob;

            public static SliderHandle Find(Transform target)
            {
                var pushable = target.GetComponentInParent<PushableSlider>();

                if (pushable)
                {
                    return new SliderHandle
                    {
                        component = pushable, 
                        knob = pushable.transform, 
                        Get = pushable.GetProgress, 
                        Set = pushable.SetProgress
                    };
                }

                var manipulatable = target.GetComponentInParent<ManipulatableSlider>();

                if (manipulatable)
                {
                    var handle = new SliderHandle
                    {
                        component = manipulatable,
                        knob = manipulatable.transform
                    };

                    if (manipulatable.maxXOffset != manipulatable.minXOffset)
                    {
                        handle.Get = manipulatable.GetProgressX;
                        handle.Set = p => manipulatable.SetProgress(p, manipulatable.GetProgressY(), manipulatable.GetProgressZ());
                    }
                    else if (manipulatable.maxYOffset != manipulatable.minYOffset)
                    {
                        handle.Get = manipulatable.GetProgressY;
                        handle.Set = p => manipulatable.SetProgress( manipulatable.GetProgressX(), p, manipulatable.GetProgressZ());
                    }
                    else
                    {
                        handle.Get = manipulatable.GetProgressZ;
                        handle.Set = p => manipulatable.SetProgress(manipulatable.GetProgressX(), manipulatable.GetProgressY(), p);
                    }

                    return handle;
                }

                return null;
            }

            public Vector3 Axis()
            {
                float progress = Get();
                float nudge = progress < .5f ? .05f : -.05f;

                Vector3 before = knob.position;
                Set(progress + nudge);
                Vector3 after = knob.position;
                Set(progress);

                return (after - before) * Mathf.Sign(nudge);
            }
        }

        IEnumerator DragSlider(HandDriver hand, SliderHandle slider)
        {
            state = State.SLIDER;

            HeadDriver.Instance.turn = false;
            hand.grip = true;
            hand.followRate = .5f;

            float progress = 0;
            float direction = 1;

            try
            {
                progress = slider.Get();

                direction = Vector3.Dot(slider.Axis(), Camera.main.transform.up) >= 0 ? 1 : -1;
            }
            catch (Exception e)
            {
                Logging.Exception(e);
            }

            while (TriggerHeld(hand) && slider.component)
            {
                if (CurrentGamepad == null)
                    break;

                // Left stick up/down controls slider progress.
                float vertical = CurrentGamepad.leftStick.ReadValue().y;

                progress = Mathf.Clamp01(progress + vertical * SliderSensitivity * 100f * Time.deltaTime * direction);

                try
                {
                    slider.Set(progress);
                }
                catch (Exception e)
                {
                    Logging.Exception(e);
                    break;
                }

                Vector3 knob = slider.knob.position;
                Vector3 view = Camera.main.transform.forward;

                hand.targetPosition = knob - view * .03f;
                hand.lookAt = knob + view;
                hand.up = hand.isLeft ? head.right : -head.right;

                yield return null;
            }

            HeadDriver.Instance.turn = true;
            hand.grip = false;
            hand.followRate = .1f;
            hand.targetPosition = hand.DefaultPosition;

            state = State.IDLE;
        }

        bool TriggerHeld(HandDriver hand)
        {
            if (CurrentGamepad == null)
                return false;

            return hand.isLeft ? CurrentGamepad.leftTrigger.isPressed : CurrentGamepad.rightTrigger.isPressed;
        }

        static readonly string[] pressMethodNames =
        {
            "Click",
            "ButtonActivationWithHand"
        };

        static (Component component, Action<bool> press)FindPressable(Transform target)
        {
            var touchPoint = target.GetComponentInParent<CustomMapsScreenTouchPoint>();

            if (touchPoint)
                return (touchPoint, _ => PressTouchPoint(touchPoint));

            var touchscreenButton = target.GetComponentInParent<SITouchscreenButton>();

            if (touchscreenButton)
                return (touchscreenButton, _ => touchscreenButton.PressButton());

            foreach (var component in target.GetComponentsInParent<MonoBehaviour>())
            {
                if (!component)
                    continue;

                foreach (var name in pressMethodNames)
                {
                    var method = AccessTools.Method(component.GetType(), name,new[] { typeof(bool) });

                    if (method != null)
                    {
                        return (component, isLeft => method.Invoke(component, new object[] { isLeft }));
                    }
                }
            }

            return (null, null);
        }

        static readonly AccessTools.FieldRef<CustomMapsScreenTouchPoint, CustomMapsTerminalScreen> touchScreenRef = AccessTools.FieldRefAccess<CustomMapsScreenTouchPoint, CustomMapsTerminalScreen>("screen");

        static readonly AccessTools.FieldRef<CustomMapsScreenTouchPoint, CustomMapKeyboardBinding> touchBindingRef = AccessTools.FieldRefAccess<CustomMapsScreenTouchPoint,CustomMapKeyboardBinding>("keyBinding");

        static readonly FieldInfo touchPressTime = AccessTools.Field(typeof(CustomMapsScreenTouchPoint), "pressTime");

        static readonly MethodInfo touchPressedEvent = AccessTools.Method(typeof(CustomMapsScreenTouchPoint), "OnButtonPressedEvent");

        static void PressTouchPoint(CustomMapsScreenTouchPoint touchPoint)
        {
            touchPressTime?.SetValue(null, Time.time);
            touchPressedEvent?.Invoke(touchPoint, null);
            touchPoint.PressButtonColourUpdate();

            var screen = touchScreenRef(touchPoint);

            if (screen)
                screen.PressButton(touchBindingRef(touchPoint));
        }

        IEnumerator PressButton(HandDriver hand, Vector3 buttonPoint, (Component component, Action<bool> press) pressable)
        {
            state = State.BUTTON;

            if (pressable.component)
            {
                try
                {
                    pressable.press(hand.isLeft);
                }
                catch (Exception e)
                {
                    Logging.Exception(e);
                }
            }

            if (pressable.component)
                SetHandTouch(hand.isLeft, false);

            Vector3 toButton = buttonPoint - hand.DefaultPosition;

            hand.grip = true;
            hand.followRate = .5f;
            hand.lookAt = buttonPoint + toButton;
            hand.up = hand.isLeft ? head.right : -head.right;

            Transform tip = IndexFingertip(hand.isLeft);

            for (float end = Time.time + .12f; Time.time < end;)
            {
                Vector3 reach = tip ? FingertipPosition(tip) - hand.transform.position : toButton.normalized * FallbackFingerReach * GTPlayer.Instance.scale;

                Vector3 sideways = Camera.main.transform.right * (hand.isLeft ? -1 : 1) * FingertipSideCorrection * GTPlayer.Instance.scale;

                hand.targetPosition = buttonPoint - reach + sideways;

                yield return null;
            }

            hand.targetPosition = hand.DefaultPosition;

            yield return new WaitForSeconds(.1f);

            hand.grip = false;
            hand.followRate = .1f;

            SetHandTouch(hand.isLeft, true);

            state = State.IDLE;
        }

        const float FallbackFingerReach = .1f;
        const float FingertipSideCorrection = .025f;

        readonly Transform[] fingertips = new Transform[2];

        static Vector3 FingertipPosition(Transform lastJoint)
        {
            var parent = lastJoint.parent;

            return parent ? lastJoint.position + (lastJoint.position - parent.position) : lastJoint.position;
        }

        Transform IndexFingertip(bool left)
        {
            int i = left ? 0 : 1;

            if (fingertips[i])
                return fingertips[i];

            var rig = GorillaTagger.Instance.offlineVRRig;
            var map = left ? rig.leftHand : rig.rightHand;

            if (map == null || !map.rigTarget)
                return null;

            float farthest = 0;

            foreach (var bone in map.rigTarget.GetComponentsInChildren<Transform>())
            {
                if (!bone.name.ToLower().Contains("index"))
                    continue;

                float distance = (bone.position - map.rigTarget.position).sqrMagnitude;

                if (distance > farthest)
                {
                    farthest = distance;
                    fingertips[i] = bone;
                }
            }

            return fingertips[i];
        }

        readonly System.Collections.Generic.List<Collider> disabledTouch = new System.Collections.Generic.List<Collider>();

        void SetHandTouch(bool left, bool enabled)
        {
            if (enabled)
            {
                foreach (var collider in disabledTouch)
                {
                    if (collider)
                        collider.enabled = true;
                }

                disabledTouch.Clear();
                return;
            }

            var tagger = GorillaTagger.Instance;

            Transform player = tagger.transform;
            Transform rig = tagger.offlineVRRig.transform;
            Transform body = GTPlayer.Instance.transform;

            foreach (var indicator in FindObjectsByType<GorillaTriggerColliderHandIndicator>(FindObjectsSortMode.None))
            {
                var t = indicator.transform;

                if (!t.IsChildOf(player) && !t.IsChildOf(rig) && !t.IsChildOf(body))
                    continue;

                foreach (var collider in indicator.GetComponentsInChildren<Collider>())
                {
                    if (!collider.enabled)
                        continue;

                    collider.enabled = false;
                    disabledTouch.Add(collider);
                }
            }
        }

        void AnimateHands()
        {
            if (state != State.IDLE)
                return;

            leftHand.targetPosition = leftHand.DefaultPosition;
            rightHand.targetPosition = rightHand.DefaultPosition;

            leftHand.lookAt = leftHand.targetPosition + head.forward;

            rightHand.lookAt = rightHand.targetPosition + head.forward;

            leftHand.up = head.right;
            rightHand.up = -head.right;
        }

        protected override void Start()
        {
            base.Start();

            reticle = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
            reticle.localScale = Vector3.one * .001f;
            reticle.GetComponent<MeshRenderer>().material.color = Color.white;
            reticle.GetComponent<MeshRenderer>().material.shader = Shader.Find("GorillaTag/UberShader");
            reticle.GetComponent<SphereCollider>().enabled = false;
            reticle.SetParent(Camera.main.transform);
            reticle.localPosition = Vector3.forward * .1f;
            reticle.gameObject.SetActive(false);
        }

        public override void Setup()
        {
            HeadDriver.Instance.LockCursor = true;
        }

        public override void Cleanup()
        {
            base.Cleanup();

            reticle.gameObject.SetActive(false);
            state = State.IDLE;

            StopAllCoroutines();
            SetHandTouch(true, true);
        }

        void OnDestroy()
        {
            if (reticle)
                Destroy(reticle.gameObject);
        }
    }
}
