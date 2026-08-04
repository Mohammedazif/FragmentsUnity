using System.Numerics;

namespace FragmentsUnity
{
    /// <summary>One placement of a geometry with its material color and identity.</summary>
    public sealed class FragmentInstance
    {
        public int LocalId { get; set; } = -1;

        public int GeometryIndex { get; set; } = -1;

        public string GlobalId { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public int MaterialIndex { get; set; } = -1;

        public FragmentTransform Transform { get; set; } = FragmentTransform.Identity;

        /// <summary>Linear RGBA in [0, 1].</summary>
        public Vector4 Color { get; set; } = Vector4.One;

        public float Opacity { get; set; } = 1.0f;

        /// <summary>True when the Fragments RenderedFaces value is TWO.</summary>
        public bool DoubleSided { get; set; }
    }
}
