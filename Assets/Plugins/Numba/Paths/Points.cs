using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Paths
{
    [Serializable]
    public class Points
    {
        #region Selectors
        [Serializable]
        public abstract class Selector : IList<Vector3>, IList
        {
            [SerializeField]
            protected Points _points;

            public int Count => _points.Count;

            public bool IsReadOnly => false;

            public bool IsFixedSize => false;

            public bool IsSynchronized => false;

            public object SyncRoot => this;

            object IList.this[int index]
            {
                get => this[index];
                set => this[index] = (Vector3)value;
            }

            public Selector(Points points) => _points = points;

            public abstract void Add(Vector3 point);

            public void AddRange(params Vector3[] points) => AddRange((IEnumerable<Vector3>)points);

            public abstract void AddRange(IEnumerable<Vector3> points);

            public abstract void Insert(int index, Vector3 point);

            public void InsertRange(int index, params Vector3[] points) => InsertRange(index, (IEnumerable<Vector3>)points);

            public abstract void InsertRange(int index, IEnumerable<Vector3> points);

            public abstract bool Remove(Vector3 point);

            public void RemoveAt(int index) => _points.RemoveAt(index);

            public abstract int IndexOf(Vector3 point);

            public void Clear() => _points.Clear();

            public abstract bool Contains(Vector3 point);

            public abstract void CopyTo(Vector3[] array, int arrayIndex);

            public abstract IEnumerator<Vector3> GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            int IList.Add(object value)
            {
                Add((Vector3)value);
                return _points.Count;
            }

            bool IList.Contains(object value) => Contains((Vector3)value);

            int IList.IndexOf(object value) => IndexOf((Vector3)value);

            void IList.Insert(int index, object value) => Insert(index, (Vector3)value);

            void IList.Remove(object value) => Remove((Vector3)value);

            void ICollection.CopyTo(Array array, int index) => CopyTo((Vector3[])array, index);

            public abstract Vector3 this[int index] { get; set; }
        }

        [Serializable]
        public class LocalPoints : Selector
        {
            public LocalPoints(Points points) : base(points) { }

            public override void Add(Vector3 point) => _points._points.Add(point);

            public override void AddRange(IEnumerable<Vector3> points) => _points._points.AddRange(points);

            public override void Insert(int index, Vector3 point) => _points._points.Insert(index, point);

            public override void InsertRange(int index, IEnumerable<Vector3> points) => _points._points.InsertRange(index, points);

            public override bool Remove(Vector3 point) => _points._points.Remove(point);

            public override int IndexOf(Vector3 point) => _points._points.IndexOf(point);

            public override bool Contains(Vector3 point) => _points._points.Contains(point);

            public override void CopyTo(Vector3[] array, int arrayIndex) => _points._points.CopyTo(array, arrayIndex);

            public override IEnumerator<Vector3> GetEnumerator() => _points._points.GetEnumerator();

            public override Vector3 this[int index]
            {
                get => _points._points[index];
                set => _points._points[index] = value;
            }
        }

        [Serializable]
        public class GlobalPoints : Selector
        {
            public GlobalPoints(Points points) : base(points) { }

            public override void Add(Vector3 point) => _points._points.Add(_points._path.transform.InverseTransformPoint(point));

            public override void AddRange(IEnumerable<Vector3> points)
            {
                _points._points.AddRange(points.Select(p => _points._path.transform.InverseTransformPoint(p)));
            }

            public override void Insert(int index, Vector3 point) => _points._points.Insert(index, _points._path.transform.InverseTransformPoint(point));

            public override void InsertRange(int index, IEnumerable<Vector3> points)
            {
                _points._points.InsertRange(index, points.Select(p => _points._path.transform.InverseTransformPoint(p)));
            }

            public override bool Remove(Vector3 point) => _points._points.Remove(_points._path.transform.InverseTransformPoint(point));

            public override int IndexOf(Vector3 point) => _points._points.IndexOf(_points._path.transform.InverseTransformPoint(point));

            public override bool Contains(Vector3 point) => _points._points.Contains(_points._path.transform.InverseTransformPoint(point));

            public override void CopyTo(Vector3[] array, int arrayIndex)
            {
                // TODO: Maybe fix it?
                _points._points.Select(p => _points._path.transform.InverseTransformPoint(p)).ToList().CopyTo(array, arrayIndex);
            }

            public override IEnumerator<Vector3> GetEnumerator()
            {
                // TODO: Maybe create own enumerator?
                for (int i = 0; i < _points.Count; i++)
                    yield return _points._path.transform.InverseTransformPoint(_points._points[i]);
            }

            public override Vector3 this[int index]
            {
                get => _points._path.transform.TransformPoint(_points._points[index]);
                set => _points._points[index] = _points._path.transform.InverseTransformPoint(value);
            }
        }
        #endregion

        [SerializeField]
        private Path _path;

        [SerializeField]
        private readonly List<Vector3> _points = new();

        public int Count => _points.Count;

        public LocalPoints Local { get; private set; }

        public GlobalPoints Global { get; private set; }

        public Points(Path path)
        {
            _path = path;

            Local = new LocalPoints(this);
            Global = new GlobalPoints(this);
        }

        public void RemoveAt(int index) => _points.RemoveAt(index);

        public void Clear() => _points.Clear();
    }
}