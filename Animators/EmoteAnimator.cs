using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using Gorillapad.Patches;
using Gorillapad.Rigging;

namespace Gorillapad.Animators
{
    public class EmoteAnimator : AnimatorBase
    {
        public enum Emote { Wave, Point, ThumbsUp, ThumbsDown, Shrug, Dance }
        public Emote emote;
        public AnimatorBase returnTo;
        float startTime;

        // Go back to whatever you were doing before the emote
        public void Stop() => Rig.Instance.Animator = returnTo ? returnTo : Plugin.Instance.walkAnimator;

        void Update()
        {
            // Moving cancels the emote
            if (InputHandler.inputDirection != Vector3.zero)
                Stop();
        }
        Func<HandDriver, float, HandPositionInfo> handPositioner;

        public override void Animate()
        {
            switch (emote)
            {
                case Emote.Wave:
                    handPositioner = WavePositioner;
                    break;
                case Emote.Point:
                    handPositioner = PointPositioner;
                    break;
                case Emote.ThumbsUp:
                    handPositioner = ThumbsUpPositioner;
                    break;
                case Emote.ThumbsDown:
                    handPositioner = ThumbsDownPositioner;
                    break;
                case Emote.Shrug:
                    handPositioner = ShrugPositioner;
                    break;
                case Emote.Dance:
                    handPositioner = DancePositioner;
                    break;
            }
            AnimateBody();
            AnimateHand(leftHand);
            AnimateHand(rightHand);
        }

        Vector3 startingPosition;
        void AnimateBody()
        {
            rig.active = true;
            rig.useGravity = false;
            if (emote == Emote.Dance)
            {
                float t = Time.time - startTime;
                Vector3 offset = new Vector3(0, Mathf.Sin(t * 10) * .1f, 0);
                rig.targetPosition = startingPosition + offset;
            }
            else
            {
                rig.targetPosition = startingPosition;
            }
        }

        void AnimateHand(HandDriver hand)
        {
            var info = handPositioner.Invoke(hand, Time.time - startTime);
            if (!info.used) return;
            hand.grip = info.grip;
            hand.trigger = info.trigger;
            hand.primary = info.thumb;
            hand.targetPosition = info.position;
            hand.lookAt = info.lookAt;
            hand.up = info.up;
        }

        struct HandPositionInfo
        {
            public Vector3 position, lookAt, up;
            public bool grip, trigger, thumb;
            public bool used;
        }

        float lastDanceSwitch, danceSwitchRate = 1.5f;
        int dance;
        HandPositionInfo DancePositioner(HandDriver hand, float t)
        {
            if (Time.time - lastDanceSwitch > danceSwitchRate)
            {
                dance = UnityEngine.Random.Range(0, 4);
                lastDanceSwitch = Time.time;
            }
            HandPositionInfo result = default;
            float toff = hand.isLeft ? 0 : Mathf.PI;
            Vector3 lookAt, offset;
            switch (dance)
            {
                case 0:
                    lookAt = hand.isLeft ?
                    hand.targetPosition + head.right + head.forward :
                    hand.targetPosition - head.right + head.forward;

                    offset = new Vector3(hand.isLeft ? -.2f : .2f, -.3f, .2f);
                    offset.y += Mathf.Sin(t * 10 + toff) * .1f;
                    result = new HandPositionInfo()
                    {
                        position = head.TransformPoint(offset),
                        lookAt = lookAt,
                        up = head.up,
                        grip = true,
                        trigger = true,
                        thumb = true,
                        used = true,
                    };
                    break;
                case 1:
                    lookAt = hand.targetPosition + head.up;
                    offset = new Vector3(hand.isLeft ? -.2f : .2f, -.2f, .3f);
                    offset.z += Mathf.Sin(t * 10 + toff) * .1f;
                    result = new HandPositionInfo()
                    {
                        position = head.TransformPoint(offset),
                        lookAt = lookAt,
                        up = hand.isLeft ? head.right : -head.right,
                        grip = false,
                        trigger = false,
                        thumb = false,
                        used = true,
                    };
                    break;
                case 2:
                    lookAt = hand.targetPosition + head.up;
                    offset = new Vector3(hand.isLeft ? -.2f : .2f, -.3f, .2f);
                    offset.y += Mathf.Sin(t * 10 + toff) * .1f;
                    result = new HandPositionInfo()
                    {
                        position = head.TransformPoint(offset),
                        lookAt = lookAt,
                        up = hand.isLeft ? head.right : -head.right,
                        grip = true,
                        trigger = false,
                        thumb = true,
                        used = true,
                    };
                    break;
                case 3:
                    lookAt = hand.targetPosition + head.up;
                    offset = new Vector3(hand.isLeft ? -.2f : .2f, -.2f, .3f);
                    offset.x += Mathf.Cos(t * 10) * .1f;
                    offset.z += Mathf.Sin(t * 10) * .1f;
                    result = new HandPositionInfo()
                    {
                        position = head.TransformPoint(offset),
                        lookAt = lookAt,
                        up = hand.isLeft ? head.right : -head.right,
                        grip = true,
                        trigger = true,
                        thumb = true,
                        used = true,
                    };
                    break;
            }

            return result;
        }

