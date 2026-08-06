using System.Collections.Generic;
using UnityEngine;

namespace FragmentsUnity
{
    /// <summary>Output of a scene build.</summary>
    public sealed class FragmentSceneBuildResult
    {
        public GameObject Root;
        public List<Mesh> Meshes = new List<Mesh>();
        public List<Material> Materials = new List<Material>();
        public int SpawnedNodes;
        public int MergedChunks;

        /// <summary>True when the build was cancelled and discarded; Root is then null and the lists are empty.</summary>
        public bool Cancelled;
    }
}
