using System.Collections.Generic;

namespace FragmentsUnity
{
    /// <summary>One storey or category the filter window lists, with the elements it shows and hides.</summary>
    public sealed class FragmentFilterRow
    {
        internal FragmentFilterRow(string name, int count, IReadOnlyList<int> localIds)
        {
            Name = name;
            Count = count;
            LocalIds = localIds;
        }

        public string Name { get; }

        /// <summary>How many elements the model counts under this name; a storey excludes the storey item itself.</summary>
        public int Count { get; }

        /// <summary>Every element this row acts on; a storey row also carries the storey item.</summary>
        public IReadOnlyList<int> LocalIds { get; }

        /// <summary>False as soon as one of this row's elements is hidden; mirrors SFragmentsFilterPanel.cpp:220.</summary>
        public bool IsVisible { get; internal set; } = true;
    }
}
