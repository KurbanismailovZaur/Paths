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
            set
            {
                _rotation = value;
#if UNITY_EDITOR
                _eulers = _rotation.eulerAngles;
#endif
            }
        }

#if UNITY_EDITOR
        [SerializeField]
        private Vector3 _eulers;

        private Vector3 Eulers
        {
            get => _eulers;
            set
            {
                _eulers = value;
                _rotation = Quaternion.Euler(_eulers);
            }
        }
#endif

        public Point(Vector3 position, Quaternion rotation)
        {
            _position = position;
            _rotation = rotation;

#if UNITY_EDITOR
            _eulers = rotation.eulerAngles;
#endif
        }
    }
}
