using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FragmentsUnity
{
    /// <summary>Turns one item's IFC metadata into the strings an inspector or a clipboard needs.</summary>
    public static class FragmentMetadataFormatter
    {
        private const string LineBreak = "\n";
        private const string ValueIndent = "  ";
        private const string NameValueSeparator = ": ";
        private const string CategoryLabel = "Category";
        private const string NameLabel = "Name";
        private const string GlobalIdLabel = "GlobalId";
        private const string TypeLabel = "Type";
        private const string ContainedInLabel = "Contained In";
        private const string StoreyLabel = "Storey";
        private const string LocalIdLabel = "LocalId";
        private const string AttributesHeading = "Attributes";
        private const string MaterialsHeading = "Materials";
        private const string ClassificationHeading = "Classification";
        private const string RelationsHeading = "Relations";
        private const string FromTypeSuffix = " (from type)";
        private const string ContainerValueFormat = "{0} ({1})";
        private const string LocalIdValueFormat = "{0} (Express #{1})";
        private const string LayerValueFormat = "{0}: {1} ({2} thick)";
        private const string MaterialThicknessFormat = "{0}  —  {1} thick";
        private const string RelatedIdSeparator = ", ";

        private const string ThicknessNumberFormat = "G6";

        public static bool HasDisplayableMetadata(FragmentItemMetadata item)
        {
            return item != null
                && (!item.IsEmpty()
                    || !string.IsNullOrEmpty(item.Name)
                    || !string.IsNullOrEmpty(item.Category));
        }

        public static string ToDisplayString(FragmentItemMetadata item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            AppendLabelled(builder, CategoryLabel, item.Category);
            AppendLabelled(builder, NameLabel, item.Name);

            if (!string.IsNullOrEmpty(item.GlobalId))
            {
                AppendLabelled(builder, GlobalIdLabel, item.GlobalId);
            }
            if (!string.IsNullOrEmpty(item.TypeName))
            {
                AppendLabelled(builder, TypeLabel, item.TypeName);
            }
            if (!string.IsNullOrEmpty(item.ContainerName))
            {
                AppendLabelled(builder, ContainedInLabel, string.Format(
                    CultureInfo.InvariantCulture, ContainerValueFormat, item.ContainerName, item.ContainerCategory));
            }
            if (!string.IsNullOrEmpty(item.StoreyName) && item.StoreyLocalId != item.ContainerLocalId)
            {
                AppendLabelled(builder, StoreyLabel, item.StoreyName);
            }
            AppendLabelled(builder, LocalIdLabel, string.Format(
                CultureInfo.InvariantCulture, LocalIdValueFormat, item.LocalId, item.ExpressId));

            AppendSection(builder, AttributesHeading, item.Attributes);
            AppendPropertySets(builder, item.PropertySets);
            AppendMaterials(builder, item.Materials);
            AppendSection(builder, ClassificationHeading, item.Classifications);

            return builder.ToString();
        }

        public static string DescribeMaterial(FragmentMaterial material)
        {
            if (material == null)
            {
                return string.Empty;
            }
            if (material.Thickness <= 0f)
            {
                return material.Name ?? string.Empty;
            }
            return string.Format(
                CultureInfo.InvariantCulture,
                MaterialThicknessFormat,
                material.Name,
                material.Thickness.ToString(ThicknessNumberFormat, CultureInfo.InvariantCulture));
        }

        /// <summary>Distinct material names in first-seen order, compared case-insensitively.</summary>
        public static List<string> GetMaterialNames(FragmentItemMetadata item)
        {
            var names = new List<string>();
            if (item == null)
            {
                return names;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (FragmentMaterial material in item.Materials)
            {
                if (material == null)
                {
                    continue;
                }

                string name = material.Name ?? string.Empty;
                if (seen.Add(name))
                {
                    names.Add(name);
                }
            }
            return names;
        }

        /// <summary>Every set name in file order, duplicates included.</summary>
        public static List<string> GetPropertySetNames(FragmentItemMetadata item)
        {
            var names = new List<string>();
            if (item == null)
            {
                return names;
            }

            foreach (FragmentPropertySet set in item.PropertySets)
            {
                if (set != null)
                {
                    names.Add(set.Name ?? string.Empty);
                }
            }
            return names;
        }

        /// <summary>A copy, matched case-insensitively; empty when the set is absent.</summary>
        public static List<FragmentAttribute> GetPropertiesInSet(FragmentItemMetadata item, string setName)
        {
            FragmentPropertySet set = item != null ? item.FindPropertySet(setName ?? string.Empty) : null;
            return set != null ? new List<FragmentAttribute>(set.Properties) : new List<FragmentAttribute>();
        }

        /// <summary>Everything ToDisplayString shows plus the item's relations.</summary>
        public static string ToClipboardText(FragmentItemMetadata item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            var builder = new StringBuilder(ToDisplayString(item));
            AppendRelations(builder, item.Relations);
            return builder.ToString();
        }

        private static void AppendLabelled(StringBuilder builder, string label, string value)
        {
            builder.Append(label).Append(NameValueSeparator).Append(value).Append(LineBreak);
        }

        private static void AppendSection(StringBuilder builder, string heading, List<FragmentAttribute> values)
        {
            if (values.Count == 0)
            {
                return;
            }

            builder.Append(LineBreak).Append(heading).Append(LineBreak);
            AppendValues(builder, values);
        }

        private static void AppendValues(StringBuilder builder, List<FragmentAttribute> values)
        {
            foreach (FragmentAttribute value in values)
            {
                if (value != null)
                {
                    builder.Append(ValueIndent).Append(value.Name).Append(NameValueSeparator)
                        .Append(value.Value).Append(LineBreak);
                }
            }
        }

        private static void AppendPropertySets(StringBuilder builder, List<FragmentPropertySet> sets)
        {
            foreach (FragmentPropertySet set in sets)
            {
                if (set == null)
                {
                    continue;
                }

                builder.Append(LineBreak).Append(set.Name);
                if (set.FromType)
                {
                    builder.Append(FromTypeSuffix);
                }
                builder.Append(LineBreak);
                AppendValues(builder, set.Properties);
            }
        }

        private static void AppendMaterials(StringBuilder builder, List<FragmentMaterial> materials)
        {
            if (materials.Count == 0)
            {
                return;
            }

            builder.Append(LineBreak).Append(MaterialsHeading).Append(LineBreak);
            foreach (FragmentMaterial material in materials)
            {
                if (material == null)
                {
                    continue;
                }

                builder.Append(ValueIndent);
                if (material.Thickness > 0f)
                {
                    builder.Append(string.Format(
                        CultureInfo.InvariantCulture,
                        LayerValueFormat,
                        material.LayerSetName,
                        material.Name,
                        material.Thickness.ToString(ThicknessNumberFormat, CultureInfo.InvariantCulture)));
                }
                else
                {
                    builder.Append(material.Name);
                }
                builder.Append(LineBreak);
            }
        }

        private static void AppendRelations(StringBuilder builder, List<FragmentRelation> relations)
        {
            if (relations.Count == 0)
            {
                return;
            }

            builder.Append(LineBreak).Append(RelationsHeading).Append(LineBreak);
            foreach (FragmentRelation relation in relations)
            {
                if (relation != null)
                {
                    builder.Append(ValueIndent).Append(relation.Name).Append(NameValueSeparator)
                        .Append(string.Join(RelatedIdSeparator, relation.RelatedLocalIds)).Append(LineBreak);
                }
            }
        }
    }
}
