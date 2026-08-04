using System.Collections.Generic;
using UnityEngine;

namespace FragmentsUnity
{
    /// <summary>Output of a scene build: the root GameObject and every mesh and material created for it.</summary>
    public sealed class FragmentSceneBuildResult
    {
        public GameObject Root;
        public List<Mesh> Meshes = new List<Mesh>();
        public List<Material> Materials = new List<Material>();
        public int SpawnedNodes;
        public int MergedChunks;

        /// <summary>True when the build was cancelled and discarded; Root, Meshes and Materials are then empty.</summary>
        public bool Cancelled;
    }
}
