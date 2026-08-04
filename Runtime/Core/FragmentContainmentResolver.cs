using System;

namespace FragmentsUnity
{
    /// <summary>Resolves each item's spatial container, type object and building storey from its relations.</summary>
    internal static class FragmentContainmentResolver
    {
        private const string RelContainedInStructure = "ContainedInStructure";
        private const string RelDecomposes = "Decomposes";
        private const string RelObjectTypeOf = "ObjectTypeOf";
        private const string RelIsDefinedBy = "IsDefinedBy";
        private const string TypeCategorySuffix = "TYPE";
        private const string CategoryBuildingStorey = "IFCBUILDINGSTOREY";

        /// <summary>Fills the item's container from its first ContainedInStructure target; Decomposes only fills an empty container.</summary>
        internal static void ResolveContainer(FragmentImportResult result, FragmentItemMetadata item)
        {
            foreach (FragmentRelation relation in item.Relations)
            {
                bool isContainment = NamesEqual(relation.Name, RelContainedInStructure);
                if (!isContainment && !NamesEqual(relation.Name, RelDecomposes))
                {
                    continue;
                }
                if (relation.RelatedLocalIds.Count == 0)
                {
                    continue;
                }
                // A later ContainedInStructure overwrites a Decomposes container; mirrors FragParser.cpp:1030.
                if (item.ContainerLocalId != -1 && !isContainment)
                {
                    continue;
                }

                int containerId = relation.RelatedLocalIds[0];
                FragmentItemMetadata container = result.FindItem(containerId);
                if (container != null)
                {
                    item.ContainerLocalId = containerId;
                    item.ContainerName = container.Name;
                    item.ContainerCategory = container.Category;
                }
                if (isContainment)
                {
                    break;
                }
            }
        }

        /// <summary>Finds the item's type object among IsDefinedBy targets and copies the type's own attributes into a FromType property set.</summary>
        internal static void ResolveTypeObject(FragmentImportResult result, FragmentItemMetadata item)
        {
            foreach (FragmentRelation relation in item.Relations)
            {
                if (!NamesEqual(relation.Name, RelIsDefinedBy))
                {
                    continue;
                }
                foreach (int targetId in relation.RelatedLocalIds)
                {
                    FragmentItemMetadata target = result.FindItem(targetId);
                    if (target == null)
                    {
                        continue;
                    }
                    if (target.Category.EndsWith(TypeCategorySuffix, StringComparison.OrdinalIgnoreCase)
                        || FragmentAssociationWalker.FindRelation(result, targetId, RelObjectTypeOf) != null)
                    {
                        item.TypeName = target.Name;
                        item.TypeLocalId = targetId;

                        if (target.Attributes.Count > 0)
                        {
                            var typeSet = new FragmentPropertySet
                            {
                                Name = Truncate(target.Category, FragmentImportLimits.MaxPropertyNameChars),
                                LocalId = targetId,
                                FromType = true
                            };

                            int typeAttributeCount = Math.Min(target.Attributes.Count, FragmentImportLimits.MaxTypeAttributes);
                            typeSet.Properties.Capacity = typeAttributeCount;
                            for (int attrIndex = 0; attrIndex < typeAttributeCount; attrIndex++)
                            {
                                FragmentAttribute source = target.Attributes[attrIndex];
                                typeSet.Properties.Add(new FragmentAttribute(
                                    Truncate(source.Name, FragmentImportLimits.MaxPropertyNameChars),
                                    Truncate(source.Value, FragmentImportLimits.MaxPropertyValueChars),
                                    Truncate(source.Type, FragmentImportLimits.MaxPropertyNameChars)));
                            }

                            item.PropertySets.Add(typeSet);
                        }
                        break;
                    }
                }
                if (item.TypeLocalId != -1)
                {
                    break;
                }
            }
        }

        /// <summary>Finds each item's building storey by walking container ancestors; a nested element's storey may be indirect.</summary>
        internal static void ResolveStoreys(FragmentImportResult result)
        {
            for (int itemIndex = 0; itemIndex < result.Items.Count; itemIndex++)
            {
                FragmentItemMetadata item = result.Items[itemIndex];

                int current = item.ContainerLocalId != -1 ? item.ContainerLocalId : itemIndex;
                for (int step = 0; step < FragmentImportLimits.StoreyAncestorSearchLimit && current != -1; step++)
                {
                    FragmentItemMetadata ancestor = result.FindItem(current);
                    if (ancestor == null)
                    {
                        break;
                    }
                    if (NamesEqual(ancestor.Category, CategoryBuildingStorey))
                    {
                        item.StoreyName = ancestor.Name;
                        item.StoreyLocalId = current;
                        break;
                    }
                    current = ancestor.ContainerLocalId;
                }
            }
        }

        // FString operator== defaults to ignore-case; mirrors FragParser.cpp:1021.
        private static bool NamesEqual(string left, string right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private static string Truncate(string value, int maxChars)
        {
            return value.Length > maxChars ? value.Substring(0, maxChars) : value;
        }
    }
}
