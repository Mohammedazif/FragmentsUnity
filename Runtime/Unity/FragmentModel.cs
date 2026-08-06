using System;
using System.Collections.Generic;
using UnityEngine;

namespace FragmentsUnity
{
    /// <summary>Root component of an imported fragment model.</summary>
    public sealed class FragmentModel : MonoBehaviour
    {
        [SerializeField] private FragmentModelAsset _modelAsset;

        private FragmentModelAsset _loadedAsset;
        private FragmentModelData _data;
        private Dictionary<string, FragmentItemMetadata> _itemsByGlobalId;

        public FragmentModelAsset ModelAsset
        {
            get { return _modelAsset; }
        }

        public FragmentModelData Data
        {
            get
            {
                if (_data == null || !ReferenceEquals(_loadedAsset, _modelAsset))
                {
                    _loadedAsset = _modelAsset;
                    _data = _modelAsset != null ? _modelAsset.Load() : new FragmentModelData();
                    _itemsByGlobalId = null;
                }
                return _data;
            }
        }

        public void SetAsset(FragmentModelAsset asset)
        {
            _modelAsset = asset;
        }

        public FragmentItemMetadata FindByLocalId(int localId)
        {
            return Data.FindItem(localId);
        }

        public FragmentItemMetadata FindByGlobalId(string globalId)
        {
            if (string.IsNullOrEmpty(globalId))
            {
                return null;
            }

            FragmentModelData data = Data;
            if (_itemsByGlobalId == null)
            {
                _itemsByGlobalId = new Dictionary<string, FragmentItemMetadata>(StringComparer.OrdinalIgnoreCase);
                foreach (FragmentItemMetadata item in data.Items)
                {
                    if (item != null && !string.IsNullOrEmpty(item.GlobalId))
                    {
                        _itemsByGlobalId[item.GlobalId] = item;
                    }
                }
            }

            return _itemsByGlobalId.TryGetValue(globalId, out FragmentItemMetadata found) ? found : null;
        }

        public List<int> FindByCategory(string category)
        {
            var found = new List<int>();
            foreach (FragmentItemMetadata item in Data.Items)
            {
                if (item != null && string.Equals(item.Category, category, StringComparison.OrdinalIgnoreCase))
                {
                    found.Add(item.LocalId);
                }
            }
            return found;
        }

        /// <summary>Finds the storey item itself plus every item contained in it.</summary>
        public List<int> FindByStorey(string storeyName)
        {
            var found = new List<int>();
            foreach (FragmentItemMetadata item in Data.Items)
            {
                if (item == null)
                {
                    continue;
                }

                bool isTheStorey = FragmentIfcCategories.Matches(item.Category, FragmentIfcCategories.BuildingStorey)
                    && string.Equals(item.Name, storeyName, StringComparison.OrdinalIgnoreCase);

                if (isTheStorey || string.Equals(item.StoreyName, storeyName, StringComparison.OrdinalIgnoreCase))
                {
                    found.Add(item.LocalId);
                }
            }
            return found;
        }

        /// <summary>Finds items by direct attribute or property-set member.</summary>
        public List<int> FindByAttribute(string attributeName, string attributeValue, bool exactMatch)
        {
            var found = new List<int>();
            foreach (FragmentItemMetadata item in Data.Items)
            {
                if (item != null && ItemMatchesAttribute(item, attributeName, attributeValue, exactMatch))
                {
                    found.Add(item.LocalId);
                }
            }
            return found;
        }

        public Dictionary<string, int> GetCategoryCounts()
        {
            return CountNonEmptyValues(item => item.Category);
        }

        public Dictionary<string, int> GetStoreyCounts()
        {
            return CountNonEmptyValues(item => item.StoreyName);
        }

        /// <summary>Flattens one item's values into name → value; later duplicates win.</summary>
        public Dictionary<string, string> GetFlattenedValues(int localId)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            FragmentItemMetadata item = Data.FindItem(localId);
            if (item == null)
            {
                return values;
            }

            foreach (FragmentAttribute attribute in item.Attributes)
            {
                if (attribute != null)
                {
                    values[attribute.Name ?? string.Empty] = attribute.Value ?? string.Empty;
                }
            }

            foreach (FragmentPropertySet set in item.PropertySets)
            {
                if (set == null)
                {
                    continue;
                }
                foreach (FragmentAttribute property in set.Properties)
                {
                    if (property != null)
                    {
                        values[$"{set.Name}.{property.Name}"] = property.Value ?? string.Empty;
                    }
                }
            }

            foreach (FragmentAttribute classification in item.Classifications)
            {
                if (classification != null)
                {
                    values[$"Classification.{classification.Name}"] = classification.Value ?? string.Empty;
                }
            }

            for (int materialIndex = 0; materialIndex < item.Materials.Count; materialIndex++)
            {
                FragmentMaterial material = item.Materials[materialIndex];
                if (material != null)
                {
                    values[$"Material.{materialIndex}"] = material.Name ?? string.Empty;
                }
            }

            if (!string.IsNullOrEmpty(item.TypeName))
            {
                values["TypeName"] = item.TypeName;
            }
            if (!string.IsNullOrEmpty(item.ContainerName))
            {
                values["ContainedIn"] = item.ContainerName;
            }
            if (!string.IsNullOrEmpty(item.StoreyName))
            {
                values["Storey"] = item.StoreyName;
            }

            return values;
        }

        private static bool ItemMatchesAttribute(FragmentItemMetadata item, string name, string value, bool exactMatch)
        {
            foreach (FragmentAttribute attribute in item.Attributes)
            {
                if (attribute != null
                    && string.Equals(attribute.Name, name, StringComparison.OrdinalIgnoreCase)
                    && MatchesValue(attribute.Value, value, exactMatch))
                {
                    return true;
                }
            }

            foreach (FragmentPropertySet set in item.PropertySets)
            {
                if (set == null)
                {
                    continue;
                }
                foreach (FragmentAttribute property in set.Properties)
                {
                    if (property != null
                        && string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)
                        && MatchesValue(property.Value, value, exactMatch))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool MatchesValue(string candidate, string query, bool exactMatch)
        {
            if (string.IsNullOrEmpty(query))
            {
                return true;
            }
            if (exactMatch)
            {
                return string.Equals(candidate, query, StringComparison.OrdinalIgnoreCase);
            }
            return candidate != null && candidate.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private Dictionary<string, int> CountNonEmptyValues(Func<FragmentItemMetadata, string> selectValue)
        {
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (FragmentItemMetadata item in Data.Items)
            {
                if (item == null)
                {
                    continue;
                }
                string value = selectValue(item);
                if (string.IsNullOrEmpty(value))
                {
                    continue;
                }
                counts.TryGetValue(value, out int count);
                counts[value] = count + 1;
            }
            return counts;
        }
    }
}
