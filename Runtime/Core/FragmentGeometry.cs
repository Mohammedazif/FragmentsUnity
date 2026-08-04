using System.Collections.Generic;
using System.Numerics;

namespace FragmentsUnity
{
    /// <summary>One triangulated shell in Unity space: left-handed, Y-up, meters at scale 1.</summary>
    public sealed class FragmentGeometry
    {
        public int GeometryIndex { get; set; } = -1;

        public List<Vector3> Positions { get; } = new List<Vector3>();

        /// <summary>Crease-merged face normals accumulated during triangulation, then normalized.</summary>
        public List<Vector3> Normals { get; } = new List<Vector3>();

        /// <summary>Always int, widened from the file's ushort when the shell is not BIG.</summary>
        public List<int> Indices { get; } = new List<int>();

        public Vector3 BoundsMin { get; set; }

        public Vector3 BoundsMax { get; set; }

        public bool HasBounds { get; set; }
    }
}
