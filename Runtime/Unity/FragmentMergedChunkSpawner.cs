using System.Collections.Generic;
using UnityEngine;

namespace FragmentsUnity
{
    /// <summary>Spawns merged chunks as child GameObjects.</summary>
    internal static class FragmentMergedChunkSpawner
    {
        internal static int Spawn(
            FragmentSceneSpawnContext context,
            IReadOnlyList<FragmentInstance> instances,
            GameObject parent,
            string namePrefix)
        {
            if (instances.Count == 0 || context.Progress.CancellationRequested)
            {
                return 0;
            }

            List<FragmentMergedChunk> chunks = FragmentMergedMeshBuilder.BuildChunks(
                instances, context.Result, namePrefix);

            int spawnedChunks = 0;
            int chunkedInstances = 0;
            foreach (FragmentMergedChunk chunk in chunks)
            {
                chunkedInstances += chunk.PartLocalIds.Length;
                if (!context.Progress.ReportSteps(chunk.PartLocalIds.Length))
                {
                    return spawnedChunks;
                }

                if (chunk.Mesh == null)
                {
                    continue;
                }

                if (context.IsSpawnLimitReached())
                {
                    break;
                }

                context.BuildResult.Meshes.Add(chunk.Mesh);

                GameObject chunkObject = context.CreateChild(chunk.Mesh.name, parent.transform);
                context.AttachMesh(chunkObject, chunk.Mesh, chunk.SurfaceKind, chunk.DoubleSided, chunk.Category);
                chunkObject.AddComponent<FragmentElementTable>().SetTable(chunk.TriangleStarts, chunk.PartLocalIds);
                RegisterChunkElements(context, chunk, chunkObject);

                context.BuildResult.MergedChunks++;
                spawnedChunks++;
            }

            int instancesWithoutAChunk = instances.Count - chunkedInstances;
            context.Progress.ReportSteps(instancesWithoutAChunk);
            return spawnedChunks;
        }

        private static void RegisterChunkElements(
            FragmentSceneSpawnContext context, FragmentMergedChunk chunk, GameObject chunkObject)
        {
            var registered = new HashSet<int>();
            foreach (int localId in chunk.PartLocalIds)
            {
                if (registered.Add(localId))
                {
                    context.RegisterElement(localId, chunkObject);
                }
            }
        }
    }
}
