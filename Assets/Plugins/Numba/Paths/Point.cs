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

        #region Editor
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
        #endregion

        /// <summary>
        /// Create point.
        /// </summary>
        /// <param name="position">Point's position.</param>
        /// <param name="rotation">Point's rotation.</param>
        public Point(Vector3 position, Quaternion rotation)
        {
            _position = position;
            _rotation = rotation;

            #region Editor
#if UNITY_EDITOR
            _eulers = rotation.eulerAngles;
#endif
            #endregion
        }

        #region Editor
#if UNITY_EDITOR
        public override bool Equals(object obj) => obj is Point point && this == point;

        public override int GetHashCode() => _position.GetHashCode() ^ _rotation.GetHashCode();

        public static bool operator ==(Point p1, Point p2) => p1._position == p2._position && p1._rotation == p2._rotation;

        public static bool operator !=(Point p1, Point p2) => !(p1 == p2);
#endif
#endregion
    }
}
