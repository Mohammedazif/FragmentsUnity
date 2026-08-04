using System;

namespace FragmentsUnity
{
    /// <summary>Collapses a property item's attributes into a single name/value/type attribute.</summary>
    internal static class FragmentPropertyValueReader
    {
        internal static void ReadPropertyValue(FragmentItemMetadata property, FragmentAttribute outProperty)
        {
            string name = property.Name.Length == 0 ? property.Category : property.Name;
            outProperty.Name = Truncate(name, FragmentImportLimits.MaxPropertyNameChars);

            foreach (FragmentAttribute candidate in property.Attributes)
            {
                if (candidate.Name.Equals("Name", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                outProperty.Value = Truncate(candidate.Value, FragmentImportLimits.MaxPropertyValueChars);
                outProperty.Type = Truncate(candidate.Type, FragmentImportLimits.MaxPropertyNameChars);
                break;
            }
        }

        private static string Truncate(string value, int maxChars)
        {
            return value.Length > maxChars ? value.Substring(0, maxChars) : value;
        }
    }
}
