using System.Collections.Generic;

namespace FragmentsUnity
{
    /// <summary>A named IFC relation from one item to others, by dense local id.</summary>
    public sealed class FragmentRelation
    {
        public string Name { get; set; } = string.Empty;

        public List<int> RelatedLocalIds { get; } = new List<int>();
    }
}
