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
                _lastPointsCount = _inspector._path.PointsCount;
            }

            public void PreUpdate() { }

            public void Release() { }

            public void Update()
            {
                _inspector._resolutionSlider.SetValueWithoutNotify(_inspector._path.Resolution);
                _inspector._loopedToggle.SetValueWithoutNotify(_inspector._path.Looped);
                _inspector._pathLengthLabel.text = $"Path length: {_inspector._path.Length}";

                if (_lastPointsCount != _inspector._path.PointsCount)
                {
                    var selectedIndex = _inspector._selectedPointIndex;
                    _inspector.DeselectPoint();

                    _listView.Rebuild();
                    _inspector.UpdateState();

                    _inspector.SelectPointInListView(selectedIndex);
                    _lastPointsCount = _inspector._path.PointsCount;
                }

                foreach (var groupBox in _listView.Query<GroupBox>("point-element").Build())
                {
                    var posField = groupBox.Q<Vector3Field>("point-position");

                    if (!int.TryParse(groupBox.Q<Label>("point-number").text, out int number))
                        return;

                    posField.value = _inspector._path.GetPoint(number, false);
                }

                if (_inspector._selectedPointIndex != -1)
                    SceneView.lastActiveSceneView.rootVisualElement.Q<Vector3Field>("point-position").value = _inspector._path.GetPoint(_inspector._selectedPointIndex, false);
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

        private SliderInt _resolutionSlider;

        private Toggle _loopedToggle;

        private Label _pathLengthLabel;

        private ListView _listView;

        private GroupBox _pointsAddGroupBox;

        private Label _helpLabel;

        private int _selectedPointIndex;

        private bool _isFramed;

        private GroupBox _toolsBox;

        private GUISkin _skin;

        private Dictionary<string, Texture> _textures = new();

        private double _lastClickTime;

        private List<int> _pointsToRemove = new();

        private List<Action> _pointsToAdd = new();

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

            _resolutionSlider = _inspector.Q<SliderInt>("resolution-slider");
            _loopedToggle = _inspector.Q<Toggle>("looped-toggle");
            _pathLengthLabel = _inspector.Q<Label>("path-length");
            _listView = _inspector.Q<ListView>("Points");
            _pointsAddGroupBox = _inspector.Q<GroupBox>("points-add-group");
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
            _textures.Add("blue circle", Resources.Load<Texture>("Numba/Paths/Textures/BlueCircle"));
            _textures.Add("white cross", Resources.Load<Texture>("Numba/Paths/Textures/WhiteCross"));
            _textures.Add("red rect", Resources.Load<Texture>("Numba/Paths/Textures/RedRect"));
        }

        #region Add and remove
        private void AddElement(int index, Vector3 position)
        {
            _path.InsertPoint(index, position, false);

            _listView.Rebuild();

            SelectPointInListView(index);
            SelectPoint(index);

            UpdateState();
        }

        private void AddElement(int index)
        {
            Vector3 newPoint;

            if (_path.PointsCount == 1)
                newPoint = _path.GetPoint(0, false);
            else
            {
                if (index < _path.PointsCount - 1)
                    newPoint = Vector3.Lerp(_path.GetPoint(index, false), _path.GetPoint(index + 1, false), 0.5f);
                else
                    newPoint = _path.GetPoint(index) + (_path.GetPoint(index) - _path.GetPoint(index - 1));
            }

            AddElement(index + 1, newPoint);
        }

        private void RemoveElement(int index)
        {
            DeselectPoint();

            _path.RemovePointAt(index);
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
            position.value = _path.GetPoint(_selectedPointIndex, false);

            if (needCreateToolsBox)
                position.RegisterValueChangedCallback(e => _path.SetPoint(_selectedPointIndex, e.newValue, false));
        }

        private void SelectPointInListView(int index)
        {
            index = Mathf.Min(index, _path.PointsCount - 1);

            _listView.ScrollToItem(index);
            _listView.selectedIndex = index;
        }

        private void FrameOnSelectedPoint()
        {
            Bounds bounds;
            if (_isFramed)
            {
                bounds = new Bounds(TransformPoint(_path.GetPoint(_selectedPointIndex, false)), Vector3.zero);
                bounds.Encapsulate(_selectedPointIndex != 0 ? TransformPoint(_path.GetPoint(_selectedPointIndex - 1, false)) : TransformPoint(_path.GetPoint(_path.PointsCount - 1, false)));
                bounds.Encapsulate(_selectedPointIndex != _path.PointsCount - 1 ? _path.GetPoint(_selectedPointIndex + 1) : _path.GetPoint(0));
            }
            else
                bounds = new Bounds(_path.GetPoint(_selectedPointIndex), Vector3.one);

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

        private Vector3 GetPointPositionInSceneView(Vector3 point, bool isLocal = false)
        {
            var sceneViewPos = SceneView.lastActiveSceneView.camera.WorldToScreenPoint(isLocal ? TransformPoint(point) : point);
            sceneViewPos.y = SceneView.lastActiveSceneView.camera.pixelHeight - sceneViewPos.y;

            return sceneViewPos;
        }

        private Rect GetPointRectInSceneView(Vector3 point, float size, bool isLocal = false) => GetPointRectInSceneView(point, new Vector2(size, size), isLocal);

        private Rect GetPointRectInSceneView(Vector3 point, Vector2 size, bool isLocal = false)
        {
            var screenPos = GetPointPositionInSceneView(point, isLocal);
            return new Rect(screenPos.x - size.x / 2f, screenPos.y - size.y / 2f, size.x, size.y);
        }
        #endregion

        #region Updates
        private void UpdatePointsAddGroup()
        {
            if (_path.PointsCount == 0)
                _pointsAddGroupBox.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.Flex);
            else
                _pointsAddGroupBox.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
        }

        private void UpdateHelp()
        {
            if (_path.PointsCount == 0)
                _helpLabel.text = @"There is no points in path. Add points by pressing '+' button above. If you don't see the button, just unfold 'Points' field.";
            else if (_path.PointsCount == 1)
                _helpLabel.text = @"When a path consists of 1 point, any attempt to calculate the value of a point on a line will always return that point.";
            else if (_path.PointsCount == 2)
                _helpLabel.text = @"When the path consists of 2 points it represents an ordinary straight line.";
            else if (_path.PointsCount == 3)
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

            _resolutionSlider.RegisterValueChangedCallback(e => _path.Resolution = e.newValue);
            _loopedToggle.RegisterValueChangedCallback(e => _path.Looped = e.newValue);

            var angleSlider = _inspector.Q<SliderInt>("max-angle-slider");
            _inspector.Q<Button>("optimize-button").clicked += () => _path.OptimizeResolutionByAngle(angleSlider.value);

            var basisSlider = _inspector.Q<Slider>("delta-basis-slider");
            _inspector.Q<Button>("optimize-by-length-button").clicked += () => _path.OptimizeResolutionByLength(basisSlider.value);

            _inspector.Q<Button>("optimize-all-button").clicked += () => _path.OptimizeResolution(angleSlider.value, basisSlider.value);

            _selectedPointIndex = -1;

            _pointsAddGroupBox.Q<Button>().clicked += () =>
            {
                AddElement(0, Vector3.zero);
                _pointsAddGroupBox.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
            };

            _listView.binding = new PointsBinding(this, _listView);

            _listView.itemsSource = (List<Vector3>)_path.GetType().GetField("_points", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_path);
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
                posField.value = _path.GetPoint(index, false);

                _positionFieldValueChangedCallbacks.Add(element, e =>
                {
                    _path.SetPoint(index, e.newValue, false);
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
            var step = 1f / _path.Resolution;
            var t = step;

            var lastPosition = p1;

            while (t < 1f)
            {
                var position = CatmullRomSpline.CalculatePoint(t, p0, p1, p2, p3);
                DrawLine(lastPosition, position, color, useDotted);

                lastPosition = position;
                t += step;
            }

            DrawLine(lastPosition, CatmullRomSpline.CalculatePoint(1f, p0, p1, p2, p3), color, useDotted);
        }

        private void DrawRoot()
        {
            Handles.BeginGUI();

            GUI.DrawTexture(GetPointRectInSceneView(Vector3.zero, 12f, true), _textures["white circle"]);
            GUI.DrawTexture(GetPointRectInSceneView(Vector3.zero, 6f, true), _textures["black circle"]);

            var rect = GetPointRectInSceneView(Vector3.zero, new Vector2(48, 24), true);
            rect.y += 24f;

            GUI.Label(rect, "pivot", _skin.customStyles[4]);

            if (_selectedPointIndex == -1)
            {
                Handles.EndGUI();
                return;
            }

            if (GUI.Button(GetPointRectInSceneView(Vector3.zero, 16f, true), "", _skin.button))
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
                removeRect = GetPointRectInSceneView(_path.GetPoint(number, false), 16f, true);
                removeRect.y -= isControl ? 30f : 24f;

                if (needDrawRemoveButton = Vector2.Distance(Event.current.mousePosition, removeRect.center) <= 20f)
                {
                    var pointCenter = GetPointRectInSceneView(_path.GetPoint(number, false), 24f, true).center;
                    var endCenter = removeRect.center;
                    endCenter.x -= 1f;

                    var lineRect = new Rect(endCenter, new Vector2(2f, pointCenter.y - endCenter.y));
                    GUI.DrawTexture(lineRect, _textures["red rect"]);
                }
            }

            GUI.DrawTexture(GetPointRectInSceneView(_path.GetPoint(number, false), 24f, true), drawYellowCircle ? _textures["yellow circle"] : _textures["white circle"]);

            if (isControl)
                GUI.DrawTexture(GetPointRectInSceneView(_path.GetPoint(number, false), 36f, true), _textures["dotted circle"]);

            if (drawBlackDot)
                GUI.DrawTexture(GetPointRectInSceneView(_path.GetPoint(number, false), 8f, true), _textures["black circle"]);

            if (drawLabel)
            {
                var labelRect = GetPointRectInSceneView(_path.GetPoint(number, false), 24f, true);
                if (number.ToString().Length != 2)
                    labelRect.x += 1;

                GUI.Label(labelRect, number.ToString(), number < 100 ? _skin.label : _skin.customStyles[6]);

            }

            if (_selectedPointIndex != number && GUI.Button(GetPointRectInSceneView(_path.GetPoint(number, false), 24f, true), "", _skin.button))
                SelectPointInListView(number);

            if (_selectedPointIndex != number && needDrawRemoveButton)
            {
                GUI.DrawTexture(removeRect, _textures["red circle"]);

                var crossRect = new Rect(removeRect.x + 4f, removeRect.y + 4f, 8f, 8f);
                GUI.DrawTexture(crossRect, _textures["white cross"]);

                if (GUI.Button(removeRect, "", _skin.button))
                {
                    if (EditorApplication.timeSinceStartup - _lastClickTime > 0.3f)
                        _lastClickTime = EditorApplication.timeSinceStartup;
                    else
                    {
                        _pointsToRemove.Add(number);
                        _lastClickTime = 0d;
                    }
                }

                SceneView.lastActiveSceneView.Repaint();
            }

            Handles.EndGUI();

            if (_selectedPointIndex == number)
            {
                var newPos = _path.transform.InverseTransformPoint(Handles.PositionHandle(TransformPoint(_path.GetPoint(number, false)), Tools.pivotRotation == PivotRotation.Local ? _path.transform.rotation : Quaternion.identity));
                var distance = Vector3.Distance(newPos, _path.GetPoint(number, false));

                if (distance == 0f || distance < 0.000001f)
                    return;

                _path.SetPoint(number, newPos, false);
            }
        }

        private void DrawAdjacentAddButton(Vector3 position, int insertTo)
        {
            Handles.BeginGUI();
            GUI.DrawTexture(GetPointRectInSceneView(position, 4f), _textures["white circle"]);
            Handles.EndGUI();

            if (Vector2.Distance(Event.current.mousePosition, GetPointPositionInSceneView(position)) > 20f)
                return;

            Handles.BeginGUI();
            GUI.DrawTexture(GetPointRectInSceneView(position, 24f), _textures["blue circle"]);

            var labelRect = GetPointRectInSceneView(position, 24f);
            labelRect.y -= 2f;
            GUI.Label(labelRect, "+", _skin.customStyles[5]);

            if (GUI.Button(GetPointRectInSceneView(position, 24f), "", _skin.button))
                _pointsToAdd.Add(() => AddElement(insertTo, _path.transform.InverseTransformPoint(position)));

            Handles.EndGUI();

            SceneView.lastActiveSceneView.Repaint();
        }

        private void DrawLastAddButton()
        {
            var direction = (_path.GetPoint(_path.PointsCount - 1, false) - _path.GetPoint(_path.PointsCount - 2, false)).normalized;

            var averageDistance = 0f;
            for (int i = 0; i < _path.PointsCount - 1; i++)
                averageDistance += Vector3.Distance(_path.GetPoint(i, false), _path.GetPoint(i + 1, false));

            direction *= averageDistance / (_path.PointsCount - 1);
            var localPos = _path.GetPoint(_path.PointsCount - 1, false) + direction;

            DrawLine(_path.GetPoint(_path.PointsCount - 1), TransformPoint(localPos), new Color32(72, 126, 214, 255), true);

            Handles.BeginGUI();
            GUI.DrawTexture(GetPointRectInSceneView(localPos, 24f, true), _textures["blue circle"]);

            var labelRect = GetPointRectInSceneView(localPos, 24f, true);
            labelRect.y -= 2f;
            GUI.Label(labelRect, "+", _skin.customStyles[5]);

            if (GUI.Button(GetPointRectInSceneView(localPos, 24f, true), "", _skin.button))
                _pointsToAdd.Add(() => AddElement(_path.PointsCount, localPos));

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
            DrawLine(_path.GetPoint(0), _path.GetPoint(1), Color.yellow);
            DrawRoot();
            DrawPoint(0, false, false, true);
            DrawAdjacentAddButton(_path.GetPoint(0, 0.5f), 1);
            DrawLastAddButton();
            DrawPoint(1, false, false, true);
        }

        private void DrawThreePoints()
        {
            DrawCatmullRomLine(_path.GetPoint(2), _path.GetPoint(0), _path.GetPoint(1), _path.GetPoint(2), Color.yellow);
            DrawCatmullRomLine(_path.GetPoint(0), _path.GetPoint(1), _path.GetPoint(2), _path.GetPoint(0), Color.yellow);

            if (!_path.Looped)
                DrawCatmullRomLine(_path.GetPoint(1), _path.GetPoint(2), _path.GetPoint(0), _path.GetPoint(1), Color.white, true);
            else
                DrawCatmullRomLine(_path.GetPoint(1), _path.GetPoint(2), _path.GetPoint(0), _path.GetPoint(1), Color.yellow);

            DrawRoot();
            DrawPoint(0, !_path.Looped, false, true);
            DrawAdjacentAddButton(_path.GetPoint(0, 0.5f), 1);
            DrawPoint(1, false, false, true);
            DrawAdjacentAddButton(_path.GetPoint(1, 0.5f), 2);
            DrawLastAddButton();
            DrawPoint(2, !_path.Looped, false, true);

            if (_path.Looped)
                DrawAdjacentAddButton(_path.GetPoint(2, 0.5f), 3);
        }

        private void DrawManyPoints()
        {
            for (int i = 0; i < _path.PointsCount - 3; i++)
                DrawCatmullRomLine(_path.GetPoint(i), _path.GetPoint(i + 1), _path.GetPoint(i + 2), _path.GetPoint(i + 3), Color.yellow);

            if (_path.Looped)
            {
                DrawCatmullRomLine(_path.GetPoint(_path.PointsCount - 3), _path.GetPoint(_path.PointsCount - 2), _path.GetPoint(_path.PointsCount - 1), _path.GetPoint(0), Color.yellow);
                DrawCatmullRomLine(_path.GetPoint(_path.PointsCount - 2), _path.GetPoint(_path.PointsCount - 1), _path.GetPoint(0), _path.GetPoint(1), Color.yellow);
                DrawCatmullRomLine(_path.GetPoint(_path.PointsCount - 1), _path.GetPoint(0), _path.GetPoint(1), _path.GetPoint(2), Color.yellow);
            }
            else
            {
                DrawLine(_path.GetPoint(_path.PointsCount - 2), _path.GetPoint(_path.PointsCount - 1), Color.white, true);
                DrawLine(_path.GetPoint(0), _path.GetPoint(1), Color.white, true);
            }


            DrawRoot();

            for (int i = 1; i < _path.PointsCount - 2; i++)
            {
                DrawPoint(i, false, false, true);
                DrawAdjacentAddButton(_path.GetPoint(i - 1, 0.5f), _path.Looped ? i : i + 1);
            }

            DrawPoint(_path.PointsCount - 2, false, false, true);

            if (_path.Looped)
            {
                DrawPoint(0, false, false, true);

                for (int i = 3; i >= 1; i--)
                    DrawAdjacentAddButton(_path.GetPoint(_path.PointsCount - i, 0.5f), _path.PointsCount - i + 1);

                DrawPoint(_path.PointsCount - 1, false, false, true);
            }
            else
            {
                DrawPoint(0, false, false, true, false);
                DrawPoint(_path.PointsCount - 1, false, false, true, false);
            }

            DrawLastAddButton();
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

            if (_path.PointsCount == 1)
                DrawOnePoint();
            else if (_path.PointsCount == 2)
                DrawTwoPoints();
            else if (_path.PointsCount == 3)
                DrawThreePoints();
            else if (_path.PointsCount > 3)
                DrawManyPoints();

            foreach (var index in _pointsToRemove.OrderByDescending(i => i))
                RemoveElement(index);

            _pointsToRemove.Clear();

            foreach (var callback in _pointsToAdd)
                callback();

            _pointsToAdd.Clear();
        }

        private void OnDisable() => DeselectPoint();
    }
}