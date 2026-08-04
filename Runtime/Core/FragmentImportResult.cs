using System.Collections.Generic;

namespace FragmentsUnity
{
    /// <summary>Engine-neutral output of a .frag parse; the seam between parsing and scene building.</summary>
    public sealed class FragmentImportResult
    {
        public string ModelGuid { get; set; } = string.Empty;

        public string ModelName { get; set; } = string.Empty;

        /// <summary>The model-header JSON string, verbatim.</summary>
        public string Metadata { get; set; } = string.Empty;

        public List<FragmentGeometry> Geometries { get; } = new List<FragmentGeometry>();

        public List<FragmentInstance> Instances { get; } = new List<FragmentInstance>();

        public FragmentSpatialNode SpatialRoot { get; set; } = new FragmentSpatialNode();

        public List<string> Categories { get; } = new List<string>();

        /// <summary>Indexed by local id, parallel to the model's local_ids; empty when metadata import is off.</summary>
        public List<FragmentItemMetadata> Items { get; } = new List<FragmentItemMetadata>();

        public FragmentItemMetadata ModelInfo { get; set; } = new FragmentItemMetadata();

        public int TotalVertices { get; set; }

        public int TotalTriangles { get; set; }

        public int TotalInstances { get; set; }

        public int TotalElements { get; set; }

        public bool Success { get; set; }

        public string ErrorMessage { get; set; } = string.Empty;

        public FragmentItemMetadata FindItem(int localId)
        {
            return localId >= 0 && localId < Items.Count ? Items[localId] : null;
        }
    }
}
