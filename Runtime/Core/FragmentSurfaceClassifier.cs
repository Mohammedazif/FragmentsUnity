using System.Numerics;

namespace FragmentsUnity
{
    /// <summary>Chooses an instance's surface kind from its colour and opacity.</summary>
    public static class FragmentSurfaceClassifier
    {
        public static FragmentSurfaceKind Classify(Vector4 color, float opacity)
        {
            bool blueish = IsBlueish(color);
            if (!blueish && opacity >= FragmentImportLimits.OpaqueOpacityThreshold)
            {
                return FragmentSurfaceKind.Opaque;
            }

            return blueish || opacity < FragmentImportLimits.GlassOpacityThreshold
                ? FragmentSurfaceKind.Glass
                : FragmentSurfaceKind.Translucent;
        }

        /// <summary>Glass with a fully opaque alpha is forced down to a visible transparency.</summary>
        public static float ResolveOpacity(FragmentSurfaceKind kind, float opacity)
        {
            if (kind == FragmentSurfaceKind.Glass && opacity > FragmentImportLimits.OpaqueOpacityThreshold)
            {
                return FragmentImportLimits.ForcedGlassOpacity;
            }

            return opacity;
        }

        private static bool IsBlueish(Vector4 color)
        {
            return color.Z > color.X + FragmentImportLimits.GlassBlueDominanceOverRed
                && color.Z > color.Y + FragmentImportLimits.GlassBlueDominanceOverGreen;
        }
    }
}
