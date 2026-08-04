using System;
using System.Collections.Generic;
using System.Globalization;

namespace FragmentsUnity
{
    /// <summary>Attaches materials and classifications to items by walking HasAssociations relations under strict budgets.</summary>
    internal static class FragmentAssociationWalker
    {
        private const string RelHasAssociations = "HasAssociations";
        private const string RelMaterials = "Materials";
        private const string RelForLayerSet = "ForLayerSet";
        private const string RelMaterialLayers = "MaterialLayers";
        private const string RelMaterial = "Material";
        private const string CategoryMaterial = "IFCMATERIAL";
        private const string CategoryClassificationFragment = "CLASSIFICATION";
        private const string AttrLayerSetName = "LayerSetName";
        private const string AttrLayerThickness = "LayerThickness";
        private const string AttrIdentification = "Identification";
        private const string AttrItemReference = "ItemReference";

        /// <summary>Walks each item's associations under per-item and per-model budgets, then resolves containment, type objects and storeys.</summary>
        internal static void WalkAssociations(FragmentImportResult result, Action<FragmentImportSeverity, string> log)
        {
            int itemCount = result.Items.Count;
            int materialCount = 0;
            int containerCount = 0;

            int modelBudget = Math.Min(itemCount, FragmentImportLimits.AssociationWalkModelItemCap)
                * FragmentImportLimits.AssociationNodesPerModelItem;
            int budgetExhaustedItems = 0;

            for (int itemIndex = 0; itemIndex < itemCount; itemIndex++)
            {
                FragmentItemMetadata item = result.Items[itemIndex];
                if (item.Relations.Count == 0)
                {
                    continue;
                }

                var visited = new HashSet<int> { itemIndex };
                int nodeBudget = Math.Min(FragmentImportLimits.MaxAssociationNodesPerItem, modelBudget);
                int startBudget = nodeBudget;

                foreach (FragmentRelation relation in item.Relations)
                {
                    if (!NamesEqual(relation.Name, RelHasAssociations))
                    {
                        continue;
                    }
                    foreach (int targetId in relation.RelatedLocalIds)
                    {
                        if (--nodeBudget <= 0)
                        {
                            break;
                        }

                        AddAssociation(result, targetId, string.Empty, 0, item, visited, ref nodeBudget);
                    }

                    if (nodeBudget <= 0)
                    {
                        break;
                    }
                }

                modelBudget = Math.Max(0, modelBudget - Math.Clamp(startBudget - nodeBudget, 0, startBudget));
                // An untouched budget of zero means the model ran dry earlier, not that this item spent it; mirrors FragParser.cpp:1013.
                if (nodeBudget <= 0 && nodeBudget < startBudget)
                {
                    budgetExhaustedItems++;
                }
                materialCount += item.Materials.Count;

                FragmentContainmentResolver.ResolveContainer(result, item);
                if (item.ContainerLocalId != -1)
                {
                    containerCount++;
                }

                FragmentContainmentResolver.ResolveTypeObject(result, item);
            }

            if (budgetExhaustedItems > 0)
            {
                log?.Invoke(FragmentImportSeverity.Warning, string.Format(
                    CultureInfo.InvariantCulture,
                    "Metadata: the association walk ran out of budget on {0} of {1} items; some materials or classifications were not imported",
                    budgetExhaustedItems, itemCount));
            }

            FragmentContainmentResolver.ResolveStoreys(result);
        }

