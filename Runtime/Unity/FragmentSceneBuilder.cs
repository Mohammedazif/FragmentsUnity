using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace FragmentsUnity
{
    /// <summary>Builds a GameObject hierarchy from a parsed fragment model.</summary>
    public static class FragmentSceneBuilder
    {
        public const string CancelledMessage =
            "Import cancelled — nothing was written and the model is empty.";

        internal const string MaterialsStage = "Preparing materials";
        internal const string GeometryStage = "Building geometry";
        internal const string HierarchyStage = "Building hierarchy";
        internal const string ChunkStage = "Merging chunks";

        internal const float BuildStageStart = 0.05f;
        internal const float BuildStageEnd = 1f;

        private const string DefaultRootName = "FragmentsModel";
        private const string FallbackElementLabel = "Element";
        private const string WholeModelChunkPrefix = "Model";
        private const float MaterialsStageProgress = 0f;

        internal static readonly string SpawnLimitMessage =
            $"Stopped at {FragmentImportLimits.MaxSpawnedObjects} objects — the model has more instances than the importer will spawn in one pass.";

        /// <summary>Null options build with the defaults.</summary>
        public static FragmentSceneBuildResult Build(
            FragmentImportResult result,
            FragmentSceneBuildOptions options,
            Action<FragmentImportSeverity, string> log)
        {
            FragmentSceneBuildOptions buildOptions = options ?? new FragmentSceneBuildOptions();
            var buildResult = new FragmentSceneBuildResult
            {
                Root = new GameObject(string.IsNullOrEmpty(result.ModelName) ? DefaultRootName : result.ModelName)
            };

            var context = new FragmentSceneSpawnContext(result, buildResult, buildOptions, log);
            context.Progress.Report(MaterialsStage, MaterialsStageProgress);
            int spawnedObjects = BuildForMode(context);

            if (context.Progress.CancellationRequested)
            {
                DiscardCancelledBuild(buildResult, log);
                return buildResult;
            }

            log?.Invoke(FragmentImportSeverity.Info,
                $"Scene built ({buildOptions.Mode}): {result.Geometries.Count} geometries, {result.Instances.Count} instances, {buildResult.SpawnedNodes} hierarchy nodes, {spawnedObjects} objects spawned, {buildResult.MergedChunks} merged chunks, {result.TotalVertices} vertices, {result.TotalTriangles} triangles");

            return buildResult;
        }

        private static void DiscardCancelledBuild(
            FragmentSceneBuildResult buildResult, Action<FragmentImportSeverity, string> log)
        {
            buildResult.Cancelled = true;

            foreach (Mesh mesh in buildResult.Meshes)
            {
                UnityEngine.Object.DestroyImmediate(mesh);
            }
            foreach (Material material in buildResult.Materials)
            {
                UnityEngine.Object.DestroyImmediate(material);
            }
            UnityEngine.Object.DestroyImmediate(buildResult.Root);

            buildResult.Root = null;
            buildResult.Meshes.Clear();
            buildResult.Materials.Clear();
            buildResult.SpawnedNodes = 0;
            buildResult.MergedChunks = 0;

            log?.Invoke(FragmentImportSeverity.Warning, CancelledMessage);
        }

        private static int BuildForMode(FragmentSceneSpawnContext context)
        {
            switch (context.Options.Mode)
            {
                case FragmentImportMode.MergedWholeModel:
                    context.Progress.BeginStage(
                        ChunkStage, BuildStageStart, BuildStageEnd, context.Result.Instances.Count);
                    return FragmentMergedChunkSpawner.Spawn(
                        context, context.Result.Instances, context.BuildResult.Root, WholeModelChunkPrefix);

                case FragmentImportMode.Instanced:
                    return BuildFlat(context);

                default:
                    return context.Result.SpatialRoot != null && context.Result.SpatialRoot.Children.Count > 0
                        ? new FragmentHierarchyBuilder(context).Build()
                        : BuildFlat(context);
            }
        }

        private static int BuildFlat(FragmentSceneSpawnContext context)
        {
            context.ElementGranularBuild = true;

            FragmentImportResult result = context.Result;
            Dictionary<int, int> instancesPerLocalId = CountInstancesPerLocalId(result.Instances);
            var bodyOrdinals = new Dictionary<int, int>();
            int spawnedObjects = 0;

            context.Progress.BeginStage(GeometryStage, BuildStageStart, BuildStageEnd, result.Instances.Count);

            foreach (FragmentInstance instance in result.Instances)
            {
                if (!context.Progress.ReportSteps(1))
                {
                    break;
                }

                if (instance.GeometryIndex < 0 || instance.GeometryIndex >= result.Geometries.Count)
                {
                    continue;
                }

                if (context.IsSpawnLimitReached())
                {
                    break;
                }

                Mesh mesh = context.GetOrCreateMesh(instance);
                if (mesh == null)
                {
                    continue;
                }

                SpawnInstanceObject(context, instance, mesh, instancesPerLocalId, bodyOrdinals);
                spawnedObjects++;
            }

            return spawnedObjects;
        }

        private static void SpawnInstanceObject(
            FragmentSceneSpawnContext context,
            FragmentInstance instance,
            Mesh mesh,
            Dictionary<int, int> instancesPerLocalId,
            Dictionary<int, int> bodyOrdinals)
        {
            GameObject child = context.CreateChild(
                BuildInstanceLabel(instance, instancesPerLocalId, bodyOrdinals),
                context.BuildResult.Root.transform);
            child.transform.localPosition = FragmentUnityMath.ToUnityPosition(instance.Transform);
            child.transform.localRotation = FragmentUnityMath.ToUnityQuaternion(instance.Transform.Rotation);
            child.AddComponent<FragmentElementReference>().LocalId = instance.LocalId;
            context.AttachMesh(child, mesh, instance);
            context.RegisterElement(instance.LocalId, child);
        }

        private static Dictionary<int, int> CountInstancesPerLocalId(List<FragmentInstance> instances)
        {
            var counts = new Dictionary<int, int>();
            foreach (FragmentInstance instance in instances)
            {
                counts.TryGetValue(instance.LocalId, out int count);
                counts[instance.LocalId] = count + 1;
            }
            return counts;
        }

        private static string BuildInstanceLabel(
            FragmentInstance instance,
            Dictionary<int, int> instancesPerLocalId,
            Dictionary<int, int> bodyOrdinals)
        {
            string label = BuildElementLabel(instance);

            if (instancesPerLocalId.TryGetValue(instance.LocalId, out int count) && count > 1)
            {
                bodyOrdinals.TryGetValue(instance.LocalId, out int ordinal);
                ordinal++;
                bodyOrdinals[instance.LocalId] = ordinal;
                label += "_body" + ordinal;
            }

            return label;
        }

        internal static string BuildElementLabel(FragmentInstance instance)
        {
            string label = instance.Name;
            if (string.IsNullOrEmpty(label))
            {
                label = string.IsNullOrEmpty(instance.Category)
                    ? $"{FallbackElementLabel}_{instance.LocalId}"
                    : $"{instance.Category}_{instance.LocalId}";
            }
            return SanitizeLabel(label);
        }

        internal static string SanitizeLabel(string label)
        {
            var sanitized = new StringBuilder(label.Length);
            foreach (char character in label)
            {
                sanitized.Append(IsAsciiAlphanumeric(character) || character == '-' || character == '_' ? character : '_');
            }
            return sanitized.ToString();
        }

        private static bool IsAsciiAlphanumeric(char character)
        {
            return (character >= '0' && character <= '9')
                || (character >= 'A' && character <= 'Z')
                || (character >= 'a' && character <= 'z');
        }
    }
}
