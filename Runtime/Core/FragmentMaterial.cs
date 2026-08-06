namespace FragmentsUnity
{
    /// <summary>An IFC material associated with an item, optionally as a layer with thickness.</summary>
    public sealed class FragmentMaterial
    {
        public string Name { get; set; } = string.Empty;

        public string LayerSetName { get; set; } = string.Empty;

        /// <summary>In the model's IFC length unit.</summary>
        public float Thickness { get; set; }

        public int LocalId { get; set; } = -1;
    }
}
