using System.Linq;
using NUnit.Framework;

namespace FragmentsUnity.Tests
{
    [TestFixture]
    public sealed class FragmentSceneBuilderTests
    {
        private const string ModelName = "AR520";
        private const string DefaultRootName = "FragmentsModel";
        private const string SingleSidedMaterialName = "FragmentVertexColor";
        private const string DoubleSidedMaterialName = "FragmentVertexColorDoubleSided";

        private const int FirstGeometryIndex = 0;
        private const int SecondGeometryIndex = 1;
        private const int MissingGeometryIndex = -1;
        private const int OutOfRangeGeometryIndex = 5;

        private const int FirstMaterialIndex = 0;
        private const int SecondMaterialIndex = 3;

        private const int FirstLocalId = 10;
        private const int SecondLocalId = 11;
        private const int ThirdLocalId = 12;
        private const int FourthLocalId = 13;
        private const int FifthLocalId = 14;

        private const int GeometryCount = 2;
        private const int InstanceCount = 5;
        private const int SpawnedObjectCount = 3;
        private const int SharedMeshCount = 2;
        private const int MaterialCount = 2;

        private const string FirstMeshName = "Geometry_0_Material_0";
        private const string SecondMeshName = "Geometry_1_Material_0";
        private const string SecondMaterialMeshName = "Geometry_0_Material_3";

        [Test]
        public void Build_ModelWithAName_NamesTheRootAfterTheModel()
        {
            FragmentSceneBuildResult scene = FragmentSceneBuilder.Build(FlatResult(), null, null);

            Assert.That(scene.Root.name, Is.EqualTo(ModelName));
        }

        [Test]
        public void Build_ModelWithoutAName_UsesTheDefaultRootName()
        {
            FragmentImportResult result = FlatResult();
            result.ModelName = string.Empty;

            FragmentSceneBuildResult scene = FragmentSceneBuilder.Build(result, null, null);

            Assert.That(scene.Root.name, Is.EqualTo(DefaultRootName));
        }

        [Test]
        public void Build_WithoutASpatialTree_SpawnsOneObjectPerValidInstance()
        {
            var log = new FragmentBuildLog();

            FragmentSceneBuildResult scene = FragmentSceneBuilder.Build(FlatResult(), null, log.Record);

            Assert.Multiple(() =>
            {
                Assert.That(log.TextOf(FragmentImportSeverity.Info),
                    Does.Contain($" {SpawnedObjectCount} objects spawned,"));
                Assert.That(scene.SpawnedNodes, Is.Zero);
            });
        }

        [Test]
        public void Build_EveryInstancePointingOutsideTheGeometryList_SpawnsNothing()
        {
            var log = new FragmentBuildLog();
            var result = new FragmentImportResult();
            result.Geometries.Add(FragmentSceneFixture.Triangle(FirstGeometryIndex));
            result.Instances.Add(FragmentSceneFixture.Instance(
                FirstLocalId, MissingGeometryIndex, FirstMaterialIndex));
            result.Instances.Add(FragmentSceneFixture.Instance(
                SecondLocalId, OutOfRangeGeometryIndex, FirstMaterialIndex));

            FragmentSceneBuildResult scene = FragmentSceneBuilder.Build(result, null, log.Record);

            Assert.Multiple(() =>
            {
                Assert.That(log.TextOf(FragmentImportSeverity.Info), Does.Contain(" 0 objects spawned,"));
                Assert.That(scene.Meshes, Is.Empty);
                Assert.That(scene.Materials, Is.Empty);
            });
        }

        [Test]
        public void Build_GeometryWithoutTriangles_ProducesNoMeshAndNoObject()
        {
            var log = new FragmentBuildLog();
            var result = new FragmentImportResult();
            result.Geometries.Add(FragmentSceneFixture.PositionsOnly(FirstGeometryIndex));
            result.Instances.Add(FragmentSceneFixture.Instance(
                FirstLocalId, FirstGeometryIndex, FirstMaterialIndex));

            FragmentSceneBuildResult scene = FragmentSceneBuilder.Build(result, null, log.Record);

            Assert.Multiple(() =>
            {
                Assert.That(scene.Meshes, Is.Empty);
                Assert.That(log.TextOf(FragmentImportSeverity.Info), Does.Contain(" 0 objects spawned,"));
            });
        }

