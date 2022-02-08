using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Paths
{
    public struct PointData
    {
        private Point _point;

        public Vector3 Position => _point.Position;

        public Quaternion Rotation => _point.Rotation;

        public Vector3 Direction { get; private set; }

        public PointData(Point point, Vector3 direction)
        {
            _point = point;
            Direction = direction;
        }

        public PointData(Vector3 position, Quaternion rotation, Vector3 direction) : this(new Point(position, rotation), direction) { }

        public static implicit operator Point(PointData pointData) => pointData._point;
    }
}
