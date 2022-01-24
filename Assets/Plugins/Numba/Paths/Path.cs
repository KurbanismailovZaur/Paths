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
        #region Segments length calculators
        private abstract class SegmentLengthCalculator
        {
            protected Path _path;

            public SegmentLengthCalculator(Path path) => _path = path;

            public abstract void CalculateLength(int segment);
        }

        private class ZeroSegmentsCalculator : SegmentLengthCalculator
        {
            public ZeroSegmentsCalculator(Path path) : base(path) { }

            public override void CalculateLength(int segment) { }
        }

        private class OneSegmentsCalculator : SegmentLengthCalculator
        {
            public OneSegmentsCalculator(Path path) : base(path) { }

            public override void CalculateLength(int segment) => _path._segments[segment] = 0f;
        }

        private class TwoSegmentsCalculator : SegmentLengthCalculator
        {
            public TwoSegmentsCalculator(Path path) : base(path) { }

            public override void CalculateLength(int segment) => _path._segments[segment] = Vector3.Distance(_path._points[0], _path._points[1]);
        }

        private class ManySegmentsCalculator : SegmentLengthCalculator
        {
            public ManySegmentsCalculator(Path path) : base(path) { }

            public override void CalculateLength(int segment)
            {
                var length = 0f;

                var t = 0f;
                var lastPosition = _path._points[segment];

                var p0 = _path._points[_path.WrapIndex(segment - 1)];
                var p1 = _path._points[segment];
                var p2 = _path._points[_path.WrapIndex(segment + 1)];
                var p3 = _path._points[_path.WrapIndex(segment + 2)];

                while (t < 1f)
                {
                    var position = CatmullRomSpline.CalculatePoint(t, p0, p1, p2, p3);
                    length += Vector3.Distance(lastPosition, position);

                    lastPosition = position;
                    t += _path._step;
                }

                _path._segments[segment] = length += Vector3.Distance(lastPosition, CatmullRomSpline.CalculatePoint(1f, p0, p1, p2, p3));
            }
        }
        #endregion

        [SerializeField]
        private List<Vector3> _points = new();

        public int PointsCount => _points.Count;

        [SerializeField]
        private List<float> _segments = new();

        public int SegmentsCount
        {
            get
            {
                if (_points.Count < 2)
                    return 0;

                return _looped ? _points.Count : _points.Count - (_points.Count < 4 ? 1 : 3);
            }
        }

        private SegmentLengthCalculator[] _segmentCalculators;

        [SerializeField]
        private int _segmentCalculatorIndex;

        [SerializeField]
        private int _resolution = 1;

        public int Resolution
        {
            get => _resolution;
            set
            {
                _resolution = Math.Clamp(value, 1, 100);
                _step = 1f / _resolution;

                RecalculateAllSegments();
            }
        }

        [SerializeField]
        [HideInInspector]
        private float _step = 1f;

        [SerializeField]
        private bool _looped;

        public bool Looped
        {
            get => _looped;
            set
            {
                _looped = value;
                RecalculatePathLength();
            }
        }

        public float Length { get; private set; }

        // Used by serialization system.
        private Path() => _segmentCalculators = new SegmentLengthCalculator[]
        {
            new ZeroSegmentsCalculator(this),
            new OneSegmentsCalculator(this),
            new TwoSegmentsCalculator(this),
            new ManySegmentsCalculator(this)
        };

        public static Path Create() => new GameObject("Path").AddComponent<Path>();

        public static Path Create(IEnumerable<Vector3> points)
        {
            var path = Create();

            foreach (var point in points)
                path.AddPoint(point);

            return path;
        }

        #region Editor
