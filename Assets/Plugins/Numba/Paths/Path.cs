using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Paths
{
    /// <summary>
    /// Represents a path in three-dimensional space, passing through points.<br/>
    /// Points are represent by the <see cref="Point"/> type.
    /// </summary>
    public class Path : MonoBehaviour
    {
        #region State
        [SerializeField]
        private List<Point> _points = new();

        /// <summary>
        /// Points count from which the path contains.
        /// </summary>
        public int PointsCount => _points.Count;

        [SerializeField]
        private List<float> _segmentsLengths = new();

        /// <summary>
        /// The number of segments that make up a path. A segment is a curve between two points.<br/>
        /// In a looped path the number of segments is equal to the number of points.<br/>
        /// In a non-circular path, the number of segments is either 3 (when the path consists of 3 points)<br/>
        /// or the number of points minus 3 (when the path consists of more than 3 points).
        /// </summary>
        public int SegmentsCount
        {
            get
            {
                if (_points.Count < 2)
                    return 0;

                return _looped ? _points.Count : _points.Count < 4 ? 1 : _points.Count - 3;
            }
        }

        [SerializeField]
        private int _segmentCalculatorIndex;

        [SerializeField]
        private int _resolution = 1;

        /// <summary>
        /// Represents the number of sub-segments between two points. Clamped between 1 and 100.
        /// </summary>
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

        /// <summary>
        /// Is the path looped?
        /// </summary>
        public bool Looped
        {
            get => _looped;
            set
            {
                _looped = value;
                RecalculatePathLength();
            }
        }

        [SerializeField]
        private float _length;

        /// <summary>
        /// Length of the whole path.
        /// </summary>
        public float Length
        {
            get => _length;
            private set => _length = value;
        }
        #endregion

        #region Create
        /// <summary>
        /// Creates a path in the center of global coordinates and with <see cref="Quaternion.identity"/> rotation.
        /// </summary>
        /// <returns>The path.</returns>
        public static Path Create() => Create(Vector3.zero);

        /// <summary>
        /// Creates a path at the specified position.
        /// </summary>
        /// <param name="pivotPosition">Position of the path pivot.</param>
        /// <returns><inheritdoc cref="Create"/></returns>
        public static Path Create(Vector3 pivotPosition)
        {
            var path = new GameObject("Path").AddComponent<Path>();
            path.transform.position = pivotPosition;

            return path;
        }

        /// <summary>
        /// Creates a path at a specified position with specified points.
        /// </summary>
        /// <param name="pivotPosition"><inheritdoc cref="Create(Vector3)" path="/param[@name='pivotPosition']"/></param>
        /// <param name="useGlobal">Are the points passed in global space?</param>
        /// <param name="points">The points representing the path.</param>
        /// <returns><inheritdoc cref="Create"/></returns>
        public static Path Create(Vector3 pivotPosition, bool useGlobal, params Vector3[] points) => Create(pivotPosition, useGlobal, (IEnumerable<Vector3>)points);

        /// <summary>
        /// <inheritdoc cref="Create(Vector3, bool, Vector3[])"/>
        /// </summary>
        /// <param name="pivotPosition"><inheritdoc cref="Create(Vector3)" path="/param[@name='pivotPosition']"/></param>
        /// <param name="useGlobal"><inheritdoc cref="Create(Vector3, bool, Vector3[])" path="/param[@name='useGlobal']"/></param>
        /// <param name="points"><inheritdoc cref="Create(Vector3, bool, Vector3[])" path="/param[@name='points']"/></param>
        /// <returns><inheritdoc cref="Create"/></returns>
        public static Path Create(Vector3 pivotPosition, bool useGlobal, IEnumerable<Vector3> points)
        {
            var path = Create(pivotPosition);

            foreach (var point in points)
                path.AddPoint(point, useGlobal);

            return path;
        }

        /// <summary>
        /// Creates a path representing a polygon.
        /// </summary>
        /// <param name="pivotPosition"><inheritdoc cref="Create(Vector3)" path="/param[@name='pivotPosition']"/></param>
        /// <param name="sideCount">How many sides does a polygon have?</param>
        /// <param name="radius">How far away is each corner of the polygon from its center?</param>
        /// <returns><inheritdoc cref="Create"/></returns>
        public static Path CreatePolygon(Vector3 pivotPosition, int sideCount, float radius) => CreatePolygon(pivotPosition, Vector3.up, sideCount, radius);

        /// <summary>
        /// <inheritdoc cref="CreatePolygon(Vector3, int, float)"/>
        /// </summary>
        /// <param name="pivotPosition"><inheritdoc cref="Create(Vector3)" path="/param[@name='pivotPosition']"/></param>
        /// <param name="normal">Where is the face of the landfill pointing?</param>
        /// <param name="sideCount"><inheritdoc cref="CreatePolygon(Vector3, int, float)" path="/param[@name='sideCount']"/></param>
        /// <param name="radius"><inheritdoc cref="CreatePolygon(Vector3, int, float)" path="/param[@name='radius']"/></param>
        /// <returns><inheritdoc cref="Create"/></returns>
        /// <exception cref="ArgumentException"></exception>
        public static Path CreatePolygon(Vector3 pivotPosition, Vector3 normal, int sideCount, float radius)
        {
            if (sideCount < 3)
                throw new ArgumentException($"Side count can't be less that 3.", nameof(sideCount));

            if (radius <= 0f)
                throw new ArgumentException($"Radius must greater than 0.", nameof(radius));

            var path = Create(pivotPosition);
            normal.Normalize();

            var normalRotation = Quaternion.LookRotation(normal, normal == Vector3.up ? Vector3.forward : Vector3.up);
            var deltaAngle = 360f / sideCount;

            for (int i = 0; i < sideCount; i++)
            {
                var angle = deltaAngle * i;
                var vector = normalRotation * new Vector3(0f, radius, 0f);
                vector = Quaternion.AngleAxis(angle, normal) * vector;

                path.AddPoint(vector, false);
            }

            return path;
        }

        /// <summary>
        /// Creates a path that represents a two-dimensional spiral.
        /// </summary>
        /// <param name="pivotPosition"><inheritdoc cref="Create(Vector3)" path="/param[@name='pivotPosition']"/></param>
        /// <param name="offsetAngle">Angular offset of the beginning of the spiral</param>
        /// <param name="coils">How many turns in the coil?</param>
        /// <param name="step">What is the distance between the coils?</param>
        /// <param name="pointsCountPerCoil">How many points should be generated per turn?</param>
        /// <returns><inheritdoc cref="Create"/></returns>
        public static Path CreateSpiral(Vector3 pivotPosition, float offsetAngle, int coils, float step, int pointsCountPerCoil) => CreateSpiral(pivotPosition, Vector3.up, offsetAngle, coils, step, pointsCountPerCoil);

        /// <summary>
        /// <inheritdoc cref="CreateSpiral(Vector3, float, int, float, int)"/>
        /// </summary>
        /// <param name="pivotPosition"><inheritdoc cref="Create(Vector3)" path="/param[@name='pivotPosition']"/></param>
        /// <param name="normal"><inheritdoc cref="CreatePolygon(Vector3, Vector3, int, float)" path="/param[@name='normal']"/></param>
        /// <param name="offsetAngle"><inheritdoc cref="CreateSpiral(Vector3, float, int, float, int)" path="/param[@name='offsetAngle']"/></param>
        /// <param name="coils"><inheritdoc cref="CreateSpiral(Vector3, float, int, float, int)" path="/param[@name='coils']"/></param>
        /// <param name="step"><inheritdoc cref="CreateSpiral(Vector3, float, int, float, int)" path="/param[@name='step']"/></param>
        /// <param name="pointsCountPerCoil"><inheritdoc cref="CreateSpiral(Vector3, float, int, float, int)" path="/param[@name='pointsCountPerCoil']"/></param>
        /// <returns><inheritdoc cref="Create"/></returns>
        /// <exception cref="ArgumentException"></exception>
        public static Path CreateSpiral(Vector3 pivotPosition, Vector3 normal, float offsetAngle, int coils, float step, int pointsCountPerCoil)
        {
            if (coils < 1)
                throw new ArgumentException("Coils can't be less than 0.", nameof(coils));

            if (Mathf.Approximately(step, 0f) || step < 0f)
                throw new ArgumentException("Step can't be equal or less than 0.", nameof(step));

            if (pointsCountPerCoil < 3)
                throw new ArgumentException("Points coint per coil cant be less than 3.", nameof(pointsCountPerCoil));

            var path = Create(pivotPosition);
            normal.Normalize();

            var normalRotation = Quaternion.LookRotation(normal, normal == Vector3.up ? Vector3.forward : Vector3.up);

            var deltaAngle = 360f / pointsCountPerCoil;
            var angle = -deltaAngle;
            offsetAngle *= -1f;

            var ray = new Ray(path.transform.position, normalRotation * Quaternion.AngleAxis(offsetAngle - deltaAngle, Vector3.back) * Vector3.up);

            for (int i = 0; i < coils; i++)
            {
                for (int j = 0; j < pointsCountPerCoil; j++)
                {
                    angle += deltaAngle;
                    ray.direction = Quaternion.AngleAxis(deltaAngle, normal) * ray.direction;

                    var distance = (step / (2f * Mathf.PI)) * (angle * Mathf.Deg2Rad);
                    path.AddPoint(ray.GetPoint(distance), false);
                }
            }

            ray = new Ray(path.transform.position, normalRotation * Quaternion.AngleAxis(offsetAngle - deltaAngle, Vector3.back) * Vector3.up);
            path.InsertPoint(0, ray.GetPoint((step / (2f * Mathf.PI)) * (-deltaAngle * Mathf.Deg2Rad)), false);

            return path;
        }

        /// <summary>
        /// Creates a path that represents a three-dimensional spiral.
        /// </summary>
        /// <param name="pivotPosition"><inheritdoc cref="Create(Vector3)" path="/param[@name='pivotPosition']"/></param>
        /// <param name="offsetAngle"><inheritdoc cref="CreateSpiral(Vector3, float, int, float, int)" path="/param[@name='offsetAngle']"/></param>
        /// <param name="coils"><inheritdoc cref="CreateSpiral(Vector3, float, int, float, int)" path="/param[@name='coils']"/></param>
        /// <param name="step"><inheritdoc cref="CreateSpiral(Vector3, float, int, float, int)" path="/param[@name='step']"/></param>
        /// <param name="pointsCountPerCoil"><inheritdoc cref="CreateSpiral(Vector3, float, int, float, int)" path="/param[@name='pointsCountPerCoil']"/></param>
        /// <returns><inheritdoc cref="Create"/></returns>
        public static Path CreateSpiral3D(Vector3 pivotPosition, float offsetAngle, int coils, float step, int pointsCountPerCoil) => CreateSpiral3D(pivotPosition, Vector3.up, offsetAngle, coils, step, pointsCountPerCoil);

        /// <summary>
        /// <inheritdoc cref="CreateSpiral3D(Vector3, float, int, float, int)"/>
        /// </summary>
        /// <param name="pivotPosition"><inheritdoc cref="Create(Vector3)" path="/param[@name='pivotPosition']"/></param>
        /// <param name="normal"><inheritdoc cref="CreatePolygon(Vector3, Vector3, int, float)" path="/param[@name='normal']"/></param>
        /// <param name="offsetAngle"><inheritdoc cref="CreateSpiral(Vector3, float, int, float, int)" path="/param[@name='offsetAngle']"/></param>
        /// <param name="coils"><inheritdoc cref="CreateSpiral(Vector3, float, int, float, int)" path="/param[@name='coils']"/></param>
        /// <param name="step"><inheritdoc cref="CreateSpiral(Vector3, float, int, float, int)" path="/param[@name='step']"/></param>
        /// <param name="pointsCountPerCoil"><inheritdoc cref="CreateSpiral(Vector3, float, int, float, int)" path="/param[@name='pointsCountPerCoil']"/></param>
        /// <returns><inheritdoc cref="Create"/></returns>
        /// <exception cref="ArgumentException"></exception>
        public static Path CreateSpiral3D(Vector3 pivotPosition, Vector3 normal, float offsetAngle, int coils, float step, int pointsCountPerCoil)
        {
            if (coils < 1)
                throw new ArgumentException("Coils can't be less than 0.", nameof(coils));

            if (Mathf.Approximately(step, 0f) || step < 0f)
                throw new ArgumentException("Step can't be equal or less than 0.", nameof(step));

            if (pointsCountPerCoil < 3)
                throw new ArgumentException("Points coint per coil cant be less than 3.", nameof(pointsCountPerCoil));

            var path = Create(pivotPosition);
            normal.Normalize();

            var normalRotation = Quaternion.LookRotation(normal, normal == Vector3.up ? Vector3.forward : Vector3.up);

            var deltaAngle = 360f / pointsCountPerCoil;
            var angle = -deltaAngle;
            offsetAngle *= -1f;
            var prevPoint = Vector3.zero;

            var ray = new Ray(path.transform.position, normalRotation * Quaternion.AngleAxis(offsetAngle - deltaAngle, Vector3.back) * Vector3.up);

            for (int i = 0; i < coils; i++)
            {
                for (int j = 0; j < pointsCountPerCoil; j++)
                {
                    angle += deltaAngle;
                    ray.direction = Quaternion.AngleAxis(deltaAngle, normal) * ray.direction;

                    var distance = (step / (2f * Mathf.PI)) * (angle * Mathf.Deg2Rad);
                    var point = ray.GetPoint(distance);
                    path.AddPoint(point + normal * Vector3.Distance(point, prevPoint), false);
                    prevPoint = point;
                }
            }

            ray = new Ray(path.transform.position, normalRotation * Quaternion.AngleAxis(offsetAngle - deltaAngle, Vector3.back) * Vector3.up);
            path.InsertPoint(0, ray.GetPoint((step / (2f * Mathf.PI)) * (-deltaAngle * Mathf.Deg2Rad)), false);
            var pos = path.GetPointByIndex(0, false).Position;
            path.SetPoint(0, pos - normal * Vector3.Distance(pos, path.GetPointByIndex(1, false).Position));

            return path;
        }
        #endregion

        #region Editor
#if UNITY_EDITOR
        [MenuItem("GameObject/Path/Empty")]
        private static void CreateEmpty()
        {
            var path = Create();
            path.name = "Path";
            path.transform.SetParent(Selection.activeTransform, false);

            Selection.activeObject = path;
            Undo.RegisterCreatedObjectUndo(path.gameObject, "Create Path");
        }

        [MenuItem("GameObject/Path/Line")]
        private static void CreateLine()
        {
            var path = Create();
            path.name = "Path (Line)";
            path.transform.SetParent(Selection.activeTransform, false);

            path.AddPoint(new Vector3(-0.5f, 0f, 0f), false);
            path.AddPoint(new Vector3(0.5f, 0f, 0f), false);

            Selection.activeObject = path;
            Undo.RegisterCreatedObjectUndo(path.gameObject, "Create Path (Line)");
        }

        private static Path CreatePolygon(int sideCount, string figureName)
        {
            var path = CreatePolygon(Vector3.zero, sideCount, 1f);
            path.name = $"Path ({figureName})";
            path.transform.SetParent(Selection.activeTransform, false);
            path.Looped = true;

            Selection.activeObject = path;
            Undo.RegisterCreatedObjectUndo(path.gameObject, $"Create Path ({figureName})");

            return path;
        }

        [MenuItem("GameObject/Path/Triangle")]
        private static void CreateTriangle() => CreatePolygon(3, "Triangle");

        [MenuItem("GameObject/Path/Rhombus")]
        private static void CreateRhombus() => CreatePolygon(4, "Rhombus");

        [MenuItem("GameObject/Path/Pentagon")]
        private static void CreatePentagon() => CreatePolygon(5, "Pentagon");

        [MenuItem("GameObject/Path/Hexagon")]
        private static void CreateHexagon() => CreatePolygon(6, "Hexagon");

        [MenuItem("GameObject/Path/Octagon")]
        private static void CreateOctagon() => CreatePolygon(8, "Octagon");

        [MenuItem("GameObject/Path/Circle")]
        private static void CreateCircle() => CreatePolygon(12, "Circle").Resolution = 2;

        [MenuItem("GameObject/Path/Spiral")]
        private static void CreateSpiral()
        {
            var path = CreateSpiral(Vector3.zero, 0f, 3, 0.3333333f, 8);
            path.name = "Path (Spiral)";
            path.transform.SetParent(Selection.activeTransform, false);

            Selection.activeObject = path;
            Undo.RegisterCreatedObjectUndo(path.gameObject, "Create Path");
        }

        [MenuItem("GameObject/Path/Spiral3D")]
        private static void CreateSpiral3D()
        {
            var path = CreateSpiral3D(Vector3.zero, 0f, 3, 0.3333333f, 8);
            path.name = "Path (Spiral3D)";
            path.transform.SetParent(Selection.activeTransform, false);

            Selection.activeObject = path;
            Undo.RegisterCreatedObjectUndo(path.gameObject, "Create Path");
        }
#endif
        #endregion

        private int WrapIndex(int index) => (((index - 0) % (_points.Count - 0)) + (_points.Count - 0)) % (_points.Count - 0) + 0;

        /// <summary>
        /// Optimize the path. The algorithm takes into account the positions of control and end points,<br/>
        /// thus trying to anticipate rough turns in the path and smooth them out by increasing the resolution.
        /// </summary>
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

        private PointData TransformPointDataToWorldSpace(PointData pointData)
        {
            return new PointData(transform.TransformPoint(pointData.Position), transform.rotation * pointData.Rotation, transform.TransformDirection(pointData.Direction));
        }

        #region Segments operations
        private void SetNewSegmentsCalculator() => _segmentCalculatorIndex = Mathf.Min(_segmentsLengths.Count, 3);

        private void RecalculateAllSegments()
        {
            for (int i = 0; i < _segmentsLengths.Count; i++)
                RecalculateSegmentLength(i);

            RecalculatePathLength();
        }

        private void RecalculateSegmentLength(int segment)
        {
            if (_segmentCalculatorIndex == 1)
                _segmentsLengths[segment] = 0f;
            else if (_segmentCalculatorIndex == 2)
                _segmentsLengths[segment] = Vector3.Distance(_points[0].Position, _points[1].Position);
            else if (_segmentCalculatorIndex == 3)
            {
                var length = 0f;

                var t = 0f;
                var lastPosition = _points[segment].Position;

                var p0 = _points[WrapIndex(segment - 1)].Position;
                var p1 = _points[segment].Position;
                var p2 = _points[WrapIndex(segment + 1)].Position;
                var p3 = _points[WrapIndex(segment + 2)].Position;

                while (t < 1f)
                {
                    var position = CatmullRomSpline.CalculatePoint(t, p0, p1, p2, p3);
                    length += Vector3.Distance(lastPosition, position);

                    lastPosition = position;
                    t += _step;
                }

                _segmentsLengths[segment] = length += Vector3.Distance(lastPosition, CatmullRomSpline.CalculatePoint(1f, p0, p1, p2, p3));
            }
        }

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

        /// <summary>
        /// Gets path's segment length.
        /// </summary>
        /// <param name="segment">The segment the length of which you want to get.</param>
        /// <returns>Segment's length.</returns>
        /// <exception cref="ArgumentException"></exception>
        public float GetSegmentLength(int segment)
        {
            if (_points.Count < 2)
                throw new ArgumentException($"Segment {segment} not exist.", nameof(segment));

            if (!_looped)
            {
                if (_points.Count < 4 && segment > 0)
                    throw new ArgumentException($"Segment {segment} not exist.", nameof(segment));

                if (_points.Count > 3 && segment > _points.Count - 4)
                    throw new ArgumentException($"Segment {segment} not exist.", nameof(segment));

                if (_points.Count > 2)
                    segment += 1;
            }
            else if (segment > _points.Count - 1)
                throw new ArgumentException($"Segment {segment} not exist.", nameof(segment));

            return _segmentsLengths[segment];
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
        #endregion

        #region Points operations
        /// <summary>
        /// Creates point with specified <paramref name="position"/> and adds it to the end of the path.
        /// </summary>
        /// <param name="position">Point's position.</param>
        /// <param name="useGlobal">Is the <paramref name="position"/> being passed in global space?</param>
        public void AddPoint(Vector3 position, bool useGlobal = true) => AddPoint(new Point(position, Quaternion.identity), useGlobal);

        /// <summary>
        /// Adds a <paramref name="point"/> to the end of the path.
        /// </summary>
        /// <param name="point">Point which will be added.</param>
        /// <param name="useGlobal">Is the <paramref name="point"/> being passed in global space?</param>
        public void AddPoint(Point point, bool useGlobal = true)
        {
            if (useGlobal)
                point.Position = transform.InverseTransformPoint(point.Position);

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

        /// <summary>
        /// Creates point with specified <paramref name="position"/> and inserts it by the specified <paramref name="index"/> in the path.
        /// </summary>
        /// <param name="index">The index by which you want to insert the point.</param>
        /// <param name="position"><inheritdoc cref="AddPoint(Vector3, bool)" path="/param[@name='position']"/></param>
        /// <param name="useGlobal"><inheritdoc cref="AddPoint(Vector3, bool)" path="/param[@name='useGlobal']"/></param>
        public void InsertPoint(int index, Vector3 position, bool useGlobal = true) => InsertPoint(index, new Point(position, Quaternion.identity), useGlobal);

        /// <summary>
        /// Inserts <paramref name="point"/> by the specified <paramref name="index"/> in the path.
        /// </summary>
        /// <param name="index"><inheritdoc cref="InsertPoint(int, Vector3, bool)" path="/param[@name='index']"/></param>
        /// <param name="point">Point which will be inserted.</param>
        /// <param name="useGlobal">Is the <paramref name="point"/> being passed in global space?</param>
        public void InsertPoint(int index, Point point, bool useGlobal = true)
        {
            if (useGlobal)
                point.Position = transform.InverseTransformPoint(point.Position);

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

        /// <summary>
        /// Checks if the path contains a point with the specified <paramref name="position"/>.
        /// </summary>
        /// <param name="position"><inheritdoc cref="AddPoint(Vector3, bool)" path="/param[@name='position']"/></param>
        /// <param name="useGlobal"><inheritdoc cref="AddPoint(Vector3, bool)" path="/param[@name='useGlobal']"/></param>
        /// <returns><see langword="true"/> if a point has been found.</returns>
        public bool ContainsPoint(Vector3 position, bool useGlobal = true)
        {
            if (useGlobal)
                position = transform.InverseTransformPoint(position);

            return _points.FindIndex(p => p.Position == position) != -1;
        }

        /// <summary>
        /// Checks if the path contains a <paramref name="point"/>.
        /// </summary>
        /// <param name="point">The point to be found.</param>
        /// <param name="useGlobal"><inheritdoc cref="AddPoint(Vector3, bool)" path="/param[@name='useGlobal']"/></param>
        /// <returns><inheritdoc cref="ContainsPoint"/></returns>
        public bool ContainsPoint(Point point, bool useGlobal = true)
        {
            if (useGlobal)
                point.Position = transform.InverseTransformPoint(point.Position);

            return _points.Contains(point);
        }

        /// <summary>
        /// Finds the index of the point with the specified <paramref name="position"/>.
        /// </summary>
        /// <param name="position"><inheritdoc cref="AddPoint(Vector3, bool)" path="/param[@name='position']"/></param>
        /// <param name="useGlobal"><inheritdoc cref="AddPoint(Vector3, bool)" path="/param[@name='useGlobal']"/></param>
        /// <returns>Index of the desired point, otherwise <see langword="-1"/>.</returns>
        public int IndexOfPoint(Vector3 position, bool useGlobal = true)
        {
            if (useGlobal)
                position = transform.InverseTransformPoint(position);

            return _points.FindIndex(p => p.Position == position);
        }

        /// <summary>
        /// Finds the index of the specified <paramref name="point"/>.
        /// </summary>
        /// <param name="point"><inheritdoc cref="ContainsPoint(Point, bool)" path="/param[@name='point']"/></param>
        /// <param name="useGlobal"><inheritdoc cref="AddPoint(Point, bool)" path="/param[@name='useGlobal']"/></param>
        /// <returns><inheritdoc cref="IndexOfPoint"/></returns>
        public int IndexOfPoint(Point point, bool useGlobal = true)
        {
            if (useGlobal)
                point.Position = transform.InverseTransformPoint(point.Position);

            return _points.IndexOf(point);
        }

        /// <summary>
        /// Removes a point at a specified <paramref name="position"/> in space.
        /// </summary>
        /// <param name="position"><inheritdoc cref="AddPoint(Vector3, bool)" path="/param[@name='position']"/></param>
        /// <param name="useGlobal"><inheritdoc cref="AddPoint(Vector3, bool)" path="/param[@name='useGlobal']"/></param>
        /// <returns><see langword="true"/> if a point has been found and removed.</returns>
        public bool RemovePoint(Vector3 position, bool useGlobal = true)
        {
            if (useGlobal)
                position = transform.InverseTransformPoint(position);

            var index = _points.FindIndex(p => p.Position == position);
            if (index == -1)
                return false;

            RemovePointAt(index);
            return true;
        }

        /// <summary>
        /// Removes a point from path.
        /// </summary>
        /// <param name="point">Point to remove.</param>
        /// <param name="useGlobal"><inheritdoc cref="AddPoint(Vector3, bool)" path="/param[@name='useGlobal']"/></param>
        /// <returns><see langword="true"/> if a point has been found and removed.</returns>
        public bool RemovePoint(Point point, bool useGlobal = true)
        {
            if (useGlobal)
                point.Position = transform.InverseTransformPoint(point.Position);

            var index = _points.IndexOf(point);
            if (index == -1)
                return false;

            RemovePointAt(index);
            return true;
        }

        /// <summary>
        /// Deletes a point at a specified <paramref name="index"/>.
        /// </summary>
        /// <param name="index">Index of the point to be removed.</param>
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

        /// <summary>
        /// Removes all points in the path.
        /// </summary>
        public void ClearPoints()
        {
            _points.Clear();
            _segmentsLengths.Clear();

            Length = 0f;
        }
        #endregion

        /// <summary>
        /// Sets the <paramref name="position"/> of the point by the <paramref name="index"/>.
        /// </summary>
        /// <param name="index">Index of the point.</param>
        /// <param name="position">Position to be set</param>
        /// <param name="useGlobal">Is the <paramref name="position"/> being passed in global space?</param>
        public void SetPoint(int index, Vector3 position, bool useGlobal = true)
        {
            if (useGlobal)
                position = transform.InverseTransformPoint(position);

            var point = _points[index];
            point.Position = position;
            _points[index] = point;

            RecalculateSegmentsAfterChanging(index);
        }

        /// <summary>
        /// Sets the <paramref name="rotation"/> of the point by the <paramref name="index"/>.
        /// </summary>
        /// <param name="index"><inheritdoc cref="SetPoint(int, Vector3, bool)" path="/param[@name='index']"/></param>
        /// <param name="rotation">Rotation to be set</param>
        /// <param name="useGlobal">Is the <paramref name="rotation"/> being passed in global space?</param>
        public void SetPoint(int index, Quaternion rotation, bool useGlobal = true)
        {
            if (useGlobal)
                rotation = Quaternion.Inverse(transform.rotation) * rotation;

            var point = _points[index];
            point.Rotation = rotation;
            _points[index] = point;

            RecalculateSegmentsAfterChanging(index);
        }

        /// <summary>
        /// Sets the <paramref name="position"/> and <paramref name="rotation"/> of the point by the <paramref name="index"/>.
        /// </summary>
        /// <param name="index"><inheritdoc cref="SetPoint(int, Vector3, bool)" path="/param[@name='index']"/></param>
        /// <param name="position"><inheritdoc cref="SetPoint(int, Vector3, bool)" path="/param[@name='position']"/></param>
        /// <param name="rotation"><inheritdoc cref="SetPoint(int, Quaternion, bool)" path="/param[@name='rotation']"/></param>
        /// <param name="useGlobal">Is the <paramref name="position"/> and <paramref name="rotation"/> being passed in global space?</param>
        public void SetPoint(int index, Vector3 position, Quaternion rotation, bool useGlobal = true) => SetPoint(index, new Point(position, rotation), useGlobal);

        /// <summary>
        /// Sets the <paramref name="point"/> by the <paramref name="index"/>.
        /// </summary>
        /// <param name="index"><inheritdoc cref="SetPoint(int, Vector3, bool)" path="/param[@name='index']"/></param>
        /// <param name="point">Point to be set.</param>
        /// <param name="useGlobal">Is the <paramref name="point"/> being passed in global space?</param>
        public void SetPoint(int index, Point point, bool useGlobal = true)
        {
            if (useGlobal)
            {
                point.Position = transform.InverseTransformPoint(point.Position);
                point.Rotation = Quaternion.Inverse(transform.rotation) * point.Rotation;
            }

            _points[index] = point;

            RecalculateSegmentsAfterChanging(index);
        }

        /// <summary>
        /// Calculates and returns a point by the specified index.
        /// </summary>
        /// <param name="index">Index of the point.</param>
        /// <param name="useGlobal">Is it necessary to convert a point from local to global space?</param>
        /// <returns>Calculated point.</returns>
        public PointData GetPointByIndex(int index, bool useGlobal = true)
        {
            var point = _points[WrapIndex(index)];
            var direction = _points.Count == 1 ? Vector3.zero : GetSegmentStartDirection(index);
            var pointData = new PointData(point, direction);

            if (useGlobal)
                pointData = TransformPointDataToWorldSpace(pointData);

            return pointData;
        }

        /// <summary>
        /// Calculates and returns a point by the specified index.<br/>
        /// This method does not take control points into account (for example, when the path is not looped and the number of points is greater than 2).        
        /// </summary>
        /// <param name="index">Index of the point.</param>
        /// <param name="useGlobal">Is it necessary to convert a calculated point from local space to global?</param>
        /// <returns><inheritdoc cref="GetPointByIndex(int, bool )"/></returns>
        /// <exception cref="ArgumentException"></exception>
        public PointData GetPoint(int index, bool useGlobal = true)
        {
            if (_points.Count == 0 || index < 0)
                throw new ArgumentException("Index can't be less than 0.", nameof(index));

            if (!_looped && _points.Count > 2)
            {
                index++;
                if (_points.Count == 3 && index > 2 || _points.Count > 3 && index > _points.Count - 2)
                    throw new ArgumentException("Index can't be greater than end points count.", nameof(index));
            }

            return GetPointByIndex(index, useGlobal);
        }

        /// <summary>
        /// Calculates and returns a point on the specified <paramref name="segment"/>.<br/>
        /// </summary>
        /// <param name="segment">On which segment do you want to calculate the point?</param>
        /// <param name="distance">At what distance from the beginning of the <paramref name="segment"/> should calculate the point?</param>
        /// <param name="useNormalizedDistance">Is the distance passed in normalized form?</param>
        /// <param name="useGlobal"><inheritdoc cref="GetPoint(int, bool)" path="/param[@name='useGlobal']"/></param>
        /// <returns><inheritdoc cref="GetPointByIndex(int, bool )"/></returns>
        /// <exception cref="ArgumentException"></exception>
        public PointData GetPoint(int segment, float distance, bool useNormalizedDistance = true, bool useGlobal = true)
        {
            if (_points.Count < 2)
                throw new ArgumentException($"Path does not contain segment {segment}.", nameof(segment));

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

            Vector3 position, direction;
            Quaternion rotation;

            if (_points.Count == 2)
            {
                var (from, to) = segment == 0 ? (0, 1) : (1, 0);

                if (_points[0].Position == _points[1].Position)
                {
                    position = _points[0].Position;
                    direction = Vector3.zero;
                }
                else
                {
                    position = Vector3.Lerp(_points[from].Position, _points[to].Position, normalizedDistance);
                    direction = (_points[to].Position - _points[from].Position).normalized;
                }

                if (_points[0].Rotation == _points[1].Rotation)
                    rotation = _points[0].Rotation;
                else
                    rotation = Quaternion.Lerp(_points[from].Rotation, _points[to].Rotation, normalizedDistance);

                pointData = new PointData(position, rotation, direction);

                if (useGlobal)
                    pointData = new PointData(transform.TransformPoint(pointData.Position), transform.rotation * pointData.Rotation, transform.TransformDirection(pointData.Direction));

                return pointData;
            }

            if (_points.Count > 2 && !_looped)
                segment += 1;

            // For 3 and more points..

            Point p0, p1, p2, p3;

            if (Mathf.Approximately(distance, 0f))
                return GetPointByIndex(segment, useGlobal);
            else if (Mathf.Approximately(distance, length))
            {
                if (_looped && segment != SegmentsCount - 1 || !_looped && segment - 1 != SegmentsCount - 1)
                    return GetPointByIndex(segment + 1, useGlobal);
                else
                {
                    direction = GetSegmentEndDirection(segment);

                    if (useGlobal)
                        pointData = TransformPointDataToWorldSpace(new PointData(_points[WrapIndex(segment + 1)], direction));

                    return pointData;
                }
            }

            var step = 1f / Resolution;
            var t = step;

            var lastPosition = _points[segment].Position;

            p0 = _points[WrapIndex(segment - 1)];
            p1 = _points[segment];
            p2 = _points[WrapIndex(segment + 1)];
            p3 = _points[WrapIndex(segment + 2)];

            float currentLength;

            while (t < 1f)
            {
                position = CatmullRomSpline.CalculatePoint(t, p0.Position, p1.Position, p2.Position, p3.Position);
                currentLength = Vector3.Distance(lastPosition, position);

                if (distance <= currentLength)
                {
                    pointData = new PointData(Vector3.Lerp(lastPosition, position, distance / currentLength), Quaternion.Lerp(p1.Rotation, p2.Rotation, normalizedDistance), (position - lastPosition).normalized);

                    if (useGlobal)
                        pointData = TransformPointDataToWorldSpace(pointData);

                    return pointData;
                }

                distance -= currentLength;
                lastPosition = position;
                t += step;
            }

            position = CatmullRomSpline.CalculatePoint(1f, p0.Position, p1.Position, p2.Position, p3.Position);
            currentLength = Vector3.Distance(lastPosition, position);

            pointData = new PointData(Vector3.Lerp(lastPosition, position, distance / currentLength), Quaternion.Lerp(p1.Rotation, p2.Rotation, normalizedDistance), (position - lastPosition).normalized);

            if (useGlobal)
                pointData = TransformPointDataToWorldSpace(pointData);

            return pointData;
        }

        /// <summary>
        /// Calculates and returns a point on whole path.<br/>
        /// </summary>
        /// <param name="distance">At what distance from the beginning of the path should calculate the point?</param>
        /// <param name="useNormalizedDistance">Is the distance passed in normalized form?</param>
        /// <param name="useGlobal"><inheritdoc cref="GetPoint(int, bool)" path="/param[@name='useGlobal']"/></param>
        /// <returns><inheritdoc cref="GetPointByIndex(int, bool )"/></returns>
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