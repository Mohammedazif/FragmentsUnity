using System;
using System.Collections.Generic;

namespace FragmentsUnity
{
    /// <summary>Recovers item names from raw attribute tuples when parsed metadata has none.</summary>
    internal sealed class FragmentNameExtractor
    {
        private const string NameTuplePrefix = "[\"Name\",";

        private readonly Schema.Model _model;
        private readonly FragmentImportResult _result;
        private readonly Dictionary<int, string> _extractedNameCache = new Dictionary<int, string>();
        private long _remainingScanBudget = FragmentImportLimits.NameScanByteBudget;

        internal FragmentNameExtractor(Schema.Model model, FragmentImportResult result)
        {
            _model = model;
            _result = result;
        }

        internal string ExtractName(int localId)
        {
            FragmentItemMetadata item = _result.FindItem(localId);
            if (item != null)
            {
                return item.Name;
            }

            if (_extractedNameCache.TryGetValue(localId, out string cached))
            {
                return cached;
            }

            if (localId >= 0 && localId < _model.AttributesLength && _remainingScanBudget > 0)
            {
                Schema.Attribute? attribute = _model.Attributes(localId);
                if (attribute.HasValue)
                {
                    string extracted = ScanTuplesForName(attribute.Value);
                    if (extracted != null)
                    {
                        _extractedNameCache[localId] = extracted;
                        return extracted;
                    }
                }
            }

            _extractedNameCache[localId] = string.Empty;
            return string.Empty;
        }

        private string ScanTuplesForName(Schema.Attribute attribute)
        {
            int scanCount = Math.Min(attribute.DataLength, (int)FragmentImportLimits.MaxItemAttributes);
            for (int j = 0; j < scanCount; j++)
            {
                string tuple = attribute.Data(j);
                // The cap and budget count decoded characters, not UTF-8 bytes.
                if (tuple == null || tuple.Length > FragmentImportLimits.MaxNameTupleBytes)
                {
                    continue;
                }

                _remainingScanBudget -= tuple.Length;
                if (_remainingScanBudget <= 0)
                {
                    break;
                }

                // The string ends at the first NUL byte.
                int nulIndex = tuple.IndexOf('\0');
                if (nulIndex >= 0)
                {
                    tuple = tuple.Substring(0, nulIndex);
                }

                if (!tuple.StartsWith(NameTuplePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string name = ExtractQuotedValue(tuple);
                if (name != null)
                {
                    return name;
                }
            }

            return null;
        }

        private static string ExtractQuotedValue(string tuple)
        {
            int firstQuote = tuple.IndexOf('"', NameTuplePrefix.Length);
            if (firstQuote < 0)
            {
                return null;
            }

            int searchStart = firstQuote + 1;
            while (searchStart < tuple.Length)
            {
                int secondQuote = tuple.IndexOf('"', searchStart);
                if (secondQuote < 0)
                {
                    break;
                }

                int backslashCount = 0;
                for (int k = secondQuote - 1; k >= 0 && tuple[k] == '\\'; k--)
                {
                    backslashCount++;
                }

                if (backslashCount % 2 == 0)
                {
                    string value = tuple.Substring(firstQuote + 1, secondQuote - firstQuote - 1);
                    return value.Replace("\\\"", "\"").Replace("\\\\", "\\");
                }

                searchStart = secondQuote + 1;
            }

            return null;
        }
    }
}
