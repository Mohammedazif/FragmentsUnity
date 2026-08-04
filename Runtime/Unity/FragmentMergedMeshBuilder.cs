using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FragmentsUnity
{
    internal static class FragmentMergedMeshBuilder
    {
        private const string DefaultNamePrefix = "Merged";

        internal static List<FragmentMergedChunk> BuildChunks(
            IReadOnlyList<FragmentInstance> instances,
            FragmentImportResult result,
            string namePrefix)
        {
            var chunks = new List<FragmentMergedChunk>();
            if (instances == null || result == null)
            {
                return chunks;
            }

            List<FragmentMergeBucket> buckets = BuildBuckets(instances, result);
            foreach (FragmentMergeBucket bucket in buckets)
            {
                AppendBucketChunks(bucket, result, namePrefix, chunks);
            }

            return chunks;
        }

        private static List<FragmentMergeBucket> BuildBuckets(
            IReadOnlyList<FragmentInstance> instances, FragmentImportResult result)
        {
            var bucketsByKey = new Dictionary<string, FragmentMergeBucket>();
            var orderedBuckets = new List<FragmentMergeBucket>();

            foreach (FragmentInstance instance in instances)
            {
                if (instance == null
                    || instance.GeometryIndex < 0
                    || instance.GeometryIndex >= result.Geometries.Count)
                {
                    continue;
                }

                string category = FragmentMergeBucket.ResolveCategory(instance.Category);
                FragmentSurfaceKind surfaceKind =
                    FragmentSurfaceClassifier.Classify(instance.Color, instance.Opacity);
                float opacity = FragmentSurfaceClassifier.ResolveOpacity(surfaceKind, instance.Opacity);
                string key = FragmentMergeBucket.BuildKey(category, instance.Color, opacity, surfaceKind);

                if (!bucketsByKey.TryGetValue(key, out FragmentMergeBucket bucket))
                {
                    bucket = new FragmentMergeBucket
                    {
                        Category = category,
                        SurfaceKind = surfaceKind,
                        Opacity = opacity
                    };
                    bucketsByKey.Add(key, bucket);
                    orderedBuckets.Add(bucket);
                }

                bucket.Instances.Add(instance);
            }

            return orderedBuckets;
        }

        private static void AppendBucketChunks(
            FragmentMergeBucket bucket,
            FragmentImportResult result,
            string namePrefix,
            List<FragmentMergedChunk> chunks)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            var triangleStarts = new List<int>();
            var partLocalIds = new List<int>();
            long chunkIndexCount = 0;
            bool hasNormals = false;

            void FlushChunk()
            {
                // mirrors FragmentsActor.cpp:682; an oversized part still becomes a chunk of its own
                if (partLocalIds.Count == 0)
                {
                    return;
                }

                chunks.Add(new FragmentMergedChunk
                {
                    Mesh = CreateChunkMesh(
                        vertices, normals, colors, triangles, hasNormals,
                        BuildMeshName(namePrefix, bucket.Category, chunks.Count)),
                    // mirrors the forced double-sided merged material at FragmentsActor.cpp:662
                    DoubleSided = true,
                    SurfaceKind = bucket.SurfaceKind,
                    Category = bucket.Category,
                    TriangleStarts = triangleStarts.ToArray(),
                    PartLocalIds = partLocalIds.ToArray()
                });

                vertices.Clear();
                normals.Clear();
                colors.Clear();
                triangles.Clear();
                triangleStarts.Clear();
                partLocalIds.Clear();
                chunkIndexCount = 0;
                hasNormals = false;
            }

            foreach (FragmentInstance instance in bucket.Instances)
            {
                FragmentGeometry geometry = result.Geometries[instance.GeometryIndex];
                if (geometry == null || geometry.Positions.Count == 0 || geometry.Indices.Count < 3)
                {
                    continue;
                }

                // mirrors FragmentsActor.cpp:740-745
                if (vertices.Count > 0
                    && (vertices.Count + (long)geometry.Positions.Count > FragmentImportLimits.MaxMergedVertices
                        || chunkIndexCount + geometry.Indices.Count > FragmentImportLimits.MaxMergedIndices))
                {
                    FlushChunk();
                }

                // mirrors FragMeshBuilder.cpp:268-271; recorded for every part so both tables stay parallel
                triangleStarts.Add(triangles.Count / 3);
                partLocalIds.Add(instance.LocalId);
                hasNormals |= AppendPart(instance, bucket.Opacity, geometry, vertices, normals, colors, triangles);
                chunkIndexCount += geometry.Indices.Count;
            }

            FlushChunk();
        }

        private static bool AppendPart(
            FragmentInstance instance,
            float bucketOpacity,
            FragmentGeometry geometry,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Color> colors,
            List<int> triangles)
        {
            int vertexOffset = vertices.Count;
            int vertexCount = geometry.Positions.Count;
            bool partHasNormals = geometry.Normals.Count == vertexCount;
            // mirrors FragmentsActor.cpp:659; every part of a bucket bakes the bucket's resolved opacity
            Color vertexColor = FragmentVertexColor.FromInstance(instance, bucketOpacity);
            Vector3 translation = FragmentUnityMath.ToUnityPosition(instance.Transform);

            for (int i = 0; i < vertexCount; i++)
            {
                vertices.Add(TransformPosition(instance.Transform.Rotation, geometry.Positions[i], translation));
                // Unity requires one normal per vertex, so a part without normals contributes zeroes.
                normals.Add(partHasNormals
                    ? TransformNormal(instance.Transform.Rotation, geometry.Normals[i])
                    : Vector3.zero);
                colors.Add(vertexColor);
            }

            AppendTriangles(geometry, vertexCount, vertexOffset, triangles);
            return partHasNormals;
        }

        private static void AppendTriangles(
            FragmentGeometry geometry, int vertexCount, int vertexOffset, List<int> triangles)
        {
            int triangleCount = geometry.Indices.Count / 3;
            for (int i = 0; i < triangleCount; i++)
            {
                int index0 = geometry.Indices[i * 3];
                int index1 = geometry.Indices[i * 3 + 1];
                int index2 = geometry.Indices[i * 3 + 2];

                // Negative indices matter too; mirrors FragMeshBuilder.cpp:313-318.
                if (index0 < 0 || index1 < 0 || index2 < 0
                    || index0 >= vertexCount || index1 >= vertexCount || index2 >= vertexCount)
                {
                    continue;
                }

                triangles.Add(vertexOffset + index0);
                triangles.Add(vertexOffset + index1);
                triangles.Add(vertexOffset + index2);
            }
        }

        private static Mesh CreateChunkMesh(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Color> colors,
            List<int> triangles,
            bool hasNormals,
            string meshName)
        {
            var mesh = new Mesh
            {
                name = meshName,
                // A 16-bit index buffer cannot address a chunk this large, and the format must be set before the triangles.
                indexFormat = vertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16
            };

            mesh.SetVertices(vertices);
            if (hasNormals)
            {
                mesh.SetNormals(normals);
            }
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0, false);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static string BuildMeshName(string namePrefix, string category, int chunkSerial)
        {
            string prefix = string.IsNullOrEmpty(namePrefix) ? DefaultNamePrefix : namePrefix;
            return $"{prefix}_{FragmentSceneBuilder.SanitizeLabel(category)}_{chunkSerial}";
        }

        private static Vector3 TransformPosition(
            System.Numerics.Quaternion rotation, System.Numerics.Vector3 position, Vector3 translation)
        {
            System.Numerics.Vector3 rotated = System.Numerics.Vector3.Transform(position, rotation);
            return new Vector3(
                rotated.X + translation.x,
                rotated.Y + translation.y,
                rotated.Z + translation.z);
        }

        private static Vector3 TransformNormal(System.Numerics.Quaternion rotation, System.Numerics.Vector3 normal)
        {
            System.Numerics.Vector3 rotated = System.Numerics.Vector3.Transform(normal, rotation);
            // mirrors GetSafeNormal at FragMeshBuilder.cpp:329
            return rotated.LengthSquared() < FragmentImportLimits.MinimumSafeNormalLengthSquared
                ? Vector3.zero
                : FragmentUnityMath.ToUnityVector(System.Numerics.Vector3.Normalize(rotated));
        }

    }
}
