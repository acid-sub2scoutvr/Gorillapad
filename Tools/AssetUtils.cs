using System;
using System.IO;
using UnityEngine;
using System.Reflection;

namespace Gorillapad.Tools
{
    public static class AssetUtils
    {
        private static string FormatPath(string path)
        {
            return path.Replace("/", ".").Replace("\\", ".");
        }

        public static AssetBundle LoadAssetBundle(string path) // Or whatever you want to call it as
        {
            path = FormatPath(path);
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(path);
            AssetBundle bundle = AssetBundle.LoadFromStream(stream);
            stream.Close();
            return bundle;
        }

        /// <summary>
        /// Returns a list of the names of all embedded resources
        /// </summary>
        public static string[] GetResourceNames()
        {
            var baseAssembly = Assembly.GetCallingAssembly();
            string[] names = baseAssembly.GetManifestResourceNames();
            if (names == null)
            {
                Console.WriteLine("No manifest resources found.");
                return null;
            }
            return names;
        }
    }
}