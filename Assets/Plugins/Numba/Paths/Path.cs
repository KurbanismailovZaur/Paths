using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Paths
{
    public class Path : MonoBehaviour
    {
        [SerializeField]
        private int _resolution = 1;

        public int Resolution
        {
            get => _resolution;
            set => _resolution = Math.Clamp(value, 1, 128);
        }

        [SerializeField]
        private bool _looped;

        public bool Looped
        {
            get => _looped;
            set => _looped = value;
        }

        #region Editor
        private static Transform CreatePoint(Transform parent, Vector3 position)
        {
            var childs = parent.GetComponentsInChildren<Transform>().Skip(1);
            var nextIndex = parent.childCount == 0 ? 0 : childs.Select(t => Convert.ToInt32(t.name)).Max() + 1;
            var point = new GameObject($"{nextIndex}").transform;
            point.parent = parent;
            point.localPosition = position;

            point.hideFlags = HideFlags.HideInHierarchy;

            return point;
        }

        [MenuItem("GameObject/3D Object/Path", priority = 19)]
        private static void CreatePathFromMenu()
        {
            var path = new GameObject("Path").AddComponent<Path>();
            path.transform.SetParent(Selection.activeTransform);

            Selection.activeObject = path;

            CreatePoint(path.transform, new Vector3(-1f, 0f, 0f));
            CreatePoint(path.transform, new Vector3(-0.5f, 0f, 0f));
            CreatePoint(path.transform, new Vector3(0.5f, 0f, 0f));
            CreatePoint(path.transform, new Vector3(1f, 0f, 0f));
        }
        #endregion

        /// <summary>
		/// Calculate CatmullRom point on line.
		/// <br/> See <see href="https://www.habrador.com/tutorials/interpolation/1-catmull-rom-splines/"/>
		/// </summary>
		/// <param name="t">Value between <see langword="0"/> and <see langword="1"/> which represent normalized distance from <paramref name="startPoint"/> to <paramref name="endPoint"/> when draw line.</param>
		/// <param name="startControlPoint">Start control point.</param>
		/// <param name="startPoint">Line start position.</param>
		/// <param name="endPoint">Line end position.</param>
		/// <param name="endControlPoint">End control point.</param>
		/// <returns>Calculated line point.</returns>
		public Vector3 CalculateLinePoint(float t, Vector3 startControlPoint, Vector3 startPoint, Vector3 endPoint, Vector3 endControlPoint)
        {
            Vector3 a = 2f * startPoint;
            Vector3 b = endPoint - startControlPoint;
            Vector3 c = 2f * startControlPoint - 5f * startPoint + 4f * endPoint - endControlPoint;
            Vector3 d = -startControlPoint + 3f * startPoint - 3f * endPoint + endControlPoint;

            return 0.5f * (a + (b * t) + (t * t * c) + (t * t * t * d));
        }

        private void OnDrawGizmosSelected()
        {
            int WrapIndex(int index)
            {
                if (index < 0)
                    return transform.childCount - 1;
                else if (index == transform.childCount)
                    return 0;
                else if (index == transform.childCount + 1)
                    return 1;
                else
                    return index;

            }
            Gizmos.color = Color.yellow;

            for (int i = 0; i < transform.childCount; i++)
            {
                if (!_looped && (i == 0 || i == transform.childCount - 2 || i == transform.childCount - 1))
                    continue;

                var p0 = transform.GetChild(WrapIndex(i - 1)).position;
                var p1 = transform.GetChild(WrapIndex(i)).position;
                var p2 = transform.GetChild(WrapIndex(i + 1)).position;
                var p3 = transform.GetChild(WrapIndex(i + 2)).position;

                var step = 1f / _resolution;
                var t = 0f;
                var lastPosition = p1;

                while (t < 1f)
                {
                    var position = CalculateLinePoint(t, p0, p1, p2, p3);
                    Gizmos.DrawLine(lastPosition, position);

                    lastPosition = position;
                    t += step;
                }

                Gizmos.DrawLine(lastPosition, CalculateLinePoint(1f, p0, p1, p2, p3));
            }
        }
    }
}