        [Test]
        public void Build_InstancesSharingGeometryAndMaterial_ShareOneMesh()
        {
            FragmentSceneBuildResult scene = FragmentSceneBuilder.Build(FlatResult(), null, null);

            Assert.Multiple(() =>
            {
                Assert.That(scene.Meshes, Has.Count.EqualTo(SharedMeshCount));
                Assert.That(scene.Meshes.Select(mesh => mesh.name),
                    Is.EqualTo(new[] { FirstMeshName, SecondMeshName }));
            });
        }

        [Test]
        public void Build_InstancesWithDifferentMaterialIndexes_GetSeparateMeshes()
        {
            var result = new FragmentImportResult();
            result.Geometries.Add(FragmentSceneFixture.Triangle(FirstGeometryIndex));
            result.Instances.Add(FragmentSceneFixture.Instance(
                FirstLocalId, FirstGeometryIndex, FirstMaterialIndex));
            result.Instances.Add(FragmentSceneFixture.Instance(
                SecondLocalId, FirstGeometryIndex, SecondMaterialIndex));

            FragmentSceneBuildResult scene = FragmentSceneBuilder.Build(result, null, null);

            Assert.That(scene.Meshes.Select(mesh => mesh.name),
                Is.EqualTo(new[] { FirstMeshName, SecondMaterialMeshName }));
        }

        [Test]
        public void Build_SingleAndDoubleSidedInstances_ShareTwoMaterials()
        {
            var result = new FragmentImportResult();
            result.Geometries.Add(FragmentSceneFixture.Triangle(FirstGeometryIndex));
            result.Instances.Add(FragmentSceneFixture.Instance(
                FirstLocalId, FirstGeometryIndex, FirstMaterialIndex));
            FragmentInstance doubleSided = FragmentSceneFixture.Instance(
                SecondLocalId, FirstGeometryIndex, FirstMaterialIndex);
            doubleSided.DoubleSided = true;
            result.Instances.Add(doubleSided);
            FragmentInstance anotherDoubleSided = FragmentSceneFixture.Instance(
                ThirdLocalId, FirstGeometryIndex, FirstMaterialIndex);
            anotherDoubleSided.DoubleSided = true;
            result.Instances.Add(anotherDoubleSided);

            FragmentSceneBuildResult scene = FragmentSceneBuilder.Build(result, null, null);

            Assert.Multiple(() =>
            {
                Assert.That(scene.Materials, Has.Count.EqualTo(MaterialCount));
                Assert.That(scene.Materials.Select(material => material.name),
                    Is.EqualTo(new[] { SingleSidedMaterialName, DoubleSidedMaterialName }));
            });
        }

        [Test]
        public void Build_LogsGeometryAndInstanceCountsInTheSummary()
        {
            var log = new FragmentBuildLog();

            FragmentSceneBuilder.Build(FlatResult(), null, log.Record);

            string summary = log.TextOf(FragmentImportSeverity.Info);
            Assert.Multiple(() =>
            {
                Assert.That(summary, Does.Contain($": {GeometryCount} geometries,"));
                Assert.That(summary, Does.Contain($" {InstanceCount} instances,"));
                Assert.That(summary, Does.Contain(" 0 hierarchy nodes,"));
                Assert.That(log.TextOf(FragmentImportSeverity.Error), Is.Empty);
            });
        }

        [Test]
        public void Build_WithoutALogCallback_DoesNotThrow()
        {
            Assert.That(() => FragmentSceneBuilder.Build(FlatResult(), null, null), Throws.Nothing);
        }

        private static FragmentImportResult FlatResult()
        {
            var result = new FragmentImportResult { ModelName = ModelName };
            result.Geometries.Add(FragmentSceneFixture.Triangle(FirstGeometryIndex));
            result.Geometries.Add(FragmentSceneFixture.Triangle(SecondGeometryIndex));
            result.Instances.Add(FragmentSceneFixture.Instance(
                FirstLocalId, FirstGeometryIndex, FirstMaterialIndex));
            result.Instances.Add(FragmentSceneFixture.Instance(
                SecondLocalId, SecondGeometryIndex, FirstMaterialIndex));
            result.Instances.Add(FragmentSceneFixture.Instance(
                ThirdLocalId, FirstGeometryIndex, FirstMaterialIndex));
            result.Instances.Add(FragmentSceneFixture.Instance(
                FourthLocalId, MissingGeometryIndex, FirstMaterialIndex));
            result.Instances.Add(FragmentSceneFixture.Instance(
                FifthLocalId, OutOfRangeGeometryIndex, FirstMaterialIndex));
            return result;
        }
    }
}
