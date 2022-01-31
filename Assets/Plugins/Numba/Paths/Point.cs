using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Paths
{
    [Serializable]
    public struct Point
    {
        [SerializeField]
        private Vector3 _position;

        public Vector3 Position
        {
            get => _position;
            set => _position = value;
        }

        [SerializeField]
        private Quaternion _rotation;

        public Quaternion Rotation
        {
            get => _rotation;
            set => _rotation = value;
        }

        public Point(Vector3 position, Quaternion rotation)
        {
            _position = position;
            _rotation = rotation;
        }
    }
}
