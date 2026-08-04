using System;
using System.Collections.Generic;
using UnityEngine;

namespace FragmentsUnity
{
    /// <summary>Builds the GameObject tree for models with a spatial hierarchy; mirrors SpawnHierarchyNode's tree shape.</summary>
    internal sealed class FragmentHierarchyBuilder
    {
        private readonly FragmentSceneSpawnContext _context;
        private readonly Dictionary<int, List<FragmentInstance>> _instancesByLocalId =
            new Dictionary<int, List<FragmentInstance>>();
        private readonly HashSet<int> _consumedLocalIds = new HashSet<int>();
        private int _spawnedElements;

        internal FragmentHierarchyBuilder(FragmentSceneSpawnContext context)
        {
            _context = context;
        }

        internal int Build()
        {
            foreach (FragmentInstance instance in _context.Result.Instances)
            {
                if (!_instancesByLocalId.TryGetValue(instance.LocalId, out List<FragmentInstance> group))
                {
                    group = new List<FragmentInstance>();
                    _instancesByLocalId[instance.LocalId] = group;
                }
                group.Add(instance);
            }

            _context.Progress.BeginStage(
                FragmentSceneBuilder.HierarchyStage,
                FragmentSceneBuilder.BuildStageStart,
                FragmentSceneBuilder.BuildStageEnd,
                _context.Result.Instances.Count);

            SpawnNode(_context.Result.SpatialRoot, _context.BuildResult.Root);
            SpawnOrphanedInstances();
            return _spawnedElements;
        }

        private void SpawnNode(FragmentSpatialNode node, GameObject parent)
        {
            // mirrors the per-node cancel check at FragmentsActor.cpp:1500-1513
            if (_context.Progress.CancellationRequested || _context.IsSpawnLimitReached())
            {
                return;
            }

            // Nodes whose subtree holds no unbuilt geometry are skipped; mirrors FragmentsActor.cpp:1393
            if (!HasUnbuiltGeometry(node))
            {
                return;
            }

            bool hasGeometry = _instancesByLocalId.ContainsKey(node.LocalId);
            bool hasRealName = !string.IsNullOrEmpty(node.Name);

            // Nameless geometry-free Group/Object nodes flatten into their parent; mirrors FragmentsActor.cpp:1401
            bool isAnonymousGroup = !hasRealName && !hasGeometry
                && (string.IsNullOrEmpty(node.Category)
                    || node.Category.Equals("Group", StringComparison.OrdinalIgnoreCase)
                    || node.Category.Equals("Object", StringComparison.OrdinalIgnoreCase));

            if (isAnonymousGroup)
            {
                foreach (FragmentSpatialNode child in node.Children)
                {
                    SpawnNode(child, parent);
                }
                return;
            }

            GameObject nodeObject = _context.CreateChild(BuildNodeLabel(node), parent.transform);
            _context.BuildResult.SpawnedNodes++;

            if (node.LocalId >= 0)
            {
                nodeObject.AddComponent<FragmentElementReference>().LocalId = node.LocalId;
            }

            if (ShouldMergeAtNode(node))
            {
                // Only a merged node owns its subtree's meshes; mirrors RegisterFilterActor at FragmentsActor.cpp:1464
                _context.RegisterElement(node.LocalId, nodeObject);
                // mirrors FragmentsActor.cpp:1466-1487; the subtree becomes meshes, so no child nodes follow
                MergeSubtree(node, nodeObject);
                return;
            }

            if (node.LocalId >= 0
                && !_consumedLocalIds.Contains(node.LocalId)
                && _instancesByLocalId.TryGetValue(node.LocalId, out List<FragmentInstance> instancesForNode)
                && instancesForNode.Count > 0)
            {
                _consumedLocalIds.Add(node.LocalId);
                SpawnElementGroup(instancesForNode, nodeObject);
            }

            foreach (FragmentSpatialNode child in node.Children)
            {
                SpawnNode(child, nodeObject);
            }
        }

        private void MergeSubtree(FragmentSpatialNode node, GameObject nodeObject)
        {
            var subtreeInstances = new List<FragmentInstance>();
            CollectSubtreeInstances(node, subtreeInstances);
            if (subtreeInstances.Count == 0)
            {
                return;
            }

            _spawnedElements += FragmentMergedChunkSpawner.Spawn(
                _context, subtreeInstances, nodeObject, nodeObject.name);
        }

        // mirrors FragmentsActor.cpp:1277-1297
        private void CollectSubtreeInstances(FragmentSpatialNode node, List<FragmentInstance> collected)
        {
            // Bucket nodes all carry LocalId -1, so the >= 0 test stops one bucket consuming every other.
            if (node.LocalId >= 0
                && !_consumedLocalIds.Contains(node.LocalId)
                && _instancesByLocalId.TryGetValue(node.LocalId, out List<FragmentInstance> instances))
            {
                collected.AddRange(instances);
                _consumedLocalIds.Add(node.LocalId);
            }

            foreach (FragmentSpatialNode child in node.Children)
            {
                CollectSubtreeInstances(child, collected);
            }
        }

