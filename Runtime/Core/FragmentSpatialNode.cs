using System.Collections.Generic;

namespace FragmentsUnity
{
    /// <summary>One node of the Project → Site → Building → Storey → Element tree.</summary>
    public sealed class FragmentSpatialNode
    {
        public int LocalId { get; set; } = -1;

        public uint ExpressId { get; set; }

        public string Category { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public List<FragmentSpatialNode> Children { get; } = new List<FragmentSpatialNode>();
    }
}
