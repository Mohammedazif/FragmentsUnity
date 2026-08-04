using System;

namespace FragmentsUnity
{
    /// <summary>Options controlling the parse stage; scene-building choices live in the Unity layer.</summary>
    public sealed class FragmentImportOptions
    {
        public float ScaleFactor { get; set; } = 1.0f;

        public bool ImportMetadata { get; set; } = true;

        public bool ImportPropertySets { get; set; } = true;

        public Action<FragmentImportSeverity, string> Log { get; set; }
    }
}
