using System;
using System.Collections.Generic;

namespace FragmentsUnity
{
    /// <summary>All metadata parsed for one IFC item.</summary>
    public sealed class FragmentItemMetadata
    {
        /// <summary>Doubles as the index into FragmentImportResult.Items; -1 when the budget stopped short of this item.</summary>
        public int LocalId { get; set; } = -1;

        public long ExpressId { get; set; }

        /// <summary>A 22-character base64 GUID.</summary>
        public string GlobalId { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public List<FragmentAttribute> Attributes { get; } = new List<FragmentAttribute>();

        public List<FragmentPropertySet> PropertySets { get; } = new List<FragmentPropertySet>();

        public List<FragmentMaterial> Materials { get; } = new List<FragmentMaterial>();

        public List<FragmentAttribute> Classifications { get; } = new List<FragmentAttribute>();

        public string TypeName { get; set; } = string.Empty;

        public int TypeLocalId { get; set; } = -1;

        public string ContainerName { get; set; } = string.Empty;

        public string ContainerCategory { get; set; } = string.Empty;

        public int ContainerLocalId { get; set; } = -1;

        public string StoreyName { get; set; } = string.Empty;

        public int StoreyLocalId { get; set; } = -1;

        public List<FragmentRelation> Relations { get; } = new List<FragmentRelation>();

        public bool IsEmpty()
        {
            return Attributes.Count == 0
                && PropertySets.Count == 0
                && Materials.Count == 0
                && Classifications.Count == 0
                && Relations.Count == 0
                && string.IsNullOrEmpty(GlobalId);
        }

        public FragmentAttribute FindAttribute(string name)
        {
            foreach (FragmentAttribute attribute in Attributes)
            {
                if (attribute.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return attribute;
                }
            }
            return null;
        }

        public FragmentPropertySet FindPropertySet(string name)
        {
            foreach (FragmentPropertySet set in PropertySets)
            {
                if (set.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return set;
                }
            }
            return null;
        }

        public FragmentAttribute FindProperty(string setName, string propertyName)
        {
            foreach (FragmentPropertySet set in PropertySets)
            {
                if (!string.IsNullOrEmpty(setName) && !set.Name.Equals(setName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                foreach (FragmentAttribute property in set.Properties)
                {
                    if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
                    {
                        return property;
                    }
                }
            }
            return null;
        }

        public int CountValues()
        {
            int count = Attributes.Count + Materials.Count + Classifications.Count;
            foreach (FragmentPropertySet set in PropertySets)
            {
                count += set.Properties.Count;
            }
            return count;
        }
    }
}
