using UnityEngine;

namespace FragmentsUnity
{
    /// <summary>Controls how a fragment file is turned into Unity scene objects.</summary>
    public sealed class FragmentSceneBuildOptions
    {
        public FragmentImportMode Mode { get; set; } = FragmentImportMode.HierarchyPerElement;

        public bool EnablePicking { get; set; } = true;

        public int ColliderLayer { get; set; }

        public Shader VertexColorShader { get; set; }

        /// <summary>Optional; when null, nothing is reported and the build is never cancelled.</summary>
        public FragmentImportProgress Progress { get; set; }
    }
}
