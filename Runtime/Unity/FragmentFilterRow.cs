using System.Collections.Generic;

namespace FragmentsUnity
{
    /// <summary>One storey or category the filter window lists.</summary>
    public sealed class FragmentFilterRow
    {
        internal FragmentFilterRow(string name, int count, IReadOnlyList<int> localIds)
        {
            Name = name;
            Count = count;
            LocalIds = localIds;
        }

        public string Name { get; }

        /// <summary>Excludes the storey item itself on a storey row, unlike LocalIds.</summary>
        public int Count { get; }

        public IReadOnlyList<int> LocalIds { get; }

        /// <summary>False as soon as any one of this row's elements is hidden.</summary>
        public bool IsVisible { get; internal set; } = true;
    }
}
