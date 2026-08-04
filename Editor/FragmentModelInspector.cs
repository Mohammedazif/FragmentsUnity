using System;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace FragmentsUnity.Editor
{
    /// <summary>Inspector for an imported model root: summary counts, GlobalId search, and the selected element's IFC data.</summary>
    [CustomEditor(typeof(FragmentModel))]
    public sealed class FragmentModelInspector : UnityEditor.Editor
    {
        private const float SearchButtonWidth = 60f;

        private const string SummaryHeading = "Model";
        private const string ModelNameLabel = "Name";
        private const string ModelGuidLabel = "Guid";
        private const string ItemCountLabel = "Items";
        private const string CategoryCountLabel = "Categories";
        private const string StoreyCountLabel = "Storeys";
        private const string SearchLabel = "Find Global Id";
        private const string SearchButtonLabel = "Find";

        private const string SearchMissMessage = "No item in this model carries that Global Id.";

        private const string EmptyModelMessage =
            "This model carries no item metadata. The .frag may have been exported without properties; "
            + "otherwise re-import it with 'Import Metadata' enabled.";

        private const string SelectionHintMessage =
            "Select an element in the scene, or search by Global Id, to see its IFC data here.";

        private FragmentModelAsset _summarizedAsset;
        private int _itemCount;
        private int _categoryCount;
        private int _storeyCount;

        private string _globalIdQuery = string.Empty;
        private FragmentItemMetadata _searchResult;
        private bool _searchMissed;

        private bool _showAttributes = true;
        private bool _showPropertySets;
        private bool _showMaterials;
        private bool _showClassifications;

        /// <summary>The element metadata a selected GameObject contributes to this model; null when it belongs elsewhere.</summary>
        public static FragmentItemMetadata ResolveSelection(GameObject selected, FragmentModel model)
        {
            if (selected == null || model == null)
            {
                return null;
            }

            // Filtering deactivates whole storeys, so inactive ancestors must still resolve.
            FragmentElementReference reference = selected.GetComponentInParent<FragmentElementReference>(true);
            if (reference == null || reference.gameObject.GetComponentInParent<FragmentModel>(true) != model)
            {
                return null;
            }

            return model.FindByLocalId(reference.LocalId);
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var model = target as FragmentModel;
            if (model == null)
            {
                return;
            }

            RefreshSummary(model);
            DrawSummary(model);

            if (_itemCount == 0)
            {
                EditorGUILayout.HelpBox(EmptyModelMessage, MessageType.Info);
                return;
            }

            DrawSearch(model);

            FragmentItemMetadata item = _searchResult ?? ResolveSelection(Selection.activeGameObject, model);
            if (item == null)
            {
                EditorGUILayout.HelpBox(SelectionHintMessage, MessageType.Info);
                return;
            }

            if (!FragmentMetadataFormatter.HasDisplayableMetadata(item))
            {
                FragmentMetadataInspectorGUI.DrawMissingMetadataHelp();
                return;
            }

            DrawItem(item);
        }

        private void RefreshSummary(FragmentModel model)
        {
            if (ReferenceEquals(_summarizedAsset, model.ModelAsset))
            {
                return;
            }

            _summarizedAsset = model.ModelAsset;
            _searchResult = null;
            _searchMissed = false;
            _itemCount = model.Data.Items.Count;
            _categoryCount = model.GetCategoryCounts().Count;
            _storeyCount = model.GetStoreyCounts().Count;
        }

        private void DrawSummary(FragmentModel model)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(SummaryHeading);
            EditorGUILayout.LabelField(ModelNameLabel, model.Data.ModelName);
            EditorGUILayout.LabelField(ModelGuidLabel, model.Data.ModelGuid);
            EditorGUILayout.LabelField(ItemCountLabel, _itemCount.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField(CategoryCountLabel, _categoryCount.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField(StoreyCountLabel, _storeyCount.ToString(CultureInfo.InvariantCulture));
        }

        private void DrawSearch(FragmentModel model)
        {
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            string query = EditorGUILayout.TextField(SearchLabel, _globalIdQuery);
            bool find = GUILayout.Button(SearchButtonLabel, GUILayout.Width(SearchButtonWidth));
            EditorGUILayout.EndHorizontal();

            if (!string.Equals(query, _globalIdQuery, StringComparison.Ordinal))
            {
                _globalIdQuery = query;
                _searchResult = null;
                _searchMissed = false;
            }

            if (find)
            {
                _searchResult = model.FindByGlobalId(_globalIdQuery);
                _searchMissed = _searchResult == null && !string.IsNullOrEmpty(_globalIdQuery);
            }

            if (_searchMissed)
            {
                EditorGUILayout.HelpBox(SearchMissMessage, MessageType.Warning);
            }
        }

        private void DrawItem(FragmentItemMetadata item)
        {
            EditorGUILayout.Space();
            FragmentMetadataInspectorGUI.DrawIdentity(item);
            _showAttributes = FragmentMetadataInspectorGUI.DrawAttributes(item, _showAttributes);
            _showPropertySets = FragmentMetadataInspectorGUI.DrawPropertySets(item, _showPropertySets);
            _showMaterials = FragmentMetadataInspectorGUI.DrawMaterials(item, _showMaterials);
            _showClassifications = FragmentMetadataInspectorGUI.DrawClassifications(item, _showClassifications);
            FragmentMetadataInspectorGUI.DrawCopyButton(item);
        }
    }
}
