using System.Collections.Generic;

namespace FragmentsUnity
{
    /// <summary>Reads the model's spatial containment tree into FragmentImportResult.SpatialRoot.</summary>
    internal static class FragmentSpatialStructureReader
    {
        internal static void BuildSpatialStructure(
            Schema.Model model,
            IReadOnlyDictionary<uint, int> expressIdToDenseIndex,
            FragmentNameExtractor nameExtractor,
            FragmentImportResult result)
        {
            Schema.SpatialStructure? root = model.SpatialStructure;
            if (!root.HasValue)
            {
                return;
            }

            ReadNode(root.Value, expressIdToDenseIndex, nameExtractor, result.SpatialRoot);
        }

        private static void ReadNode(
            Schema.SpatialStructure node,
            IReadOnlyDictionary<uint, int> expressIdToDenseIndex,
            FragmentNameExtractor nameExtractor,
            FragmentSpatialNode outNode)
        {
            // SpatialStructure.local_id is an express id, not the dense index categories[] use.
            uint? expressId = node.LocalId;
            if (expressId.HasValue)
            {
                outNode.ExpressId = expressId.Value;
                outNode.LocalId = expressIdToDenseIndex.TryGetValue(expressId.Value, out int denseIndex)
                    ? denseIndex
                    : -1;
            }
            else
            {
                outNode.LocalId = -1;
            }

            outNode.Category = ReadBoundedCategory(node.Category);
            outNode.Name = nameExtractor.ExtractName(outNode.LocalId);

            int childCount = node.ChildrenLength;
            for (int c = 0; c < childCount; c++)
            {
                var childNode = new FragmentSpatialNode();
                ReadNode(node.Children(c).Value, expressIdToDenseIndex, nameExtractor, childNode);
                outNode.Children.Add(childNode);
            }
        }

        // The cap counts decoded characters, not UTF-8 bytes.
        private static string ReadBoundedCategory(string value)
        {
            if (value == null || value.Length > FragmentImportLimits.MaxCategoryBytes)
            {
                return string.Empty;
            }

            // The string ends at the first NUL byte.
            int nulIndex = value.IndexOf('\0');
            return nulIndex < 0 ? value : value.Substring(0, nulIndex);
        }
    }
}
