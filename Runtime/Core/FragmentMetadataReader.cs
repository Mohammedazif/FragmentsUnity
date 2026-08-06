using System;
using System.Collections.Generic;
using System.Globalization;

namespace FragmentsUnity
{
    /// <summary>Reads per-item metadata into FragmentImportResult.Items.</summary>
    internal static class FragmentMetadataReader
    {
        internal static void BuildItemMetadata(
            Schema.Model model,
            IReadOnlyList<uint> localIds,
            IReadOnlyDictionary<uint, int> expressIdToDenseIndex,
            IReadOnlyDictionary<int, string> localIdToGlobalId,
            FragmentImportOptions options,
            int bufferSize,
            FragmentImportResult result,
            Action<FragmentImportSeverity, string> log)
        {
            int numItems = localIds.Count;
            if (numItems == 0)
            {
                log?.Invoke(FragmentImportSeverity.Warning, "Metadata: model has no local_ids, skipping");
                return;
            }

            result.Items.Capacity = numItems;
            for (int i = 0; i < numItems; i++)
            {
                result.Items.Add(new FragmentItemMetadata());
            }

            var tokens = new List<string>();
            int truncatedAttributeItems = 0;
            int rejectedTuples = 0;

            long tupleByteBudget = Math.Max(
                FragmentImportLimits.MinTupleByteBudgetBytes,
                (long)FragmentImportLimits.TupleByteBudgetPerInputByte * bufferSize);
            bool tupleBudgetSpent = false;

            bool ReadTuple(string tupleString)
            {
                // The cap and budget count decoded characters, not UTF-8 bytes.
                if (tupleString == null || tupleString.Length > FragmentImportLimits.MaxTupleBytes)
                {
                    rejectedTuples++;
                    return false;
                }
                if (tupleBudgetSpent)
                {
                    return false;
                }

                tupleByteBudget -= tupleString.Length;
                if (tupleByteBudget <= 0)
                {
                    tupleBudgetSpent = true;
                    return false;
                }

                // The tuple string ends at the first NUL byte.
                int nulIndex = tupleString.IndexOf('\0');
                if (nulIndex >= 0)
                {
                    tupleString = tupleString.Substring(0, nulIndex);
                }

                FragmentTupleTokenizer.SplitTuple(tupleString, tokens);
                return true;
            }

            for (int itemIdx = 0; itemIdx < numItems; itemIdx++)
            {
                if (tupleBudgetSpent)
                {
                    break;
                }

                FragmentItemMetadata item = result.Items[itemIdx];
                item.LocalId = itemIdx;
                item.ExpressId = localIds[itemIdx];

                if (itemIdx < result.Categories.Count)
                {
                    item.Category = result.Categories[itemIdx];
                }
                if (localIdToGlobalId.TryGetValue(itemIdx, out string globalId))
                {
                    item.GlobalId = globalId;
                }

                if (itemIdx >= model.AttributesLength)
                {
                    continue;
                }

                Schema.Attribute? attributeEntry = model.Attributes(itemIdx);
                if (!attributeEntry.HasValue)
                {
                    continue;
                }

                Schema.Attribute attributeData = attributeEntry.Value;
                int attributeEntryCount = Math.Min(attributeData.DataLength, (int)FragmentImportLimits.MaxItemAttributes);
                if (attributeData.DataLength > FragmentImportLimits.MaxItemAttributes)
                {
                    truncatedAttributeItems++;
                }
                item.Attributes.Capacity = attributeEntryCount;

                for (int j = 0; j < attributeEntryCount; j++)
                {
                    if (!ReadTuple(attributeData.Data(j)) || tokens.Count == 0)
                    {
                        continue;
                    }

                    var newAttribute = new FragmentAttribute
                    {
                        Name = Truncate(tokens[0], FragmentImportLimits.MaxPropertyNameChars)
                    };
                    if (tokens.Count > 1)
                    {
                        newAttribute.Value = Truncate(tokens[1], FragmentImportLimits.MaxPropertyValueChars);
                    }
                    if (tokens.Count > 2)
                    {
                        newAttribute.Type = Truncate(tokens[2], FragmentImportLimits.MaxPropertyNameChars);
                    }

                    if (item.Name.Length == 0 && newAttribute.Name.Equals("Name", StringComparison.OrdinalIgnoreCase))
                    {
                        item.Name = newAttribute.Value;
                    }

                    item.Attributes.Add(newAttribute);
                }
            }

            if (truncatedAttributeItems > 0)
            {
                log?.Invoke(FragmentImportSeverity.Warning, string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} item(s) declared more than {1} direct attributes; the rest were dropped.",
                    truncatedAttributeItems, FragmentImportLimits.MaxItemAttributes));
            }

