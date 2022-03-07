using UnityEngine;

namespace Redcode.Paths
{
    public static class CatmullRomSpline
    {
        /// <summary>
        /// Calculate CatmullRom point by 2 controls and 2 end points.
        /// <br/> See <see href="https://www.habrador.com/tutorials/interpolation/1-catmull-rom-splines/"/>
        /// </summary>
        /// <param name="t">Value between <see langword="0"/> and <see langword="1"/> which represent normalized distance from <paramref name="startPoint"/> to <paramref name="endPoint"/> when draw line.</param>
        /// <param name="startControlPoint">Start control point.</param>
        /// <param name="startPoint">Line start position.</param>
        /// <param name="endPoint">Line end position.</param>
        /// <param name="endControlPoint">End control point.</param>
        /// <returns>Calculated line point.</returns>
        public static Vector3 CalculatePoint(float t, Vector3 startControlPoint, Vector3 startPoint, Vector3 endPoint, Vector3 endControlPoint)
        {
            Vector3 a = 2f * startPoint;
            Vector3 b = endPoint - startControlPoint;
            Vector3 c = 2f * startControlPoint - 5f * startPoint + 4f * endPoint - endControlPoint;
            Vector3 d = -startControlPoint + 3f * startPoint - 3f * endPoint + endControlPoint;

            return 0.5f * (a + (b * t) + (t * t * c) + (t * t * t * d));
        }

    }
}