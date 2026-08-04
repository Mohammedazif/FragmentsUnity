using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FragmentsUnity
{
    /// <summary>Builds a UnityEngine.Mesh from parsed geometry with the IFC material color baked into vertex colors.</summary>
    public static class FragmentMeshFactory
    {
        /// <summary>Returns null when the geometry has no positions or indices; mirrors FragMeshBuilder.cpp:59.</summary>
        public static Mesh CreateMesh(FragmentGeometry geometry, Color vertexColor, string meshName)
        {
            if (geometry == null || geometry.Positions.Count == 0 || geometry.Indices.Count == 0)
            {
                return null;
            }

            int vertexCount = geometry.Positions.Count;

            var mesh = new Mesh
            {
                name = meshName,
                indexFormat = vertexCount > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16
            };

            var vertices = new List<Vector3>(vertexCount);
            for (int i = 0; i < vertexCount; i++)
            {
                vertices.Add(FragmentUnityMath.ToUnityVector(geometry.Positions[i]));
            }
            mesh.SetVertices(vertices);

            if (geometry.Normals.Count == vertexCount)
            {
                var normals = new List<Vector3>(vertexCount);
                for (int i = 0; i < vertexCount; i++)
                {
                    normals.Add(FragmentUnityMath.ToUnityVector(geometry.Normals[i]));
                }
                mesh.SetNormals(normals);
            }

            var colors = new List<Color>(vertexCount);
            for (int i = 0; i < vertexCount; i++)
            {
                colors.Add(vertexColor);
            }
            mesh.SetColors(colors);

            mesh.SetTriangles(CollectValidTriangleIndices(geometry, vertexCount), 0, false);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static List<int> CollectValidTriangleIndices(FragmentGeometry geometry, int vertexCount)
        {
            int triangleCount = geometry.Indices.Count / 3;
            var triangles = new List<int>(triangleCount * 3);
            for (int i = 0; i < triangleCount; i++)
            {
                int index0 = geometry.Indices[i * 3];
                int index1 = geometry.Indices[i * 3 + 1];
                int index2 = geometry.Indices[i * 3 + 2];

                // Negative indices matter too; mirrors FragMeshBuilder.cpp:408.
                if (index0 < 0 || index1 < 0 || index2 < 0
                    || index0 >= vertexCount || index1 >= vertexCount || index2 >= vertexCount)
                {
                    continue;
                }

                triangles.Add(index0);
                triangles.Add(index1);
                triangles.Add(index2);
            }
            return triangles;
        }
    }
}
