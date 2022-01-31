using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Paths
{
    public struct PointData
    {
        public Vector3 Position { get; set; }

        public Quaternion Rotation { get; set; }

        public Vector3 PathDirection { get; set; }

        public PointData(Point point, Vector3 pathDirection) : this(point.Position, point.Rotation, pathDirection) { }

        public PointData(Vector3 position, Quaternion rotation, Vector3 pathDirection)
        {
            Position = position;
            Rotation = rotation;
            PathDirection = pathDirection;
        }
    }
}
