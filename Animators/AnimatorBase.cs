
using System;
using System.Collections;
using UnityEngine;
using Gorillapad.Rigging;

namespace Gorillapad.Animators
{
    public abstract class AnimatorBase : MonoBehaviour
    {
        protected Transform body, head;
        protected Rigidbody rigidbody;
        protected HandDriver leftHand, rightHand;
        protected Rig rig;

        protected virtual void Start()
        {
            Logging.Debug("==START==");

            rig = Rig.Instance;

            if (rig == null)
            {
                Logging.Debug("AnimatorBase: Rig.Instance is null!");
                return;
            }

            body = rig.body;
            head = rig.head;
            rigidbody = rig.rigidbody;
            leftHand = rig.leftHand;
            rightHand = rig.rightHand;
        }

        public abstract void Setup();

        public virtual void Cleanup()
        {
            this.enabled = false;

            if (rig == null)
                return;

            rig.active = false;
            rig.useGravity = true;
            rig.headDriver.turn = true;

            leftHand?.Reset();
            rightHand?.Reset();
        }

        public abstract void Animate();
    }
}
