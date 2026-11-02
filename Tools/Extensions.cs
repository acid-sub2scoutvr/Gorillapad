using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Gorillapad.Tools
{
    public static class Extensions
    {

        public static T GetOrAddComponent<T>(this GameObject obj) where T : Component
        {
            T component = obj.GetComponent<T>();
            return component ? component : obj.AddComponent<T>();
        }

        public static void Obliterate(this GameObject self)
        {
            Object.Destroy(self);
        }

        public static void Obliterate(this Component self)
        {
            Object.Destroy(self);
        }

        // The desktop (third person) camera that the game renders to the monitor.
        // Found the same way the game's MonitorOutputController finds it.
        public static Camera ShoulderCamera()
        {
            return GorillaTagger.Instance.thirdPersonCamera.GetComponentInChildren<Camera>();
        }

        // Converts a "lerp this much per tick" rate, tuned at 90 ticks/sec, into the equivalent for
        // the current tick length, so smoothing looks the same at any physics rate
        // (the game runs physics at the headset refresh rate, or 144Hz without one)
        public static float Smoothing(float ratePerTickAt90Hz)
        {
            return 1 - Mathf.Pow(1 - Mathf.Clamp01(ratePerTickAt90Hz), Time.deltaTime * 90f);
        }

        public static string GetPath(this Transform self)
        {
            string path = self.name;
            for (var parent = self.parent; parent; parent = parent.parent)
                path = parent.name + "/" + path;
            return path;
        }

        public static float Distance(this Vector3 self, Vector3 other)
        {
            return Vector3.Distance(self, other);
        }
        public static int Wrap(int x, int min, int max)
        {
            int range = max - min;
            int result = (x - min) % range;
            if (result < 0)
            {
                result += range;
            }
            return result + min;
        }


        public static float Map(float x, float a1, float a2, float b1, float b2)
        {
            // Calculate the range differences
            float inputRange = a2 - a1;
            float outputRange = b2 - b1;

            // Calculate the normalized value of x within the input range
            float normalizedValue = (x - a1) / inputRange;

            // Map the normalized value to the output range
            float mappedValue = b1 + normalizedValue * outputRange;

            return mappedValue;
        }

        public static byte[] StringToBytes(this string str)
        {
            return Encoding.UTF8.GetBytes(str);
        }

        public static string BytesToString(this byte[] bytes)
        {
            return Encoding.UTF8.GetString(bytes);
        }

        private static System.Random rng = new System.Random();
        public static void Shuffle<T>(this IList<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }
    }
}
