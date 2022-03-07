using System;
using UnityEngine;

namespace Redcode.Paths
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
        /// <param name="x">Position on <see langword="x"/> axis.</param>
        /// <param name="y">Position on <see langword="y"/> axis.</param>
        /// <param name="z">Position on <see langword="z"/> axis.</param>
        public Point(float x, float y, float z) : this(x, y, z, Quaternion.identity) { }

        /// <summary>
        /// <inheritdoc cref="Point(float, float, float)"/>
        /// </summary>
        /// <param name="x"><inheritdoc cref="Point(float, float, float)" path="/param[@name='x']"/></param>
        /// <param name="y"><inheritdoc cref="Point(float, float, float)" path="/param[@name='y']"/></param>
        /// <param name="z"><inheritdoc cref="Point(float, float, float)" path="/param[@name='z']"/></param>
        /// <param name="rotation">Point's rotation.</param>
        public Point(float x, float y, float z, Quaternion rotation) : this(new Vector3(x, y, z), rotation) { }

        /// <summary>
        /// <inheritdoc cref="Point(float, float, float)"/>
        /// </summary>
        /// <param name="position">Point's position.</param>
        /// <inheritdoc cref="Point(float, float, float, Quaternion)" path="/param[@name='rotation']"/>
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

        public override string ToString() => $"Point: {{Position: {Position}, Rotation: {Rotation}}}";
    }
}
