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
            private PathInspector _inspector;

            private ListView _listView;

            private int _lastPointsCount;

            public PointsBinding(PathInspector inscpector, ListView listView)
            {
                _inspector = inscpector;
                _listView = listView;
                _lastPointsCount = _inspector._path.Points.Count;
            }

            public void PreUpdate() { }

            public void Release() { }

            public void Update()
            {
                if (_lastPointsCount != _inspector._path.Points.Count)
                {
                    var selectedIndex = _inspector._selectedPointIndex;
                    _inspector.DeselectPoint();

                    _listView.Rebuild();
                    _inspector.UpdateState();

                    _inspector.SelectPointInListView(selectedIndex);
                    _lastPointsCount = _inspector._path.Points.Count;
                }

                foreach (var groupBox in _listView.Query<GroupBox>("point-element").Build())
                {
                    var posField = groupBox.Q<Vector3Field>("point-position");

                    var number = Convert.ToInt32(groupBox.Q<Label>("point-number").text);
                    posField.value = _inspector._path.Points[number];
                }

                if (_inspector._selectedPointIndex != -1)
                    SceneView.lastActiveSceneView.rootVisualElement.Q<Vector3Field>("point-position").value = _inspector._path.Points[_inspector._selectedPointIndex];
            }
        }

        #region UXML
        [SerializeField]
        private VisualTreeAsset _pathInspectorUXML;

        [SerializeField]
        private VisualTreeAsset _pathSceneViewUXML;

        [SerializeField]
        private VisualTreeAsset _pathPointsListElementUXML;
        #endregion

        private VisualElement _inspector;

        private Path _path;

        private ListView _listView;

        private Foldout _foldout;

        private GroupBox _pointsAddGroupBox;

        private Label _helpLabel;

        private int _selectedPointIndex;

        private bool _isFramed;

        private GroupBox _toolsBox;

        private GUISkin _skin;

        private Dictionary<string, Texture> _textures = new();

        private double _lastClickRemoveTime;

        private List<int> _pointsToRemove = new();

        #region Callbacks
        private Dictionary<VisualElement, EventCallback<ChangeEvent<Vector3>>> _positionFieldValueChangedCallbacks = new();

        private Dictionary<VisualElement, Action> _addButtonCallbacks = new();

        private Dictionary<VisualElement, Action> _removeButtonCallbacks = new();
        #endregion

        private void FindAllMainElements()
        {
            _inspector = new VisualElement();
            _pathInspectorUXML.CloneTree(_inspector);

            _path = ((Path)serializedObject.targetObject);

            _listView = _inspector.Q<ListView>("Points");
            _pointsAddGroupBox = _inspector.Q<GroupBox>("points-add-group");
            _foldout = _listView.Q<Foldout>();
            _helpLabel = _inspector.Q<Label>("help-label");
        }

        private void LoadResources()
        {
            _skin = Resources.Load<GUISkin>("Numba/Paths/Skin");

            _textures.Add("yellow circle", Resources.Load<Texture>("Numba/Paths/Textures/YellowCircle"));
            _textures.Add("dotted circle", Resources.Load<Texture>("Numba/Paths/Textures/DottedCircle"));
            _textures.Add("black circle", Resources.Load<Texture>("Numba/Paths/Textures/BlackCircle"));
            _textures.Add("white circle", Resources.Load<Texture>("Numba/Paths/Textures/WhiteCircle"));
            _textures.Add("red circle", Resources.Load<Texture>("Numba/Paths/Textures/RedCircle"));
            _textures.Add("cross", Resources.Load<Texture>("Numba/Paths/Textures/Cross"));
            _textures.Add("red cross", Resources.Load<Texture>("Numba/Paths/Textures/RedCross"));
            _textures.Add("red rect", Resources.Load<Texture>("Numba/Paths/Textures/RedRect"));
        }

        #region Add and remove
        private void AddElement(int index, Vector3 position)
        {
            _path.Points.Insert(index, position);

            _listView.Rebuild();

            SelectPointInListView(index);
            SelectPoint(index);

            UpdateState();
        }

        private void AddElement(int index)
        {
            Vector3 newPoint;

            if (_path.Points.Count == 1)
                newPoint = _path.Points[0];
            else
            {
                if (index < _path.Points.Count - 1)
                    newPoint = Vector3.Lerp(_path.Points[index], _path.Points[index + 1], 0.5f);
                else
                    newPoint = _path.Points[index] + (_path.Points[index] - _path.Points[index - 1]);
            }

            AddElement(index + 1, newPoint);
        }

        private void RemoveElement(int index)
        {
            DeselectPoint();

            _path.Points.RemoveAt(index);
            _listView.Rebuild();

            UpdateState();
        }
        #endregion

        #region Selecting
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
            index = Mathf.Min(index, _path.Points.Count - 1);

            _listView.ScrollToItem(index);
            _listView.selectedIndex = index;
        }

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
        }
        #endregion

        #region Transforming
        private Vector3 TransformPoint(Vector3 point) => _path.transform.TransformPoint(point);

        private Vector3 GetPointPositionInSceneView(Vector3 point)
        {
            var sceneViewPos = SceneView.lastActiveSceneView.camera.WorldToScreenPoint(TransformPoint(point));
            sceneViewPos.y = SceneView.lastActiveSceneView.camera.pixelHeight - sceneViewPos.y;

            return sceneViewPos;
        }

        private Rect GetLocalPointRectInSceneView(Vector3 point, float size) => GetPointRectInSceneView(point, new Vector2(size, size));

        private Rect GetPointRectInSceneView(Vector3 point, Vector2 size)
        {
            var screenPos = GetPointPositionInSceneView(point);
            return new Rect(screenPos.x - size.x / 2f, screenPos.y - size.y / 2f, size.x, size.y);
        }
        #endregion

        #region Updates
        private void UpdatePointsAddGroup()
        {
            if (_foldout.value && _path.Points.Count == 0)
                _pointsAddGroupBox.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.Flex);
            else
                _pointsAddGroupBox.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
        }

        private void UpdateHelp()
        {
            if (_path.Points.Count == 0)
                _helpLabel.text = @"There is no points in path. Add points by pressing '+' button above. If you don't see the button, just unfold 'Points' field.";
            else if (_path.Points.Count == 1)
                _helpLabel.text = @"When a path consists of 1 point, any attempt to calculate the value of a point on a line will always return that point.";
            else if (_path.Points.Count == 2)
                _helpLabel.text = @"When the path consists of 2 points it represents an ordinary straight line.";
            else if (_path.Points.Count == 3)
                _helpLabel.text = @"A path that consists of 3 points represents a triangle (almost), the first and last points being both control and end points of the curve.";
            else
                _helpLabel.text = @"A path that consists of 4 or more points represents a curve, the first and last points of which are controlling points, and adjacent points are both controlling and end points of the curve.";
        }

        private void UpdateState()
        {
            UpdatePointsAddGroup();
            SceneView.lastActiveSceneView.Repaint();
            UpdateHelp();
        }
        #endregion

        public override VisualElement CreateInspectorGUI()
        {
            if (_inspector != null)
                return _inspector;

            FindAllMainElements();
            LoadResources();

            _selectedPointIndex = -1;

            _foldout.RegisterValueChangedCallback(e => UpdatePointsAddGroup());

            _pointsAddGroupBox.Q<Button>().clicked += () =>
            {
                AddElement(0, _path.transform.position);
                _pointsAddGroupBox.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
            };

            _listView.binding = new PointsBinding(this, _listView);

            _listView.itemsSource = _path.Points;
            _listView.makeItem = () =>
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
                _addButtonCallbacks.Add(element, () => AddElement(index));
                addButton.clicked += _addButtonCallbacks[element];

                var removeButton = element.Q<Button>("remove-point-button");
                _removeButtonCallbacks.Add(element, () => RemoveElement(index));
                removeButton.clicked += _removeButtonCallbacks[element];
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

            _listView.bindItem = Bind;
            _listView.unbindItem += Unbind;

            _listView.itemIndexChanged += (from, to) => SelectPointInListView(to);

            _listView.onSelectedIndicesChange += indeces =>
            {
                if (indeces.Count() == 0)
                    return;

                SelectPoint(indeces.First());
                SceneView.lastActiveSceneView.Repaint();
            };

            UpdateState();

            return _inspector;
        }

        public bool NearlyEquals(float value, float other, float epsilon = 0.0000001f)
        {
            if (value == other) // Simple check, also handles infinities.
                return true;

            value = Mathf.Abs(value);
            other = Mathf.Abs(other);

            return Mathf.Abs(value - other) < epsilon;
        }

        #region Draw primitives
        private void DrawLine(Vector3 from, Vector3 to, Color color, bool useDotted = false)
        {
            Handles.color = color;

            if (useDotted)
                Handles.DrawDottedLine(from, to, 4f);
            else
                Handles.DrawLine(from, to);
        }

        private void DrawCatmullRomLine(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Color color, bool useDotted = false)
        {
            var t = 0f;
            var step = 1f / _path.Resolution;

            var lastPosition = p1;

            while (t < 1f)
            {
                var position = Path.GetPoint(t, p0, p1, p2, p3);
                DrawLine(lastPosition, position, color, useDotted);

                lastPosition = position;
                t += step;
            }

            // TODO: DrawLine(lastPosition, Path.GetPoint(1f, p0, p1, p2, p3), color, useDotted);
        }

        private void DrawRoot()
        {
            Handles.BeginGUI();

            GUI.DrawTexture(GetLocalPointRectInSceneView(Vector3.zero, 12f), _textures["white circle"]);
            GUI.DrawTexture(GetLocalPointRectInSceneView(Vector3.zero, 6f), _textures["black circle"]);

            var rect = GetPointRectInSceneView(Vector3.zero, new Vector2(48, 24));
            rect.y += 24f;

            GUI.Label(rect, "pivot", _skin.customStyles[4]);

            if (_selectedPointIndex == -1)
            {
                Handles.EndGUI();
                return;
            }

            if (GUI.Button(GetLocalPointRectInSceneView(Vector3.zero, 16f), "", _skin.button))
                DeselectPoint();

            Handles.EndGUI();
        }

        private void DrawPoint(int number, bool isControl, bool drawBlackDot, bool drawLabel, bool drawYellowCircle = true)
        {
            Handles.BeginGUI();

            var removeRect = Rect.zero;
            var needDrawRemoveButton = false;

            if (_selectedPointIndex != number)
            {
                removeRect = GetLocalPointRectInSceneView(_path.Points[number], 16f);
                removeRect.y -= isControl ? 30f : 24f;

                if (needDrawRemoveButton = Vector2.Distance(Event.current.mousePosition, removeRect.center) <= 40f)
                {
                    var pointCenter = GetLocalPointRectInSceneView(_path.Points[number], 24f).center;
                    var endCenter = removeRect.center;
                    endCenter.x -= 1f;

                    var lineRect = new Rect(endCenter, new Vector2(2f, pointCenter.y - endCenter.y));
                    GUI.DrawTexture(lineRect, _textures["red rect"]);
                }
            }

            GUI.DrawTexture(GetLocalPointRectInSceneView(_path.Points[number], 24f), drawYellowCircle ? _textures["yellow circle"] : _textures["white circle"]);

            if (isControl)
                GUI.DrawTexture(GetLocalPointRectInSceneView(_path.Points[number], 36f), _textures["dotted circle"]);

            if (drawBlackDot)
                GUI.DrawTexture(GetLocalPointRectInSceneView(_path.Points[number], 8f), _textures["black circle"]);

            if (drawLabel)
            {
                var labelRect = GetLocalPointRectInSceneView(_path.Points[number], 24f);
                if (number.ToString().Length == 1)
                    labelRect.x += 1;

                GUI.Label(labelRect, number.ToString(), _skin.label);

            }

            if (_selectedPointIndex != number && GUI.Button(GetLocalPointRectInSceneView(_path.Points[number], 24f), "", _skin.button))
                SelectPointInListView(number);

            if (_selectedPointIndex != number && needDrawRemoveButton)
            {
                GUI.DrawTexture(removeRect, _textures["red circle"]);

                var crossRect = new Rect(removeRect.x + 4f, removeRect.y + 4f, 8f, 8f);
                GUI.DrawTexture(crossRect, _textures["cross"]);

                if (GUI.Button(removeRect, "", _skin.button))
                {
                    if (EditorApplication.timeSinceStartup - _lastClickRemoveTime > 0.3f)
                        _lastClickRemoveTime = EditorApplication.timeSinceStartup;
                    else
                    {
                        _pointsToRemove.Add(number);
                        _lastClickRemoveTime = 0d;
                    }
                }

                SceneView.lastActiveSceneView.Repaint();
            }

            Handles.EndGUI();

            if (_selectedPointIndex == number)
            {
                var newPos = _path.transform.InverseTransformPoint(Handles.PositionHandle(TransformPoint(_path.Points[number]), Tools.pivotRotation == PivotRotation.Local ? _path.transform.rotation : Quaternion.identity));

                if (NearlyEquals(Vector3.Distance(newPos, _path.Points[number]), 0f, 0.000001f))
                    return;

                _path.Points[number] = newPos;
            }
        }

        private void DrawAddButton(Vector3 localPos)
        {
            Handles.BeginGUI();
            GUI.DrawTexture(GetLocalPointRectInSceneView(localPos, 6f), _textures["white circle"]);
            Handles.EndGUI();

            if (Vector2.Distance(Event.current.mousePosition, GetPointPositionInSceneView(localPos)) > 40f)
                return;

            Handles.BeginGUI();
            GUI.DrawTexture(GetLocalPointRectInSceneView(localPos, 24f), _textures["white circle"]);

            var labelRect = GetLocalPointRectInSceneView(localPos, 24f);
            labelRect.y -= 2f;
            GUI.Label(labelRect, "+", _skin.customStyles[3]);

            if (GUI.Button(GetLocalPointRectInSceneView(localPos, 24f), "", _skin.button))
                AddElement(1, localPos);

            Handles.EndGUI();

            SceneView.lastActiveSceneView.Repaint();
        }
        #endregion

        #region Draw points
        private void DrawOnePoint()
        {
            DrawRoot();
            DrawPoint(0, false, true, false);
        }

        private void DrawTwoPoints()
        {
            DrawLine(_path.GetGlobalPoint(0), _path.GetGlobalPoint(1), Color.yellow);
            DrawRoot();
            DrawPoint(0, false, false, true);
            DrawAddButton(Vector3.Lerp(_path.Points[0], _path.Points[1], 0.5f));
            DrawPoint(1, false, false, true);
        }

        private void DrawThreePoints()
        {
            DrawCatmullRomLine(_path.GetGlobalPoint(2), _path.GetGlobalPoint(0), _path.GetGlobalPoint(1), _path.GetGlobalPoint(2), Color.yellow);
            DrawCatmullRomLine(_path.GetGlobalPoint(0), _path.GetGlobalPoint(1), _path.GetGlobalPoint(2), _path.GetGlobalPoint(0), Color.yellow);

            if (!_path.Looped)
                DrawLine(_path.GetGlobalPoint(2), _path.GetGlobalPoint(0), Color.white, true);
            else
                DrawCatmullRomLine(_path.GetGlobalPoint(1), _path.GetGlobalPoint(2), _path.GetGlobalPoint(0), _path.GetGlobalPoint(1), Color.yellow);

            DrawRoot();
            DrawPoint(0, !_path.Looped, false, true);
            DrawPoint(1, false, false, true);
            DrawPoint(2, !_path.Looped, false, true);
        }

        private void DrawManyPoints()
        {
            for (int i = 0; i < _path.Points.Count - 3; i++)
                DrawCatmullRomLine(_path.GetGlobalPoint(i), _path.GetGlobalPoint(i + 1), _path.GetGlobalPoint(i + 2), _path.GetGlobalPoint(i + 3), Color.yellow);

            if (_path.Looped)
            {
                DrawCatmullRomLine(_path.GetGlobalPoint(_path.Points.Count - 3), _path.GetGlobalPoint(_path.Points.Count - 2), _path.GetGlobalPoint(_path.Points.Count - 1), _path.GetGlobalPoint(0), Color.yellow);
                DrawCatmullRomLine(_path.GetGlobalPoint(_path.Points.Count - 2), _path.GetGlobalPoint(_path.Points.Count - 1), _path.GetGlobalPoint(0), _path.GetGlobalPoint(1), Color.yellow);
                DrawCatmullRomLine(_path.GetGlobalPoint(_path.Points.Count - 1), _path.GetGlobalPoint(0), _path.GetGlobalPoint(1), _path.GetGlobalPoint(2), Color.yellow);
            }
            else
            {
                DrawLine(_path.GetGlobalPoint(_path.Points.Count - 2), _path.GetGlobalPoint(_path.Points.Count - 1), Color.white, true);
                DrawLine(_path.GetGlobalPoint(0), _path.GetGlobalPoint(1), Color.white, true);
            }


            DrawRoot();

            for (int i = 1; i < _path.Points.Count - 1; i++)
                DrawPoint(i, false, false, true);

            if (_path.Looped)
            {
                DrawPoint(0, false, false, true);
                DrawPoint(_path.Points.Count - 1, false, false, true);
            }
            else
            {
                DrawPoint(0, false, false, true, false);
                DrawPoint(_path.Points.Count - 1, false, false, true, false);
            }
        }
        #endregion

        private void OnSceneGUI()
        {
            if (_inspector == null)
                return;

            if (Event.current.isKey && Event.current.keyCode == KeyCode.F && Event.current.type == EventType.KeyDown && _selectedPointIndex != -1)
            {
                FrameOnSelectedPoint();
                Event.current.Use();
            }

            if (_path.Points.Count == 1)
                DrawOnePoint();
            else if (_path.Points.Count == 2)
                DrawTwoPoints();
            else if (_path.Points.Count == 3)
                DrawThreePoints();
            else if (_path.Points.Count > 3)
                DrawManyPoints();

            foreach (var index in _pointsToRemove.OrderByDescending(i => i))
                RemoveElement(index);
            
            _pointsToRemove.Clear();
        }

        private void OnDisable() => DeselectPoint();
    }
}