        // mirrors FragmentsActor.cpp:1318-1343
        private bool ShouldMergeAtNode(FragmentSpatialNode node)
        {
            switch (_context.Options.Mode)
            {
                case FragmentImportMode.HierarchyPerStorey:
                    // The spatial tree alternates bucket nodes (a label, no id) with item nodes (an id, no label).
                    return FragmentIfcCategories.Matches(
                        CategoryOfItem(node.LocalId), FragmentIfcCategories.BuildingStorey);

                case FragmentImportMode.HierarchyPerElement:
                    return IsElementNode(node);

                default:
                    return false;
            }
        }

        // mirrors FragmentsActor.cpp:1308-1316
        private bool IsElementNode(FragmentSpatialNode node)
        {
            return node.LocalId >= 0
                && node.LocalId < _context.Result.Categories.Count
                && !FragmentIfcCategories.IsSpatialContainer(CategoryOfItem(node.LocalId));
        }

        private string CategoryOfItem(int localId)
        {
            List<string> categories = _context.Result.Categories;
            if (localId < 0 || localId >= categories.Count)
            {
                return string.Empty;
            }
            return categories[localId] ?? string.Empty;
        }

        private void SpawnElementGroup(List<FragmentInstance> instances, GameObject parent)
        {
            int bodyOrdinal = 1;
            foreach (FragmentInstance instance in instances)
            {
                // mirrors the per-instance cancel check at FragmentsActor.cpp:1369-1378
                if (!_context.Progress.ReportSteps(1))
                {
                    return;
                }

                if (_context.IsSpawnLimitReached())
                {
                    return;
                }

                string label = FragmentSceneBuilder.BuildElementLabel(instance);
                if (instances.Count > 1)
                {
                    // mirrors FragmentsActor.cpp:1556-1559
                    label += "_body" + bodyOrdinal;
                    bodyOrdinal++;
                }

                GameObject elementObject = _context.CreateChild(label, parent.transform);
                elementObject.AddComponent<FragmentElementReference>().LocalId = instance.LocalId;
                // mirrors RegisterFilterActor at FragmentsActor.cpp:1643
                _context.RegisterElement(instance.LocalId, elementObject);
                _spawnedElements++;

                Mesh mesh = _context.GetOrCreateMesh(instance);
                if (mesh == null)
                {
                    // An element whose mesh fails still exists, empty at identity; mirrors FragmentsActor.cpp:1616
                    continue;
                }

                elementObject.transform.localPosition = FragmentUnityMath.ToUnityPosition(instance.Transform);
                elementObject.transform.localRotation = FragmentUnityMath.ToUnityQuaternion(instance.Transform.Rotation);
                _context.AttachMesh(elementObject, mesh, instance);
            }
        }

        // FragmentsUE drops instances the spatial tree never names; keeping them avoids silently losing geometry.
        private void SpawnOrphanedInstances()
        {
            var orphanLocalIds = new List<int>();
            foreach (FragmentInstance instance in _context.Result.Instances)
            {
                if (!_consumedLocalIds.Contains(instance.LocalId))
                {
                    _consumedLocalIds.Add(instance.LocalId);
                    orphanLocalIds.Add(instance.LocalId);
                }
            }

            if (orphanLocalIds.Count > 0)
            {
                _context.Log?.Invoke(FragmentImportSeverity.Info,
                    $"{orphanLocalIds.Count} element(s) are absent from the spatial tree and were placed under the model root.");
            }

            foreach (int localId in orphanLocalIds)
            {
                if (_context.Progress.CancellationRequested || _context.IsSpawnLimitReached())
                {
                    return;
                }
                SpawnElementGroup(_instancesByLocalId[localId], _context.BuildResult.Root);
            }
        }

        // mirrors FragmentsActor.cpp:1258-1275
        private bool HasUnbuiltGeometry(FragmentSpatialNode node)
        {
            if (node.LocalId >= 0
                && !_consumedLocalIds.Contains(node.LocalId)
                && _instancesByLocalId.ContainsKey(node.LocalId))
            {
                return true;
            }

            foreach (FragmentSpatialNode child in node.Children)
            {
                if (HasUnbuiltGeometry(child))
                {
                    return true;
                }
            }
            return false;
        }

        private static string BuildNodeLabel(FragmentSpatialNode node)
        {
            // mirrors FragmentsActor.cpp:1413-1425
            string label;
            if (!string.IsNullOrEmpty(node.Name))
            {
                label = node.Name;
            }
            else if (node.ExpressId != 0)
            {
                label = $"{node.Category}_{node.ExpressId}";
            }
            else
            {
                label = node.Category;
            }
            return FragmentSceneBuilder.SanitizeLabel(label);
        }
    }
}
