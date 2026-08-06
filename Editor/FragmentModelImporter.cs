using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace FragmentsUnity.Editor
{
    /// <summary>Imports .frag files as a GameObject hierarchy with mesh and material sub-assets.</summary>
    [ScriptedImporter(5, "frag")]
    public sealed class FragmentModelImporter : ScriptedImporter
    {
        private const string ConsolePrefix = "[FragmentsUnity] ";
        private const string ProgressBarTitle = "Importing Fragments";
        private const int UnityDefaultLayer = 0;

        [SerializeField] private float _scaleFactor = 1.0f;
        [SerializeField] private bool _importMetadata = true;
        [SerializeField] private bool _importPropertySets = true;
        [SerializeField] private FragmentImportMode _importMode = FragmentImportMode.HierarchyPerBody;
        [SerializeField] private bool _enableElementPicking = true;
        [SerializeField] private int _colliderLayer = UnityDefaultLayer;

        public override void OnImportAsset(AssetImportContext ctx)
        {
            try
            {
                Import(ctx);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void Import(AssetImportContext ctx)
        {
            var options = new FragmentImportOptions
            {
                ScaleFactor = _scaleFactor,
                ImportMetadata = _importMetadata,
                ImportPropertySets = _importPropertySets,
                Log = LogToConsole
            };

            FragmentImportResult result = FragmentParser.LoadFromFile(ctx.assetPath, options);
            if (!result.Success)
            {
                ctx.LogImportError(result.ErrorMessage);
                return;
            }

            // Shader.Find is unreliable during a clean import.
            string shaderAssetPath = FragmentMaterialFactory.ActiveShaderAssetPath;
            ctx.DependsOnSourceAsset(shaderAssetPath);
            var vertexColorShader = AssetDatabase.LoadAssetAtPath<Shader>(shaderAssetPath);

            string modelLabel = string.IsNullOrEmpty(result.ModelName) ? ctx.assetPath : result.ModelName;
            var buildOptions = new FragmentSceneBuildOptions
            {
                Mode = _importMode,
                EnablePicking = _enableElementPicking,
                ColliderLayer = _colliderLayer,
                VertexColorShader = vertexColorShader,
                Progress = new FragmentImportProgress(
                    (stage, normalizedProgress) =>
                        !EditorUtility.DisplayCancelableProgressBar(ProgressBarTitle, $"{modelLabel} — {stage}", normalizedProgress))
            };

            FragmentSceneBuildResult scene = FragmentSceneBuilder.Build(result, buildOptions, LogToConsole);
            if (scene.Cancelled)
            {
                ctx.LogImportError(FragmentSceneBuilder.CancelledMessage);
                return;
            }

            FragmentModelAsset modelAsset = FragmentModelAsset.Create(result);
            scene.Root.AddComponent<FragmentModel>().SetAsset(modelAsset);
            scene.Root.AddComponent<FragmentFilter>();

            ctx.AddObjectToAsset("root", scene.Root);
            ctx.SetMainObject(scene.Root);
            ctx.AddObjectToAsset("model-data", modelAsset);

            for (int meshIndex = 0; meshIndex < scene.Meshes.Count; meshIndex++)
            {
                ctx.AddObjectToAsset("mesh_" + meshIndex, scene.Meshes[meshIndex]);
            }

            for (int materialIndex = 0; materialIndex < scene.Materials.Count; materialIndex++)
            {
                ctx.AddObjectToAsset("material_" + materialIndex, scene.Materials[materialIndex]);
            }

            Debug.Log(
                $"{ConsolePrefix}Imported '{result.ModelName}': {result.Geometries.Count} geometries, {result.Instances.Count} instances, {result.TotalVertices} vertices, {result.TotalTriangles} triangles");
        }

        private static void LogToConsole(FragmentImportSeverity severity, string message)
        {
            switch (severity)
            {
                case FragmentImportSeverity.Error:
                    Debug.LogError(ConsolePrefix + message);
                    break;
                case FragmentImportSeverity.Warning:
                    Debug.LogWarning(ConsolePrefix + message);
                    break;
                default:
                    Debug.Log(ConsolePrefix + message);
                    break;
            }
        }
    }
}