#if UNITY_EDITOR
        [MenuItem("GameObject/3D Object/Path", priority = 19)]
        private static void CreatePathFromMenu()
        {
            var path = Create();
            path.transform.SetParent(Selection.activeTransform);

            path.AddPoint(new Vector3(-0.5f, 0f, 0f));
            path.AddPoint(new Vector3(0.5f, 0f, 0f));

            Selection.activeObject = path;
        }

        private int WrapIndex(int index) => (((index - 0) % (_points.Count - 0)) + (_points.Count - 0)) % (_points.Count - 0) + 0;
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

            if (_points.Count < 3)
            {
                _resolution = 1;
                return;
            }

            var currentSegment = 0;

            for (int i = 3; i <= 100; i++)
            {
                _resolution = i;
                _step = 1f / _resolution;

                Vector3 lastVector;

                RecalculateSegmentLength(currentSegment);
                var prevSegment = currentSegment - 1;

                if (_looped)
                {
                    prevSegment = WrapIndex(prevSegment);
                    RecalculateSegmentLength(prevSegment);

                    lastVector = (GetPoint(currentSegment, 0f) - GetPoint(prevSegment, 1f - _step)).normalized;
                }
                else
                {
                    if (currentSegment == 0)
                        lastVector = (GetPoint(currentSegment, _step) - GetPoint(currentSegment, 0f)).normalized;
                    else
                    {
                        RecalculateSegmentLength(prevSegment);
                        lastVector = (GetPoint(currentSegment, 0f) - GetPoint(prevSegment, 1f - _step)).normalized;
                    }
                }

                for (int j = currentSegment; j < SegmentsCount; j++)
                {
                    var t = _step;
                    var lastPosition = GetPoint(j, 0f);

                    Vector3 position, vector;
                    float angle;

                    while (t < 1f)
                    {
                        if (CheckAngle(j, t, lastPosition, lastVector, out position, out vector, out angle))
                            goto ResolutionCycle;

                        lastPosition = position;
                        lastVector = vector;
                        t += _step;
                    }

                    if (CheckAngle(j, 1f, lastPosition, lastVector, out position, out vector, out angle))
                        goto ResolutionCycle;

                    lastVector = vector;

                    if (++currentSegment < SegmentsCount)
                        RecalculateSegmentLength(currentSegment);
                }

                // Reaching this code means, that all segments are fairly smooth.
                break;

            ResolutionCycle:;
            }

            RecalculateAllSegments();
        }

        public void OptimizeResolutionByLength(float deltaBasis = 0.01f)
        {
            Resolution = 1;

            if (_points.Count < 3)
                return;

            deltaBasis *= Length * deltaBasis;
            var prevLength = Length;

            for (int i = 2; i <= 100; i++)
            {
                Resolution = i;

                if (Length - prevLength <= deltaBasis)
                    break;

                prevLength = Length;
            }
        }

        public void OptimizeResolution(float maxAngle = 8f, float deltaBasis = 0.01f)
        {
            var ticks = Environment.TickCount;
            OptimizeResolutionByAngle(maxAngle);
            var angleResolution = Resolution;
            Debug.Log($"Optimize by angle: {Environment.TickCount - ticks}");

            ticks = Environment.TickCount;
            OptimizeResolutionByLength(deltaBasis);
            var lengthResolution = Resolution;
            Debug.Log($"Optimize by length: {Environment.TickCount - ticks}");

            if (lengthResolution > angleResolution)
                (lengthResolution, angleResolution) = (angleResolution, lengthResolution);

            Resolution = (int)Mathf.Lerp(lengthResolution, angleResolution, 0.25f);
        }

        #region Transforms
        private void CheckAndTransformPointToLocal(ref Vector3 point, bool useGlobal)
        {
            if (useGlobal)
                point = transform.InverseTransformPoint(point);
        }

        private Vector3 CheckAndTransformPointToGlobal(int index, bool useGlobal)
        {
            return useGlobal ? transform.TransformPoint(_points[index]) : _points[index];
        }
        #endregion

        private void SetNewSegmentsCalculator() => _segmentCalculatorIndex = _segments.Count < 3 ? _segments.Count : 3;

        private SegmentLengthCalculator GetCurrentSegmentCalculator() => _segmentCalculators[_segmentCalculatorIndex];

        private void RecalculateAllSegments()
        {
            for (int i = 0; i < _segments.Count; i++)
                RecalculateSegmentLength(i);

            RecalculatePathLength();
        }

        private void RecalculateSegmentLength(int segment) => GetCurrentSegmentCalculator().CalculateLength(segment);

        private void RecalculatePathLength()
        {
            var segmentShift = (_points.Count > 3 && !_looped) ? 1 : 0;

            Length = 0f;
            for (int i = 0; i < SegmentsCount; i++)
                Length += _segments[i + segmentShift];
        }

        public float GetSegmentLength(int segment)
        {
            if (_points.Count < 2)
                throw new Exception($"Segment {segment} not exist.");

            if (!_looped && segment > _points.Count - (_points.Count < 4 ? 2 : 4) || _looped && segment > _points.Count - 1)
                throw new Exception($"Segment {segment} not exist.");

            if (_points.Count > 3 && !_looped)
                segment += 1;

            return _segments[segment];
        }

        public void AddPoint(Vector3 point, bool useGlobal = true)
        {
            CheckAndTransformPointToLocal(ref point, useGlobal);

            _points.Add(point);
            _segments.Add(0f);

            SetNewSegmentsCalculator();

            if (_segments.Count < 5)
                RecalculateAllSegments();
            else
            {
                for (int i = _points.Count - 3; i <= _points.Count; i++)
                    RecalculateSegmentLength(WrapIndex(i));

                RecalculatePathLength();
            }
        }

        public void InsertPoint(int index, Vector3 point, bool useGlobal = true)
        {
            CheckAndTransformPointToLocal(ref point, useGlobal);

            _points.Insert(index, point);
            _segments.Insert(index, 0f);

            SetNewSegmentsCalculator();

            if (_segments.Count < 5)
                RecalculateAllSegments();
            else
            {
                for (int i = index - 2; i <= index + 1; i++)
                    RecalculateSegmentLength(WrapIndex(i));

                RecalculatePathLength();
            }
        }

        public bool ContainsPoint(Vector3 point, bool useGlobal = true)
        {
            CheckAndTransformPointToLocal(ref point, useGlobal);
            return _points.Contains(point);
        }

        public int IndexOfPoint(Vector3 point, bool useGlobal = true)
        {
            CheckAndTransformPointToLocal(ref point, useGlobal);
            return _points.IndexOf(point);
        }

        public bool RemovePoint(Vector3 point, bool useGlobal = true)
        {
            CheckAndTransformPointToLocal(ref point, useGlobal);

            var index = _points.IndexOf(point);
            if (index == -1)
                return false;

            RemovePointAt(index);
            return true;
        }

        public void RemovePointAt(int index)
        {
            _points.RemoveAt(index);
            _segments.RemoveAt(index);

            SetNewSegmentsCalculator();

            if (_segments.Count == 0)
            {
                Length = 0f;
                return;
            }

            if (_segments.Count < 5)
                RecalculateAllSegments();
            else
            {
                for (int i = index - 2; i <= index; i++)
                    RecalculateSegmentLength(WrapIndex(i));

                RecalculatePathLength();
            }
        }

        public void ClearPoints()
        {
            _points.Clear();
            _segments.Clear();

            Length = 0f;
        }

        public Vector3 GetPoint(int index, bool useGlobal = true) => CheckAndTransformPointToGlobal(index, useGlobal);

        public void SetPoint(int index, Vector3 position, bool useGlobal)
        {
            CheckAndTransformPointToLocal(ref position, useGlobal);
            _points[index] = position;

            if (_segments.Count < 5)
                RecalculateAllSegments();
            else
            {
                for (int i = index - 2; i <= index + 1; i++)
                    RecalculateSegmentLength(WrapIndex(i));

                RecalculatePathLength();
            }
        }

        public Vector3 GetPoint(int segment, float distance, bool useNormalizedDistance = true, bool useGlobal = true)
        {
            if (_points.Count == 0)
                throw new Exception("Path does not contain points.");

            if (_points.Count == 1)
                return CheckAndTransformPointToGlobal(0, useGlobal);

            var length = GetSegmentLength(segment);
            distance = Mathf.Clamp(useNormalizedDistance ? length * distance : distance, 0f, length);

            Vector3 point;

            if (_points.Count == 2)
            {
                var (from, to) = segment == 0 ? (0, 1) : (1, 0);
                point = _points[from] + (_points[to] - _points[from]).normalized * distance;

                return useGlobal ? transform.TransformPoint(point) : point;
            }

            if (_points.Count > 3 && !_looped)
                segment += 1;

            // For 3 and more points..

            if (distance == 0f)
                return CheckAndTransformPointToGlobal(segment, useGlobal);
            else if (distance == length)
                return CheckAndTransformPointToGlobal(WrapIndex(segment + 1), useGlobal);

            var step = 1f / Resolution;
            var t = step;

            var lastPosition = _points[segment];

            var p0 = _points[WrapIndex(segment - 1)];
            var p1 = _points[segment];
            var p2 = _points[WrapIndex(segment + 1)];
            var p3 = _points[WrapIndex(segment + 2)];

            Vector3 position;
            float currentLength;

            while (t < 1f)
            {
                position = CatmullRomSpline.CalculatePoint(t, p0, p1, p2, p3);
                currentLength = Vector3.Distance(lastPosition, position);

                if (distance <= currentLength)
                {
                    point = Vector3.Lerp(lastPosition, position, distance / currentLength);
                    return useGlobal ? transform.TransformPoint(point) : point;
                }

                distance -= currentLength;
                lastPosition = position;
                t += step;
            }

            position = CatmullRomSpline.CalculatePoint(1f, p0, p1, p2, p3);
            currentLength = Vector3.Distance(lastPosition, position);

            point = Vector3.Lerp(lastPosition, position, distance / currentLength);
            return useGlobal ? transform.TransformPoint(point) : point;
        }

        public Vector3 GetPoint(float distance, bool useNormalizedDistance = true, bool useGlobal = true)
        {
            if (useNormalizedDistance)
                distance *= Length;

            distance = Mathf.Clamp(distance, 0f, Length);

            var segment = 0;
            for (; segment < SegmentsCount; segment++)
            {
                var segmentLength = GetSegmentLength(segment);

                if (distance <= segmentLength)
                    break;

                distance -= segmentLength;
            }

            if (segment == SegmentsCount)
            {
                segment -= 1;
                distance = GetSegmentLength(SegmentsCount - 1);
            }

            return GetPoint(segment, distance, false, useGlobal);
        }
    }
}