using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Redcode.Paths.Editor
{
    [CustomEditor(typeof(Path))]
    public class PathInspector : UnityEditor.Editor
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
                    var rotField = groupBox.Q<Vector3Field>("point-rotation");

                    if (!int.TryParse(groupBox.Q<Label>("point-number").text, out int number))
                        return;

                    var point = _inspector.GetPathPoint(number, false);

                    posField.SetValueWithoutNotify(point.Position);
                    rotField.SetValueWithoutNotify(_inspector.GetPointEuler(point));
                }

                if (_inspector._selectedPointIndex != -1)
                {
                    var point = _inspector.GetPathPoint(_inspector._selectedPointIndex, false);
                    SceneView.lastActiveSceneView.rootVisualElement.Q<Vector3Field>("point-position").SetValueWithoutNotify(point.Position);

                    var rotField = SceneView.lastActiveSceneView.rootVisualElement.Q<Vector3Field>("point-rotation");
                    rotField.SetValueWithoutNotify(_inspector.GetPointEuler(point));
                }
            }
        }

        private Point GetPathPoint(int index, bool useGlobal = true)
        {
            var method = _path.GetType().GetMethods().Single(m => m.Name == "GetPointByIndex" && m.GetParameters().Length == 2);
            return (PointData)method.Invoke(_path, new object[] { index, useGlobal });
        }

        private Point GetPathPoint(int segment, float distance, bool useNormalizedDistance = true, bool useGlobal = true)
        {
            var method = _path.GetType().GetMethods()
                .Single(m => m.Name == "GetPointAtDistance" && m.GetParameters().Length == 4);

            return (PointData)method.Invoke(_path, new object[] { segment, distance, useNormalizedDistance, useGlobal });
        }

        #region UXML
        [SerializeField]
        private VisualTreeAsset _pathInspectorUXML;

        [SerializeField]
        private VisualTreeAsset _pathSceneViewUXML;

        [SerializeField]
        private VisualTreeAsset _pathPointsListElementUXML;
        #endregion

        #region Fields
        private VisualElement _inspector;

        private Path _path;

        private SliderInt _resolutionSlider;

        private Toggle _loopedToggle;

        private Label _pathLengthLabel;

        private ListView _listView;

        private GroupBox _pointsAddGroupBox;

        private Label _helpLabel;

        [SerializeField]
        private int _selectedPointIndex;

        [SerializeField]
        private bool _isFramed;

        private GroupBox _toolsBox;

        private GUISkin _skin;

        private Dictionary<string, Texture> _textures = new();

        private double _lastClickTime;

        private List<int> _pointsToRemove = new();

        private List<Action> _pointsToAdd = new();

        private Plane[] _planes = new Plane[6];
        #endregion

        #region Callbacks
        private Dictionary<VisualElement, EventCallback<ChangeEvent<Vector3>>> _positionFieldValueChangedCallbacks = new();

        private Dictionary<VisualElement, EventCallback<ChangeEvent<Vector3>>> _rotationFieldValueChangedCallbacks = new();

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
            _skin = Resources.Load<GUISkin>("Redcode/Paths/Skin");

            _textures.Add("yellow circle", Resources.Load<Texture>("Redcode/Paths/Textures/YellowCircle"));
            _textures.Add("dotted circle", Resources.Load<Texture>("Redcode/Paths/Textures/DottedCircle"));
            _textures.Add("black circle", Resources.Load<Texture>("Redcode/Paths/Textures/BlackCircle"));
            _textures.Add("white circle", Resources.Load<Texture>("Redcode/Paths/Textures/WhiteCircle"));
            _textures.Add("red circle", Resources.Load<Texture>("Redcode/Paths/Textures/RedCircle"));
            _textures.Add("blue circle", Resources.Load<Texture>("Redcode/Paths/Textures/BlueCircle"));
            _textures.Add("white cross", Resources.Load<Texture>("Redcode/Paths/Textures/WhiteCross"));
            _textures.Add("red rect", Resources.Load<Texture>("Redcode/Paths/Textures/RedRect"));
        }

        #region Add and remove
        private void InsertElement(int index, Point point)
        {
            Undo.IncrementCurrentGroup();
            Undo.RecordObject(_path, $"Path Point Inserted To Index: {index}");

            _path.InsertPoint(index, point, false);
            _listView.Rebuild();

            SelectPointInListView(index);
            //SelectPoint(index);

            UpdateState();
            Undo.RegisterCompleteObjectUndo(this, "Path Inspector Changed");
        }

        private void InsertElement(int index)
        {
            Point newPoint;

            if (_path.PointsCount == 1)
                newPoint = GetPathPoint(0, false);
            else if (index < _path.PointsCount - 1)
                newPoint = GetPathPoint(index, 0.5f, true, false);
            else
            {
                var lastPoint = GetPathPoint(index, false);
                newPoint = new Point(lastPoint.Position + (lastPoint.Position - GetPathPoint(index - 1, false).Position), lastPoint.Rotation);
            }

            InsertElement(index + 1, newPoint);
        }

        private void RemoveElement(int index)
        {
            Undo.RecordObject(_path, $"Path Point {index} Removed");

            DeselectPoint();

            _path.RemovePointAt(index);
            _listView.Rebuild();

            UpdateState();
            Undo.RegisterCompleteObjectUndo(this, "Path Inspector Changed");
        }
        #endregion

        #region Selecting
        private void SelectPoint(int index)
        {
            Vector3Field positionField = null;
            Vector3Field rotationField = null;

            var needCreateToolsBox = _selectedPointIndex == -1;
            if (needCreateToolsBox)
            {
                var sceneViewElement = SceneView.lastActiveSceneView.rootVisualElement.Q("unity-scene-view-camera-rect");
                _pathSceneViewUXML.CloneTree(sceneViewElement);
                _toolsBox = sceneViewElement.Q<GroupBox>("paths-root");

                positionField = _toolsBox.Q<Vector3Field>("point-position");
                rotationField = _toolsBox.Q<Vector3Field>("point-rotation");

                _toolsBox.focusable = true;
                _toolsBox.RegisterCallback<KeyDownEvent>(e =>
                {
                    if (!e.ctrlKey || e.keyCode != KeyCode.Z)
                        return;

                    positionField.Blur();
                    rotationField.Blur();
                });
            }
            else
            {
                positionField = _toolsBox.Q<Vector3Field>("point-position");
                rotationField = _toolsBox.Q<Vector3Field>("point-rotation");
            }

            _selectedPointIndex = index;
            _isFramed = false;

            var label = _toolsBox.Q<Label>("point-number");
            label.text = $"Point number: {_selectedPointIndex}";

            var point = GetPathPoint(_selectedPointIndex, false);

            positionField.SetValueWithoutNotify(point.Position);
            rotationField.SetValueWithoutNotify(GetPointEuler(point));

            if (needCreateToolsBox)
            {
                positionField.RegisterValueChangedCallback(e => _path.SetPoint(_selectedPointIndex, e.newValue, false));

                var oldPos = Vector3.zero;
                positionField.RegisterCallback<FocusEvent>(e => oldPos = positionField.value);

                positionField.RegisterCallback<BlurEvent>(e =>
                {
                    if (positionField.value == oldPos)
                        return;

                    _path.SetPoint(index, oldPos, false);

                    Undo.RecordObject(_path, $"Path Point {index} Position Changed To {positionField.value}");
                    _path.SetPoint(index, positionField.value, false);
                });

                rotationField.RegisterValueChangedCallback(e =>
                {
                    var point = GetPathPoint(_selectedPointIndex, false);
                    SetPointEuler(ref point, e.newValue);
                    _path.SetPoint(_selectedPointIndex, point, false);
                });

                var oldRot = Vector3.zero;
                rotationField.RegisterCallback<FocusEvent>(e => oldRot = rotationField.value);

                rotationField.RegisterCallback<BlurEvent>(e =>
                {
                    if (rotationField.value == oldRot)
                        return;

                    var point = GetPathPoint(_selectedPointIndex, false);
                    SetPointEuler(ref point, oldRot);
                    _path.SetPoint(index, point, false); ;

                    Undo.RecordObject(_path, $"Path Point {index} Rotation Changed To {rotationField.value}");

                    SetPointEuler(ref point, rotationField.value);
                    _path.SetPoint(index, point, false);
                });
            }
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
                bounds = new Bounds(TransformPoint(GetPathPoint(_selectedPointIndex, false).Position), Vector3.zero);
                bounds.Encapsulate(_selectedPointIndex != 0 ? TransformPoint(GetPathPoint(_selectedPointIndex - 1, false).Position) : TransformPoint(GetPathPoint(_path.PointsCount - 1, false).Position));
                bounds.Encapsulate(_selectedPointIndex != _path.PointsCount - 1 ? GetPathPoint(_selectedPointIndex + 1).Position : GetPathPoint(0).Position);
            }
            else
                bounds = new Bounds(GetPathPoint(_selectedPointIndex).Position, Vector3.one);

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

        private Quaternion TransformRotation(Quaternion rotation) => _path.transform.rotation * rotation;

        private Vector3 GetPointPositionInSceneView(Vector3 point, bool isLocal = false)
        {
            var sceneViewPos = SceneView.lastActiveSceneView.camera.WorldToScreenPoint(isLocal ? TransformPoint(point) : point);
            sceneViewPos.y = SceneView.lastActiveSceneView.camera.pixelHeight - sceneViewPos.y;

#if UNITY_EDITOR_OSX
            sceneViewPos.x /= 2f;
            sceneViewPos.y /= 2f;
#endif
            return sceneViewPos;
        }

        private Rect GetPointRectInSceneView(Vector3 point, float size, bool isLocal = false) => GetPointRectInSceneView(point, new Vector2(size, size), isLocal);

        private Rect GetPointRectInSceneView(Vector3 point, Vector2 size, bool isLocal = false)
        {
            var screenPos = GetPointPositionInSceneView(point, isLocal);
            return new Rect(screenPos.x - size.x / 2f, screenPos.y - size.y / 2f, size.x, size.y);
        }
        #endregion

        private void UpdateState()
        {
            _pointsAddGroupBox.style.display = new StyleEnum<DisplayStyle>(_path.PointsCount == 0 ? DisplayStyle.Flex : DisplayStyle.None);
            SceneView.lastActiveSceneView.Repaint();

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

        private Vector3 GetPointEuler(Point point)
        {
            var euler = (Vector3)typeof(Point).GetField("_eulers", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(point);
            return euler;
        }

        private void SetPointEuler(ref Point point, Vector3 eulers)
        {
            object boxedPoint = point;
            typeof(Point).GetProperty("Eulers", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(boxedPoint, eulers);
            point = (Point)boxedPoint;
        }

        public override VisualElement CreateInspectorGUI()
        {
            if (_inspector != null)
                return _inspector;


            FindAllMainElements();
            LoadResources();

            _resolutionSlider.RegisterValueChangedCallback(e =>
            {
                if (e.previousValue == e.newValue)
                    return;

                Undo.RecordObject(_path, null);
                _path.Resolution = e.newValue;
                Undo.SetCurrentGroupName($"Path Resolution Changed To {e.newValue}");

                SceneView.lastActiveSceneView.Repaint();
            });

            _loopedToggle.RegisterValueChangedCallback(e =>
            {
                if (e.previousValue == e.newValue)
                    return;

                Undo.RecordObject(_path, $"Path Loop Changed To {e.newValue}");
                _path.Looped = e.newValue;

                SceneView.lastActiveSceneView.Repaint();
            });

            _inspector.Q<Button>("optimize-button").clicked += () =>
            {
                _path.Optimize();
                SceneView.lastActiveSceneView.Repaint();
            };

            _selectedPointIndex = -1;

            _pointsAddGroupBox.Q<Button>().clicked += () => InsertElement(0, new Point(Vector3.zero, Quaternion.identity));

            _listView.binding = new PointsBinding(this, _listView);

            _listView.itemsSource = (List<Point>)_path.GetType().GetField("_points", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_path);
            _listView.makeItem = () =>
            {
                _pathPointsListElementUXML.CloneTree(_inspector);

                var groupBox = _inspector.Children().Last();
                _inspector.Remove(groupBox);

                return groupBox;
            };

            _listView.bindItem = (VisualElement element, int index) =>
            {
                if (index < 0)
                    return;

                #region Point name
                var labelField = element.Q<Label>("point-number");
                labelField.text = index.ToString();
                #endregion

                var point = GetPathPoint(index, false);

                #region Point position
                var posField = element.Q<Vector3Field>("point-position");
                posField.value = point.Position;

                _positionFieldValueChangedCallbacks.Add(element, e =>
                {
                    if (e.previousValue == e.newValue)
                        return;

                    _path.SetPoint(index, e.newValue, false);
                    SceneView.lastActiveSceneView.Repaint();
                });

                posField.RegisterValueChangedCallback(_positionFieldValueChangedCallbacks[element]);

                var oldPos = Vector3.zero;

                posField.RegisterCallback<FocusEvent>(e => oldPos = posField.value);
                posField.RegisterCallback<BlurEvent>(e =>
                {
                    if (posField.value == oldPos)
                        return;

                    _path.SetPoint(index, oldPos, false);

                    Undo.RecordObject(_path, $"Path Point {index} Position Changed To {posField.value}");
                    _path.SetPoint(index, posField.value, false);
                });
                #endregion

                #region Point rotation
                var rotField = element.Q<Vector3Field>("point-rotation");
                var euler = GetPointEuler(point);
                rotField.SetValueWithoutNotify(euler);

                _rotationFieldValueChangedCallbacks.Add(element, e =>
                {
                    if (e.previousValue == e.newValue)
                        return;

                    var point = GetPathPoint(index, false);
                    SetPointEuler(ref point, e.newValue);

                    _path.SetPoint(index, point, false);
                    SceneView.lastActiveSceneView.Repaint();
                });

                rotField.RegisterValueChangedCallback(_rotationFieldValueChangedCallbacks[element]);

                var oldRot = Vector3.zero;

                rotField.RegisterCallback<FocusEvent>(e => oldRot = rotField.value);
                rotField.RegisterCallback<BlurEvent>(e =>
                {
                    if (rotField.value == oldRot)
                        return;

                    var point = GetPathPoint(index, false);
                    SetPointEuler(ref point, oldRot);

                    _path.SetPoint(index, point, false);

                    Undo.RecordObject(_path, $"Path Point {index} Rotation Changed To {rotField.value}");

                    point = GetPathPoint(index, false);
                    SetPointEuler(ref point, rotField.value);

                    _path.SetPoint(index, point, false);
                });
                #endregion

                #region Add and remove point
                var addButton = element.Q<Button>("add-point-button");
                _addButtonCallbacks.Add(element, () => InsertElement(index));
                addButton.clicked += _addButtonCallbacks[element];

                var removeButton = element.Q<Button>("remove-point-button");
                _removeButtonCallbacks.Add(element, () => RemoveElement(index));
                removeButton.clicked += _removeButtonCallbacks[element];
                #endregion
            };
            _listView.unbindItem = (VisualElement element, int index) =>
            {
                if (index < 0)
                    return;

                var posField = element.Q<Vector3Field>("point-position");
                posField.UnregisterValueChangedCallback(_positionFieldValueChangedCallbacks[element]);
                _positionFieldValueChangedCallbacks.Remove(element);

                var rotField = element.Q<Vector3Field>("point-rotation");
                rotField.UnregisterValueChangedCallback(_rotationFieldValueChangedCallbacks[element]);
                _rotationFieldValueChangedCallbacks.Remove(element);

                var addButton = element.Q<Button>("add-point-button");
                addButton.clicked -= _addButtonCallbacks[element];
                _addButtonCallbacks.Remove(element);

                var removeButton = element.Q<Button>("remove-point-button");
                removeButton.clicked -= _removeButtonCallbacks[element];
                _removeButtonCallbacks.Remove(element);
            };

            _listView.itemIndexChanged += (from, to) =>
            {
                var fromPoint = _path.GetPointByIndex(from, false);
                var toPoint = _path.GetPointByIndex(to, false);

                _path.SetPoint(from, toPoint, false);
                _path.SetPoint(to, fromPoint, false);

                Undo.RecordObject(_path, $"Path Points {from} And {to} Swaped");

                _path.SetPoint(from, fromPoint, false);
                _path.SetPoint(to, toPoint, false);

                SelectPointInListView(to);
            };

            _listView.onSelectedIndicesChange += indeces =>
            {
                if (indeces.Count() == 0)
                    return;

                SelectPoint(indeces.First());
                SceneView.lastActiveSceneView.Repaint();
            };

            _inspector.focusable = true;
            _inspector.RegisterCallback<KeyDownEvent>(e =>
            {
                if (!e.ctrlKey || e.keyCode != KeyCode.Z)
                    return;

                _listView.Blur();
            });

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
            var removeRect = Rect.zero;
            var needDrawRemoveButton = false;

            var point = GetPathPoint(number, false);
            var svRect = GetPointRectInSceneView(point.Position, 1f, true);

            GeometryUtility.CalculateFrustumPlanes(SceneView.lastActiveSceneView.camera, _planes);
            if (_selectedPointIndex != number && !GeometryUtility.TestPlanesAABB(_planes, new Bounds(TransformPoint(point.Position), Vector3.zero)))
                return;

            Handles.BeginGUI();

            if (_selectedPointIndex != number)
            {
                removeRect = GetPointRectInSceneView(point.Position, 16f, true);
                removeRect.y -= isControl ? 30f : 24f;

                if (needDrawRemoveButton = Vector2.Distance(Event.current.mousePosition, removeRect.center) <= 20f)
                {
                    var pointCenter = GetPointRectInSceneView(point.Position, 24f, true).center;
                    var endCenter = removeRect.center;
                    endCenter.x -= 1f;

                    var lineRect = new Rect(endCenter, new Vector2(4f, pointCenter.y - endCenter.y));
                    GUI.DrawTexture(lineRect, _textures["red rect"]);
                }
            }

            var isCursorNearPoint = Vector2.Distance(Event.current.mousePosition, GetPointPositionInSceneView(point.Position, true)) <= 20f;

            if (!isCursorNearPoint)
                GUI.DrawTexture(GetPointRectInSceneView(point.Position, 12f, true), drawYellowCircle ? _textures["yellow circle"] : _textures["white circle"]);
            else
                GUI.DrawTexture(GetPointRectInSceneView(point.Position, 24f, true), drawYellowCircle ? _textures["yellow circle"] : _textures["white circle"]);

            if (isControl)
                GUI.DrawTexture(GetPointRectInSceneView(point.Position, 36f, true), _textures["dotted circle"]);

            if (drawBlackDot)
                GUI.DrawTexture(GetPointRectInSceneView(point.Position, 8f, true), _textures["black circle"]);

            if (drawLabel && isCursorNearPoint)
            {
                var labelRect = GetPointRectInSceneView(point.Position, 24f, true);
                if (number.ToString().Length != 2)
                    labelRect.x += 1;

                GUI.Label(labelRect, number.ToString(), number < 100 ? _skin.label : _skin.customStyles[6]);

            }

            if (_selectedPointIndex != number && GUI.Button(GetPointRectInSceneView(point.Position, 24f, true), "", _skin.button))
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
                if (Tools.current == Tool.Move)
                {
                    var newPos = _path.transform.InverseTransformPoint(Handles.PositionHandle(TransformPoint(point.Position), Quaternion.identity));
                    var distance = Vector3.Distance(newPos, point.Position);

                    if (Mathf.Approximately(distance, 0f))
                        return;

                    Undo.RecordObject(_path, null);
                    _path.SetPoint(number, newPos, false);
                    Undo.SetCurrentGroupName($"Path Point {number} Position Changed To {newPos}");
                }
                else if (Tools.current == Tool.Rotate)
                {
                    var rot = TransformRotation(point.Rotation);
                    var newRot = Handles.RotationHandle(rot, TransformPoint(point.Position));
                    var angle = Quaternion.Angle(rot, newRot);

                    if (Mathf.Approximately(angle, 0f))
                        return;

                    Undo.RecordObject(_path, null);
                    _path.SetPoint(number, newRot, true);
                    Undo.SetCurrentGroupName($"Path Point {number} Rotation Changed To {newRot}");
                }
            }
        }

        private void DrawAdjacentAddButton(Vector3 position, int insertTo)
        {
            GeometryUtility.CalculateFrustumPlanes(SceneView.lastActiveSceneView.camera, _planes);
            if (!GeometryUtility.TestPlanesAABB(_planes, new Bounds(position, Vector3.zero)))
                return;

            var screenPos = GetPointPositionInSceneView(position);

            static Rect GetPointRectInSceneView(Vector3 screenPos, float size) => new Rect(screenPos.x - size / 2f, screenPos.y - size / 2f, size, size);
            var pointRect6 = GetPointRectInSceneView(screenPos, 6f);
            var pointRect24 = GetPointRectInSceneView(screenPos, 24f);

            Handles.BeginGUI();
            GUI.DrawTexture(pointRect6, _textures["white circle"]);
            Handles.EndGUI();

            if (Vector2.Distance(Event.current.mousePosition, GetPointPositionInSceneView(position)) > 20f)
                return;

            Handles.BeginGUI();
            GUI.DrawTexture(pointRect24, _textures["blue circle"]);

            var labelRect = pointRect24;
            labelRect.y -= 2f;
            GUI.Label(labelRect, "+", _skin.customStyles[5]);

            if (GUI.Button(pointRect24, "", _skin.button))
            {
                _pointsToAdd.Add(() =>
                {
                    var fromRot = GetPathPoint(insertTo - 1).Rotation;
                    var toRot = GetPathPoint(insertTo).Rotation;
                    var point = new Point(_path.transform.InverseTransformPoint(position), Quaternion.Lerp(fromRot, toRot, 0.5f));
                    InsertElement(insertTo, point);
                });
            }

            Handles.EndGUI();

            SceneView.lastActiveSceneView.Repaint();
        }

        private void DrawLastAddButton(float averageDistance)
        {
            var direction = (GetPathPoint(_path.PointsCount - 1, false).Position - GetPathPoint(_path.PointsCount - 2, false).Position).normalized;

            direction *= averageDistance;
            var localPos = GetPathPoint(_path.PointsCount - 1, false).Position + direction;

            DrawLine(GetPathPoint(_path.PointsCount - 1).Position, TransformPoint(localPos), new Color32(72, 126, 214, 255), true);

            GeometryUtility.CalculateFrustumPlanes(SceneView.lastActiveSceneView.camera, _planes);
            if (!GeometryUtility.TestPlanesAABB(_planes, new Bounds(TransformPoint(localPos), Vector3.zero)))
                return;

            Handles.BeginGUI();
            GUI.DrawTexture(GetPointRectInSceneView(localPos, 24f, true), _textures["blue circle"]);

            var labelRect = GetPointRectInSceneView(localPos, 24f, true);
            labelRect.y -= 2f;
            GUI.Label(labelRect, "+", _skin.customStyles[5]);

            if (GUI.Button(GetPointRectInSceneView(localPos, 24f, true), "", _skin.button))
                _pointsToAdd.Add(() => InsertElement(_path.PointsCount, new Point(localPos, GetPathPoint(_path.PointsCount - 1).Rotation)));

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
            DrawLine(GetPathPoint(0).Position, GetPathPoint(1).Position, Color.yellow);
            DrawRoot();
            DrawPoint(0, false, false, true);
            DrawAdjacentAddButton(GetPathPoint(0, 0.5f).Position, 1);
            DrawLastAddButton(Vector3.Distance(GetPathPoint(0).Position, GetPathPoint(1).Position));
            DrawPoint(1, false, false, true);
        }

        private void DrawThreePoints()
        {
            var point0 = GetPathPoint(0);
            var point1 = GetPathPoint(1);
            var point2 = GetPathPoint(2);

            DrawCatmullRomLine(point0.Position, point1.Position, point2.Position, point0.Position, Color.yellow);

            if (!_path.Looped)
            {
                DrawCatmullRomLine(point2.Position, point0.Position, point1.Position, point2.Position, Color.white, true);
                DrawCatmullRomLine(point1.Position, point2.Position, point0.Position, point1.Position, Color.white, true);
            }
            else
            {
                DrawCatmullRomLine(point2.Position, point0.Position, point1.Position, point2.Position, Color.yellow);
                DrawCatmullRomLine(point1.Position, point2.Position, point0.Position, point1.Position, Color.yellow);
            }

            DrawRoot();

            var dis0 = Vector3.Distance(GetPathPoint(0).Position, GetPathPoint(1).Position);
            var dis1 = Vector3.Distance(GetPathPoint(1).Position, GetPathPoint(2).Position);
            var averageDis = (dis0 + dis1) / 2f;

            if (!_path.Looped)
            {
                DrawPoint(0, true, false, true);
                DrawPoint(1, false, false, true);
                DrawAdjacentAddButton(GetPathPoint(0, 0.5f).Position, 2);
                DrawLastAddButton(averageDis);
                DrawPoint(2, false, false, true);
            }
            else
            {
                DrawPoint(0, false, false, true);
                DrawAdjacentAddButton(GetPathPoint(0, 0.5f).Position, 1);
                DrawPoint(1, false, false, true);
                DrawAdjacentAddButton(GetPathPoint(1, 0.5f).Position, 2);
                DrawLastAddButton(averageDis);
                DrawPoint(2, false, false, true);
                DrawAdjacentAddButton(GetPathPoint(2, 0.5f).Position, 3);
            }
        }

        private void DrawManyPoints()
        {
            #region Convert points
            Span<Point> points = stackalloc Point[_path.PointsCount];
            Span<Point> localPoints = stackalloc Point[_path.PointsCount];

            for (int i = 0; i < _path.PointsCount; i++)
            {
                points[i] = GetPathPoint(i);
                localPoints[i] = GetPathPoint(i, false);
            }
            #endregion

            #region Draw lines
            for (int i = 0; i < points.Length - 3; i++)
                DrawCatmullRomLine(points[i].Position, points[i + 1].Position, points[i + 2].Position, points[i + 3].Position, Color.yellow);

            if (_path.Looped)
            {
                DrawCatmullRomLine(points[points.Length - 3].Position, points[points.Length - 2].Position, points[points.Length - 1].Position, points[0].Position, Color.yellow);
                DrawCatmullRomLine(points[points.Length - 2].Position, points[points.Length - 1].Position, points[0].Position, points[1].Position, Color.yellow);
                DrawCatmullRomLine(points[points.Length - 1].Position, points[0].Position, points[1].Position, points[2].Position, Color.yellow);
            }
            else
            {
                DrawLine(points[points.Length - 2].Position, points[points.Length - 1].Position, Color.white, true);
                DrawLine(points[0].Position, points[1].Position, Color.white, true);
            }
            #endregion

            DrawRoot();

            #region Optimized draw last add button
            var averageDis = 0f;
            for (int i = 0; i < points.Length - 1; i++)
                averageDis += Vector3.Distance(points[i].Position, points[i + 1].Position);

            averageDis /= points.Length - 1;

            var direction = (localPoints[points.Length - 1].Position - localPoints[points.Length - 2].Position).normalized;
            direction *= averageDis;

            var localPos = localPoints[_path.PointsCount - 1].Position + direction;

            DrawLine(points[_path.PointsCount - 1].Position, TransformPoint(localPos), new Color32(72, 126, 214, 255), true);

            GeometryUtility.CalculateFrustumPlanes(SceneView.lastActiveSceneView.camera, _planes);
            if (GeometryUtility.TestPlanesAABB(_planes, new Bounds(TransformPoint(localPos), Vector3.zero)))
            {
                Handles.BeginGUI();
                GUI.DrawTexture(GetPointRectInSceneView(localPos, 24f, true), _textures["blue circle"]);

                var labelRect = GetPointRectInSceneView(localPos, 24f, true);
                labelRect.y -= 2f;
                GUI.Label(labelRect, "+", _skin.customStyles[5]);

                if (GUI.Button(GetPointRectInSceneView(localPos, 24f, true), "", _skin.button))
                    _pointsToAdd.Add(() => InsertElement(_path.PointsCount, new Point(localPos, GetPathPoint(_path.PointsCount - 1).Rotation)));

                Handles.EndGUI();

                SceneView.lastActiveSceneView.Repaint();
            }
            #endregion

            void DrawPointOptimized(Span<Point> localPoints, int number, bool isControl, bool drawBlackDot, bool drawLabel, bool drawYellowCircle = true)
            {
                var removeRect = Rect.zero;
                var needDrawRemoveButton = false;

                var point = localPoints[number];

                GeometryUtility.CalculateFrustumPlanes(SceneView.lastActiveSceneView.camera, _planes);
                if (_selectedPointIndex != number && !GeometryUtility.TestPlanesAABB(_planes, new Bounds(TransformPoint(point.Position), Vector3.zero)))
                    return;

                Rect GetPointRectInSceneView(Vector3 screenPos, float size)
                {
                    return new Rect(screenPos.x - size / 2f, screenPos.y - size / 2f, size, size);
                }

                var screenPos = GetPointPositionInSceneView(point.Position, true);

                var pointRectSource = GetPointRectInSceneView(screenPos, 0f);
                var pointRect8 = GetPointRectInSceneView(screenPos, 8f);
                var pointRect12 = GetPointRectInSceneView(screenPos, 12f);
                var pointRect16 = GetPointRectInSceneView(screenPos, 16f);
                var pointRect24 = GetPointRectInSceneView(screenPos, 24f);
                var pointRect36 = GetPointRectInSceneView(screenPos, 36f);

                Handles.BeginGUI();

                if (_selectedPointIndex != number)
                {
                    removeRect = pointRect16;
                    removeRect.y -= isControl ? 30f : 24f;

                    if (needDrawRemoveButton = Vector2.Distance(Event.current.mousePosition, removeRect.center) <= 20f)
                    {
                        var pointCenter = pointRect24.center;
                        var endCenter = removeRect.center;
                        endCenter.x -= 1f;

                        var lineRect = new Rect(endCenter, new Vector2(4f, pointCenter.y - endCenter.y));
                        GUI.DrawTexture(lineRect, _textures["red rect"]);
                    }
                }

                var isCursorNearPoint = Vector2.Distance(Event.current.mousePosition, GetPointPositionInSceneView(point.Position, true)) <= 20f;

                if (!isCursorNearPoint)
                    GUI.DrawTexture(pointRect12, drawYellowCircle ? _textures["yellow circle"] : _textures["white circle"]);
                else
                    GUI.DrawTexture(pointRect24, drawYellowCircle ? _textures["yellow circle"] : _textures["white circle"]);

                if (isControl)
                    GUI.DrawTexture(pointRect36, _textures["dotted circle"]);

                if (drawBlackDot)
                    GUI.DrawTexture(pointRect8, _textures["black circle"]);

                if (drawLabel && isCursorNearPoint)
                {
                    var labelRect = pointRect24;
                    if (number.ToString().Length != 2)
                        labelRect.x += 1;

                    GUI.Label(labelRect, number.ToString(), number < 100 ? _skin.label : _skin.customStyles[6]);

                }

                if (_selectedPointIndex != number && GUI.Button(pointRect24, "", _skin.button))
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
                    if (Tools.current == Tool.Move)
                    {
                        var newPos = _path.transform.InverseTransformPoint(Handles.PositionHandle(TransformPoint(point.Position), Quaternion.identity));
                        var distance = Vector3.Distance(newPos, point.Position);

                        if (Mathf.Approximately(distance, 0f))
                            return;

                        Undo.RecordObject(_path, null);
                        _path.SetPoint(number, newPos, false);
                        Undo.SetCurrentGroupName($"Path Point {number} Position Changed To {newPos}");
                    }
                    else if (Tools.current == Tool.Rotate)
                    {
                        var rot = TransformRotation(point.Rotation);
                        var newRot = Handles.RotationHandle(rot, TransformPoint(point.Position));
                        var angle = Quaternion.Angle(rot, newRot);

                        if (Mathf.Approximately(angle, 0f))
                            return;

                        Undo.RecordObject(_path, null);
                        _path.SetPoint(number, newRot, true);
                        Undo.SetCurrentGroupName($"Path Point {number} Rotation Changed To {newRot}");
                    }
                }
            }

            for (int i = 1; i < localPoints.Length - 2; i++)
            {
                DrawPointOptimized(localPoints, i, false, false, true);
                DrawAdjacentAddButton(GetPathPoint(i - 1, 0.5f).Position, _path.Looped ? i : i + 1);
            }

            DrawPointOptimized(localPoints, _path.PointsCount - 2, false, false, true);

            if (_path.Looped)
            {
                DrawPointOptimized(localPoints, 0, false, false, true);

                for (int i = 3; i >= 1; i--)
                    DrawAdjacentAddButton(GetPathPoint(_path.PointsCount - i, 0.5f).Position, _path.PointsCount - i + 1);

                DrawPointOptimized(localPoints, _path.PointsCount - 1, false, false, true);
            }
            else
            {
                DrawPointOptimized(localPoints, 0, false, false, true, false);
                DrawPointOptimized(localPoints, _path.PointsCount - 1, false, false, true, false);
            }
        }
        #endregion

        private void OnSceneGUI()
        {
            if (_inspector == null)
                return;

            // Need for prevent errors in editor when undo operation performed.
            if (_selectedPointIndex >= _path.PointsCount)
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

            if (Tools.current == Tool.Rotate && _selectedPointIndex != -1)
            {
                var point = GetPathPoint(_selectedPointIndex);
                Handles.PositionHandle(point.Position, point.Rotation);
            }

            foreach (var index in _pointsToRemove.OrderByDescending(i => i))
                RemoveElement(index);

            _pointsToRemove.Clear();

            foreach (var callback in _pointsToAdd)
                callback();

            _pointsToAdd.Clear();

            if (!_inspector.Q<Foldout>("debug-foldout").value || _path.PointsCount < 2)
                return;

            var useDirection = _inspector.Q<Toggle>("debug-use-direction").value;
            var pointData = _path.GetPointAtDistance(_inspector.Q<Slider>("debug-distance").value);
            var size = HandleUtility.GetHandleSize(pointData.Position);
            var rotation = useDirection ? Quaternion.LookRotation(pointData.Direction != Vector3.zero ? pointData.Direction : Vector3.forward) : pointData.Rotation;

            Handles.matrix = Matrix4x4.TRS(pointData.Position, rotation, Vector3.one * size * 0.5f);

            Handles.color = new Color32(0, 255, 120, 255);
            Handles.DrawWireCube(Vector3.zero, Vector3.one);

            DrawLine(Vector3.zero, Vector3.right * 2f, Color.magenta);
            DrawLine(Vector3.zero, Vector3.up * 2f, Color.green);
            DrawLine(Vector3.zero, Vector3.forward * 2f, Color.cyan);
        }

        private void OnDisable() => DeselectPoint();
    }
}