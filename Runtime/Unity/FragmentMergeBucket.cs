using System;
using System.Collections.Generic;

namespace FragmentsUnity
{
    internal sealed class FragmentMergeBucket
    {
        internal const string UnclassifiedCategory = "Unclassified";

        internal string Category = UnclassifiedCategory;
        internal FragmentSurfaceKind SurfaceKind;
        internal float Opacity = 1f;
        internal readonly List<FragmentInstance> Instances = new List<FragmentInstance>();

        internal static string ResolveCategory(string category)
        {
            // mirrors FragmentsActor.cpp:627
            return string.IsNullOrEmpty(category) ? UnclassifiedCategory : category;
        }

        // mirrors FragmentsActor.cpp:628-634; the surface kind stands in for its bIsGlass term
        internal static string BuildKey(
            string category, System.Numerics.Vector4 color, float resolvedOpacity, FragmentSurfaceKind surfaceKind)
        {
            return string.Format(
                "{0}|{1:X2}{2:X2}{3:X2}|{4}|{5}",
                category,
                QuantizeChannel(color.X),
                QuantizeChannel(color.Y),
                QuantizeChannel(color.Z),
                QuantizeOpacity(resolvedOpacity),
                (int)surfaceKind);
        }

        private static int QuantizeChannel(float channel)
        {
            float levels = FragmentImportLimits.MergeBucketColorLevels;
            return (int)Math.Clamp(channel * levels, 0f, levels);
        }

        private static int QuantizeOpacity(float opacity)
        {
            return (int)Math.Round(
                opacity * (double)FragmentImportLimits.MergeBucketOpacityLevels,
                MidpointRounding.AwayFromZero);
        }
    }
}
