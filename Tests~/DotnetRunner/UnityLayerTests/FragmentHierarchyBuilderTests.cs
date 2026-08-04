using NUnit.Framework;

namespace FragmentsUnity.Tests
{
    [TestFixture]
    public sealed class FragmentHierarchyBuilderTests
    {
        private const string ModelName = "AR520";
        private const string ProjectCategory = "IFCPROJECT";
        private const string StoreyCategory = "IFCBUILDINGSTOREY";
        private const string WallCategory = "IFCWALL";
        private const string GroupCategory = "Group";

        private const string ProjectName = "37464650";
        private const string FirstStoreyName = "Level 1";
        private const string SecondStoreyName = "Level 2";
        private const string WallName = "Wall A";
        private const string SecondWallName = "Wall B";

        private const int RootLocalId = -1;
        private const int FirstStoreyLocalId = 1;
        private const int WallLocalId = 2;
        private const int SecondStoreyLocalId = 3;
        private const int SecondWallLocalId = 4;
        private const int OrphanLocalId = 8;

        private const int GeometryIndex = 0;
        private const int MaterialIndex = 0;

        private const int NamedNodeCount = 3;
        private const int SharedMeshCount = 1;
        private const string OrphanNoticeFragment = "spatial tree";

        private static FragmentSceneBuildOptions PerBodyOptions =>
            FragmentSceneFixture.Options(FragmentImportMode.HierarchyPerBody);

        [Test]
        public void Build_WithASpatialTree_SpawnsOneNodePerNamedNode()
        {
            FragmentSceneBuildResult scene = FragmentSceneBuilder.Build(TreeResult(), PerBodyOptions, null);

            Assert.That(scene.SpawnedNodes, Is.EqualTo(NamedNodeCount));
        }

        [Test]
        public void Build_WithASpatialTree_SpawnsTheElementsOfNodesThatCarryGeometry()
        {
            var log = new FragmentBuildLog();

            FragmentSceneBuildResult scene = FragmentSceneBuilder.Build(TreeResult(), PerBodyOptions, log.Record);

            Assert.Multiple(() =>
            {
                Assert.That(log.TextOf(FragmentImportSeverity.Info), Does.Contain(" 1 objects spawned,"));
                Assert.That(scene.Meshes, Has.Count.EqualTo(SharedMeshCount));
            });
        }

        [Test]
        public void Build_AnonymousGroupNode_IsFlattenedIntoItsParent()
        {
            FragmentImportResult result = TreeResult();
            FragmentSpatialNode storey = result.SpatialRoot.Children[0];
            FragmentSpatialNode wall = storey.Children[0];
            FragmentSpatialNode group = FragmentSceneFixture.Node(
                RootLocalId, GroupCategory, string.Empty);
            group.Children.Add(wall);
            storey.Children.Clear();
            storey.Children.Add(group);

            FragmentSceneBuildResult scene = FragmentSceneBuilder.Build(result, PerBodyOptions, null);

            Assert.That(scene.SpawnedNodes, Is.EqualTo(NamedNodeCount));
        }

        [Test]
        public void Build_SubtreeWithoutAnyGeometry_IsSkippedEntirely()
        {
            FragmentImportResult result = TreeResult();
            FragmentSpatialNode emptyStorey = FragmentSceneFixture.Node(
                SecondStoreyLocalId, StoreyCategory, SecondStoreyName);
            emptyStorey.Children.Add(
                FragmentSceneFixture.Node(SecondWallLocalId, WallCategory, SecondWallName));
            result.SpatialRoot.Children.Add(emptyStorey);

            FragmentSceneBuildResult scene = FragmentSceneBuilder.Build(result, PerBodyOptions, null);

            Assert.That(scene.SpawnedNodes, Is.EqualTo(NamedNodeCount));
        }

