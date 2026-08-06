using System.Collections.Generic;

namespace FragmentsUnity
{
    /// <summary>The deserialized metadata payload of a FragmentModelAsset.</summary>
    public sealed class FragmentModelData
    {
        private Dictionary<int, FragmentItemMetadata> _itemsByLocalId;
        private int _indexedItemCount = -1;

        public string ModelName { get; set; } = string.Empty;

        public string ModelGuid { get; set; } = string.Empty;

        /// <summary>The model-header JSON exactly as the .frag carried it.</summary>
        public string Metadata { get; set; } = string.Empty;

        /// <summary>One entry per instanced item, in first-instance order.</summary>
        public List<FragmentItemMetadata> Items { get; } = new List<FragmentItemMetadata>();

        public FragmentItemMetadata ModelInfo { get; set; } = new FragmentItemMetadata();

        public FragmentItemMetadata FindItem(int localId)
        {
            if (_itemsByLocalId == null || _indexedItemCount != Items.Count)
            {
                RebuildIndex();
            }

            if (!_itemsByLocalId.TryGetValue(localId, out FragmentItemMetadata found))
            {
                return null;
            }
            if (found.LocalId == localId)
            {
                return found;
            }

            // An entry edited in place leaves the index keyed by its former id.
            RebuildIndex();
            return _itemsByLocalId.TryGetValue(localId, out found) ? found : null;
        }

        private void RebuildIndex()
        {
            _itemsByLocalId = new Dictionary<int, FragmentItemMetadata>(Items.Count);
            foreach (FragmentItemMetadata item in Items)
            {
                // A hand-edited payload can carry null entries.
                if (item != null)
                {
                    _itemsByLocalId[item.LocalId] = item;
                }
            }
            _indexedItemCount = Items.Count;
        }
    }
}