        private static void AddAssociation(
            FragmentImportResult result,
            int targetId,
            string layerSetName,
            int depth,
            FragmentItemMetadata item,
            HashSet<int> visited,
            ref int budget)
        {
            FragmentItemMetadata target = result.FindItem(targetId);
            if (target == null
                || depth > FragmentImportLimits.AssociationWalkMaxDepth
                || budget <= 0
                || visited.Contains(targetId))
            {
                return;
            }
            visited.Add(targetId);
            budget--;

            if (string.Equals(target.Category, CategoryMaterial, StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(target.Name))
                {
                    item.Materials.Add(new FragmentMaterial
                    {
                        Name = Truncate(target.Name, FragmentImportLimits.MaxAssociationTextChars),
                        LayerSetName = layerSetName,
                        LocalId = targetId
                    });
                }
                return;
            }

            long nodeSize = (long)target.Attributes.Count + target.Relations.Count + target.Category.Length;
            if (nodeSize >= FragmentImportLimits.MetadataBudgetCharsPerUnit)
            {
                budget -= (int)Math.Min(budget, nodeSize / FragmentImportLimits.MetadataBudgetCharsPerUnit);
                if (budget <= 0)
                {
                    return;
                }
            }

            if (target.Category.IndexOf(CategoryClassificationFragment, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                // IFC2X3 calls it ItemReference, IFC4 calls it Identification; mirrors FragParser.cpp:878-883.
                FragmentAttribute identification = target.FindAttribute(AttrIdentification);
                FragmentAttribute itemReference = target.FindAttribute(AttrItemReference);
                string classificationName = identification != null
                    ? identification.Value
                    : itemReference != null ? itemReference.Value : target.Category;
                item.Classifications.Add(new FragmentAttribute(
                    Truncate(classificationName, FragmentImportLimits.MaxAssociationTextChars),
                    Truncate(target.Name, FragmentImportLimits.MaxAssociationTextChars),
                    Truncate(target.Category, FragmentImportLimits.MaxAssociationTextChars)));
                return;
            }

            FragmentRelation listRelation = FindRelation(result, targetId, RelMaterials);
            if (listRelation != null)
            {
                foreach (int childId in listRelation.RelatedLocalIds)
                {
                    if (--budget <= 0)
                    {
                        break;
                    }

                    AddAssociation(result, childId, layerSetName, depth + 1, item, visited, ref budget);
                }
            }
            FragmentRelation usageRelation = FindRelation(result, targetId, RelForLayerSet);
            if (usageRelation != null)
            {
                foreach (int childId in usageRelation.RelatedLocalIds)
                {
                    if (--budget <= 0)
                    {
                        break;
                    }

                    AddAssociation(result, childId, layerSetName, depth + 1, item, visited, ref budget);
                }
            }

            FragmentRelation layersRelation = FindRelation(result, targetId, RelMaterialLayers);
            if (layersRelation != null)
            {
                string setName = Truncate(AttributeValue(result, targetId, AttrLayerSetName), FragmentImportLimits.MaxAssociationTextChars);
                if (string.IsNullOrEmpty(setName))
                {
                    setName = layerSetName;
                }

                foreach (int layerId in layersRelation.RelatedLocalIds)
                {
                    if (--budget <= 0)
                    {
                        break;
                    }

                    FragmentItemMetadata layerItem = result.FindItem(layerId);
                    if (layerItem != null)
                    {
                        long layerSize = (long)layerItem.Attributes.Count + layerItem.Relations.Count;
                        if (layerSize >= FragmentImportLimits.MetadataBudgetCharsPerUnit)
                        {
                            budget -= (int)Math.Min(budget, layerSize / FragmentImportLimits.MetadataBudgetCharsPerUnit);
                            if (budget <= 0)
                            {
                                break;
                            }
                        }
                    }

                    float thickness = ParseThickness(AttributeValue(result, layerId, AttrLayerThickness));
                    FragmentRelation materialRelation = FindRelation(result, layerId, RelMaterial);
                    if (materialRelation == null)
                    {
                        continue;
                    }

                    foreach (int materialId in materialRelation.RelatedLocalIds)
                    {
                        if (--budget <= 0)
                        {
                            break;
                        }

                        FragmentItemMetadata materialItem = result.FindItem(materialId);
                        if (materialItem == null || string.IsNullOrEmpty(materialItem.Name))
                        {
                            continue;
                        }

                        item.Materials.Add(new FragmentMaterial
                        {
                            Name = Truncate(materialItem.Name, FragmentImportLimits.MaxAssociationTextChars),
                            LayerSetName = setName,
                            Thickness = thickness,
                            LocalId = materialId
                        });
                    }
                }
            }
        }

        internal static FragmentRelation FindRelation(FragmentImportResult result, int itemId, string relationName)
        {
            FragmentItemMetadata found = result.FindItem(itemId);
            if (found != null)
            {
                foreach (FragmentRelation candidate in found.Relations)
                {
                    if (NamesEqual(candidate.Name, relationName))
                    {
                        return candidate;
                    }
                }
            }
            return null;
        }

        private static string AttributeValue(FragmentImportResult result, int itemId, string attributeName)
        {
            FragmentItemMetadata found = result.FindItem(itemId);
            if (found != null)
            {
                FragmentAttribute attribute = found.FindAttribute(attributeName);
                if (attribute != null)
                {
                    return attribute.Value;
                }
            }
            return string.Empty;
        }

        // Unlike FCString::Atof, trailing garbage yields 0 rather than the numeric prefix; mirrors FragParser.cpp:946.
        private static float ParseThickness(string value)
        {
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) ? parsed : 0f;
        }

        // FString operator== defaults to ignore-case; mirrors FragParser.cpp:812.
        private static bool NamesEqual(string relationName, string expected)
        {
            return string.Equals(relationName, expected, StringComparison.OrdinalIgnoreCase);
        }

        private static string Truncate(string value, int maxChars)
        {
            return value.Length > maxChars ? value.Substring(0, maxChars) : value;
        }
    }
}
