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
        private Points _points;

        public Points Points => _points;

        public int SegmentsCount
        {
            get
            {
                if (_points.Count < 2)
                    return 0;

                if (_points.Count == 2)
                    return _looped ? 2 : 1;

                if (_points.Count == 3)
                    return _looped ? 3 : 2;

                return _looped ? _points.Count : _points.Count - 3;
            }
        }

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
#if UNITY_EDITOR
        [MenuItem("GameObject/3D Object/Path", priority = 19)]
        private static void CreatePathFromMenu()
        {
            var path = new GameObject("Path").AddComponent<Path>();
            path.transform.SetParent(Selection.activeTransform);

            path._points = new Points(path);
            path._points.Local.AddRange(new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f));

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
#endif
        #endregion

        public void OptimizeResolutionByAngle(float maxAngle = 8f)
        {
            bool CheckAngle(int segment, float t, Vector3 lastPosition, Vector3 lastVector, out Vector3 position, out Vector3 vector, out float angle)
            {
                position = GetPoint(segment, t);
                vector = (position - lastPosition).normalized;

                return (angle = Vector3.Angle(lastVector, vector)) > maxAngle;
            }

            var ticks = Environment.TickCount;

            if (_points.Count < 3)
            {
                _resolution = 1;
                return;
            }

            for (int i = 3; i <= 128;)
            {
                _resolution = i;
                var step = 1f / _resolution;

                var lastVector = (GetPoint(0, step) - GetPoint(0, 0f)).normalized;
                var maxAngleByResolution = 0f;

                for (int j = 0; j < SegmentsCount; j++)
                {
                    var t = step;
                    var lastPosition = GetPoint(j, 0f);

                    Vector3 position, vector;
                    float angle;

                    while (t < 1f)
                    {
                        if (CheckAngle(j, t, lastPosition, lastVector, out position, out vector, out angle))
                        {
                            if (angle > maxAngleByResolution)
                                maxAngleByResolution = angle;
                        }

                        lastPosition = position;
                        lastVector = vector;
                        t += step;
                    }

                    if (CheckAngle(j, 1f, lastPosition, lastVector, out position, out vector, out angle))
                    {
                        if (angle > maxAngleByResolution)
                            maxAngleByResolution = angle;
                    }

                    lastVector = vector;
                }

                if (maxAngleByResolution > maxAngle)
                {
                    var excess = maxAngleByResolution - maxAngle;
                    //Debug.Log($"Excess: {excess}");

                    if (excess > 135f)
                        i += 32;
                    else if (excess > 90f)
                        i += 24;
                    else if (excess > 45f)
                        i += 16;
                    else if (excess > 15f)
                        i += 8;
                    else if (excess > 10f)
                        i += 4;
                    else if (excess > 5f)
                        i += 2;
                    else
                        i += 1;

                    continue;
                }

                // Reaching this code means, that all segments are fairly smooth.
                break;
            }

            Debug.Log(Environment.TickCount - ticks);
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

                return Vector3.Distance(_points.Local[0], _points.Local[1]);
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

            var lastPosition = _points.Local[segment];

            var p0 = _points.Local[WrapIndex(segment - 1)];
            var p1 = _points.Local[segment];
            var p2 = _points.Local[WrapIndex(segment + 1)];
            var p3 = _points.Local[WrapIndex(segment + 2)];

            while (t < 1f)
            {
                var position = CatmullRomSpline.CalculatePoint(t, p0, p1, p2, p3);
                length += Vector3.Distance(lastPosition, position);

                lastPosition = position;
                t += step;
            }

            return length += Vector3.Distance(lastPosition, CatmullRomSpline.CalculatePoint(1f, p0, p1, p2, p3));
        }

        public Vector3 GetPoint(int segment, float distance, bool useNormalizedDistance = true)
        {
            if (_points.Count == 0)
                throw new Exception("Path does not contain points.");

            if (_points.Count == 1)
                return _points.Global[0];

            var length = GetSegmentLength(segment);
            distance = Mathf.Clamp(useNormalizedDistance ? length * distance : distance, 0f, length);

            if (_points.Count == 2)
            {
                var (from, to) = segment == 0 ? (0, 1) : (1, 0);
                return transform.TransformPoint(_points.Local[from] + (_points.Local[to] - _points.Local[from]).normalized * distance);
            }

            if (_points.Count > 3 && !_looped)
                segment += 1;

            // For 3 and more points..

            if (distance == 0f)
                return _points.Global[segment];
            else if (distance == length)
                return _points.Global[WrapIndex(segment + 1)];

            var step = 1f / _resolution;
            var t = step;

            var lastPosition = _points.Local[segment];

            var p0 = _points.Local[WrapIndex(segment - 1)];
            var p1 = _points.Local[segment];
            var p2 = _points.Local[WrapIndex(segment + 1)];
            var p3 = _points.Local[WrapIndex(segment + 2)];

            Vector3 position;
            float currentLength;

            while (t < 1f)
            {
                position = CatmullRomSpline.CalculatePoint(t, p0, p1, p2, p3);
                currentLength = Vector3.Distance(lastPosition, position);

                if (distance <= currentLength)
                    return transform.TransformPoint(Vector3.Lerp(lastPosition, position, distance / currentLength));

                distance -= currentLength;
                lastPosition = position;
                t += step;
            }

            position = CatmullRomSpline.CalculatePoint(1f, p0, p1, p2, p3);
            currentLength = Vector3.Distance(lastPosition, position);

            return transform.TransformPoint(Vector3.Lerp(lastPosition, position, distance / currentLength));
        }
    }
}