using UnityEngine;

namespace FragmentsUnity
{
    /// <summary>What a scene build should produce: the import mode, the picking colliders and their layer, and the shader every material is built from.</summary>
    public sealed class FragmentSceneBuildOptions
    {
        public FragmentImportMode Mode { get; set; } = FragmentImportMode.HierarchyPerElement;

        public bool EnablePicking { get; set; } = true;

        /// <summary>Layer given to every object that receives a picking collider; 0 is Unity's Default layer.</summary>
        public int ColliderLayer { get; set; }

        public Shader VertexColorShader { get; set; }

        /// <summary>Optional cancellable progress channel; a null one reports nothing and never cancels.</summary>
        public FragmentImportProgress Progress { get; set; }
    }
}
