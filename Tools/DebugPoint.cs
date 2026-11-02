using GorillaLocomotion;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEngine;

namespace Gorillapad.Tools
{
    public class DebugPoint : MonoBehaviour
    {
        public static Dictionary<string, DebugPoint> points = new Dictionary<string, DebugPoint>();

        public float size = 0.1f;
        public Color color = Color.white;
        Material material;

        void Awake()
        {
            material = Instantiate(Plugin.Instance.bundle.LoadAsset<Material>("m_xRay"));
            //material = Instantiate(Plugin.Instance.bundle.LoadAsset<Material>("m_Unlit"));
            material.color = color;
            GetComponent<MeshRenderer>().material = material;
        }

        void FixedUpdate()
        {
            material.color = color;
            transform.localScale = Vector3.one * size * GTPlayer.Instance.scale;
        }

        void OnDestroy()
        {
            points.Remove(name);
        }

        public static Transform Get(string name, Vector3 position, Color color = default, float size = .1f)
        {
            if (points.ContainsKey(name))
            {
                points[name].color = color;
                points[name].transform.position = position;
                points[name].size = size;
                return points[name].transform;
            }
            else
            {
                return Create(name, position, color);
            }
        }

        private static Transform Create(string name, Vector3 position, Color color)
        {
            var debugger = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
            debugger.name = $"Cipher Debugger ({name})";
            debugger.localScale = Vector3.one * .2f;
            debugger.position = position;
            debugger.GetComponent<Collider>().enabled = false;
            var mat = Instantiate(GorillaTagger.Instance.offlineVRRig.mainSkin.material);
            debugger.GetComponent<Renderer>().material = mat;

            var point = debugger.gameObject.AddComponent<DebugPoint>();
            point.color = color;
            points.Add(name, point);
            return debugger.transform;
        }

    }
}
