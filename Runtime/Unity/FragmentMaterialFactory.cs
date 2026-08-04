using UnityEngine;
using UnityEngine.Rendering;

namespace FragmentsUnity
{
    /// <summary>Creates the vertex-color materials fragment meshes render with, one per surface kind and pipeline.</summary>
    public static class FragmentMaterialFactory
    {
        public const string VertexColorShaderName = "FragmentsUnity/VertexColor";

        public const string VertexColorUrpShaderName = "FragmentsUnity/URP/VertexColor";

        public const string PackageShaderAssetPath =
            "Packages/com.fragmentsunity.importer/Runtime/Shaders/FragmentVertexColor.shader";

        public const string PackageUrpShaderAssetPath =
            "Packages/com.fragmentsunity.importer/Runtime/Shaders/FragmentVertexColorURP.shader";

        /// <summary>The shader asset an import should load and depend on, chosen for the active render pipeline.</summary>
        public static string ActiveShaderAssetPath =>
            GraphicsSettings.currentRenderPipeline != null ? PackageUrpShaderAssetPath : PackageShaderAssetPath;

        private const string FallbackShaderName = "Standard";

        private const string CullPropertyName = "_Cull";
        private const string ModePropertyName = "_Mode";
        private const string SrcBlendPropertyName = "_SrcBlend";
        private const string DstBlendPropertyName = "_DstBlend";
        private const string ZWritePropertyName = "_ZWrite";
        private const string SurfacePropertyName = "_Surface";
        private const string SmoothnessPropertyName = "_Smoothness";

        private const float StandardTransparentMode = 3.0f;
        private const float UrpTransparentSurface = 1.0f;
        private const float ZWriteDisabled = 0.0f;

        private const string MaterialNamePrefix = "FragmentVertexColor";
        private const string DoubleSidedNameSuffix = "DoubleSided";

        public static Material CreateDefaultMaterial(bool doubleSided, Shader vertexColorShader = null)
        {
            return CreateMaterial(FragmentSurfaceKind.Opaque, doubleSided, vertexColorShader);
        }

        /// <summary>Builds a material configured for the surface kind, preferring the URP shader when a render pipeline asset is assigned.</summary>
        public static Material CreateMaterial(FragmentSurfaceKind kind, bool doubleSided, Shader vertexColorShader = null)
        {
            var material = new Material(ResolveShader(vertexColorShader))
            {
                name = BuildMaterialName(kind, doubleSided)
            };

            if (doubleSided)
            {
                TrySetFloat(material, CullPropertyName, (float)CullMode.Off);
            }

            if (kind != FragmentSurfaceKind.Opaque)
            {
                ConfigureTransparency(material);
            }

            TrySetFloat(
                material,
                SmoothnessPropertyName,
                kind == FragmentSurfaceKind.Glass
                    ? FragmentImportLimits.GlassSmoothness
                    : FragmentImportLimits.NonGlassSmoothness);

            return material;
        }

        private static Shader ResolveShader(Shader vertexColorShader)
        {
            if (vertexColorShader != null)
            {
                return vertexColorShader;
            }

            Shader shader = GraphicsSettings.currentRenderPipeline != null
                ? Shader.Find(VertexColorUrpShaderName)
                : null;

            if (shader == null)
            {
                shader = Shader.Find(VertexColorShaderName);
            }

            if (shader != null)
            {
                return shader;
            }

            Debug.LogWarning(
                $"[FragmentsUnity] Shader '{VertexColorShaderName}' was not found; falling back to '{FallbackShaderName}'. Imported meshes will not show their vertex colors.");
            return Shader.Find(FallbackShaderName);
        }

        private static void ConfigureTransparency(Material material)
        {
            TrySetFloat(material, ModePropertyName, StandardTransparentMode);
            TrySetFloat(material, SurfacePropertyName, UrpTransparentSurface);
            TrySetFloat(material, SrcBlendPropertyName, (float)BlendMode.SrcAlpha);
            TrySetFloat(material, DstBlendPropertyName, (float)BlendMode.OneMinusSrcAlpha);
            TrySetFloat(material, ZWritePropertyName, ZWriteDisabled);
            material.renderQueue = (int)RenderQueue.Transparent;
        }

        private static void TrySetFloat(Material material, string propertyName, float value)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetFloat(propertyName, value);
            }
        }

        private static string BuildMaterialName(FragmentSurfaceKind kind, bool doubleSided)
        {
            string kindSuffix = kind == FragmentSurfaceKind.Opaque ? string.Empty : kind.ToString();
            return doubleSided
                ? MaterialNamePrefix + kindSuffix + DoubleSidedNameSuffix
                : MaterialNamePrefix + kindSuffix;
        }
    }
}
