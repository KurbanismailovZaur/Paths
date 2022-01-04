using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Paths
{
    [CustomEditor(typeof(Path))]
    public class PathInspector : Editor
    {
        private VisualElement _inspector;

        private Path _path;

        [SerializeField]
        private VisualTreeAsset _pathInspectorUXML;

        private Transform _selectedPoint;

        private Tool _lastTool = Tool.None;

        private bool _isFramed;

        private void MakeInspectorAndPath(out VisualElement inspector, out Path path)
        {
            inspector = new VisualElement();
            _pathInspectorUXML.CloneTree(inspector);

            path = ((Path)serializedObject.targetObject);
        }

        private void InitializeListView(ListView list, IList source, Func<VisualElement> maker, Action<VisualElement, int> binder)
        {
            list.itemsSource = source;
            list.makeItem = maker;
            list.bindItem = binder;
        }

        private void AddElement(ListView list, Path path, List<Transform> points, int index)
        {
            var point = new GameObject((index + 1).ToString());
            point.transform.parent = path.transform;
            point.hideFlags = HideFlags.HideInHierarchy;

            if (index < points.Count - 1)
                point.transform.position = Vector3.Lerp(points[index].position, points[index + 1].position, 0.5f);
            else
                point.transform.position = points[index].position + (points[index].position - points[index - 1].position);

            points.Insert(index + 1, point.transform);
            point.transform.SetSiblingIndex(index + 1);

            for (int i = index + 2; i < points.Count; i++)
                points[i].name = i.ToString();

            list.Rebuild();
            list.ScrollToItem(index + 1);
            list.selectedIndex = index + 1;
        }

        private void RemoveElement(ListView list, List<Transform> points, int index)
        {
            DestroyImmediate(points[index].gameObject);
            points.RemoveAt(index);

            for (int i = index; i < points.Count; i++)
                points[i].name = (Convert.ToInt32(points[i].name) - 1).ToString();

            list.Rebuild();
        }

        public override VisualElement CreateInspectorGUI()
        {
            _selectedPoint = null;

            MakeInspectorAndPath(out _inspector, out _path);

            var points = _path.transform.Cast<Transform>().ToList();

            var list = _inspector.Q<ListView>("Points");
            InitializeListView(list, points, () => new GroupBox(), (element, index) =>
            {
                var itemGroup = (GroupBox)element;
                itemGroup.Clear();

                #region Item group styling
                itemGroup.style.flexDirection = new StyleEnum<FlexDirection>(FlexDirection.Row);
                itemGroup.style.borderLeftWidth = 10f;
                itemGroup.style.justifyContent = new StyleEnum<Justify>(Justify.SpaceAround);
                #endregion

                #region Creating point number label
                var pointNumberLabel = new Label();
                pointNumberLabel.BindProperty(new SerializedObject(points[index].gameObject).FindProperty("m_Name"));
                pointNumberLabel.style.width = 40f;
                pointNumberLabel.style.unityTextAlign = new StyleEnum<TextAnchor>(TextAnchor.MiddleLeft);
                pointNumberLabel.style.color = Color.yellow;

                itemGroup.Add(pointNumberLabel);
                #endregion

                #region Creating point position Vector3 field
                var position = new Vector3Field() { value = points[index].position };
                position.Q("unity-x-input").ElementAt(1).style.width = 60f;
                position.Q("unity-y-input").ElementAt(1).style.width = 60f;
                position.Q("unity-z-input").ElementAt(1).style.width = 60f;

                position.BindProperty(new SerializedObject(points[index]).FindProperty("m_LocalPosition"));

                itemGroup.Add(position);
                #endregion

                #region Creating add, remove and focus point buttons
                var twoButtons = points.Count > 4;

                var addButton = new Button(() => AddElement(list, _path, points, index)) { text = "+" };
                addButton.style.marginBottom = addButton.style.marginLeft = addButton.style.marginRight = addButton.style.marginTop = 0f;
                addButton.style.paddingBottom = addButton.style.paddingLeft = addButton.style.paddingRight = addButton.style.paddingTop = 0f;
                addButton.style.width = 30f;
                addButton.style.height = 20f;

                Button removeButton = null;

                if (twoButtons)
                {
                    addButton.style.borderRightWidth = 0f;
                    addButton.style.borderTopRightRadius = addButton.style.borderBottomRightRadius = 0f;

                    removeButton = new Button(() => RemoveElement(list, points, index)) { text = "-" };
                    removeButton.style.marginBottom = removeButton.style.marginLeft = removeButton.style.marginRight = removeButton.style.marginTop = 0f;
                    removeButton.style.paddingBottom = removeButton.style.paddingLeft = removeButton.style.paddingRight = removeButton.style.paddingTop = 0f;
                    removeButton.style.width = 30f;
                    removeButton.style.height = 20f;
                    removeButton.style.borderLeftWidth = 0f;
                    removeButton.style.borderTopLeftRadius = removeButton.style.borderBottomLeftRadius = 0f;
                }

                #region Creating focus button
                var focusButton = new Button(() =>
                {
                    SelectPointInListView(index);
                    FrameOnSelectedPoint();
                });
                focusButton.style.marginBottom = focusButton.style.marginLeft = focusButton.style.marginRight = focusButton.style.marginTop = 0f;
                focusButton.style.paddingBottom = focusButton.style.paddingLeft = focusButton.style.paddingRight = focusButton.style.paddingTop = 0f;
                focusButton.style.width = 30f;
                focusButton.style.height = 20f;
                focusButton.style.alignItems = new StyleEnum<Align>(Align.Center);
                focusButton.style.justifyContent = new StyleEnum<Justify>(Justify.Center);
                focusButton.style.marginLeft = 20f;

                var image = new VisualElement();
                image.style.backgroundImage = Resources.Load<Texture2D>("Focus");
                image.style.height = image.style.width = 12f;

                focusButton.Add(image);
                #endregion

                #region Creating group for these buttons
                var buttonsGroup = new GroupBox();
                buttonsGroup.style.width = 110f;
                buttonsGroup.style.marginBottom = buttonsGroup.style.marginLeft = buttonsGroup.style.marginRight = buttonsGroup.style.marginTop = 0f;
                buttonsGroup.style.paddingBottom = buttonsGroup.style.paddingLeft = buttonsGroup.style.paddingRight = buttonsGroup.style.paddingTop = 0f;
                buttonsGroup.style.flexDirection = new StyleEnum<FlexDirection>(FlexDirection.Row);
                buttonsGroup.Add(addButton);

                if (twoButtons)
                    buttonsGroup.Add(removeButton);

                buttonsGroup.Add(focusButton);

                itemGroup.Add(buttonsGroup);
                #endregion
                #endregion
            });

            list.onSelectedIndicesChange += indeces =>
            {
                if (indeces.Count() == 0)
                    return;

                SelectPoint(points[indeces.First()]);
                SceneView.lastActiveSceneView.Repaint();
            };

            list.itemIndexChanged += (int index0, int index1) =>
            {
                var points = _path.transform.Cast<Transform>().ToList();

                if (index1 > index0)
                {
                    for (int i = index0 + 1; i <= index1; i++)
                        points[i].name = (Convert.ToInt32(points[i].name) - 1).ToString();
                }
                else
                {
                    for (int i = index0 - 1; i >= index1; i--)
                        points[i].name = (Convert.ToInt32(points[i].name) + 1).ToString();
                }

                points[index0].name = index1.ToString();
                points[index0].SetSiblingIndex(index1);
            };

            return _inspector;
        }

        private void SelectPointInListView(int index)
        {
            var list = _inspector.Q<ListView>();
            list.ScrollToItem(index);
            list.selectedIndex = index;
        }

        private void FrameOnSelectedPoint()
        {
            var index = _selectedPoint.GetSiblingIndex();
            
            Bounds bounds;
            if (!_isFramed)
            {
                bounds = new Bounds(_selectedPoint.position, Vector3.zero);
                bounds.Encapsulate(index != 0 ? _path.transform.GetChild(index - 1).position : _path.transform.GetChild(_path.transform.childCount - 1).position);
                bounds.Encapsulate(index != _path.transform.childCount - 1 ? _path.transform.GetChild(index + 1).position : _path.transform.GetChild(0).position);
            }
            else
                bounds = new Bounds(_selectedPoint.position, Vector3.one);

            SceneView.lastActiveSceneView.Frame(bounds, false);
            _isFramed = !_isFramed;
        }

        private void SelectPoint(Transform point)
        {
            _selectedPoint = point;
            _isFramed = false;
        }

        private void OnSceneGUI()
        {
            if (Event.current.isKey && Event.current.keyCode == KeyCode.F && Event.current.type == EventType.KeyDown && _selectedPoint != null)
            {
                FrameOnSelectedPoint();
                Event.current.Use();
            }

            var skin = Resources.Load<GUISkin>("Skin");

            foreach (Transform point in _path.transform)
            {
                var screenPos = SceneView.lastActiveSceneView.camera.WorldToScreenPoint(point.position);
                var texture = Resources.Load<Texture>("YellowCircle");

                Handles.BeginGUI();

                var pointRect = new Rect(screenPos.x, SceneView.lastActiveSceneView.camera.pixelHeight - screenPos.y, 24f, 24f);

                GUI.DrawTexture(new Rect(pointRect.x - 12f, pointRect.y - 12f, pointRect.width, pointRect.height), texture);
                GUI.Label(new Rect(pointRect.x - ((point.name.Length == 1) ? 11f : 12), pointRect.y - 12f, pointRect.width, pointRect.height), point.name, skin.label);

                if (_selectedPoint != point && GUI.Button(new Rect(pointRect.x - 12f, pointRect.y - 12f, pointRect.width, pointRect.height), "", skin.button))
                {
                    SelectPoint(point);
                    SelectPointInListView(_selectedPoint.GetSiblingIndex());
                }

                Handles.EndGUI();

                if (_selectedPoint == point)
                {
                    Undo.RecordObject(_selectedPoint, $"Changed path point {point.name} position");
                    point.position = Handles.PositionHandle(point.position, point.rotation);
                }
            }

            if (_selectedPoint != null)
            {
                if (_lastTool == Tool.None)
                    _lastTool = Tools.current;

                Tools.current = Tool.None;
                Handles.BeginGUI();

                var screenPos = SceneView.lastActiveSceneView.camera.WorldToScreenPoint(_path.transform.position);
                var rootRect = new Rect(screenPos.x - 12f, SceneView.lastActiveSceneView.camera.pixelHeight - screenPos.y - 12f, 24f, 24f);

                GUI.DrawTexture(rootRect, Resources.Load<Texture2D>("Root"));

                if (GUI.Button(rootRect, "", skin.button))
                {
                    _selectedPoint = null;

                    Tools.current = _lastTool;
                    _lastTool = Tool.None;
                }

                Handles.EndGUI();
            }
        }

        private void OnDisable()
        {
            if (_selectedPoint != null)
                Tools.current = _lastTool;
        }
    }
}