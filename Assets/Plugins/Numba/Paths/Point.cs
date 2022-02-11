using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Paths
{
    /// <summary>
    /// The point through which the <see cref="Path"/> passes.
    /// </summary>
    [Serializable]
    public struct Point
    {
        [SerializeField]
        private Vector3 _position;

        /// <summary>
        /// Position of the point.
        /// </summary>
        public Vector3 Position
        {
            get => _position;
            set => _position = value;
        }

        [SerializeField]
        private Quaternion _rotation;

        /// <summary>
        /// Rotation of the point.
        /// </summary>
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

        /// <summary>
        /// Create point.
        /// </summary>
        /// <param name="position">Point's position.</param>
        /// <param name="rotation">Point's rotation.</param>
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
