using UnityEngine;

namespace Redcode.Paths
{
    /// <summary>
    /// Represents the point that lies on the path.<br/>
    /// Usually this point is calculated using the <see cref="Path.GetPointAtDistance(float, bool, bool)"/> method.
    /// </summary>
    public struct PointData
    {
        private Point _point;

        /// <summary>
        /// Position of the point.
        /// </summary>
        public Vector3 Position => _point.Position;

        /// <summary>
        /// Rotation of the point.
        /// </summary>
        public Quaternion Rotation => _point.Rotation;

        /// <summary>
        /// Direction of the point.
        /// </summary>
        public Vector3 Direction { get; private set; }

        internal PointData(Point point, Vector3 direction)
        {
            _point = point;
            Direction = direction;
        }

        internal PointData(Vector3 position, Quaternion rotation, Vector3 direction) : this(new Point(position, rotation), direction) { }

        public static implicit operator Point(PointData pointData) => pointData._point;

        public override string ToString() => $"PointData: {{Position: {Position}, Rotation: {Rotation}, Direction: {Direction}}}";
    }
}
