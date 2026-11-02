using UnityEngine;

namespace Gorillapad.Tools
{
    /// <summary>
    /// Borrows the game's own desktop (shoulder) camera for first person and the computer view,
    /// instead of rendering through an extra camera on top of it (which could show a black screen on
    /// some setups). Taking it switches off its Cinemachine brain so the mod can place it, the same
    /// way the game does when its own camera system takes over; releasing it hands it back.
    /// </summary>
    public static class ShoulderCameraControl
    {
        static Camera camera;
        static Behaviour brain;
        static float originalFieldOfView;
        static object owner;

        public static Camera Camera
        {
            get
            {
                if (!camera)
                    camera = Extensions.ShoulderCamera();
                return camera;
            }
        }

        public static bool IsOwnedBy(object who) => owner == who;

        /// <summary>Takes the camera (or keeps it, if this already has it) and sets its field of view.</summary>
        public static bool Take(object who, float fieldOfView)
        {
            var cam = Camera;
            if (!cam) return false;
            if (owner == null)
            {
                brain = cam.GetComponentInChildren<Unity.Cinemachine.CinemachineBrain>(true);
                originalFieldOfView = cam.fieldOfView;
            }
            // Keep the brain off for as long as it's borrowed; the game turns it back on in places
            // (e.g. when its loading overlay closes)
            if (brain && brain.enabled) brain.enabled = false;
            owner = who;
            cam.fieldOfView = fieldOfView;
            return true;
        }

        public static void Place(Vector3 position, Quaternion rotation)
        {
            if (Camera) Camera.transform.SetPositionAndRotation(position, rotation);
        }

        /// <summary>Hands the camera back to the game, if this is what has it.</summary>
        public static void Release(object who)
        {
            if (owner == null || owner != who) return;
            owner = null;
            if (brain) brain.enabled = true;
            if (Camera) Camera.fieldOfView = originalFieldOfView;
        }
    }
}
