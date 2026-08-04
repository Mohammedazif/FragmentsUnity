using System;
using System.Collections.Generic;
using System.Globalization;

namespace FragmentsUnity
{
    /// <summary>Attaches IFC property and quantity sets to items by walking IsDefinedBy/HasPropertySets relations under strict budgets.</summary>
    internal static class FragmentPropertySetWalker
    {
        private const string RelIsDefinedBy = "IsDefinedBy";
        private const string RelHasPropertySets = "HasPropertySets";
        private const string RelHasProperties = "HasProperties";
        private const string RelQuantities = "Quantities";

        /// <summary>Walks each item's property relations, spending per-item and per-model unit budgets; caller gates on ImportPropertySets.</summary>
        internal static void WalkPropertySets(FragmentImportResult result, Action<FragmentImportSeverity, string> log)
        {
            int itemCount = result.Items.Count;
            int modelBudget = Math.Min(itemCount, FragmentImportLimits.PropertyWalkModelItemCap)
                * FragmentImportLimits.PropertyWalkUnitsPerModelItem;
            int budgetExhaustedItems = 0;
            int propertyCandidateItems = 0;

            for (int itemIndex = 0; itemIndex < itemCount; itemIndex++)
            {
                FragmentItemMetadata item = result.Items[itemIndex];
                if (item.Relations.Count == 0)
                {
                    continue;
                }

                var visited = new HashSet<int> { itemIndex };
                int nodeBudget = Math.Min(FragmentImportLimits.MaxPropertyUnitsPerItem, modelBudget);
                int startBudget = nodeBudget;
                bool wantedProperties = false;

                foreach (FragmentRelation relation in item.Relations)
                {
                    bool definesFromType = NamesEqual(relation.Name, RelHasPropertySets);
                    if (!definesFromType && !NamesEqual(relation.Name, RelIsDefinedBy))
                    {
                        continue;
                    }
                    wantedProperties = true;

                    foreach (int targetId in relation.RelatedLocalIds)
                    {
                        if (--nodeBudget <= 0)
                        {
                            break;
                        }

                        GatherSets(result, targetId, 1, definesFromType, item.PropertySets, visited, ref nodeBudget);
                    }

                    if (nodeBudget <= 0)
                    {
                        break;
                    }
                }

                modelBudget = Math.Max(0, modelBudget - Math.Clamp(startBudget - nodeBudget, 0, startBudget));

                if (wantedProperties)
                {
                    propertyCandidateItems++;
                    if (nodeBudget <= 0)
                    {
                        budgetExhaustedItems++;
                    }
                }
            }

            if (budgetExhaustedItems > 0)
            {
                log?.Invoke(FragmentImportSeverity.Warning, string.Format(
                    CultureInfo.InvariantCulture,
                    "Metadata: the property set walk ran out of budget on {0} of {1} items with property relations; some properties were not imported",
                    budgetExhaustedItems, propertyCandidateItems));
            }
        }

        private static void GatherSets(
            FragmentImportResult result,
            int targetId,
            int depth,
            bool fromType,
            List<FragmentPropertySet> outSets,
            HashSet<int> visited,
            ref int budget)
        {
            if (depth > FragmentImportLimits.PropertyWalkMaxDepth
                || budget <= 0
                || targetId < 0
                || targetId >= result.Items.Count
                || visited.Contains(targetId))
            {
                return;
            }
            visited.Add(targetId);
            budget--;

            FragmentItemMetadata target = result.Items[targetId];

            long nodeSize = (long)target.Relations.Count + target.Name.Length + target.Category.Length;
            if (nodeSize >= FragmentImportLimits.MetadataBudgetCharsPerUnit)
            {
                budget -= (int)Math.Min(budget, nodeSize / FragmentImportLimits.MetadataBudgetCharsPerUnit);
                if (budget <= 0)
                {
                    return;
                }
            }

            bool isPropertySet = false;

            // HasProperties and Quantities are schema SETs, so a repeated id is padding or corruption.
            var seenProperties = new HashSet<int>();

            foreach (FragmentRelation relation in target.Relations)
            {
                if (--budget <= 0)
                {
                    break;
                }

                if (!NamesEqual(relation.Name, RelHasProperties) && !NamesEqual(relation.Name, RelQuantities))
                {
                    continue;
                }
                isPropertySet = true;

                string setName = string.IsNullOrEmpty(target.Name) ? target.Category : target.Name;
                var newSet = new FragmentPropertySet
                {
                    Name = setName.Length > FragmentImportLimits.MaxPropertyNameChars
                        ? setName.Substring(0, FragmentImportLimits.MaxPropertyNameChars)
                        : setName,
                    LocalId = targetId,
                    FromType = fromType
                };

                int setNameCharge = newSet.Name.Length / FragmentImportLimits.MetadataBudgetCharsPerUnit;
                if (setNameCharge > 0)
                {
                    budget -= Math.Min(budget, setNameCharge);
                    if (budget <= 0)
                    {
                        break;
                    }
                }

                newSet.Properties.Capacity = Math.Min(relation.RelatedLocalIds.Count, budget);

                foreach (int propertyId in relation.RelatedLocalIds)
                {
                    if (--budget <= 0)
                    {
                        break;
                    }

                    if (propertyId < 0 || propertyId >= result.Items.Count)
                    {
                        continue;
                    }

                    if (!seenProperties.Add(propertyId))
                    {
                        continue;
                    }

                    FragmentItemMetadata propertyItem = result.Items[propertyId];
                    if (propertyItem.Attributes.Count >= FragmentImportLimits.MetadataBudgetCharsPerUnit)
                    {
                        budget -= Math.Min(budget, propertyItem.Attributes.Count / FragmentImportLimits.MetadataBudgetCharsPerUnit);
                        if (budget <= 0)
                        {
                            break;
                        }
                    }

                    var newProperty = new FragmentAttribute();
                    FragmentPropertyValueReader.ReadPropertyValue(propertyItem, newProperty);
                    if (string.IsNullOrEmpty(newProperty.Name))
                    {
                        continue;
                    }

                    int textCharge = (newProperty.Name.Length + newProperty.Value.Length + newProperty.Type.Length)
                        / FragmentImportLimits.MetadataBudgetCharsPerUnit;
                    if (textCharge > 0)
                    {
                        budget -= Math.Min(budget, textCharge);
                    }

                    newSet.Properties.Add(newProperty);
                }

                if (newSet.Properties.Count > 0)
                {
                    // mirrors FragParser.cpp:694-697
                    if (newSet.Properties.Capacity > newSet.Properties.Count * 2)
                    {
                        newSet.Properties.Capacity = newSet.Properties.Count;
                    }
                    outSets.Add(newSet);
                }
            }

            // A node that is itself a set never chains into HasPropertySets; mirrors FragParser.cpp:702-705.
            if (isPropertySet)
            {
                return;
            }

            foreach (FragmentRelation relation in target.Relations)
            {
                if (--budget <= 0)
                {
                    break;
                }

                if (!NamesEqual(relation.Name, RelHasPropertySets))
                {
                    continue;
                }
                foreach (int childId in relation.RelatedLocalIds)
                {
                    if (--budget <= 0)
                    {
                        break;
                    }

                    GatherSets(result, childId, depth + 1, true, outSets, visited, ref budget);
                }
            }
        }

        // FString operator== defaults to ignore-case; mirrors FragParser.cpp:624.
        private static bool NamesEqual(string relationName, string expected)
        {
            return string.Equals(relationName, expected, StringComparison.OrdinalIgnoreCase);
        }
    }
}
