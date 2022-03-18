using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Redcode.Paths
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

        /// <summary>
        /// The number of points that lie on the path (yello points in scene view).
        /// </summary>
        public int PointsCountOnPath
        {
            get
            {
                if (_points.Count < 3)
                    return _points.Count;

                return _looped ? _points.Count : _points.Count <= 4 ? 2 : _points.Count - 2;
            }
        }

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

        public Path SetResolution(int resolution)
        {
            Resolution = resolution;
            return this;
        }

        public Path SetLooped(bool looped)
        {
            Looped = looped;
            return this;
        }

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
        /// <param name="sideCount">How many sides does a polygon have?</param>
        /// <param name="radius">How far away is each corner of the polygon from its center?</param>
        /// <returns><inheritdoc cref="Create"/></returns>
        public static Path CreatePolygon(int sideCount, float radius) => CreatePolygon(Vector3.zero, sideCount, radius);

        /// <summary>
        /// <inheritdoc cref="CreatePolygon(int, float)"/>
        /// </summary>
        /// <param name="pivotPosition"><inheritdoc cref="Create(Vector3)" path="/param[@name='pivotPosition']"/></param>
        /// <param name="sideCount"><inheritdoc cref="CreatePolygon(int, float)" path="/param[@name='sideCount']"/></param>
        /// <param name="radius"><inheritdoc cref="CreatePolygon(int, float)" path="/param[@name='radius']"/></param>
        /// <returns><inheritdoc cref="Create"/></returns>
        public static Path CreatePolygon(Vector3 pivotPosition, int sideCount, float radius) => CreatePolygon(pivotPosition, Vector3.up, Vector3.forward, sideCount, radius);

        /// <summary>
        /// <inheritdoc cref="CreatePolygon(int, float)"/>
        /// </summary>
        /// <param name="pivotPosition"><inheritdoc cref="Create(Vector3)" path="/param[@name='pivotPosition']"/></param>
        /// <param name="normal">Where is the face of the landfill pointing?</param>
        /// <param name="up">Where is the top pointing?</param>
        /// <param name="sideCount"><inheritdoc cref="CreatePolygon(Vector3, int, float)" path="/param[@name='sideCount']"/></param>
        /// <param name="radius"><inheritdoc cref="CreatePolygon(Vector3, int, float)" path="/param[@name='radius']"/></param>
        /// <returns><inheritdoc cref="Create"/></returns>
        /// <exception cref="ArgumentException"></exception>
        public static Path CreatePolygon(Vector3 pivotPosition, Vector3 normal, Vector3 up, int sideCount, float radius)
        {
            if (sideCount < 3)
                throw new ArgumentException($"Side count can't be less that 3.", nameof(sideCount));

            if (radius <= 0f)
                throw new ArgumentException($"Radius must greater than 0.", nameof(radius));

            var path = Create(pivotPosition);
            normal.Normalize();

            var normalRotation = Quaternion.LookRotation(normal, up);
            var deltaAngle = 360f / sideCount;

            for (int i = 0; i < sideCount; i++)
            {
                var angle = deltaAngle * i;
                var vector = normalRotation * new Vector3(0f, radius, 0f);
                vector = Quaternion.AngleAxis(angle, normal) * vector;

                path.AddPoint(vector, false);
            }

            path.Looped = true;

            return path;
        }

        /// <summary>
        /// Creates a path that represents spiral (2D or 3D).
        /// </summary>
        /// <param name="offsetAngle">Angular offset of the beginning of the spiral</param>
        /// <param name="coils">How many turns in the coil?</param>
        /// <param name="step">What is the distance between the coils?</param>
        /// <param name="pointsCountPerCoil">How many points should be generated per turn?</param>
        /// <param name="use3D">Is it necessary to use a three-dimensional spiral?</param>
        /// <returns><inheritdoc cref="Create"/></returns>
        public static Path CreateSpiral(float offsetAngle, int coils, float step, int pointsCountPerCoil, bool use3D = false) => CreateSpiral(Vector3.zero, offsetAngle, coils, step, pointsCountPerCoil, use3D);

        /// <summary>
        /// <inheritdoc cref="CreateSpiral(float, int, float, int, bool)"/>
        /// </summary>
        /// <param name="pivotPosition"><inheritdoc cref="Create(Vector3)" path="/param[@name='pivotPosition']"/></param>
        /// <param name="offsetAngle"><inheritdoc cref="CreateSpiral(float, int, float, int, bool)" path="/param[@name='offsetAngle']"/></param>
        /// <param name="coils"><inheritdoc cref="CreateSpiral(float, int, float, int, bool)" path="/param[@name='coils']"/></param>
        /// <param name="step"><inheritdoc cref="CreateSpiral(float, int, float, int, bool)" path="/param[@name='step']"/></param>
        /// <param name="pointsCountPerCoil"><inheritdoc cref="CreateSpiral(float, int, float, int, bool)" path="/param[@name='pointsCountPerCoil']"/></param>
        /// <param name="use3D"><inheritdoc cref="CreateSpiral(float, int, float, int, bool)" path="/param[@name='pointsCountPerCoil']"/></param>
        /// <returns><inheritdoc cref="Create"/></returns>
        public static Path CreateSpiral(Vector3 pivotPosition, float offsetAngle, int coils, float step, int pointsCountPerCoil, bool use3D = false) => CreateSpiral(pivotPosition, Vector3.up, Vector3.forward, offsetAngle, coils, step, pointsCountPerCoil, use3D);

        /// <summary>
        /// <inheritdoc cref="CreateSpiral(float, int, float, int, bool)"/>
        /// </summary>
        /// <param name="pivotPosition"><inheritdoc cref="Create(Vector3)" path="/param[@name='pivotPosition']"/></param>
        /// <param name="normal"><inheritdoc cref="CreatePolygon(Vector3, Vector3, Vector3, int, float)" path="/param[@name='normal']"/></param>
        /// <param name="up">Where is the top pointing?</param>
        /// <param name="offsetAngle"><inheritdoc cref="CreateSpiral(Vector3, float, int, float, int, bool)" path="/param[@name='offsetAngle']"/></param>
        /// <param name="coils"><inheritdoc cref="CreateSpiral(Vector3, float, int, float, int, bool)" path="/param[@name='coils']"/></param>
        /// <param name="step"><inheritdoc cref="CreateSpiral(Vector3, float, int, float, int, bool)" path="/param[@name='step']"/></param>
        /// <param name="pointsCountPerCoil"><inheritdoc cref="CreateSpiral(Vector3, float, int, float, int, bool)" path="/param[@name='pointsCountPerCoil']"/></param>
        /// <returns><inheritdoc cref="Create"/></returns>
        /// <exception cref="ArgumentException"></exception>
        public static Path CreateSpiral(Vector3 pivotPosition, Vector3 normal, Vector3 up, float offsetAngle, int coils, float step, int pointsCountPerCoil, bool use3D = false)
        {
            if (coils < 1)
                throw new ArgumentException("Coils can't be less than 0.", nameof(coils));

            if (Mathf.Approximately(step, 0f) || step < 0f)
                throw new ArgumentException("Step can't be equal or less than 0.", nameof(step));

            if (pointsCountPerCoil < 3)
                throw new ArgumentException("Points coint per coil cant be less than 3.", nameof(pointsCountPerCoil));

            var path = Create(pivotPosition);
            normal.Normalize();

            var normalRotation = Quaternion.LookRotation(normal, up);

            var deltaAngle = 360f / pointsCountPerCoil;
            var angle = -deltaAngle;
            offsetAngle *= -1f;

            var ray = new Ray(path.transform.position, normalRotation * Quaternion.AngleAxis(offsetAngle - deltaAngle, Vector3.back) * Vector3.up);

            if (!use3D)
            {
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
            }
            else
            {
                var prevPoint = Vector3.zero;
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
            }

            return path;
        }

        /// <summary>
        /// Creates a path representing an arc.
        /// </summary>
        /// <param name="width">What width of the arc?</param>
        /// <param name="height">What height of the arc?</param>
        /// <param name="sideCount">How many sides does an arc have?</param>
        /// <returns><inheritdoc cref="Create"/></returns>
        public static Path CreateArc(float width, float height, int sideCount) => CreateArc(Vector3.zero, width, height, sideCount);

        /// <summary>
        /// <inheritdoc cref="CreateArc(float, float, int)"/>
        /// </summary>
        /// <param name="pivotPosition"><inheritdoc cref="Create(Vector3)" path="/param[@name='pivotPosition']"/></param>
        /// <param name="width"><inheritdoc cref="CreateArc(float, float, int)" path="/param[@name='width']"/></param>
        /// <param name="height"><inheritdoc cref="CreateArc(float, float, int)" path="/param[@name='height']"/></param>
        /// <param name="sideCount"><inheritdoc cref="CreateArc(float, float, int)" path="/param[@name='sideCount']"/></param>
        /// <returns><inheritdoc cref="CreateArc(float, float, int)"/></returns>
        public static Path CreateArc(Vector3 pivotPosition, float width, float height, int sideCount) => CreateArc(pivotPosition, Vector3.up, Vector3.forward, width, height, sideCount);

        /// <summary>
        /// <inheritdoc cref="CreateArc(float, float, int)"/>
        /// </summary>
        /// <param name="pivotPosition"><inheritdoc cref="Create(Vector3)" path="/param[@name='pivotPosition']"/></param>
        /// <param name="normal"><inheritdoc cref="CreatePolygon(Vector3, Vector3, Vector3, int, float)" path="/param[@name='normal']"/></param>
        /// <param name="up">Where is the top pointing?</param>
        /// <param name="width"><inheritdoc cref="CreateArc(float, float, int)" path="/param[@name='width']"/></param>
        /// <param name="height"><inheritdoc cref="CreateArc(float, float, int)" path="/param[@name='height']"/></param>
        /// <param name="sideCount"><inheritdoc cref="CreateArc(float, float, int)" path="/param[@name='sideCount']"/></param>
        /// <returns><inheritdoc cref="CreateArc(float, float, int)"/></returns>
        public static Path CreateArc(Vector3 pivotPosition, Vector3 normal, Vector3 up, float width, float height, int sideCount)
        {
            if (sideCount < 3)
                throw new ArgumentException($"Side count can't be less that 3.", nameof(sideCount));

            if (width <= 0f || height <= 0f)
                throw new ArgumentException($"Width and height must greater than 0.");

            var path = Create(pivotPosition);

            normal.Normalize();
            up.Normalize();

            var normalRotation = Quaternion.LookRotation(normal, up);
            var deltaAngle = 180f / (sideCount - 1);

            width = width / 2f - 1f;
            height = height - 1f;

            for (int i = -1; i <= sideCount; i++)
            {
                var angle = deltaAngle * i;

                var vectorX = normalRotation * Vector3.right;
                var rotatedVector = Quaternion.AngleAxis(angle, normal) * vectorX;
                vectorX *= Vector3.Dot(rotatedVector, vectorX);
                rotatedVector += vectorX * width;

                var vectorY = normalRotation * Vector3.up;
                vectorY *= Vector3.Dot(rotatedVector, vectorY);
                rotatedVector += vectorY * height;

                path.AddPoint(rotatedVector, false);
            }

            return path;
        }

        /// <summary>
        /// Creates a path representing a wave.
        /// </summary>
        /// <param name="height">What height of the wave?</param>
        /// <param name="frequency">Frequency of the wave.</param>
        /// <param name="repeat">How many times need repeat wave?</param>
        /// <param name="startToUp">Does a wave starts in up direction (<see langword="true"/>) or down (<see langword="false"/>).</param>
        /// <returns><inheritdoc cref="Create"/></returns>
        public static Path CreateWave(float height, float frequency, int repeat, bool startToUp = true) => CreateWave(Vector3.zero, height, frequency, repeat, startToUp);

        /// <summary>
        /// <inheritdoc cref="CreateWave(float, float, int, int, bool)"/>
        /// </summary>
        /// <param name="pivotPosition"><inheritdoc cref="Create(Vector3)" path="/param[@name='pivotPosition']"/></param>
        /// <param name="height"><inheritdoc cref="CreateWave(float, float, int, int, bool)" path="/param[@name='height']"/></param>
        /// <param name="frequency"><inheritdoc cref="CreateWave(float, float, int, int, bool)" path="/param[@name='frequency']"/></param>
        /// <param name="repeat"><inheritdoc cref="CreateWave(float, float, int, int, bool)" path="/param[@name='repeat']"/></param>
        /// <param name="startToUp"><inheritdoc cref="CreateWave(float, float, int, int, bool)" path="/param[@name='startToUp']"/></param>
        /// <returns><inheritdoc cref="Create"/></returns>
        public static Path CreateWave(Vector3 pivotPosition, float height, float frequency, int repeat, bool startToUp = true) => CreateWave(pivotPosition, Vector3.up, Vector3.forward, height, frequency, repeat, startToUp);

        /// <summary>
        /// <inheritdoc cref="CreateWave(float, float, int, int, bool)"/>
        /// </summary>
        /// <param name="pivotPosition"><inheritdoc cref="Create(Vector3)" path="/param[@name='pivotPosition']"/></param>
        /// <param name="normal"><inheritdoc cref="CreatePolygon(Vector3, Vector3, Vector3, int, float)" path="/param[@name='normal']"/></param>
        /// <param name="up">Where is the top pointing?</param>
        /// <param name="height"><inheritdoc cref="CreateWave(float, float, int, int, bool)" path="/param[@name='height']"/></param>
        /// <param name="frequency"><inheritdoc cref="CreateWave(float, float, int, int, bool)" path="/param[@name='frequency']"/></param>
        /// <param name="repeat"><inheritdoc cref="CreateWave(float, float, int, int, bool)" path="/param[@name='repeat']"/></param>
        /// <param name="startToUp"><inheritdoc cref="CreateWave(float, float, int, int, bool)" path="/param[@name='startToUp']"/></param>
        /// <returns><inheritdoc cref="Create"/></returns>
        public static Path CreateWave(Vector3 pivotPosition, Vector3 normal, Vector3 up, float height, float frequency, int repeat, bool startToUp = true)
        {
            if (height <= 0f)
                throw new ArgumentException($"Height must greater than 0.", nameof(height));

            if (frequency <= 0f)
                throw new ArgumentException($"Frequency must greater than 0.", nameof(frequency));

            if (repeat < 1)
                throw new ArgumentException($"Segments count can't be less that 1.", nameof(repeat));

            var path = Create(pivotPosition);

            normal.Normalize();
            up.Normalize();

            var normalRotation = Quaternion.LookRotation(-normal, up);

            Vector3 currentPosition;
            Vector3 vectorToNext;

            if (startToUp)
            {
                currentPosition = new Vector3(-1f, -1f, 0f);
                vectorToNext = new Vector3(2f, 2f);
            }
            else
            {
                currentPosition = new Vector3(-1f, 1f, 0f);
                vectorToNext = new Vector3(2f, -2f);
            }

            void AddPoint()
            {
                var point = currentPosition;
                point.x = point.x / 4f / frequency;
                point.y *= height;

                point = normalRotation * point;
                path.AddPoint(point, false);

                currentPosition += vectorToNext;
                vectorToNext.y *= -1f;
            }

            for (int i = 0; i <= repeat; i++)
            {
                AddPoint();
                AddPoint();
            }

            path.InsertPoint(1, Vector3.Lerp(path.GetPointByIndex(0, false).Position, path.GetPointByIndex(1, false).Position, 0.5f));
            path.InsertPoint(path.PointsCount - 1, Vector3.Lerp(path.GetPointByIndex(path.PointsCount - 2, false).Position, path.GetPointByIndex(path.PointsCount - 1, false).Position, 0.5f));

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
            var path = CreateSpiral(0f, 3, 0.3333333f, 8);
            path.name = "Path (Spiral)";
            path.transform.SetParent(Selection.activeTransform, false);

            Selection.activeObject = path;
            Undo.RegisterCreatedObjectUndo(path.gameObject, "Create Path");
        }

        [MenuItem("GameObject/Path/Spiral3D")]
        private static void CreateSpiral3D()
        {
            var path = CreateSpiral(0f, 3, 0.3333333f, 8, true);
            path.name = "Path (Spiral3D)";
            path.transform.SetParent(Selection.activeTransform, false);

            Selection.activeObject = path;
            Undo.RegisterCreatedObjectUndo(path.gameObject, "Create Path");
        }

        [MenuItem("GameObject/Path/Arc")]
        private static void CreateArc()
        {
            var path = CreateArc(2f, 2, 8);
            path.name = "Path (Arc)";
            path.transform.SetParent(Selection.activeTransform, false);

            Selection.activeObject = path;
            Undo.RegisterCreatedObjectUndo(path.gameObject, "Create Path");
        }

        [MenuItem("GameObject/Path/Wave")]
        private static void CreateWave()
        {
            var path = CreateWave(1f, 1f, 3);
            path.name = "Path (Arc)";
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
        /// Creates point with specified position and adds it to the end of the path.
        /// </summary>
        /// <param name="x">Point's <see langword="x"/> axis position.</param>
        /// <param name="y">Point's <see langword="y"/> axis position.</param>
        /// <param name="z">Point's <see langword="z"/> axis position.</param>
        /// <param name="useGlobal">Is the position being passed in global space?</param>
        public void AddPoint(float x, float y, float z, bool useGlobal = true) => AddPoint(new Point(new Vector3(x, y, z), Quaternion.identity), useGlobal);

        /// <summary>
        /// <inheritdoc cref="AddPoint(float, float, float, bool)"/>
        /// </summary>
        /// <param name="position">Point's position.</param>
        /// <param name="useGlobal"><inheritdoc cref="AddPoint(float, float, float, bool)" path="/param[@name='useGlobal']"/></param>
        public void AddPoint(Vector3 position, bool useGlobal = true) => AddPoint(new Point(position, Quaternion.identity), useGlobal);

        /// <summary>
        /// Adds a <paramref name="point"/> to the end of the path.
        /// </summary>
        /// <param name="point">Point which will be added.</param>
        /// <param name="useGlobal"><inheritdoc cref="AddPoint(float, float, float, bool)" path="/param[@name='useGlobal']"/></param>
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
        /// Creates point with specified position and inserts it by the specified <paramref name="index"/> in the path.
        /// </summary>
        /// <param name="index">The index by which you want to insert the point.</param>
        /// <param name="x">Point's <see langword="x"/> axis position.</param>
        /// <param name="y">Point's <see langword="y"/> axis position.</param>
        /// <param name="z">Point's <see langword="z"/> axis position.</param>
        /// <param name="useGlobal"><inheritdoc cref="AddPoint(Vector3, bool)" path="/param[@name='useGlobal']"/></param>
        public void InsertPoint(int index, float x, float y, float z, bool useGlobal = true) => InsertPoint(index, new Point(new Vector3(x, y, z), Quaternion.identity), useGlobal);

        /// <summary>
        /// <inheritdoc cref="InsertPoint(int, float, float, float, bool)"/>
        /// </summary>
        /// <param name="index"><inheritdoc cref="InsertPoint(int, float, float, float, bool)" path="/param[@name='index']"/></param>
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
        /// Checks if the path contains a point with the specified position/>.
        /// </summary>
        /// <param name="x">Point's <see langword="x"/> axis position.</param>
        /// <param name="y">Point's <see langword="y"/> axis position.</param>
        /// <param name="z">Point's <see langword="z"/> axis position.</param>
        /// <param name="useGlobal"><inheritdoc cref="AddPoint(Vector3, bool)" path="/param[@name='useGlobal']"/></param>
        /// <returns><see langword="true"/> if a point has been found.</returns>
        public bool ContainsPoint(float x, float y, float z, bool useGlobal = true) => ContainsPoint(new Vector3(x, y, z), useGlobal);

        /// <summary>
        /// <inheritdoc cref="ContainsPoint(float, float, float, bool)"/>
        /// </summary>
        /// <param name="position"><inheritdoc cref="AddPoint(Vector3, bool)" path="/param[@name='position']"/></param>
        /// <param name="useGlobal"><inheritdoc cref="AddPoint(Vector3, bool)" path="/param[@name='useGlobal']"/></param>
        /// <returns><inheritdoc cref="ContainsPoint(float, float, float, bool)"/></returns>
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
        /// Finds the index of the point with the specified position.
        /// </summary>
        /// <param name="x">Point's <see langword="x"/> axis position.</param>
        /// <param name="y">Point's <see langword="y"/> axis position.</param>
        /// <param name="z">Point's <see langword="z"/> axis position.</param>
        /// <param name="useGlobal"><inheritdoc cref="AddPoint(Vector3, bool)" path="/param[@name='useGlobal']"/></param>
        /// <returns>Index of the desired point, otherwise <see langword="-1"/>.</returns>
        public int IndexOfPoint(float x, float y, float z, bool useGlobal = true) => IndexOfPoint(new Vector3(x, y, z), useGlobal);

        /// <summary>
        /// <inheritdoc cref="IndexOfPoint(float, float, float, bool)"/>
        /// </summary>
        /// <param name="position"><inheritdoc cref="AddPoint(Vector3, bool)" path="/param[@name='position']"/></param>
        /// <param name="useGlobal"><inheritdoc cref="AddPoint(Vector3, bool)" path="/param[@name='useGlobal']"/></param>
        /// <returns><inheritdoc cref="IndexOfPoint(float, float, float, bool)"/></returns>
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
        /// <param name="x">Point's <see langword="x"/> axis position.</param>
        /// <param name="y">Point's <see langword="y"/> axis position.</param>
        /// <param name="z">Point's <see langword="z"/> axis position.</param>
        /// <param name="useGlobal"><inheritdoc cref="AddPoint(Vector3, bool)" path="/param[@name='useGlobal']"/></param>
        /// <returns><see langword="true"/> if a point has been found and removed.</returns>
        public bool RemovePoint(float x, float y, float z, bool useGlobal = true) => RemovePoint(new Vector3(x, y, z), useGlobal);

        /// <summary>
        /// <inheritdoc cref="RemovePoint(float, float, float, bool)"/>
        /// </summary>
        /// <param name="position"><inheritdoc cref="AddPoint(Vector3, bool)" path="/param[@name='position']"/></param>
        /// <param name="useGlobal"><inheritdoc cref="AddPoint(Vector3, bool)" path="/param[@name='useGlobal']"/></param>
        /// <returns><inheritdoc cref="RemovePoint(float, float, float, bool)"/></returns>
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

        #region Get and set point
        /// <summary>
        /// Sets the position of the point by the <paramref name="index"/>.
        /// </summary>
        /// <param name="index">Index of the point.</param>
        /// <param name="x">Point's <see langword="x"/> axis position.</param>
        /// <param name="y">Point's <see langword="y"/> axis position.</param>
        /// <param name="z">Point's <see langword="z"/> axis position.</param>
        /// <param name="useGlobal"><inheritdoc cref="AddPoint(Vector3, bool)" path="/param[@name='useGlobal']"/></param>
        public void SetPoint(int index, float x, float y, float z, bool useGlobal = true) => SetPoint(index, new Vector3(x, y, z), useGlobal);

        /// <summary>
        /// <inheritdoc cref="SetPoint(int, float, float, float, bool)"/>
        /// </summary>
        /// <param name="index"><inheritdoc cref="SetPoint(int, float, float, float, bool)" path="/param[@name='index']"/></param>
        /// <param name="position">Position to be set</param>
        /// <param name="useGlobal"><inheritdoc cref="AddPoint(Vector3, bool)" path="/param[@name='useGlobal']"/></param>
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
        /// <param name="x">Point's <see langword="x"/> axis position.</param>
        /// <param name="y">Point's <see langword="y"/> axis position.</param>
        /// <param name="z">Point's <see langword="z"/> axis position.</param>
        /// <param name="rotation"><inheritdoc cref="SetPoint(int, Quaternion, bool)" path="/param[@name='rotation']"/></param>
        /// <param name="useGlobal">Is the position and rotation being passed in global space?</param>
        public void SetPoint(int index, float x, float y, float z, Quaternion rotation, bool useGlobal = true) => SetPoint(index, new Point(new Vector3(x, y, z), rotation), useGlobal);

        /// <summary>
        /// Sets the <paramref name="position"/> and <paramref name="rotation"/> of the point by the <paramref name="index"/>.
        /// </summary>
        /// <param name="index"><inheritdoc cref="SetPoint(int, Vector3, bool)" path="/param[@name='index']"/></param>
        /// <param name="position"><inheritdoc cref="SetPoint(int, Vector3, bool)" path="/param[@name='position']"/></param>
        /// <param name="rotation"><inheritdoc cref="SetPoint(int, Quaternion, bool)" path="/param[@name='rotation']"/></param>
        /// <param name="useGlobal"><inheritdoc cref="SetPoint(int, float, float, float, Quaternion, bool)" path="/param[@name='useGlobal']"/></param>
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
        /// Returns a point by the specified index.
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
        #endregion

        #region Calculating
        /// <summary>
        /// Calculates and returns a point by the specified index.<br/>
        /// This method does not take control points into account (for example, when the path is not looped and the number of points is greater than 2).        
        /// </summary>
        /// <param name="index">Index of the point.</param>
        /// <param name="useGlobal">Is it necessary to convert a calculated point from local space to global?</param>
        /// <returns><inheritdoc cref="GetPointByIndex(int, bool )"/></returns>
        /// <exception cref="ArgumentException"></exception>
        public PointData GetPointOnPathByIndex(int index, bool useGlobal = true)
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
        /// <param name="useGlobal"><inheritdoc cref="GetPointOnPathByIndex(int, bool)" path="/param[@name='useGlobal']"/></param>
        /// <returns><inheritdoc cref="GetPointByIndex(int, bool )"/></returns>
        /// <exception cref="ArgumentException"></exception>
        public PointData GetPointAtDistance(int segment, float distance, bool useNormalizedDistance = true, bool useGlobal = true)
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
        /// <param name="useGlobal"><inheritdoc cref="GetPointOnPathByIndex(int, bool)" path="/param[@name='useGlobal']"/></param>
        /// <returns><inheritdoc cref="GetPointByIndex(int, bool )"/></returns>
        public PointData GetPointAtDistance(float distance, bool useNormalizedDistance = true, bool useGlobal = true)
        {
            if (PointsCount == 0)
                throw new Exception($"Path \"{name}\" does not contain points.");

            if (useNormalizedDistance)
                distance *= Length;

            distance = Mathf.Max(distance, 0f);

            Vector3 offsetPosition;
            Quaternion offsetRotation;

            if (Looped || PointsCount == 1)
            {
                offsetPosition = Vector3.zero;
                offsetRotation = Quaternion.identity;
            }
            else
            {
                var firstPoint = GetPointOnPathByIndex(0);
                var lastPoint = GetPointOnPathByIndex(PointsCountOnPath - 1);

                offsetPosition = lastPoint.Position - firstPoint.Position;
                offsetRotation = lastPoint.Rotation * Quaternion.Inverse(firstPoint.Rotation);
            }

            var repeated = (int)(distance / Length);
            var frac = distance % Length;
            if (Mathf.Approximately(frac, 0f))
                repeated = Math.Max(repeated - 1, 0);

            offsetPosition *= repeated;
            offsetRotation = Quaternion.SlerpUnclamped(Quaternion.identity, offsetRotation, repeated);

            if (!Mathf.Approximately(distance, 0f) && Mathf.Approximately(frac, 0f))
                distance = Length;
            else
                distance = frac;

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

            var pointData = GetPointAtDistance(segment, distance, false, useGlobal);

            return new PointData(pointData.Position + offsetPosition, offsetRotation * pointData.Rotation, pointData.Direction);
        }
        #endregion
    }
}