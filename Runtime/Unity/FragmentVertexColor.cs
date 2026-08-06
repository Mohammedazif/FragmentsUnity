using UnityEngine;

namespace FragmentsUnity
{
    /// <summary>Bakes an instance's IFC colour into the vertex colour every fragment mesh carries.</summary>
    internal static class FragmentVertexColor
    {
        internal static Color FromInstance(FragmentInstance instance, float resolvedOpacity)
        {
            return new Color(
                Mathf.Pow(instance.Color.X, FragmentImportLimits.VertexColorGammaExponent),
                Mathf.Pow(instance.Color.Y, FragmentImportLimits.VertexColorGammaExponent),
                Mathf.Pow(instance.Color.Z, FragmentImportLimits.VertexColorGammaExponent),
                resolvedOpacity);
        }
    }
}
