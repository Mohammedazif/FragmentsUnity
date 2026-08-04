using System.Collections.Generic;

namespace FragmentsUnity
{
    /// <summary>An IFC property or quantity set attached to an item.</summary>
    public sealed class FragmentPropertySet
    {
        public string Name { get; set; } = string.Empty;

        public int LocalId { get; set; } = -1;

        public bool FromType { get; set; }

        public List<FragmentAttribute> Properties { get; } = new List<FragmentAttribute>();
    }
}
