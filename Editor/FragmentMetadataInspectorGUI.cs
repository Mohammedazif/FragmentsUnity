using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace FragmentsUnity.Editor
{
    /// <summary>Draws one item's IFC metadata as inspector rows.</summary>
    public static class FragmentMetadataInspectorGUI
    {
        private const float RowLabelWidth = 150f;
        private const int NoLocalId = -1;

        private const string IfcClassLabel = "IFC Class";
        private const string NameLabel = "Name";
        private const string GlobalIdLabel = "Global Id";
        private const string TypeLabel = "Type";
        private const string ContainedInLabel = "Contained In";
        private const string StoreyLabel = "Storey";
        private const string LocalIdLabel = "Local Id";
        private const string MaterialRowLabel = "Material";
        private const string AttributesHeading = "Attributes";
        private const string PropertySetsHeading = "Property Sets";
        private const string MaterialsHeading = "Materials";
        private const string ClassificationHeading = "Classification";
        private const string CopyButtonLabel = "Copy metadata";
        private const string CountedHeadingFormat = "{0} ({1})";
        private const string PropertySetTitleFormat = "{0} ({1})";
        private const string PropertySetFromTypeTitleFormat = "{0} ({1}, from type)";
        private const string OverflowFormat = "… {0} more not shown — use Copy metadata.";

        private const string MissingMetadataMessage =
            "No IFC metadata on this element. The .frag may have been exported without properties; "
            + "otherwise re-import it with 'Import Metadata' enabled.";

        public static void DrawMissingMetadataHelp()
        {
            EditorGUILayout.HelpBox(MissingMetadataMessage, MessageType.Info);
        }

        public static void DrawIdentity(FragmentItemMetadata item)
        {
            if (item == null)
            {
                return;
            }

            DrawRow(IfcClassLabel, item.Category);
            DrawRow(NameLabel, item.Name);

            if (!string.IsNullOrEmpty(item.GlobalId))
            {
                DrawRow(GlobalIdLabel, item.GlobalId);
            }
            if (!string.IsNullOrEmpty(item.TypeName))
            {
                DrawRow(TypeLabel, item.TypeName);
            }
            if (!string.IsNullOrEmpty(item.ContainerName) || item.ContainerLocalId != NoLocalId)
            {
                DrawRow(
                    ContainedInLabel,
                    string.IsNullOrEmpty(item.ContainerName) ? item.ContainerCategory : item.ContainerName);
            }
            if (!string.IsNullOrEmpty(item.StoreyName) && item.StoreyLocalId != item.ContainerLocalId)
            {
                DrawRow(StoreyLabel, item.StoreyName);
            }
            if (item.LocalId != NoLocalId)
            {
                DrawRow(LocalIdLabel, item.LocalId.ToString(CultureInfo.InvariantCulture));
            }
        }

        public static bool DrawAttributes(FragmentItemMetadata item, bool expanded)
        {
            return DrawValueSection(AttributesHeading, item?.Attributes, expanded);
        }

        public static bool DrawClassifications(FragmentItemMetadata item, bool expanded)
        {
            return DrawValueSection(ClassificationHeading, item?.Classifications, expanded);
        }

        public static bool DrawPropertySets(FragmentItemMetadata item, bool expanded)
        {
            List<FragmentPropertySet> sets = item?.PropertySets;
            if (sets == null || sets.Count == 0)
            {
                return expanded;
            }

            bool open = DrawFoldout(PropertySetsHeading, sets.Count, expanded);
            if (!open)
            {
                return false;
            }

            int shownSets = Math.Min(sets.Count, FragmentImportLimits.MaxInspectorPropertySets);
            for (int setIndex = 0; setIndex < shownSets; setIndex++)
            {
                FragmentPropertySet set = sets[setIndex];
                if (set == null)
                {
                    continue;
                }

                EditorGUILayout.LabelField(string.Format(
                    CultureInfo.InvariantCulture,
                    set.FromType ? PropertySetFromTypeTitleFormat : PropertySetTitleFormat,
                    set.Name,
                    set.Properties.Count));
                DrawValueRows(set.Properties);
            }

            DrawOverflow(sets.Count - shownSets);
            return true;
        }

        public static bool DrawMaterials(FragmentItemMetadata item, bool expanded)
        {
            List<FragmentMaterial> materials = item?.Materials;
            if (materials == null || materials.Count == 0)
            {
                return expanded;
            }

            bool open = DrawFoldout(MaterialsHeading, materials.Count, expanded);
            if (!open)
            {
                return false;
            }

            int shown = Math.Min(materials.Count, FragmentImportLimits.MaxInspectorRowsPerSection);
            for (int materialIndex = 0; materialIndex < shown; materialIndex++)
            {
                FragmentMaterial material = materials[materialIndex];
                if (material == null)
                {
                    continue;
                }

                string label = string.IsNullOrEmpty(material.LayerSetName) ? MaterialRowLabel : material.LayerSetName;
                DrawRow(label, FragmentMetadataFormatter.DescribeMaterial(material));
            }

            DrawOverflow(materials.Count - shown);
            return true;
        }

        public static void DrawCopyButton(FragmentItemMetadata item)
        {
            if (GUILayout.Button(CopyButtonLabel))
            {
                GUIUtility.systemCopyBuffer = FragmentMetadataFormatter.ToClipboardText(item);
            }
        }

        private static bool DrawValueSection(string heading, List<FragmentAttribute> values, bool expanded)
        {
            if (values == null || values.Count == 0)
            {
                return expanded;
            }

            bool open = DrawFoldout(heading, values.Count, expanded);
            if (open)
            {
                DrawValueRows(values);
            }
            return open;
        }

        private static bool DrawFoldout(string heading, int count, bool expanded)
        {
            string title = string.Format(CultureInfo.InvariantCulture, CountedHeadingFormat, heading, count);
            return EditorGUILayout.Foldout(expanded, title, true);
        }

        private static void DrawValueRows(List<FragmentAttribute> values)
        {
            int shown = Math.Min(values.Count, FragmentImportLimits.MaxInspectorRowsPerSection);
            for (int valueIndex = 0; valueIndex < shown; valueIndex++)
            {
                FragmentAttribute value = values[valueIndex];
                if (value != null)
                {
                    DrawRow(value.Name, value.Value);
                }
            }

            DrawOverflow(values.Count - shown);
        }

        private static void DrawOverflow(int hiddenCount)
        {
            if (hiddenCount > 0)
            {
                EditorGUILayout.LabelField(string.Format(
                    CultureInfo.InvariantCulture, OverflowFormat, hiddenCount));
            }
        }

        private static void DrawRow(string name, string value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(name, GUILayout.Width(RowLabelWidth));
            EditorGUILayout.SelectableLabel(value, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            EditorGUILayout.EndHorizontal();
        }
    }
}
