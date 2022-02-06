using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Paths
{
    public struct PointData
    {
        public Vector3 Position { get; set; }

        public Quaternion Rotation { get; set; }

        public Vector3 Direction { get; set; }

        public PointData(Point point, Vector3 direction) : this(point.Position, point.Rotation, direction) { }

        public PointData(Vector3 position, Quaternion rotation, Vector3 direction)
        {
            Position = position;
            Rotation = rotation;
            Direction = direction;
        }

        public static implicit operator Point(PointData pointData) => new Point(pointData.Position, pointData.Rotation);
    }
}