        HandPositionInfo ThumbsDownPositioner(HandDriver hand, float _)
        {
            Vector3 lookAt = hand.isLeft ?
                hand.targetPosition + head.right :
                hand.targetPosition - head.right;
            return new HandPositionInfo()
            {
                position = head.TransformPoint(new Vector3(hand.isLeft ? -.2f : .2f, 0, .4f)),
                lookAt = lookAt,
                up = -head.up,
                grip = true,
                trigger = true,
                used = true,
            };
        }

        HandPositionInfo ThumbsUpPositioner(HandDriver hand, float _)
        {
            Vector3 lookAt = hand.isLeft ?
                hand.targetPosition + head.right :
                hand.targetPosition - head.right;
            return new HandPositionInfo()
            {
                position = head.TransformPoint(new Vector3(hand.isLeft ? -.2f : .2f, 0, .4f)),
                lookAt = lookAt,
                up = head.up,
                grip = true,
                trigger = true,
                used = true,
            };
        }

        HandPositionInfo ShrugPositioner(HandDriver hand, float _)
        {
            Vector3 lookAt = hand.isLeft ?
                hand.targetPosition - head.right + head.forward :
                hand.targetPosition + head.right + head.forward;
            return new HandPositionInfo()
            {
                position = body.TransformPoint(new Vector3(hand.isLeft ? -.4f : .4f, 0, .2f)),
                lookAt = lookAt,
                up = -head.forward,
                used = true,
            };
        }

        HandPositionInfo PointPositioner(HandDriver hand, float __)
        {
            if (hand.isLeft)
                return new HandPositionInfo()
                {
                    used = true,
                    position = hand.DefaultPosition,
                    lookAt = hand.targetPosition + head.forward,
                    up = head.right
                };
            return new HandPositionInfo()
            {
                position = head.TransformPoint(new Vector3(.25f, 0, .7f)),
                lookAt = hand.targetPosition + head.forward,
                up = -head.right,
                grip = true,
                trigger = false,
                used = true,
            };
        }

        HandPositionInfo WavePositioner(HandDriver hand, float time)
        {
            if (hand.isLeft)
                return new HandPositionInfo()
                {
                    used = true,
                    position = hand.DefaultPosition,
                    lookAt = hand.targetPosition + head.forward,
                    up = head.right
                };

            Vector3 pivot = new Vector3(.25f, 0, .2f);
            float radius = .25f;
            float height = .25f;
            float speed = 5;
            float tx = Mathf.Sin(Time.time * speed);
            float ty = Mathf.Cos(Time.time * speed);
            Vector3 offset = Vector3.zero;
            offset.x = tx * radius;
            offset.y = Mathf.Abs(ty) * height;
            offset += pivot;

            Vector3 up = hand.targetPosition - head.TransformPoint(pivot - Vector3.up * .25f);
            return new HandPositionInfo()
            {
                position = head.TransformPoint(offset),
                lookAt = hand.targetPosition + up,
                up = -head.right,
                used = true,
            };
        }

        public override void Setup()
        {
            Logging.Debug("===SETUP===");
            base.Start();
            startingPosition = body.position;
        }
    }
}
