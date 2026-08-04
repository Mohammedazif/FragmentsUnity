using UnityEngine;

namespace FragmentsUnity
{
    internal sealed class FragmentMergedChunk
    {
        public Mesh Mesh;
        public bool DoubleSided;
        public FragmentSurfaceKind SurfaceKind;
        public string Category;
        public int[] TriangleStarts;
        public int[] PartLocalIds;
    }
}