            int truncatedRelationItems = 0;
            int pairCount = Math.Min(model.RelationsLength, model.RelationsItemsLength);

            for (int k = 0; k < pairCount; k++)
            {
                if (tupleBudgetSpent)
                {
                    break;
                }

                int ownerExpressId = model.RelationsItems(k);
                if (ownerExpressId < 0)
                {
                    continue;
                }

                if (!expressIdToDenseIndex.TryGetValue((uint)ownerExpressId, out int ownerDenseIndex)
                    || ownerDenseIndex < 0 || ownerDenseIndex >= result.Items.Count)
                {
                    continue;
                }

                Schema.Relation? relationEntry = model.Relations(k);
                if (!relationEntry.HasValue)
                {
                    continue;
                }

                FragmentItemMetadata owner = result.Items[ownerDenseIndex];
                Schema.Relation relationData = relationEntry.Value;

                // relations_items may name one owner many times, so the cap is per item, not per entry.
                int relationHeadroom = FragmentImportLimits.MaxItemRelations - owner.Relations.Count;
                if (relationHeadroom <= 0)
                {
                    truncatedRelationItems++;
                    continue;
                }

                int relationEntryCount = Math.Min(relationData.DataLength, relationHeadroom);

                for (int j = 0; j < relationEntryCount; j++)
                {
                    if (!ReadTuple(relationData.Data(j)) || tokens.Count == 0)
                    {
                        continue;
                    }

                    int targetCount = Math.Min(tokens.Count - 1, FragmentImportLimits.MaxRelationTargets);

                    var newRelation = new FragmentRelation
                    {
                        Name = Truncate(tokens[0], FragmentImportLimits.MaxPropertyNameChars)
                    };
                    newRelation.RelatedLocalIds.Capacity = targetCount;

                    for (int t = 1; t <= targetCount; t++)
                    {
                        // Parse failure leaves 0, which the range check skips.
                        long.TryParse(tokens[t], NumberStyles.Integer, CultureInfo.InvariantCulture,
                            out long targetExpressId);
                        if (targetExpressId <= 0 || targetExpressId > uint.MaxValue)
                        {
                            continue;
                        }
                        if (expressIdToDenseIndex.TryGetValue((uint)targetExpressId, out int targetDenseIndex))
                        {
                            newRelation.RelatedLocalIds.Add(targetDenseIndex);
                        }
                    }

                    owner.Relations.Add(newRelation);
                }
            }

            if (truncatedRelationItems > 0)
            {
                log?.Invoke(FragmentImportSeverity.Warning, string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} relation entr(ies) went past the {1}-relation limit for one item; the rest were dropped.",
                    truncatedRelationItems, FragmentImportLimits.MaxItemRelations));
            }

            if (rejectedTuples > 0)
            {
                log?.Invoke(FragmentImportSeverity.Warning, string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} metadata tuple(s) were larger than {1} bytes and were skipped.",
                    rejectedTuples, FragmentImportLimits.MaxTupleBytes));
            }
            if (tupleBudgetSpent)
            {
                log?.Invoke(FragmentImportSeverity.Error,
                    "Metadata text allowance spent; the remaining attributes and relations were not read.");
            }

            if (options.ImportPropertySets)
            {
                FragmentPropertySetWalker.WalkPropertySets(result, log);
            }
            FragmentAssociationWalker.WalkAssociations(result, log);
        }

        private static string Truncate(string value, int maxChars)
        {
            return value.Length > maxChars ? value.Substring(0, maxChars) : value;
        }
    }
}
