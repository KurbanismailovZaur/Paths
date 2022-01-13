using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Paths
{
    public class Path : MonoBehaviour
    {
        [SerializeField]
        private List<Vector3> _points = new();

        public List<Vector3> Points => _points;

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

        [MenuItem("GameObject/3D Object/Path", priority = 19)]
        private static void CreatePathFromMenu()
        {
            var path = new GameObject("Path").AddComponent<Path>();
            path.transform.SetParent(Selection.activeTransform);

            path.Points.Add(new Vector3(-1f, 0f, 0f));
            path.Points.Add(new Vector3(-0.5f, 0f, 0f));
            path.Points.Add(new Vector3(0.5f, 0f, 0f));
            path.Points.Add(new Vector3(1f, 0f, 0f));

             Selection.activeObject = path;
        }

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
		private Vector3 GetPoint(float t, Vector3 startControlPoint, Vector3 startPoint, Vector3 endPoint, Vector3 endControlPoint)
        {
            Vector3 a = 2f * startPoint;
            Vector3 b = endPoint - startControlPoint;
            Vector3 c = 2f * startControlPoint - 5f * startPoint + 4f * endPoint - endControlPoint;
            Vector3 d = -startControlPoint + 3f * startPoint - 3f * endPoint + endControlPoint;

            return 0.5f * (a + (b * t) + (t * t * c) + (t * t * t * d));
        }

        //public Vector3 GetPoint(float t)
        //{
        //    if (!_looped)
        //    {
        //        t *= 
        //    }
        //}

        private Vector3 TransformPoint(Vector3 point) => transform.TransformPoint(point);

        private void OnDrawGizmosSelected()
        {
            int WrapIndex(int index)
            {
                if (index < 0)
                    return Points.Count - 1;
                else if (index == Points.Count)
                    return 0;
                else if (index == Points.Count + 1)
                    return 1;
                else
                    return index;

            }

            Gizmos.color = Color.yellow;

            for (int i = 0; i < Points.Count; i++)
            {
                if (!_looped && (i == 0 || i == Points.Count - 2 || i == Points.Count - 1))
                    continue;

                var p0 = TransformPoint(Points[WrapIndex(i - 1)]);
                var p1 = TransformPoint(Points[WrapIndex(i)]);
                var p2 = TransformPoint(Points[WrapIndex(i + 1)]);
                var p3 = TransformPoint(Points[WrapIndex(i + 2)]);

                var step = 1f / _resolution;
                var t = 0f;
                var lastPosition = p1;

                while (t < 1f)
                {
                    var position = GetPoint(t, p0, p1, p2, p3);
                    Gizmos.DrawLine(lastPosition, position);

                    lastPosition = position;
                    t += step;
                }

                Gizmos.DrawLine(lastPosition, GetPoint(1f, p0, p1, p2, p3));
            }

            if (!_looped)
            {
                Handles.color = Color.white;
                Handles.DrawDottedLine(TransformPoint(Points[0]), TransformPoint(Points[1]), 5f);
                Handles.DrawDottedLine(TransformPoint(Points[Points.Count - 2]), TransformPoint(Points[Points.Count - 1]), 5f);
            }
        }
    }
}