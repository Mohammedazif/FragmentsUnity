namespace FragmentsUnity
{
    /// <summary>One IFC attribute; values stay strings so any IFC type survives round-tripping.</summary>
    public sealed class FragmentAttribute
    {
        public string Name { get; set; } = string.Empty;

        public string Value { get; set; } = string.Empty;

        public string Type { get; set; } = string.Empty;

        public FragmentAttribute()
        {
        }

        public FragmentAttribute(string name, string value, string type)
        {
            Name = name;
            Value = value;
            Type = type;
        }
    }
}
