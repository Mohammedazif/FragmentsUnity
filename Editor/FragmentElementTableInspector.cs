using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace FragmentsUnity.Editor
{
    /// <summary>Inspector that draws IFC metadata for one element welded into a merged chunk.</summary>
    [CustomEditor(typeof(FragmentElementTable))]
    public sealed class FragmentElementTableInspector : UnityEditor.Editor
    {
        private const string ChunkHeading = "Merged Chunk";
        private const string ElementCountLabel = "Elements";
        private const string ElementPopupLabel = "Element";
        private const string ElementLabelFormat = "{0} — {1}";
        private const string UnnamedElementLabel = "Element";
        private const string OverflowFormat = "… {0} more welded into this chunk are not listed.";

        private const string DetachedMessage =
            "This chunk has no FragmentModel above it, so its elements cannot be resolved.";

        private const string EmptyTableMessage =
            "This chunk carries no element table, so no element inside it can be identified.";

        private FragmentElementTable _listedTable;
        private List<int> _localIds = new List<int>();
        private string[] _elementLabels = new string[0];
        private int _hiddenElementCount;
        private int _selectedIndex;

        private bool _showAttributes = true;
        private bool _showPropertySets;
        private bool _showMaterials;
        private bool _showClassifications;

        public override void OnInspectorGUI()
        {
            var table = target as FragmentElementTable;
            if (table == null)
            {
                return;
            }

            // Filtering deactivates geometry-free nodes, so inactive ancestors must still resolve.
            FragmentModel model = table.GetComponentInParent<FragmentModel>(true);
            if (model == null)
            {
                EditorGUILayout.HelpBox(DetachedMessage, MessageType.Info);
                return;
            }

            RefreshElementList(table, model);
            if (_localIds.Count == 0)
            {
                EditorGUILayout.HelpBox(EmptyTableMessage, MessageType.Info);
                return;
            }

            DrawChunkSummary();

            FragmentItemMetadata item = model.FindByLocalId(_localIds[_selectedIndex]);
            if (!FragmentMetadataFormatter.HasDisplayableMetadata(item))
            {
                FragmentMetadataInspectorGUI.DrawMissingMetadataHelp();
                return;
            }

            EditorGUILayout.Space();
            FragmentMetadataInspectorGUI.DrawIdentity(item);
            _showAttributes = FragmentMetadataInspectorGUI.DrawAttributes(item, _showAttributes);
            _showPropertySets = FragmentMetadataInspectorGUI.DrawPropertySets(item, _showPropertySets);
            _showMaterials = FragmentMetadataInspectorGUI.DrawMaterials(item, _showMaterials);
            _showClassifications = FragmentMetadataInspectorGUI.DrawClassifications(item, _showClassifications);
            FragmentMetadataInspectorGUI.DrawCopyButton(item);
        }

        private void DrawChunkSummary()
        {
            EditorGUILayout.LabelField(ChunkHeading);
            EditorGUILayout.LabelField(
                ElementCountLabel,
                (_localIds.Count + _hiddenElementCount).ToString(CultureInfo.InvariantCulture));

            _selectedIndex = Mathf.Clamp(
                EditorGUILayout.Popup(ElementPopupLabel, _selectedIndex, _elementLabels),
                0,
                _localIds.Count - 1);

            if (_hiddenElementCount > 0)
            {
                EditorGUILayout.LabelField(string.Format(
                    CultureInfo.InvariantCulture, OverflowFormat, _hiddenElementCount));
            }
        }

        private void RefreshElementList(FragmentElementTable table, FragmentModel model)
        {
            if (ReferenceEquals(_listedTable, table))
            {
                return;
            }

            _listedTable = table;
            _selectedIndex = 0;

            List<int> distinct = table.GetDistinctLocalIds();
            _hiddenElementCount = Mathf.Max(0, distinct.Count - FragmentImportLimits.MaxInspectorChunkElements);
            if (_hiddenElementCount > 0)
            {
                distinct.RemoveRange(FragmentImportLimits.MaxInspectorChunkElements, _hiddenElementCount);
            }

            _localIds = distinct;
            _elementLabels = new string[distinct.Count];
            for (int index = 0; index < distinct.Count; index++)
            {
                _elementLabels[index] = BuildElementLabel(model, distinct[index]);
            }
        }

        private static string BuildElementLabel(FragmentModel model, int localId)
        {
            FragmentItemMetadata item = model.FindByLocalId(localId);
            string identity = item != null && !string.IsNullOrEmpty(item.Name)
                ? item.Name
                : localId.ToString(CultureInfo.InvariantCulture);
            string category = item != null && !string.IsNullOrEmpty(item.Category)
                ? item.Category
                : UnnamedElementLabel;

            string label = string.Format(CultureInfo.InvariantCulture, ElementLabelFormat, category, identity);
            // Unity's popup reads '/' as a submenu separator, so an IFC name must not carry one.
            return label.Replace('/', ' ');
        }
    }
}
