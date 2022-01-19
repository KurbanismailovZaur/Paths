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
        private List<Vector3> _points;

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

#if UNITY_EDITOR
        [MenuItem("GameObject/3D Object/Path", priority = 19)]
        private static void CreatePathFromMenu()
        {
            var path = new GameObject("Path").AddComponent<Path>();
            path.transform.SetParent(Selection.activeTransform);

            path._points = new List<Vector3>()
            {
                new Vector3(-0.5f, 0f, 0f),
                new Vector3(0.5f, 0f, 0f)
            };

            Selection.activeObject = path;
        }

        private int WrapIndex(int index)
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

        private void OnDrawGizmosSelected()
        {
            //Gizmos.color = Color.yellow;

            //if (_points.Count == 2)
            //    DrawTwoPoints();
            //else if (_points.Count == 3) 
            //    DrawThreePoints();
            //else if (_points.Count >= 4)
            //    DrawPoints();

            //for (int i = 0; i < Points.Count; i++)
            //{
            //    if (!_looped && (i == 0 || i == Points.Count - 2 || i == Points.Count - 1))
            //        continue;

            //    var p0 = TransformPoint(Points[WrapIndex(i - 1)]);
            //    var p1 = TransformPoint(Points[WrapIndex(i)]);
            //    var p2 = TransformPoint(Points[WrapIndex(i + 1)]);
            //    var p3 = TransformPoint(Points[WrapIndex(i + 2)]);

            //    var t = 0f;
            //    var lastPosition = p1;

            //    while (t < 1f)
            //    {
            //        var position = GetPoint(t, p0, p1, p2, p3);
            //        Gizmos.DrawLine(lastPosition, position);

            //        lastPosition = position;
            //        t += step;
            //    }

            //    Gizmos.DrawLine(lastPosition, GetPoint(1f, p0, p1, p2, p3));
            //}

            //if (!_looped)
            //{
            //    Handles.color = Color.white;
            //    Handles.DrawDottedLine(TransformPoint(Points[0]), TransformPoint(Points[1]), 5f);
            //    Handles.DrawDottedLine(TransformPoint(Points[Points.Count - 2]), TransformPoint(Points[Points.Count - 1]), 5f);
            //}
        }
#endif

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
        public static Vector3 GetPoint(float t, Vector3 startControlPoint, Vector3 startPoint, Vector3 endPoint, Vector3 endControlPoint)
        {
            Vector3 a = 2f * startPoint;
            Vector3 b = endPoint - startControlPoint;
            Vector3 c = 2f * startControlPoint - 5f * startPoint + 4f * endPoint - endControlPoint;
            Vector3 d = -startControlPoint + 3f * startPoint - 3f * endPoint + endControlPoint;

            return 0.5f * (a + (b * t) + (t * t * c) + (t * t * t * d));
        }

        public float GetSegmentLength(int segment)
        {
            if (_points.Count < 2)
                throw new Exception("There is no segments.");

            if (segment < 0)
                throw new Exception($"Segment {segment} not exist.");

            if (_points.Count == 2)
            {
                if (!_looped && segment > 0 || _looped && segment > 1)
                    throw new Exception($"Segment {segment} not exist.");

                return Vector3.Distance(_points[0], _points[1]);
            }

            if (_points.Count == 3)
            {
                if (!_looped && segment > 1 || _looped && segment > 2)
                    throw new Exception($"Segment {segment} not exist.");
            }
            else if (_points.Count > 3)
            {
                if (!_looped && segment > _points.Count - 4 || _looped && segment > _points.Count - 1)
                    throw new Exception($"Segment {segment} not exist.");

                if (!_looped)
                    segment += 1;
            }

            // For 3 and more points..

            var length = 0f;

            var t = 0f;
            var step = 1f / _resolution;

            var lastPosition = _points[segment];

            var p0 = _points[WrapIndex(segment - 1)];
            var p1 = _points[segment];
            var p2 = _points[WrapIndex(segment + 1)];
            var p3 = _points[WrapIndex(segment + 2)];

            while (t < 1f)
            {
                var position = GetPoint(t, p0, p1, p2, p3);
                length += Vector3.Distance(lastPosition, position);

                lastPosition = position;
                t += step;
            }

            return length += Vector3.Distance(lastPosition, GetPoint(1f, p0, p1, p2, p3));
        }

        public Vector3 GetPoint(int segment, float distance, bool useNormalizedDistance = true)
        {
            if (_points.Count == 0)
                throw new Exception("Path does not contain points.");

            if (_points.Count == 1)
                return TransformPoint(_points[0]);

            var length = GetSegmentLength(segment);
            distance = Mathf.Clamp(useNormalizedDistance ? length * distance : distance, 0f, length);

            if (_points.Count == 2)
            {
                var (from, to) = segment == 0 ? (0, 1) : (1, 0);
                return TransformPoint(_points[from] + (_points[to] - _points[from]).normalized * distance);
            }

            if (_points.Count > 3 && !_looped)
                segment += 1;

            // For 3 and more points..

            if (distance == 0f)
                return _points[segment];
            else if (distance == length)
                return _points[WrapIndex(segment + 1)];

            var t = 0f;
            var step = 1f / _resolution;

            var lastPosition = _points[segment];

            var p0 = _points[WrapIndex(segment - 1)];
            var p1 = _points[segment];
            var p2 = _points[WrapIndex(segment + 1)];
            var p3 = _points[WrapIndex(segment + 2)];

            Vector3 position;
            float currentLength;

            while (t < 1f)
            {
                position = GetPoint(t, p0, p1, p2, p3);
                currentLength = Vector3.Distance(lastPosition, position);

                if (distance <= currentLength)
                    return TransformPoint(Vector3.Lerp(lastPosition, position, distance / currentLength));

                distance -= currentLength;
                lastPosition = position;
                t += step;
            }

            position = GetPoint(1f, p0, p1, p2, p3);
            currentLength = Vector3.Distance(lastPosition, position);

            return TransformPoint(Vector3.Lerp(lastPosition, position, distance / currentLength));
        }

        private Vector3 TransformPoint(Vector3 point) => transform.TransformPoint(point);

        public Vector3 GetGlobalPoint(int index) => TransformPoint(_points[index]);

        public void SetGlobalPoint(int index, Vector3 point) => _points[index] = point - transform.position;
    }
}