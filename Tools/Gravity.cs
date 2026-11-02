using GorillaLocomotion;
using UnityEngine;

namespace Gorillapad.Tools
{
    /// <summary>
    /// The player's current gravity. Gravity zones can point it any way (walls, ceilings, planets),
    /// and the game rotates the player to match, so "up" and "down" come from the game instead of
    /// the world axes. Falls back to normal gravity if the game's values aren't available.
    /// </summary>
    public static class Gravity
    {
        public static Vector3 Up
        {
            get
            {
                try
                {
                    Vector3 up = GTPlayerTransform.Up;
                    if (up.sqrMagnitude > .01f) return up.normalized;
                }
                catch { }
                return Vector3.up;
            }
        }

        public static Vector3 Down => -Up;

        /// <summary>The gravity acceleration the game applies to the player (before scaling by player size).</summary>
        public static Vector3 Acceleration
        {
            get
            {
                try
                {
                    Vector3 force = GTPlayerTransform.GravityForce;
                    if (force.sqrMagnitude > .01f) return force;
                }
                catch { }
                return Physics.gravity;
            }
        }

        /// <summary>The part of a vector along the ground (perpendicular to gravity).</summary>
        public static Vector3 Flatten(Vector3 vector) => Vector3.ProjectOnPlane(vector, Up);

        /// <summary>How high a point is, measured along gravity's up.</summary>
        public static float Height(Vector3 point) => Vector3.Dot(point, Up);
    }
}
