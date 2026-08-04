using System.Numerics;

namespace FragmentsUnity.Tests
{
    internal static class FragmentSceneFixture
    {
        internal static FragmentGeometry Triangle(int geometryIndex)
        {
            FragmentGeometry geometry = PositionsOnly(geometryIndex);
            geometry.Indices.Add(0);
            geometry.Indices.Add(1);
            geometry.Indices.Add(2);
            return geometry;
        }

        internal static FragmentGeometry PositionsOnly(int geometryIndex)
        {
            var geometry = new FragmentGeometry { GeometryIndex = geometryIndex };
            geometry.Positions.Add(new Vector3(0f, 0f, 0f));
            geometry.Positions.Add(new Vector3(1f, 0f, 0f));
            geometry.Positions.Add(new Vector3(0f, 1f, 0f));
            return geometry;
        }

        internal static FragmentInstance Instance(int localId, int geometryIndex, int materialIndex)
        {
            return new FragmentInstance
            {
                LocalId = localId,
                GeometryIndex = geometryIndex,
                MaterialIndex = materialIndex
            };
        }

        internal static FragmentSceneBuildOptions Options(FragmentImportMode mode)
        {
            return new FragmentSceneBuildOptions { Mode = mode };
        }

        internal static FragmentSpatialNode Node(int localId, string category, string name)
        {
            return new FragmentSpatialNode
            {
                LocalId = localId,
                Category = category,
                Name = name
            };
        }
    }
}
