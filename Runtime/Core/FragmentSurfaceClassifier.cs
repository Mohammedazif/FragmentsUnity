using System.Numerics;

namespace FragmentsUnity
{
    /// <summary>Chooses the opaque, translucent or glass surface for an instance from its IFC colour and opacity.</summary>
    public static class FragmentSurfaceClassifier
    {
        // mirrors FragmentsActor.cpp:603-617
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

        /// <summary>Glass classified purely by hue keeps a fully opaque alpha, so it is forced down to a visible transparency.</summary>
        // mirrors FragmentsActor.cpp:611-614
        public static float ResolveOpacity(FragmentSurfaceKind kind, float opacity)
        {
            if (kind == FragmentSurfaceKind.Glass && opacity > FragmentImportLimits.OpaqueOpacityThreshold)
            {
                return FragmentImportLimits.ForcedGlassOpacity;
            }

            return opacity;
        }

        // mirrors FragmentsActor.cpp:603
        private static bool IsBlueish(Vector4 color)
        {
            return color.Z > color.X + FragmentImportLimits.GlassBlueDominanceOverRed
                && color.Z > color.Y + FragmentImportLimits.GlassBlueDominanceOverGreen;
        }
    }
}
