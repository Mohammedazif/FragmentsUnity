using System;
using System.Collections.Generic;
using UnityEngine;

namespace FragmentsUnity
{
    /// <summary>Per-build state shared by every import mode.</summary>
    internal sealed class FragmentSceneSpawnContext
    {
        private readonly Dictionary<long, Mesh> _meshCache = new Dictionary<long, Mesh>();
        private readonly Dictionary<(FragmentSurfaceKind Kind, bool DoubleSided), Material> _materialCache =
            new Dictionary<(FragmentSurfaceKind, bool), Material>();
        private FragmentVisibilityIndex _visibilityIndex;
        private bool _reportedSpawnLimit;

        internal FragmentSceneSpawnContext(
            FragmentImportResult result,
            FragmentSceneBuildResult buildResult,
            FragmentSceneBuildOptions options,
            Action<FragmentImportSeverity, string> log)
        {
            Result = result;
            BuildResult = buildResult;
            Options = options;
            Log = log;
            Progress = options.Progress ?? new FragmentImportProgress(null);
            ElementGranularBuild = IsElementGranular(options.Mode);
        }

        internal FragmentImportResult Result { get; }

        internal FragmentSceneBuildResult BuildResult { get; }

        internal FragmentSceneBuildOptions Options { get; }

        internal Action<FragmentImportSeverity, string> Log { get; }

        internal FragmentImportProgress Progress { get; }

        internal int SpawnedObjects { get; private set; }

        internal bool ElementGranularBuild { get; set; }

        internal bool IsSpawnLimitReached()
        {
            if (SpawnedObjects < FragmentImportLimits.MaxSpawnedObjects)
            {
                return false;
            }

            if (!_reportedSpawnLimit)
            {
                _reportedSpawnLimit = true;
                Log?.Invoke(FragmentImportSeverity.Error, FragmentSceneBuilder.SpawnLimitMessage);
            }
            return true;
        }

        internal GameObject CreateChild(string label, Transform parent)
        {
            var child = new GameObject(label);
            child.transform.SetParent(parent, false);
            SpawnedObjects++;
            return child;
        }

        internal Mesh GetOrCreateMesh(FragmentInstance instance)
        {
            long pairKey = ((long)instance.GeometryIndex << 32) | (uint)instance.MaterialIndex;
            if (_meshCache.TryGetValue(pairKey, out Mesh cachedMesh))
            {
                return cachedMesh;
            }

            if (instance.GeometryIndex < 0 || instance.GeometryIndex >= Result.Geometries.Count)
            {
                return null;
            }

            FragmentSurfaceKind kind = FragmentSurfaceClassifier.Classify(instance.Color, instance.Opacity);
            FragmentGeometry geometry = Result.Geometries[instance.GeometryIndex];
            string meshName = $"Geometry_{geometry.GeometryIndex}_Material_{instance.MaterialIndex}";
            Mesh mesh = FragmentMeshFactory.CreateMesh(
                geometry,
                FragmentVertexColor.FromInstance(instance, FragmentSurfaceClassifier.ResolveOpacity(kind, instance.Opacity)),
                meshName);
            _meshCache[pairKey] = mesh;
            if (mesh != null)
            {
                BuildResult.Meshes.Add(mesh);
            }
            return mesh;
        }

        internal Material GetOrCreateMaterial(FragmentSurfaceKind kind, bool doubleSided)
        {
            var cacheKey = (kind, doubleSided);
            if (!_materialCache.TryGetValue(cacheKey, out Material material))
            {
                material = FragmentMaterialFactory.CreateMaterial(kind, doubleSided, Options.VertexColorShader);
                material.enableInstancing = Options.Mode == FragmentImportMode.Instanced;
                _materialCache[cacheKey] = material;
                BuildResult.Materials.Add(material);
            }
            return material;
        }

        internal void AttachMesh(GameObject target, Mesh mesh, FragmentInstance instance)
        {
            AttachMesh(
                target,
                mesh,
                FragmentSurfaceClassifier.Classify(instance.Color, instance.Opacity),
                instance.DoubleSided,
                instance.Category);
        }

        internal void AttachMesh(
            GameObject target, Mesh mesh, FragmentSurfaceKind kind, bool doubleSided, string category)
        {
            target.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = target.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = GetOrCreateMaterial(kind, doubleSided);

            if (FragmentIfcCategories.IsInvisibleVolume(category))
            {
                renderer.enabled = false;
            }

            if (Options.EnablePicking)
            {
                target.AddComponent<MeshCollider>().sharedMesh = mesh;
                target.layer = Options.ColliderLayer;
            }
        }

        internal void RegisterElement(int localId, GameObject target)
        {
            if (localId < 0)
            {
                return;
            }

            if (_visibilityIndex == null)
            {
                _visibilityIndex = BuildResult.Root.GetComponent<FragmentVisibilityIndex>();
                if (_visibilityIndex == null)
                {
                    _visibilityIndex = BuildResult.Root.AddComponent<FragmentVisibilityIndex>();
                }
                _visibilityIndex.SetElementGranular(ElementGranularBuild);
            }

            _visibilityIndex.Register(localId, target);
        }

        private static bool IsElementGranular(FragmentImportMode mode)
        {
            return mode == FragmentImportMode.HierarchyPerBody
                || mode == FragmentImportMode.HierarchyPerElement
                || mode == FragmentImportMode.Instanced;
        }
    }
}
