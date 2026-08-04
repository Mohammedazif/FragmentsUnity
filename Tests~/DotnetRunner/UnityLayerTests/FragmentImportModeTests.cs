using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace FragmentsUnity.Tests
{
    [TestFixture]
    public sealed class FragmentImportModeTests
    {
        private const string ModelName = "AR520";
        private const string ProjectCategory = "IFCPROJECT";
        private const string StoreyCategory = "IFCBUILDINGSTOREY";
        private const string WallCategory = "IFCWALL";
        private const string SpaceCategory = "IfcSpace";
        private const string StoreyName = "Level_1";
        private const string FirstWallName = "Wall_A";
        private const string SecondWallName = "Wall_B";

        private const int ProjectLocalId = 0;
        private const int StoreyLocalId = 1;
        private const int FirstWallLocalId = 2;
        private const int SecondWallLocalId = 3;

        private const int GeometryIndex = 0;
        private const int MaterialIndex = 0;

        private const int InstanceCount = 3;
        private const int ElementNodeCount = 2;
        private const int SharedMeshCount = 1;

        private const int DefaultLayer = 0;
        private const int PickingLayer = 9;

        [Test]
        public void Build_MergedWholeModel_ParentsEveryChunkDirectlyUnderTheRoot()
        {
            FragmentSceneBuildResult scene = Build(FragmentImportMode.MergedWholeModel);

            Assert.Multiple(() =>
            {
                Assert.That(scene.MergedChunks, Is.EqualTo(scene.Root.transform.childCount));
                Assert.That(scene.MergedChunks, Is.GreaterThan(0));
                Assert.That(MeshObjects(scene.Root).All(child => child.transform.parent == scene.Root.transform),
                    Is.True);
            });
        }

        [Test]
        public void Build_MergedWholeModel_GivesEveryChunkATriangleTableCoveringItsParts()
        {
            FragmentSceneBuildResult scene = Build(FragmentImportMode.MergedWholeModel);

            FragmentElementTable table = scene.Root.GetComponentsInChildren<FragmentElementTable>().Single();
            Assert.That(table.FindLocalId(0), Is.EqualTo(FirstWallLocalId));
        }

        [Test]
        public void Build_MergedWholeModel_RegistersEveryDistinctLocalIdOnTheChunk()
        {
            FragmentSceneBuildResult scene = Build(FragmentImportMode.MergedWholeModel);

            Assert.That(scene.Root.GetComponent<FragmentVisibilityIndex>().HasEntries, Is.True);
        }

        [Test]
        public void Build_WithPickingEnabled_AddsAColliderSharingEveryRenderedMesh()
        {
            FragmentSceneBuildResult scene = Build(FragmentImportMode.MergedWholeModel);

            MeshCollider[] colliders = scene.Root.GetComponentsInChildren<MeshCollider>();
            MeshFilter[] filters = scene.Root.GetComponentsInChildren<MeshFilter>();
            Assert.Multiple(() =>
            {
                Assert.That(colliders, Has.Length.EqualTo(filters.Length));
                Assert.That(colliders.Select(collider => collider.sharedMesh),
                    Is.EqualTo(filters.Select(filter => filter.sharedMesh)));
            });
        }

        [Test]
        public void Build_WithPickingDisabled_AddsNoColliders()
        {
            var options = new FragmentSceneBuildOptions
            {
                Mode = FragmentImportMode.MergedWholeModel,
                EnablePicking = false
            };

            FragmentSceneBuildResult scene = FragmentSceneBuilder.Build(TreeResult(), options, null);

            Assert.That(scene.Root.GetComponentsInChildren<MeshCollider>(), Is.Empty);
        }

        [Test]
        public void Build_WithAColliderLayer_MovesEveryCollidingObjectOntoIt()
        {
            FragmentSceneBuildResult scene = BuildOnLayer(FragmentImportMode.HierarchyPerBody, PickingLayer);

            GameObject[] colliding = CollidingObjects(scene.Root);
            Assert.Multiple(() =>
            {
                Assert.That(colliding, Is.Not.Empty);
                Assert.That(colliding.Where(target => target.layer != PickingLayer), Is.Empty);
            });
        }

        [Test]
        public void Build_WithAColliderLayer_LeavesObjectsWithoutAColliderOnTheDefaultLayer()
        {
            FragmentSceneBuildResult scene = BuildOnLayer(FragmentImportMode.HierarchyPerBody, PickingLayer);

            var colliding = new HashSet<GameObject>(CollidingObjects(scene.Root));
            GameObject[] untouched = Descendants(scene.Root)
                .Where(target => !colliding.Contains(target)).ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(untouched, Is.Not.Empty);
                Assert.That(untouched.Where(target => target.layer != DefaultLayer), Is.Empty);
            });
        }

        [Test]
        public void Build_WithPickingDisabled_LeavesEveryObjectOnTheDefaultLayer()
        {
            var options = new FragmentSceneBuildOptions
            {
                Mode = FragmentImportMode.HierarchyPerBody,
                EnablePicking = false,
                ColliderLayer = PickingLayer
            };

            FragmentSceneBuildResult scene = FragmentSceneBuilder.Build(TreeResult(), options, null);

            Assert.That(Descendants(scene.Root).Where(target => target.layer != DefaultLayer), Is.Empty);
        }

        [TestCase(FragmentImportMode.HierarchyPerBody, true)]
        [TestCase(FragmentImportMode.HierarchyPerElement, true)]
        [TestCase(FragmentImportMode.Instanced, true)]
        [TestCase(FragmentImportMode.HierarchyPerStorey, false)]
        [TestCase(FragmentImportMode.MergedWholeModel, false)]
        public void Build_RecordsWhetherTheModeGivesEachElementItsOwnObject(
            FragmentImportMode mode, bool elementGranular)
        {
            FragmentSceneBuildResult scene = Build(mode);

            Assert.That(
                scene.Root.GetComponent<FragmentVisibilityIndex>().SupportsElementFiltering,
                Is.EqualTo(elementGranular));
        }

        [Test]
        public void Build_MergedModeFallingBackToFlat_StillReportsElementGranularity()
        {
            FragmentImportResult result = TreeResult();
            result.SpatialRoot = null;

            FragmentSceneBuildResult scene = FragmentSceneBuilder.Build(
                result, FragmentSceneFixture.Options(FragmentImportMode.HierarchyPerStorey), null);

            Assert.That(
                scene.Root.GetComponent<FragmentVisibilityIndex>().SupportsElementFiltering, Is.True);
        }

        [Test]
        public void Build_Instanced_SpawnsOneObjectPerBodyOverSharedMeshes()
        {
            FragmentSceneBuildResult scene = Build(FragmentImportMode.Instanced);

            Assert.Multiple(() =>
            {
                Assert.That(scene.Root.transform.childCount, Is.EqualTo(InstanceCount));
                Assert.That(scene.Meshes, Has.Count.EqualTo(SharedMeshCount));
                Assert.That(scene.Root.GetComponentsInChildren<MeshFilter>()
                    .Select(filter => filter.sharedMesh).Distinct().Count(), Is.EqualTo(SharedMeshCount));
            });
        }

        [Test]
        public void Build_Instanced_TurnsOnGpuInstancingForTheSharedMaterial()
        {
            FragmentSceneBuildResult scene = Build(FragmentImportMode.Instanced);

            Assert.That(scene.Materials.All(material => material.enableInstancing), Is.True);
        }

        [Test]
        public void Build_HierarchyPerBody_LeavesTheSharedMaterialUninstanced()
        {
            FragmentSceneBuildResult scene = Build(FragmentImportMode.HierarchyPerBody);

            Assert.That(scene.Materials.Any(material => material.enableInstancing), Is.False);
        }

        [Test]
        public void Build_HierarchyPerStorey_MergesTheWholeStoreyAndSpawnsNoElementNodes()
        {
            FragmentSceneBuildResult scene = Build(FragmentImportMode.HierarchyPerStorey);

            Assert.Multiple(() =>
            {
                Assert.That(scene.MergedChunks, Is.GreaterThan(0));
                Assert.That(scene.Root.GetComponentsInChildren<FragmentElementReference>()
                    .Select(reference => reference.LocalId),
                    Is.EqualTo(new[] { ProjectLocalId, StoreyLocalId }));
            });
        }

        [Test]
        public void Build_HierarchyPerElement_MergesEachElementUnderItsOwnNode()
        {
            FragmentSceneBuildResult scene = Build(FragmentImportMode.HierarchyPerElement);

            FragmentElementTable[] tables = scene.Root.GetComponentsInChildren<FragmentElementTable>();
            Assert.Multiple(() =>
            {
                Assert.That(tables, Has.Length.EqualTo(ElementNodeCount));
                Assert.That(tables.Select(table => table.gameObject.transform.parent.gameObject.name),
                    Is.EqualTo(new[] { FirstWallName, SecondWallName }));
            });
        }

        [Test]
        public void Build_HierarchyPerElement_KeepsBothBodiesOfOneElementInOneMesh()
        {
            FragmentSceneBuildResult scene = Build(FragmentImportMode.HierarchyPerElement);

            FragmentElementTable table = scene.Root.GetComponentsInChildren<FragmentElementTable>().First();
            Assert.That(table.FindLocalId(1), Is.EqualTo(FirstWallLocalId));
        }

        [Test]
        public void Build_MergedChunkOfAVolumeCategory_KeepsItsMeshButStopsRendering()
        {
            FragmentImportResult result = TreeResult();
            foreach (FragmentInstance instance in result.Instances)
            {
                instance.Category = SpaceCategory;
            }

            FragmentSceneBuildResult scene = FragmentSceneBuilder.Build(
                result, FragmentSceneFixture.Options(FragmentImportMode.MergedWholeModel), null);

            Assert.Multiple(() =>
            {
                Assert.That(scene.Root.GetComponentsInChildren<MeshFilter>(), Is.Not.Empty);
                Assert.That(scene.Root.GetComponentsInChildren<MeshRenderer>()
                    .All(renderer => renderer.enabled), Is.False);
            });
        }

        private static FragmentSceneBuildResult Build(FragmentImportMode mode)
        {
            return FragmentSceneBuilder.Build(TreeResult(), FragmentSceneFixture.Options(mode), null);
        }

        private static FragmentSceneBuildResult BuildOnLayer(FragmentImportMode mode, int colliderLayer)
        {
            var options = new FragmentSceneBuildOptions { Mode = mode, ColliderLayer = colliderLayer };
            return FragmentSceneBuilder.Build(TreeResult(), options, null);
        }

        private static GameObject[] MeshObjects(GameObject root)
        {
            return root.GetComponentsInChildren<MeshFilter>().Select(filter => filter.gameObject).ToArray();
        }

        private static GameObject[] CollidingObjects(GameObject root)
        {
            return root.GetComponentsInChildren<MeshCollider>(true)
                .Select(collider => collider.gameObject).ToArray();
        }

        private static IEnumerable<GameObject> Descendants(GameObject root)
        {
            yield return root;
            for (int child = 0; child < root.transform.childCount; child++)
            {
                foreach (GameObject descendant in Descendants(root.transform.GetChild(child).gameObject))
                {
                    yield return descendant;
                }
            }
        }

        private static FragmentImportResult TreeResult()
        {
            var result = new FragmentImportResult { ModelName = ModelName };
            result.Categories.Add(ProjectCategory);
            result.Categories.Add(StoreyCategory);
            result.Categories.Add(WallCategory);
            result.Categories.Add(WallCategory);

            result.Geometries.Add(FragmentSceneFixture.Triangle(GeometryIndex));
            result.Instances.Add(WallInstance(FirstWallLocalId));
            result.Instances.Add(WallInstance(FirstWallLocalId));
            result.Instances.Add(WallInstance(SecondWallLocalId));

            FragmentSpatialNode root = FragmentSceneFixture.Node(ProjectLocalId, ProjectCategory, ModelName);
            FragmentSpatialNode storey = FragmentSceneFixture.Node(StoreyLocalId, StoreyCategory, StoreyName);
            storey.Children.Add(FragmentSceneFixture.Node(FirstWallLocalId, WallCategory, FirstWallName));
            storey.Children.Add(FragmentSceneFixture.Node(SecondWallLocalId, WallCategory, SecondWallName));
            root.Children.Add(storey);
            result.SpatialRoot = root;
            return result;
        }

        private static FragmentInstance WallInstance(int localId)
        {
            FragmentInstance instance = FragmentSceneFixture.Instance(localId, GeometryIndex, MaterialIndex);
            instance.Category = WallCategory;
            return instance;
        }
    }
}
