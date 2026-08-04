using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FragmentsUnity
{
    /// <summary>Builds the synthetic IfcProject model-info item from the header JSON and the project/site/building items.</summary>
    internal static class FragmentModelInfoBuilder
    {
        // names = FILE_NAME(name, timestamp, author, org, preprocessor, system, authorization); mirrors FragParser.cpp:1152.
        private static readonly string[] NameLabels =
        {
            "File Name", "Exported", "Author", "Organization",
            "Preprocessor", "Authoring Tool", "Authorization"
        };

        internal static void BuildModelInfo(FragmentImportResult source, FragmentItemMetadata outInfo)
        {
            outInfo.LocalId = -1;
            outInfo.Category = "IfcProject";
            outInfo.Name = source.ModelName;
            outInfo.GlobalId = source.ModelGuid;

            void AddValue(string name, string value)
            {
                if (!string.IsNullOrEmpty(value))
                {
                    outInfo.Attributes.Add(new FragmentAttribute(name, value, string.Empty));
                }
            }

            if (!string.IsNullOrEmpty(source.Metadata))
            {
                AppendHeaderAttributes(source.Metadata, AddValue);
            }

            var units = new FragmentPropertySet { Name = "Units" };
            var address = new FragmentPropertySet { Name = "Address" };

            foreach (FragmentItemMetadata item in source.Items)
            {
                if (string.IsNullOrEmpty(item.Name) && item.Relations.Count == 0)
                {
                    continue;
                }

                if (item.Category.Equals("IFCPROJECT", StringComparison.OrdinalIgnoreCase))
                {
                    AddValue("Project", item.Name);
                }
                else if (item.Category.Equals("IFCSITE", StringComparison.OrdinalIgnoreCase))
                {
                    AddValue("Site", item.Name);
                }
                else if (item.Category.Equals("IFCBUILDING", StringComparison.OrdinalIgnoreCase))
                {
                    AddValue("Building", item.Name);
                }

                if (address.Properties.Count == 0)
                {
                    AppendAddressProperties(source, item, address);
                }

                if (units.Properties.Count > 0)
                {
                    continue;
                }

                AppendUnitProperties(source, item, units);
            }

            if (address.Properties.Count > 0)
            {
                outInfo.PropertySets.Add(address);
            }
            if (units.Properties.Count > 0)
            {
                outInfo.PropertySets.Add(units);
            }
        }

        private static void AppendHeaderAttributes(string metadata, Action<string, string> addValue)
        {
            JObject root;
            try
            {
                root = ParseHeader(metadata);
            }
            catch (JsonException)
            {
                addValue("Header", metadata);
                return;
            }

            if (TryGetStringField(root, "schema", out string schema))
            {
                addValue("IFC Schema", schema);
            }

            if (GetField(root, "names") is JArray names)
            {
                for (int i = 0; i < names.Count; i++)
                {
                    string label = i < NameLabels.Length ? NameLabels[i] : "Header";
                    addValue(label, TokenToString(names[i]));
                }
            }

            if (GetField(root, "descriptions") is JArray descriptions)
            {
                for (int i = 0; i < descriptions.Count; i++)
                {
                    addValue("Description", TokenToString(descriptions[i]));
                }
            }

            if (TryGetStringField(root, "crs", out string crs))
            {
                addValue("Coordinate Reference", crs);
            }
        }

        private static JObject ParseHeader(string metadata)
        {
            using var stringReader = new StringReader(metadata);
            // DateParseHandling.None keeps FILE_NAME timestamps verbatim, as UE's reader does.
            using var jsonReader = new JsonTextReader(stringReader) { DateParseHandling = DateParseHandling.None };
            return JObject.Load(jsonReader);
        }

        // UE FJsonObject field lookup is case-insensitive (TMap<FString> hashing).
        private static JToken GetField(JObject root, string fieldName)
        {
            return root.GetValue(fieldName, StringComparison.OrdinalIgnoreCase);
        }

        // mirrors FJsonObject::TryGetStringField: numbers and bools coerce; objects, arrays and null do not.
        private static bool TryGetStringField(JObject root, string fieldName, out string value)
        {
            JToken token = GetField(root, fieldName);
            if (token is JValue && token.Type != JTokenType.Null)
            {
                value = token.ToString();
                return true;
            }
            value = string.Empty;
            return false;
        }

        // mirrors FJsonValue::AsString: objects and arrays read as empty strings.
        private static string TokenToString(JToken token)
        {
            return token is JValue ? token.ToString() : string.Empty;
        }

        private static void AppendAddressProperties(
            FragmentImportResult source,
            FragmentItemMetadata item,
            FragmentPropertySet address)
        {
            foreach (FragmentRelation relation in item.Relations)
            {
                if (!relation.Name.Equals("BuildingAddress", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                foreach (int addressId in relation.RelatedLocalIds)
                {
                    FragmentItemMetadata postal = source.FindItem(addressId);
                    if (postal != null)
                    {
                        address.Properties.AddRange(postal.Attributes);
                    }
                }
            }
        }

        private static void AppendUnitProperties(
            FragmentImportResult source,
            FragmentItemMetadata item,
            FragmentPropertySet units)
        {
            foreach (FragmentRelation relation in item.Relations)
            {
                if (!relation.Name.Equals("Units", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (int unitId in relation.RelatedLocalIds)
                {
                    FragmentItemMetadata unit = source.FindItem(unitId);
                    if (unit == null)
                    {
                        continue;
                    }

                    FragmentAttribute unitType = unit.FindAttribute("UnitType");
                    if (unitType == null)
                    {
                        continue;
                    }

                    // Derived units carry no name of their own; mirrors FragParser.cpp:1273-1277.
                    if (string.IsNullOrEmpty(unit.Name))
                    {
                        continue;
                    }

                    string value = unit.Name;
                    FragmentAttribute prefix = unit.FindAttribute("Prefix");
                    if (prefix != null)
                    {
                        value = prefix.Value + value;
                    }

                    units.Properties.Add(new FragmentAttribute(unitType.Value, value, unit.Category));
                }
            }
        }
    }
}
