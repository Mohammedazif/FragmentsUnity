using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace FragmentsUnity.Editor
{
    /// <summary>Dockable window listing the selected model's storeys and categories and driving its FragmentFilter.</summary>
    public sealed class FragmentFilterWindow : EditorWindow
    {
        private const string MenuPath = "Window/Fragments/Filter";
        private const string WindowTitle = "Fragments Filter";
        private const string StoreysHeading = "Levels";
        private const string CategoriesHeading = "Categories";
        private const string AttributeHeading = "Attribute";
        private const string AttributeNameLabel = "Name";
        private const string AttributeValueLabel = "Value";
        private const string ExactMatchLabel = "Exact match";
        private const string EmptyRowsLabel = "Nothing here — re-import with metadata enabled.";
        private const string NoFilterMessage =
            "This model has no FragmentFilter component, so nothing here could change what is drawn.";

        private const float CountColumnWidth = 48f;
        private const float IsolateButtonWidth = 64f;
        private const float ExactMatchColumnWidth = 96f;

        private static readonly GUIContent IsolateButton =
            new GUIContent("Isolate", "Show only this group and hide everything else.");

        private static readonly GUIContent ClearFilterButton =
            new GUIContent("Clear Filter", "Clear the filter and bring the whole model back.");

        private static readonly GUIContent RefreshButton =
            new GUIContent("Refresh", "Re-read the model and the visibility the scene is showing now.");

        [SerializeField] private string _attributeName = string.Empty;
        [SerializeField] private string _attributeValue = string.Empty;
        [SerializeField] private bool _attributeExactMatch;
        [SerializeField] private Vector2 _scroll;

        private readonly FragmentFilterState _state = new FragmentFilterState();

        /// <summary>Opens the filter window, docked wherever it was last placed.</summary>
        [MenuItem(MenuPath)]
        public static void Open()
        {
            GetWindow<FragmentFilterWindow>(WindowTitle);
        }

        private void OnSelectionChange()
        {
            Repaint();
        }

        // OnGUI is a Unity message, not a base member; the stubs this package compile-checks against declare it virtual.
#pragma warning disable CS0114
        private void OnGUI()
#pragma warning restore CS0114
        {
            _state.SetModel(FindSelectedModel());

            if (!_state.HasModel)
            {
                EditorGUILayout.HelpBox(_state.StatusMessage, MessageType.Info);
                return;
            }

            FragmentFilter filter = _state.Model.GetComponent<FragmentFilter>();
            if (filter == null)
            {
                EditorGUILayout.HelpBox(NoFilterMessage, MessageType.Warning);
                return;
            }

            string warning = _state.WarningMessage;
            if (warning != null)
            {
                EditorGUILayout.HelpBox(warning, MessageType.Warning);
            }

            DrawToolbar(filter);
            EditorGUILayout.LabelField(_state.StatusMessage);
            DrawAttributeFilter(filter);

            _scroll = GUILayout.BeginScrollView(_scroll);
            DrawRows(StoreysHeading, _state.Storeys, filter, storeys: true);
            DrawRows(CategoriesHeading, _state.Categories, filter, storeys: false);
            GUILayout.EndScrollView();
        }

        private void DrawToolbar(FragmentFilter filter)
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(ClearFilterButton))
            {
                _state.ClearAll();
                filter.ClearFilter();
            }

            if (GUILayout.Button(RefreshButton))
            {
                _state.Refresh();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawAttributeFilter(FragmentFilter filter)
        {
            EditorGUILayout.LabelField(AttributeHeading);
            EditorGUILayout.BeginHorizontal();

            _attributeName = EditorGUILayout.TextField(AttributeNameLabel, _attributeName);
            _attributeValue = EditorGUILayout.TextField(AttributeValueLabel, _attributeValue);
            _attributeExactMatch = EditorGUILayout.ToggleLeft(
                ExactMatchLabel, _attributeExactMatch, GUILayout.Width(ExactMatchColumnWidth));

            bool wasEnabled = GUI.enabled;
            GUI.enabled = wasEnabled && _state.CanIsolateAttribute(_attributeName);
            bool isolate = GUILayout.Button(IsolateButton, GUILayout.Width(IsolateButtonWidth));
            GUI.enabled = wasEnabled;

            EditorGUILayout.EndHorizontal();

            if (isolate)
            {
                filter.IsolateLocalIds(
                    _state.IsolateAttribute(_attributeName, _attributeValue, _attributeExactMatch));
            }
        }

        private void DrawRows(
            string heading, IReadOnlyList<FragmentFilterRow> rows, FragmentFilter filter, bool storeys)
        {
            EditorGUILayout.LabelField(heading);

            if (rows.Count == 0)
            {
                EditorGUILayout.LabelField(EmptyRowsLabel);
                return;
            }

            // Acting on a row rewrites the visibility of the rows around it, so the list itself must not be rebuilt here.
            for (int index = 0; index < rows.Count; index++)
            {
                FragmentFilterRow row = rows[index];

                EditorGUILayout.BeginHorizontal();
                bool visible = EditorGUILayout.ToggleLeft(row.Name, row.IsVisible);
                EditorGUILayout.LabelField(
                    row.Count.ToString(CultureInfo.CurrentCulture), GUILayout.Width(CountColumnWidth));
                bool isolate = GUILayout.Button(IsolateButton, GUILayout.Width(IsolateButtonWidth));
                EditorGUILayout.EndHorizontal();

                if (isolate)
                {
                    filter.IsolateLocalIds(
                        storeys ? _state.IsolateStorey(row.Name) : _state.IsolateCategory(row.Name));
                }
                else if (visible != row.IsVisible)
                {
                    filter.SetVisibleLocalIds(
                        storeys ? _state.ToggleStorey(row.Name, visible) : _state.ToggleCategory(row.Name, visible),
                        visible);
                }
            }
        }

        private static FragmentModel FindSelectedModel()
        {
            GameObject selected = Selection.activeGameObject;
            return selected != null ? selected.GetComponentInParent<FragmentModel>(true) : null;
        }
    }
}