        [Test]
        public void Build_InstanceAbsentFromTheSpatialTree_IsPlacedUnderTheRootAndReported()
        {
            var log = new FragmentBuildLog();
            FragmentImportResult result = TreeResult();
            result.Instances.Add(FragmentSceneFixture.Instance(OrphanLocalId, GeometryIndex, MaterialIndex));

            FragmentSceneBuildResult scene = FragmentSceneBuilder.Build(result, PerBodyOptions, log.Record);

            string info = log.TextOf(FragmentImportSeverity.Info);
            Assert.Multiple(() =>
            {
                Assert.That(info, Does.Contain(OrphanNoticeFragment));
                Assert.That(info, Does.Contain(" 2 objects spawned,"));
                Assert.That(scene.SpawnedNodes, Is.EqualTo(NamedNodeCount));
            });
        }

        [Test]
        public void Build_ModelWhereNoInstanceIsInTheTree_ReportsEveryElementAsOrphaned()
        {
            var log = new FragmentBuildLog();
            FragmentImportResult result = TreeResult();
            result.Instances.Clear();
            result.Instances.Add(FragmentSceneFixture.Instance(OrphanLocalId, GeometryIndex, MaterialIndex));

            FragmentSceneBuildResult scene = FragmentSceneBuilder.Build(result, PerBodyOptions, log.Record);

            Assert.Multiple(() =>
            {
                Assert.That(log.TextOf(FragmentImportSeverity.Info), Does.Contain(OrphanNoticeFragment));
                Assert.That(scene.SpawnedNodes, Is.Zero);
            });
        }

        [Test]
        public void Build_ElementWithTwoBodies_SpawnsBothAndSharesOneMesh()
        {
            var log = new FragmentBuildLog();
            FragmentImportResult result = TreeResult();
            result.Instances.Add(FragmentSceneFixture.Instance(WallLocalId, GeometryIndex, MaterialIndex));

            FragmentSceneBuildResult scene = FragmentSceneBuilder.Build(result, PerBodyOptions, log.Record);

            Assert.Multiple(() =>
            {
                Assert.That(log.TextOf(FragmentImportSeverity.Info), Does.Contain(" 2 objects spawned,"));
                Assert.That(scene.Meshes, Has.Count.EqualTo(SharedMeshCount));
            });
        }

        [Test]
        public void Build_ElementWhoseMeshCannotBeBuilt_StillSpawnsTheObject()
        {
            var log = new FragmentBuildLog();
            FragmentImportResult result = TreeResult();
            result.Geometries.Clear();
            result.Geometries.Add(FragmentSceneFixture.PositionsOnly(GeometryIndex));

            FragmentSceneBuildResult scene = FragmentSceneBuilder.Build(result, PerBodyOptions, log.Record);

            Assert.Multiple(() =>
            {
                Assert.That(log.TextOf(FragmentImportSeverity.Info), Does.Contain(" 1 objects spawned,"));
                Assert.That(scene.Meshes, Is.Empty);
                Assert.That(scene.Materials, Is.Empty);
            });
        }

        [Test]
        public void Build_SpatialRootWithoutChildren_TakesTheFlatPath()
        {
            var log = new FragmentBuildLog();
            FragmentImportResult result = TreeResult();
            result.SpatialRoot = new FragmentSpatialNode();

            FragmentSceneBuildResult scene = FragmentSceneBuilder.Build(result, PerBodyOptions, log.Record);

            Assert.Multiple(() =>
            {
                Assert.That(scene.SpawnedNodes, Is.Zero);
                Assert.That(log.TextOf(FragmentImportSeverity.Info), Does.Contain(" 1 objects spawned,"));
                Assert.That(log.TextOf(FragmentImportSeverity.Info), Does.Not.Contain(OrphanNoticeFragment));
            });
        }

        private static FragmentImportResult TreeResult()
        {
            var result = new FragmentImportResult { ModelName = ModelName };
            result.Geometries.Add(FragmentSceneFixture.Triangle(GeometryIndex));
            result.Instances.Add(FragmentSceneFixture.Instance(WallLocalId, GeometryIndex, MaterialIndex));

            FragmentSpatialNode root = FragmentSceneFixture.Node(RootLocalId, ProjectCategory, ProjectName);
            FragmentSpatialNode storey = FragmentSceneFixture.Node(
                FirstStoreyLocalId, StoreyCategory, FirstStoreyName);
            storey.Children.Add(FragmentSceneFixture.Node(WallLocalId, WallCategory, WallName));
            root.Children.Add(storey);
            result.SpatialRoot = root;
            return result;
        }
    }
}
