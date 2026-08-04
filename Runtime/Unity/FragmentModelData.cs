using System.Collections.Generic;

namespace FragmentsUnity
{
    /// <summary>The deserialized metadata payload of a FragmentModelAsset: model identity plus every geometry-bearing item's metadata.</summary>
    public sealed class FragmentModelData
    {
        private Dictionary<int, FragmentItemMetadata> _itemsByLocalId;
        private int _indexedItemCount = -1;

        public string ModelName { get; set; } = string.Empty;

        public string ModelGuid { get; set; } = string.Empty;

        /// <summary>The model-header JSON exactly as the .frag carried it.</summary>
        public string Metadata { get; set; } = string.Empty;

        /// <summary>One entry per item that has geometry, in first-instance order; mirrors FragmentsActor.cpp:781.</summary>
        public List<FragmentItemMetadata> Items { get; } = new List<FragmentItemMetadata>();

        public FragmentItemMetadata ModelInfo { get; set; } = new FragmentItemMetadata();

        public FragmentItemMetadata FindItem(int localId)
        {
            if (_itemsByLocalId == null || _indexedItemCount != Items.Count)
            {
                _itemsByLocalId = new Dictionary<int, FragmentItemMetadata>(Items.Count);
                foreach (FragmentItemMetadata item in Items)
                {
                    // A hand-edited payload can carry null entries; they must not break every lookup.
                    if (item != null)
                    {
                        _itemsByLocalId[item.LocalId] = item;
                    }
                }
                _indexedItemCount = Items.Count;
            }

            return _itemsByLocalId.TryGetValue(localId, out FragmentItemMetadata found) ? found : null;
        }
    }
}
