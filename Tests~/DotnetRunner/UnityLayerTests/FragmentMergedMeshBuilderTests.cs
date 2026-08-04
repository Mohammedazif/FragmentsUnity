using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace FragmentsUnity.Tests
{
    [TestFixture]
    public sealed class FragmentMergedMeshBuilderTests
    {
        private const string BuilderTypeName = "FragmentsUnity.FragmentMergedMeshBuilder";
        private const string BuildChunksMethodName = "BuildChunks";

        private const string NamePrefix = "Model";
        private const string WallCategory = "IFCWALL";
        private const string SlabCategory = "IFCSLAB";
        private const string PunctuatedCategory = "Basic Wall";
        private const string UnclassifiedCategory = "Unclassified";

        private const int FirstGeometryIndex = 0;
        private const int SecondGeometryIndex = 1;
        private const int OutOfRangeGeometryIndex = 5;
        private const int MaterialIndex = 0;

        private const int FirstLocalId = 10;
        private const int SecondLocalId = 11;
        private const int ThirdLocalId = 12;

        private const float ColorTolerance = 1e-4f;
        private const float PositionTolerance = 1e-4f;

        [Test]
        public void BuildChunks_InstancesSharingCategoryAndColor_MergeIntoOneChunk()
        {
            FragmentImportResult result = ResultWith(FragmentSceneFixture.Triangle(FirstGeometryIndex));
            result.Instances.Add(Instance(FirstLocalId, FirstGeometryIndex));
            result.Instances.Add(Instance(SecondLocalId, FirstGeometryIndex));

            List<object> chunks = BuildChunks(result);

            Assert.That(chunks.Count, Is.EqualTo(1));
            Assert.That(PartLocalIds(chunks[0]), Is.EqualTo(new[] { FirstLocalId, SecondLocalId }));
            Assert.That(TriangleStarts(chunks[0]), Is.EqualTo(new[] { 0, 1 }));
            Assert.That(Mesh(chunks[0]).triangles, Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5 }));
        }

        [Test]
        public void BuildChunks_MergedChunk_IsDoubleSidedAndKeepsItsCategory()
        {
            FragmentImportResult result = ResultWith(FragmentSceneFixture.Triangle(FirstGeometryIndex));
            result.Instances.Add(Instance(FirstLocalId, FirstGeometryIndex));

            object chunk = BuildChunks(result)[0];

            Assert.That(Field<bool>(chunk, "DoubleSided"), Is.True);
            Assert.That(Field<string>(chunk, "Category"), Is.EqualTo(UnclassifiedCategory));
        }

        [Test]
        public void BuildChunks_DifferentCategories_ProduceSeparateChunks()
        {
            FragmentImportResult result = ResultWith(FragmentSceneFixture.Triangle(FirstGeometryIndex));
            result.Instances.Add(Categorized(Instance(FirstLocalId, FirstGeometryIndex), WallCategory));
            result.Instances.Add(Categorized(Instance(SecondLocalId, FirstGeometryIndex), SlabCategory));

            List<object> chunks = BuildChunks(result);

            Assert.That(chunks.Select(chunk => Field<string>(chunk, "Category")),
                Is.EqualTo(new[] { WallCategory, SlabCategory }));
        }

        [Test]
        public void BuildChunks_ColorsInDifferentQuantizationSteps_ProduceSeparateChunks()
        {
            FragmentImportResult result = ResultWith(FragmentSceneFixture.Triangle(FirstGeometryIndex));
            result.Instances.Add(Colored(Instance(FirstLocalId, FirstGeometryIndex), 0.5f));
            result.Instances.Add(Colored(Instance(SecondLocalId, FirstGeometryIndex), 0.6f));

            Assert.That(BuildChunks(result).Count, Is.EqualTo(2));
        }

        [Test]
        public void BuildChunks_ColorsInTheSameQuantizationStep_MergeIntoOneChunk()
        {
            FragmentImportResult result = ResultWith(FragmentSceneFixture.Triangle(FirstGeometryIndex));
            result.Instances.Add(Colored(Instance(FirstLocalId, FirstGeometryIndex), 0.5f));
            result.Instances.Add(Colored(Instance(SecondLocalId, FirstGeometryIndex), 0.5005f));

            Assert.That(BuildChunks(result).Count, Is.EqualTo(1));
        }

        [Test]
        public void BuildChunks_UnusableGeometry_LeavesNoPartEntry()
        {
            FragmentImportResult result = ResultWith(
                FragmentSceneFixture.Triangle(FirstGeometryIndex),
                FragmentSceneFixture.PositionsOnly(SecondGeometryIndex));
            result.Instances.Add(Instance(FirstLocalId, OutOfRangeGeometryIndex));
            result.Instances.Add(Instance(SecondLocalId, SecondGeometryIndex));
            result.Instances.Add(Instance(ThirdLocalId, FirstGeometryIndex));

            object chunk = BuildChunks(result)[0];

            Assert.That(PartLocalIds(chunk), Is.EqualTo(new[] { ThirdLocalId }));
            Assert.That(TriangleStarts(chunk), Is.EqualTo(new[] { 0 }));
        }

        [Test]
        public void BuildChunks_PartWhoseTrianglesAreAllOutOfRange_StillRecordsItsTriangleStart()
        {
            FragmentImportResult result = ResultWith(
                FragmentSceneFixture.Triangle(FirstGeometryIndex),
                OutOfRangeIndices(SecondGeometryIndex));
            result.Instances.Add(Instance(FirstLocalId, FirstGeometryIndex));
            result.Instances.Add(Instance(SecondLocalId, SecondGeometryIndex));
            result.Instances.Add(Instance(ThirdLocalId, FirstGeometryIndex));

            object chunk = BuildChunks(result)[0];

            Assert.That(PartLocalIds(chunk), Is.EqualTo(new[] { FirstLocalId, SecondLocalId, ThirdLocalId }));
            Assert.That(TriangleStarts(chunk), Is.EqualTo(new[] { 0, 1, 1 }));
            Assert.That(Mesh(chunk).triangles, Is.EqualTo(new[] { 0, 1, 2, 6, 7, 8 }));
        }

        [Test]
        public void BuildChunks_ChunkMesh_IsNamedAfterThePrefixSanitizedCategoryAndSerial()
        {
            FragmentImportResult result = ResultWith(FragmentSceneFixture.Triangle(FirstGeometryIndex));
            result.Instances.Add(Categorized(Instance(FirstLocalId, FirstGeometryIndex), PunctuatedCategory));
            result.Instances.Add(Categorized(Instance(SecondLocalId, FirstGeometryIndex), SlabCategory));

            List<object> chunks = BuildChunks(result);

            Assert.That(Mesh(chunks[0]).name, Is.EqualTo("Model_Basic_Wall_0"));
            Assert.That(Mesh(chunks[1]).name, Is.EqualTo("Model_IFCSLAB_1"));
        }

        [Test]
        public void BuildChunks_InstanceTransform_IsBakedIntoTheVertices()
        {
            FragmentImportResult result = ResultWith(FragmentSceneFixture.Triangle(FirstGeometryIndex));
            FragmentInstance instance = Instance(FirstLocalId, FirstGeometryIndex);
            instance.Transform = new FragmentTransform
            {
                PositionX = 10.0,
                PositionY = 0.0,
                PositionZ = 0.0,
                Rotation = System.Numerics.Quaternion.CreateFromAxisAngle(
                    new System.Numerics.Vector3(0f, 1f, 0f), (float)Math.PI)
            };
            result.Instances.Add(instance);

            Vector3[] vertices = Mesh(BuildChunks(result)[0]).vertices;

            Assert.That(vertices[1].x, Is.EqualTo(9f).Within(PositionTolerance));
            Assert.That(vertices[1].z, Is.EqualTo(0f).Within(PositionTolerance));
            Assert.That(vertices[2].y, Is.EqualTo(1f).Within(PositionTolerance));
        }

        [Test]
        public void BuildChunks_GeometryWithNormals_RotatesThemByTheInstanceRotationOnly()
        {
            FragmentGeometry geometry = FragmentSceneFixture.Triangle(FirstGeometryIndex);
            for (int i = 0; i < geometry.Positions.Count; i++)
            {
                geometry.Normals.Add(new System.Numerics.Vector3(0f, 0f, 1f));
            }

            FragmentImportResult result = ResultWith(geometry);
            FragmentInstance instance = Instance(FirstLocalId, FirstGeometryIndex);
            instance.Transform = new FragmentTransform
            {
                PositionX = 10.0,
                Rotation = System.Numerics.Quaternion.CreateFromAxisAngle(
                    new System.Numerics.Vector3(0f, 1f, 0f), (float)Math.PI)
            };
            result.Instances.Add(instance);

            Vector3[] normals = Mesh(BuildChunks(result)[0]).normals;

            Assert.That(normals.Length, Is.EqualTo(geometry.Positions.Count));
            Assert.That(normals[0].z, Is.EqualTo(-1f).Within(PositionTolerance));
            Assert.That(normals[0].x, Is.EqualTo(0f).Within(PositionTolerance));
        }

        [Test]
        public void BuildChunks_GeometryWithoutNormals_LeavesTheMeshNormalsUnset()
        {
            FragmentImportResult result = ResultWith(FragmentSceneFixture.Triangle(FirstGeometryIndex));
            result.Instances.Add(Instance(FirstLocalId, FirstGeometryIndex));

            Assert.That(Mesh(BuildChunks(result)[0]).normals.Length, Is.EqualTo(0));
        }

        [Test]
        public void BuildChunks_VertexColors_AreGammaCorrectedWithOpacityInAlpha()
        {
            FragmentImportResult result = ResultWith(FragmentSceneFixture.Triangle(FirstGeometryIndex));
            FragmentInstance instance = Colored(Instance(FirstLocalId, FirstGeometryIndex), 0.5f);
            instance.Opacity = 0.4f;
            result.Instances.Add(instance);

            Color color = Mesh(BuildChunks(result)[0]).colors[0];

            Assert.That(color.r, Is.EqualTo(Math.Pow(0.5, 2.2)).Within(ColorTolerance));
            Assert.That(color.a, Is.EqualTo(0.4f).Within(ColorTolerance));
        }

        [Test]
        public void BuildChunks_PartsPastTheVertexBudget_StartANewChunk()
        {
            FragmentImportResult result = ResultWith(
                LargeGeometry(FirstGeometryIndex, FragmentImportLimits.MaxMergedVertices / 2 + 1));
            result.Instances.Add(Instance(FirstLocalId, FirstGeometryIndex));
            result.Instances.Add(Instance(SecondLocalId, FirstGeometryIndex));

            List<object> chunks = BuildChunks(result);

            Assert.That(chunks.Count, Is.EqualTo(2));
            Assert.That(PartLocalIds(chunks[0]), Is.EqualTo(new[] { FirstLocalId }));
            Assert.That(PartLocalIds(chunks[1]), Is.EqualTo(new[] { SecondLocalId }));
            Assert.That(TriangleStarts(chunks[1]), Is.EqualTo(new[] { 0 }));
            Assert.That(Mesh(chunks[0]).indexFormat, Is.EqualTo(IndexFormat.UInt32));
        }

        [Test]
        public void BuildChunks_SmallChunk_KeepsTheSixteenBitIndexFormat()
        {
            FragmentImportResult result = ResultWith(FragmentSceneFixture.Triangle(FirstGeometryIndex));
            result.Instances.Add(Instance(FirstLocalId, FirstGeometryIndex));

            Assert.That(Mesh(BuildChunks(result)[0]).indexFormat, Is.EqualTo(IndexFormat.UInt16));
        }

        [Test]
        public void BuildChunks_NoUsableInstances_ProducesNoChunks()
        {
            FragmentImportResult result = ResultWith(FragmentSceneFixture.Triangle(FirstGeometryIndex));
            result.Instances.Add(Instance(FirstLocalId, OutOfRangeGeometryIndex));

            Assert.That(BuildChunks(result).Count, Is.EqualTo(0));
        }

        private static FragmentImportResult ResultWith(params FragmentGeometry[] geometries)
        {
            var result = new FragmentImportResult();
            result.Geometries.AddRange(geometries);
            return result;
        }

        private static FragmentInstance Instance(int localId, int geometryIndex)
        {
            return FragmentSceneFixture.Instance(localId, geometryIndex, MaterialIndex);
        }

        private static FragmentInstance Categorized(FragmentInstance instance, string category)
        {
            instance.Category = category;
            return instance;
        }

        private static FragmentInstance Colored(FragmentInstance instance, float channel)
        {
            instance.Color = new System.Numerics.Vector4(channel, channel, channel, 1f);
            return instance;
        }

        private static FragmentGeometry OutOfRangeIndices(int geometryIndex)
        {
            FragmentGeometry geometry = FragmentSceneFixture.PositionsOnly(geometryIndex);
            geometry.Indices.Add(0);
            geometry.Indices.Add(1);
            geometry.Indices.Add(geometry.Positions.Count);
            return geometry;
        }

        private static FragmentGeometry LargeGeometry(int geometryIndex, int vertexCount)
        {
            var geometry = new FragmentGeometry { GeometryIndex = geometryIndex };
            for (int i = 0; i < vertexCount; i++)
            {
                geometry.Positions.Add(new System.Numerics.Vector3(i, 0f, 0f));
            }
            geometry.Indices.Add(0);
            geometry.Indices.Add(1);
            geometry.Indices.Add(2);
            return geometry;
        }

        private static List<object> BuildChunks(FragmentImportResult result)
        {
            Type builderType = typeof(FragmentSceneBuilder).Assembly.GetType(BuilderTypeName);
            MethodInfo method = builderType.GetMethod(
                BuildChunksMethodName, BindingFlags.NonPublic | BindingFlags.Static);
            object chunks = method.Invoke(null, new object[] { result.Instances, result, NamePrefix });
            return ((IEnumerable)chunks).Cast<object>().ToList();
        }

        private static Mesh Mesh(object chunk)
        {
            return Field<Mesh>(chunk, "Mesh");
        }

        private static int[] TriangleStarts(object chunk)
        {
            return Field<int[]>(chunk, "TriangleStarts");
        }

        private static int[] PartLocalIds(object chunk)
        {
            return Field<int[]>(chunk, "PartLocalIds");
        }

        private static T Field<T>(object chunk, string fieldName)
        {
            return (T)chunk.GetType().GetField(fieldName).GetValue(chunk);
        }
    }
}
