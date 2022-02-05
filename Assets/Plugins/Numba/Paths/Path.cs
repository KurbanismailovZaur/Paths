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

            public override void CalculateLength(int segment) => _path._segmentsLengths[segment] = 0f;
        }

        private class TwoSegmentsCalculator : SegmentLengthCalculator
        {
            public TwoSegmentsCalculator(Path path) : base(path) { }

            public override void CalculateLength(int segment) => _path._segmentsLengths[segment] = Vector3.Distance(_path._points[0].Position, _path._points[1].Position);
        }

        private class ManySegmentsCalculator : SegmentLengthCalculator
        {
            public ManySegmentsCalculator(Path path) : base(path) { }

            public override void CalculateLength(int segment)
            {
                var length = 0f;

                var t = 0f;
                var lastPosition = _path._points[segment].Position;

                var p0 = _path._points[_path.WrapIndex(segment - 1)].Position;
                var p1 = _path._points[segment].Position;
                var p2 = _path._points[_path.WrapIndex(segment + 1)].Position;
                var p3 = _path._points[_path.WrapIndex(segment + 2)].Position;

                while (t < 1f)
                {
                    var position = CatmullRomSpline.CalculatePoint(t, p0, p1, p2, p3);
                    length += Vector3.Distance(lastPosition, position);

                    lastPosition = position;
                    t += _path._step;
                }

                _path._segmentsLengths[segment] = length += Vector3.Distance(lastPosition, CatmullRomSpline.CalculatePoint(1f, p0, p1, p2, p3));
            }
        }
        #endregion

        [SerializeField]
        private List<Point> _points = new();

        public int PointsCount => _points.Count;

        [SerializeField]
        private List<float> _segmentsLengths = new();

        public int SegmentsCount
        {
            get
            {
                if (_points.Count < 2)
                    return 0;

                return _looped ? _points.Count : _points.Count < 4 ? 1 : _points.Count - 3;
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

        public static Path Create() => Create(Vector3.zero);

        public static Path Create(Vector3 pivotPosition)
        {
            var path = new GameObject("Path").AddComponent<Path>();
            path.transform.position = pivotPosition;

            return path;
        }

        public static Path Create(Vector3 pivotPosition, bool useGlobals, params Vector3[] points) => Create(pivotPosition, useGlobals, (IEnumerable<Vector3>)points);

        public static Path Create(Vector3 pivotPosition, bool useGlobal, IEnumerable<Vector3> points)
        {
            var path = Create(pivotPosition);

            foreach (var point in points)
                path.AddPoint(point, useGlobal);

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

        public void OptimizeByAngle(float maxAngle = 8f)
        {
            bool CheckAngle(int segment, float t, Vector3 lastPosition, Vector3 lastVector, out PointData pointData, out Vector3 vector, out float angle)
            {
                pointData = GetPoint(segment, t);
                vector = (pointData.Position - lastPosition).normalized;

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

                    lastVector = (GetPoint(currentSegment, 0f).Position - GetPoint(prevSegment, 1f - _step).Position).normalized;
                }
                else
                {
                    if (currentSegment == 0)
                        lastVector = (GetPoint(currentSegment, _step).Position - GetPoint(currentSegment, 0f).Position).normalized;
                    else
                    {
                        RecalculateSegmentLength(prevSegment);
                        lastVector = (GetPoint(currentSegment, 0f).Position - GetPoint(prevSegment, 1f - _step).Position).normalized;
                    }
                }

                for (int j = currentSegment; j < SegmentsCount; j++)
                {
                    var t = _step;
                    var lastPosition = GetPoint(j, 0f).Position;

                    PointData pointData;
                    Vector3 vector;
                    float angle;

                    while (t < 1f)
                    {
                        if (CheckAngle(j, t, lastPosition, lastVector, out pointData, out vector, out angle))
                            goto ResolutionCycle;

                        lastPosition = pointData.Position;
                        lastVector = vector;
                        t += _step;
                    }

                    if (CheckAngle(j, 1f, lastPosition, lastVector, out pointData, out vector, out angle))
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

        public void Optimize()
        {
            if (_points.Count < 3)
            {
                Resolution = 1;
                return;
            }

            int startIndex, endIndex;
            if (_looped)
            {
                startIndex = 0;
                endIndex = _points.Count;
            }
            else
            {
                startIndex = 1;
                endIndex = _points.Count == 3 ? 3 : _points.Count - 1;
            }

            var resolution = 1f;

            for (int i = startIndex; i < endIndex; i++)
            {
                var back = _points[WrapIndex(i - 1)].Position - _points[i].Position;
                var forward = _points[WrapIndex(i + 1)].Position - _points[i].Position;

                var dot = Vector3.Dot(back.normalized, forward.normalized);
                dot = Math.Max(dot, Vector3.Dot(-back.normalized, forward.normalized));

                var newResolution = (dot + 1f) / (2f) * (15f) + 1f;

                var aspect = back.magnitude / forward.magnitude;
                if (aspect < 1f)
                    aspect = 1f / aspect;

                aspect = Mathf.Max(aspect / 2.5f, 1f);

                newResolution += newResolution * (aspect - 1f) * 0.1f;

                if (newResolution > resolution)
                    resolution = newResolution;
            }

            Resolution = Mathf.Max((int)resolution, 4);
        }

        #region Transforms
        private void CheckAndTransformPositionToLocal(ref Vector3 position, bool useGlobal)
        {
            if (useGlobal)
                position = transform.InverseTransformPoint(position);
        }

        private void CheckAndTransformRotationToLocal(ref Quaternion rotation, bool useGlobal)
        {
            if (useGlobal)
                rotation = Quaternion.Inverse(transform.rotation) * rotation;
        }

        private void CheckAndTransformPointToLocal(ref Point point, bool useGlobal)
        {
            if (useGlobal)
                point.Position = transform.InverseTransformPoint(point.Position);
        }

        private Point CheckAndTransformPointToGlobal(int index, bool useGlobal)
        {
            var point = _points[index];

            if (useGlobal)
            {
                point.Position = transform.TransformPoint(point.Position);
                point.Rotation = transform.rotation * point.Rotation;
            }

            return point;
        }

        private PointData CheckAndTransformPointToGlobal(PointData pointData, bool useGlobal)
        {
            if (useGlobal)
            {
                pointData.Position = transform.TransformPoint(pointData.Position);
                pointData.Rotation = transform.rotation * pointData.Rotation;
                pointData.Direction = transform.TransformDirection(pointData.Direction);
            }

            return pointData;
        }
        #endregion

        private void SetNewSegmentsCalculator() => _segmentCalculatorIndex = Mathf.Min(_segmentsLengths.Count, 3);

        private SegmentLengthCalculator GetCurrentSegmentCalculator() => _segmentCalculators[_segmentCalculatorIndex];

        private void RecalculateAllSegments()
        {
            for (int i = 0; i < _segmentsLengths.Count; i++)
                RecalculateSegmentLength(i);

            RecalculatePathLength();
        }

        private void RecalculateSegmentLength(int segment) => GetCurrentSegmentCalculator().CalculateLength(segment);

        private void RecalculatePathLength()
        {
            var segmentShift = (_points.Count > 2 && !_looped) ? 1 : 0;

            Length = 0f;
            for (int i = 0; i < SegmentsCount; i++)
                Length += _segmentsLengths[i + segmentShift];
        }

        private void RecalculateSegmentsAfterChanging(int index)
        {
            if (_segmentsLengths.Count < 5)
                RecalculateAllSegments();
            else
            {
                for (int i = index - 2; i <= index + 1; i++)
                    RecalculateSegmentLength(WrapIndex(i));

                RecalculatePathLength();
            }
        }

        public float GetSegmentLength(int segment)
        {
            if (_points.Count < 2)
                throw new Exception($"Segment {segment} not exist.");

            if (!_looped)
            {
                if (_points.Count < 4 && segment > 0)
                    throw new Exception($"Segment {segment} not exist.");

                if (_points.Count > 3 && segment > _points.Count - 4)
                    throw new Exception($"Segment {segment} not exist.");

                if (_points.Count > 2)
                    segment += 1;
            }
            else if (segment > _points.Count - 1)
                throw new Exception($"Segment {segment} not exist.");

            return _segmentsLengths[segment];
        }

        public void AddPoint(Vector3 position, bool useGlobal = true) => AddPoint(new Point(position, Quaternion.identity), useGlobal);

        public void AddPoint(Point point, bool useGlobal = true)
        {
            CheckAndTransformPointToLocal(ref point, useGlobal);

            _points.Add(point);
            _segmentsLengths.Add(0f);

            SetNewSegmentsCalculator();

            if (_segmentsLengths.Count < 5)
                RecalculateAllSegments();
            else
            {
                for (int i = _points.Count - 3; i <= _points.Count; i++)
                    RecalculateSegmentLength(WrapIndex(i));

                RecalculatePathLength();
            }
        }

        public void InsertPoint(int index, Vector3 position, bool useGlobal = true) => InsertPoint(index, new Point(position, Quaternion.identity), useGlobal);

        public void InsertPoint(int index, Point point, bool useGlobal = true)
        {
            CheckAndTransformPointToLocal(ref point, useGlobal);

            _points.Insert(index, point);
            _segmentsLengths.Insert(index, 0f);

            SetNewSegmentsCalculator();

            if (_segmentsLengths.Count < 5)
                RecalculateAllSegments();
            else
            {
                for (int i = index - 2; i <= index + 1; i++)
                    RecalculateSegmentLength(WrapIndex(i));

                RecalculatePathLength();
            }
        }

        public bool ContainsPoint(Vector3 position, bool useGlobal = true)
        {
            CheckAndTransformPositionToLocal(ref position, useGlobal);
            return _points.FindIndex(p => p.Position == position) != -1;
        }

        public bool ContainsPoint(Point point, bool useGlobal = true)
        {
            CheckAndTransformPointToLocal(ref point, useGlobal);
            return _points.Contains(point);
        }

        public int IndexOfPoint(Vector3 position, bool useGlobal = true)
        {
            CheckAndTransformPositionToLocal(ref position, useGlobal);
            return _points.FindIndex(p => p.Position == position);
        }

        public int IndexOfPoint(Point point, bool useGlobal = true)
        {
            CheckAndTransformPointToLocal(ref point, useGlobal);
            return _points.IndexOf(point);
        }

        public bool RemovePoint(Vector3 position, bool useGlobal = true)
        {
            CheckAndTransformPositionToLocal(ref position, useGlobal);

            var index = _points.FindIndex(p => p.Position == position);
            if (index == -1)
                return false;

            RemovePointAt(index);
            return true;
        }

        public bool RemovePoint(Point point, bool useGlobal = true)
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
            _segmentsLengths.RemoveAt(index);

            SetNewSegmentsCalculator();

            if (_segmentsLengths.Count == 0)
            {
                Length = 0f;
                return;
            }

            if (_segmentsLengths.Count < 5)
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
            _segmentsLengths.Clear();

            Length = 0f;
        }

        public void SetPoint(int index, Vector3 position, bool useGlobal = true)
        {
            CheckAndTransformPositionToLocal(ref position, useGlobal);
            _points[index] = new Point(position, _points[index].Rotation);

            RecalculateSegmentsAfterChanging(index);
        }

        public void SetPoint(int index, Quaternion rotation, bool useGlobal = true)
        {
            CheckAndTransformRotationToLocal(ref rotation, useGlobal);
            _points[index] = new Point(_points[index].Position, rotation);

            RecalculateSegmentsAfterChanging(index);
        }

        public void SetPoint(int index, Vector3 position, Quaternion rotation, bool useGlobal = true) => SetPoint(index, new Point(position, rotation), useGlobal);

        public void SetPoint(int index, Point point, bool useGlobal = true)
        {
            CheckAndTransformPointToLocal(ref point, useGlobal);
            _points[index] = point;

            RecalculateSegmentsAfterChanging(index);
        }

        private Vector3 GetSegmentStartDirection(int segment)
        {
            var p0 = _points[WrapIndex(segment - 1)];
            var p1 = _points[WrapIndex(segment)];
            var p2 = _points[WrapIndex(segment + 1)];
            var p3 = _points[WrapIndex(segment + 2)];

            var newPoint = CatmullRomSpline.CalculatePoint(_step, p0.Position, p1.Position, p2.Position, p3.Position);
            return (newPoint - p1.Position).normalized;
        }

        private Vector3 GetSegmentEndDirection(int segment)
        {
            var p0 = _points[WrapIndex(segment + 2)];
            var p1 = _points[WrapIndex(segment + 1)];
            var p2 = _points[WrapIndex(segment)];
            var p3 = _points[WrapIndex(segment - 1)];

            var newPoint = CatmullRomSpline.CalculatePoint(_step, p0.Position, p1.Position, p2.Position, p3.Position);
            return (p1.Position - newPoint).normalized;
        }

        public PointData GetPointSimple(int index, bool useGlobal = true)
        {
            var point = _points[index];
            var direction = _points.Count == 1 ? Vector3.zero : GetSegmentStartDirection(index);
            var pointData = new PointData(point, direction);

            return CheckAndTransformPointToGlobal(pointData, useGlobal);
        }

        public PointData GetPoint(int index, bool useGlobal = true)
        {
            if (index < 0)
                throw new Exception("Index can't be less that 0.");

            if (_points.Count == 0)
                throw new Exception("Path does not exist.");

            if (!_looped && _points.Count > 2)
            {
                index++;
                if (_points.Count == 3 && index > 2 || _points.Count > 3 && index > _points.Count - 2)
                    throw new Exception("Index can't be greater than active points count.");
            }

            return GetPointSimple(index, useGlobal);
        }

        public PointData GetPoint(int segment, float distance, bool useNormalizedDistance = true, bool useGlobal = true)
        {
            if (_points.Count == 0)
                throw new Exception("Path does not contain points.");

            if (_points.Count == 1)
                return new PointData(CheckAndTransformPointToGlobal(0, useGlobal), Vector3.zero);

            var length = GetSegmentLength(segment);
            float normalizedDistance;

            if (useNormalizedDistance)
            {
                normalizedDistance = Mathf.Clamp01(distance);
                distance = length * normalizedDistance;
            }
            else
            {
                distance = Mathf.Clamp(distance, 0f, length);
                normalizedDistance = distance / length;
            }

            PointData pointData = new();

            if (_points.Count == 2)
            {
                var (from, to) = segment == 0 ? (0, 1) : (1, 0);
                pointData.Position = Vector3.Lerp(_points[from].Position, _points[to].Position, normalizedDistance);
                pointData.Rotation = Quaternion.Lerp(_points[from].Rotation, _points[to].Rotation, normalizedDistance);
                pointData.Direction = (_points[to].Position - _points[from].Position).normalized;

                if (useGlobal)
                {
                    pointData.Position = transform.TransformPoint(pointData.Position);
                    pointData.Rotation = transform.rotation * pointData.Rotation;
                    pointData.Direction = transform.TransformDirection(pointData.Direction);
                }

                return pointData;
            }

            if (_points.Count > 2 && !_looped)
                segment += 1;

            // For 3 and more points..

            Point p0, p1, p2, p3;
            Vector3 direction;

            if (distance == 0f)
                return GetPointSimple(segment, useGlobal);
            else if (distance == length)
            {
                if (_looped && segment != SegmentsCount - 1 || !_looped && segment - 1 != SegmentsCount - 1)
                    return GetPointSimple(segment + 1, useGlobal);
                else
                {
                    direction = GetSegmentEndDirection(segment);
                    return CheckAndTransformPointToGlobal(new PointData(_points[WrapIndex(segment + 1)], direction), useGlobal);
                }
            }

            var step = 1f / Resolution;
            var t = step;

            var lastPosition = _points[segment].Position;

            p0 = _points[WrapIndex(segment - 1)];
            p1 = _points[segment];
            p2 = _points[WrapIndex(segment + 1)];
            p3 = _points[WrapIndex(segment + 2)];

            Vector3 position;
            float currentLength;

            while (t < 1f)
            {
                position = CatmullRomSpline.CalculatePoint(t, p0.Position, p1.Position, p2.Position, p3.Position);
                currentLength = Vector3.Distance(lastPosition, position);

                if (distance <= currentLength)
                {
                    pointData.Position = Vector3.Lerp(lastPosition, position, distance / currentLength);
                    pointData.Rotation = Quaternion.Lerp(p1.Rotation, p2.Rotation, normalizedDistance);
                    pointData.Direction = (position - lastPosition).normalized;

                    return CheckAndTransformPointToGlobal(pointData, useGlobal);
                }

                distance -= currentLength;
                lastPosition = position;
                t += step;
            }

            position = CatmullRomSpline.CalculatePoint(1f, p0.Position, p1.Position, p2.Position, p3.Position);
            currentLength = Vector3.Distance(lastPosition, position);

            pointData.Position = Vector3.Lerp(lastPosition, position, distance / currentLength);
            pointData.Rotation = Quaternion.Lerp(p1.Rotation, p2.Rotation, normalizedDistance);
            pointData.Direction = (position - lastPosition).normalized;

            return CheckAndTransformPointToGlobal(pointData, useGlobal);
        }

        public PointData GetPoint(float distance, bool useNormalizedDistance = true, bool useGlobal = true)
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