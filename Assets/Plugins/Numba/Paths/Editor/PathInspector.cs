using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Paths
{
    [CustomEditor(typeof(Path))]
    public class PathInspector : Editor
    {
        private class PointsBinding : IBinding
        {
            private PathInspector _inscpector;
            private VisualElement _listView;

            public PointsBinding(PathInspector inscpector, VisualElement listView)
            {
                _inscpector = inscpector;
                _listView = listView;
            }

            public void PreUpdate() { }

            public void Release() { }

            public void Update()
            {
                foreach (var groupBox in _listView.Query<GroupBox>("point-element").Build())
                {
                    var posField = groupBox.Q<Vector3Field>("point-position");

                    var numberText = groupBox.Q<Label>("point-number").text;
                    if (int.TryParse(numberText, out int number))
                        posField.value = _inscpector._path.Points[number];
                }

                if (_inscpector._selectedPointIndex != -1)
                    SceneView.lastActiveSceneView.rootVisualElement.Q<Vector3Field>("point-position").value = _inscpector._path.Points[_inscpector._selectedPointIndex];
            }
        }

        private VisualElement _inspector;

        private Path _path;

        [SerializeField]
        private VisualTreeAsset _pathInspectorUXML;

        [SerializeField]
        private VisualTreeAsset _pathSceneViewUXML;

        [SerializeField]
        private VisualTreeAsset _pathPointsListElementUXML;

        private int _selectedPointIndex;

        private Tool _lastTool = Tool.None;

        private bool _isFramed;

        private GroupBox _toolsBox;

        private Dictionary<VisualElement, EventCallback<ChangeEvent<Vector3>>> _positionFieldValueChangedCallbacks = new();

        private Dictionary<VisualElement, Action> _addButtonCallbacks = new();

        private Dictionary<VisualElement, Action> _removeButtonCallbacks = new();

        private void MakeInspectorAndPath(out VisualElement inspector, out Path path)
        {
            inspector = new VisualElement();
            _pathInspectorUXML.CloneTree(inspector);

            path = ((Path)serializedObject.targetObject);
        }

        private void AddElement(ListView listView, int index)
        {
            Vector3 newPoint;

            if (index < _path.Points.Count - 1)
                newPoint = Vector3.Lerp(_path.Points[index], _path.Points[index + 1], 0.5f);
            else
                newPoint = _path.Points[index] + (_path.Points[index] - _path.Points[index - 1]);

            _path.Points.Insert(index + 1, newPoint);

            listView.Rebuild();

            listView.ScrollToItem(index + 1);
            listView.selectedIndex = index + 1;
        }

        private void RemoveElement(ListView listView, int index)
        {
            _path.Points.RemoveAt(index);
            listView.Rebuild();
        }

        private void SelectPoint(int index)
        {
            var needCreateToolsBox = _selectedPointIndex == -1;
            if (needCreateToolsBox)
            {
                var sceneViewElement = SceneView.lastActiveSceneView.rootVisualElement.Q("unity-scene-view-camera-rect");
                _pathSceneViewUXML.CloneTree(sceneViewElement);
                _toolsBox = sceneViewElement.Q<GroupBox>("paths-root");
            }

            _selectedPointIndex = index;
            _isFramed = false;

            var label = _toolsBox.Q<Label>("point-number");
            label.text = $"Point number: {_selectedPointIndex}";

            var position = _toolsBox.Q<Vector3Field>("point-position");
            position.value = _path.Points[_selectedPointIndex];

            if (needCreateToolsBox)
                position.RegisterValueChangedCallback(e => _path.Points[_selectedPointIndex] = e.newValue);
        }

        private void SelectPointInListView(int index)
        {
            var listView = _inspector.Q<ListView>();
            listView.ScrollToItem(index);
            listView.selectedIndex = index;
        }

        private Vector3 TransformPoint(Vector3 point) => _path.transform.TransformPoint(point);

        private void FrameOnSelectedPoint()
        {
            Bounds bounds;
            if (_isFramed)
            {
                bounds = new Bounds(TransformPoint(_path.Points[_selectedPointIndex]), Vector3.zero);
                bounds.Encapsulate(_selectedPointIndex != 0 ? TransformPoint(_path.Points[_selectedPointIndex - 1]) : TransformPoint(_path.Points[_path.Points.Count - 1]));
                bounds.Encapsulate(_selectedPointIndex != _path.Points.Count - 1 ? TransformPoint(_path.Points[_selectedPointIndex + 1]) : TransformPoint(_path.Points[0]));
            }
            else
                bounds = new Bounds(TransformPoint(_path.Points[_selectedPointIndex]), Vector3.one);

            SceneView.lastActiveSceneView.Frame(bounds, false);
            _isFramed = !_isFramed;
        }

        private void DeselectPoint()
        {
            _selectedPointIndex = -1;
            _toolsBox?.parent?.Remove(_toolsBox);
            //_editorUpdateCallbacks.Remove(ToolsBoxPositionSyncHandler);
        }

        public override VisualElement CreateInspectorGUI()
        {
            MakeInspectorAndPath(out _inspector, out _path);

            _selectedPointIndex = -1;
            var listView = _inspector.Q<ListView>("Points");

            listView.binding = new PointsBinding(this, listView);

            listView.itemsSource = _path.Points;
            listView.makeItem = () =>
            {
                _pathPointsListElementUXML.CloneTree(_inspector);

                var groupBox = _inspector.Children().Last();
                _inspector.Remove(groupBox);

                return groupBox;
            };

            void Bind(VisualElement element, int index)
            {
                if (index < 0)
                    return;

                #region Point name
                var labelField = element.Q<Label>("point-number");
                labelField.text = index.ToString();
                #endregion

                #region Point position
                var posField = element.Q<Vector3Field>("point-position");
                posField.value = _path.Points[index];

                _positionFieldValueChangedCallbacks.Add(element, e =>
                {
                    _path.Points[index] = e.newValue;
                    SceneView.lastActiveSceneView.Repaint();
                });

                posField.RegisterValueChangedCallback(_positionFieldValueChangedCallbacks[element]);
                #endregion

                #region Add and remove point
                var addButton = element.Q<Button>("add-point-button");
                _addButtonCallbacks.Add(element, () => AddElement(listView, index));
                addButton.clicked += _addButtonCallbacks[element];

                var removeButton = element.Q<Button>("remove-point-button");
                _removeButtonCallbacks.Add(element, () => RemoveElement(listView, index));
                removeButton.clicked += _removeButtonCallbacks[element];

                if (_path.Points.Count <= 4)
                    removeButton.SetEnabled(false);
                #endregion
            }

            void Unbind(VisualElement element, int index)
            {
                if (index < 0)
                    return;

                var posField = element.Q<Vector3Field>("point-position");
                posField.UnregisterValueChangedCallback(_positionFieldValueChangedCallbacks[element]);
                _positionFieldValueChangedCallbacks.Remove(element);

                var addButton = element.Q<Button>("add-point-button");
                addButton.clicked -= _addButtonCallbacks[element];
                _addButtonCallbacks.Remove(element);

                var removeButton = element.Q<Button>("remove-point-button");
                removeButton.clicked -= _removeButtonCallbacks[element];
                _removeButtonCallbacks.Remove(element);
            };

            listView.bindItem = Bind;
            listView.unbindItem += Unbind;

            listView.itemIndexChanged += (from, to) => SelectPointInListView(to);

            #region Selecting and index changing
            listView.onSelectedIndicesChange += indeces =>
            {
                if (indeces.Count() == 0)
                    return;

                SelectPoint(indeces.First());
                SceneView.lastActiveSceneView.Repaint();
            };
            #endregion

            return _inspector;
        }

        private void OnSceneGUI()
        {
            if (Event.current.isKey && Event.current.keyCode == KeyCode.F && Event.current.type == EventType.KeyDown && _selectedPointIndex != -1)
            {
                FrameOnSelectedPoint();
                Event.current.Use();
            }

            var skin = Resources.Load<GUISkin>("Numba/Paths/Skin");
            var yellowCircleTex = Resources.Load<Texture>("Numba/Paths/Textures/YellowCircle");

            for (int i = 0; i < _path.Points.Count; i++)
            {
                var point = _path.Points[i];
                var screenPos = SceneView.lastActiveSceneView.camera.WorldToScreenPoint(TransformPoint(point));

                Handles.BeginGUI();

                var pointRect = new Rect(screenPos.x, SceneView.lastActiveSceneView.camera.pixelHeight - screenPos.y, 24f, 24f);

                GUI.DrawTexture(new Rect(pointRect.x - 12f, pointRect.y - 12f, pointRect.width, pointRect.height), yellowCircleTex);
                GUI.Label(new Rect(pointRect.x - ((i.ToString().Length == 1) ? 11f : 12), pointRect.y - 12f, pointRect.width, pointRect.height), i.ToString(), skin.label);

                if (_selectedPointIndex != i && GUI.Button(new Rect(pointRect.x - 12f, pointRect.y - 12f, pointRect.width, pointRect.height), "", skin.button))
                    SelectPointInListView(i);

                Handles.EndGUI();

                if (_selectedPointIndex == i)
                    _path.Points[i] = _path.transform.InverseTransformPoint(Handles.PositionHandle(TransformPoint(_path.Points[i]), Tools.pivotRotation == PivotRotation.Local ? _path.transform.rotation : Quaternion.identity));
            }

            for (int i = 0; i < _path.Points.Count - 1; i++)
            {
                var middlePoint = Vector3.Lerp(TransformPoint(_path.Points[i]), TransformPoint(_path.Points[i + 1]), 0.5f);
                var screenPos = SceneView.lastActiveSceneView.camera.WorldToScreenPoint(middlePoint);
                screenPos.y = SceneView.lastActiveSceneView.camera.pixelHeight - screenPos.y;

                if (Vector2.Distance(screenPos, Event.current.mousePosition) <= 50f)
                {
                    Handles.BeginGUI();
                    GUI.DrawTexture(new Rect(screenPos.x - 12f, screenPos.y - 12f, 24f, 24f), yellowCircleTex);
                    GUI.Label(new Rect(screenPos.x - 12f, screenPos.y - 14f, 24f, 24f), "+", skin.GetStyle("addbutton"));
                    Handles.EndGUI();
                }

                SceneView.lastActiveSceneView.Repaint();
            }

            if (_selectedPointIndex != -1)
            {
                if (_lastTool == Tool.None)
                    _lastTool = Tools.current;

                Tools.current = Tool.None;
                Handles.BeginGUI();

                var screenPos = SceneView.lastActiveSceneView.camera.WorldToScreenPoint(_path.transform.position);
                var rootRect = new Rect(screenPos.x - 6f, SceneView.lastActiveSceneView.camera.pixelHeight - screenPos.y - 6f, 12f, 12f);

                GUI.DrawTexture(rootRect, yellowCircleTex);
                GUI.DrawTexture(new Rect(screenPos.x - 3f, SceneView.lastActiveSceneView.camera.pixelHeight - screenPos.y - 3f, 6f, 6f), Resources.Load<Texture2D>("Numba/Paths/Textures/BlackCircle"));

                if (GUI.Button(rootRect, "", skin.button))
                {
                    DeselectPoint();

                    Tools.current = _lastTool;
                    _lastTool = Tool.None;
                }

                Handles.EndGUI();
            }
        }

        private void OnDisable()
        {
            if (_selectedPointIndex != -1)
                Tools.current = _lastTool;

            DeselectPoint();
        }
    }
}