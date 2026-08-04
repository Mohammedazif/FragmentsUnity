using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace FragmentsUnity.Tests
{
    [TestFixture]
    public sealed class FragmentSceneIntegrationTests
    {
        private const string Ar520RootName = "AR520";

        private const int Ar520BodyCount = 1158;
        private const int Ar520GeometryMeshCount = 128;
        private const int Ar520MaterialCount = 2;

        private const int Ar520PerBodyObjectCount = 2297;
        private const int Ar520PerBodyNodeCount = 1138;
        private const int Ar520PerElementObjectCount = 43;
        private const int Ar520PerElementNodeCount = 21;
        private const int Ar520PerElementChunkCount = 21;
        private const int Ar520PerStoreyObjectCount = 14;
        private const int Ar520PerStoreyNodeCount = 7;
        private const int Ar520PerStoreyChunkCount = 6;
        private const int Ar520InstancedObjectCount = 1159;
        private const int Ar520MergedObjectCount = 7;
        private const int Ar520MergedChunkCount = 6;

        private const int JoysonPerElementObjectCount = 4174;
        private const int JoysonPerElementNodeCount = 2062;
        private const int JoysonPerElementChunkCount = 2110;
        private const int JoysonPerElementMeshCount = 2111;
        private const int JoysonPerElementMaterialCount = 4;

        private const int NoChunks = 0;
        private const int NoNodes = 0;
        private const int RootObject = 1;

        [Test]
        public void Build_EveryMode_ProducesARootOverANonEmptyObjectGraph([Values] FragmentImportMode mode)
        {
            FragmentSceneBuildResult scene = BuildAr520(mode);

            Assert.Multiple(() =>
            {
                Assert.That(scene.Root, Is.Not.Null);
                Assert.That(scene.Root.name, Is.EqualTo(Ar520RootName));
                Assert.That(FragmentSampleModels.CountObjects(scene.Root), Is.GreaterThan(RootObject));
                Assert.That(scene.Root.GetComponentsInChildren<MeshFilter>(true), Is.Not.Empty);
                Assert.That(scene.Meshes, Is.Not.Empty);
                Assert.That(scene.Materials, Is.Not.Empty);
            });
        }

        [Test]
        public void Build_Ar520HierarchyPerBody_SpawnsEveryBodyUnderItsNamedElementNode()
        {
            FragmentSceneBuildResult scene = BuildAr520(FragmentImportMode.HierarchyPerBody);

            Assert.Multiple(() =>
            {
                Assert.That(FragmentSampleModels.CountObjects(scene.Root), Is.EqualTo(Ar520PerBodyObjectCount));
                Assert.That(scene.SpawnedNodes, Is.EqualTo(Ar520PerBodyNodeCount));
                Assert.That(scene.MergedChunks, Is.EqualTo(NoChunks));
                Assert.That(scene.Meshes, Has.Count.EqualTo(Ar520GeometryMeshCount));
                Assert.That(scene.Materials, Has.Count.EqualTo(Ar520MaterialCount));
                Assert.That(scene.Root.GetComponentsInChildren<MeshFilter>(true),
                    Has.Length.EqualTo(Ar520BodyCount));
            });
        }

        [Test]
        public void Build_Ar520HierarchyPerElement_MergesEachElementSubtreeIntoItsOwnChunks()
        {
            FragmentSceneBuildResult scene = BuildAr520(FragmentImportMode.HierarchyPerElement);

            Assert.Multiple(() =>
            {
                Assert.That(FragmentSampleModels.CountObjects(scene.Root), Is.EqualTo(Ar520PerElementObjectCount));
                Assert.That(scene.SpawnedNodes, Is.EqualTo(Ar520PerElementNodeCount));
                Assert.That(scene.MergedChunks, Is.EqualTo(Ar520PerElementChunkCount));
                Assert.That(scene.Meshes, Has.Count.EqualTo(Ar520PerElementChunkCount));
                Assert.That(scene.Materials, Has.Count.EqualTo(Ar520MaterialCount));
                Assert.That(scene.Root.GetComponentsInChildren<FragmentElementTable>(true),
                    Has.Length.EqualTo(Ar520PerElementChunkCount));
            });
        }

        [Test]
        public void Build_Ar520HierarchyPerStorey_MergesTheWholeStoreyIntoOneChunkPerBucket()
        {
            FragmentSceneBuildResult scene = BuildAr520(FragmentImportMode.HierarchyPerStorey);

            Assert.Multiple(() =>
            {
                Assert.That(FragmentSampleModels.CountObjects(scene.Root), Is.EqualTo(Ar520PerStoreyObjectCount));
                Assert.That(scene.SpawnedNodes, Is.EqualTo(Ar520PerStoreyNodeCount));
                Assert.That(scene.MergedChunks, Is.EqualTo(Ar520PerStoreyChunkCount));
                Assert.That(scene.Meshes, Has.Count.EqualTo(Ar520PerStoreyChunkCount));
                Assert.That(scene.Materials, Has.Count.EqualTo(Ar520MaterialCount));
            });
        }

        [Test]
        public void Build_Ar520Instanced_SpawnsEveryBodyFlatOverTheSharedGeometryMeshes()
        {
            FragmentSceneBuildResult scene = BuildAr520(FragmentImportMode.Instanced);

            Assert.Multiple(() =>
            {
                Assert.That(FragmentSampleModels.CountObjects(scene.Root), Is.EqualTo(Ar520InstancedObjectCount));
                Assert.That(scene.Root.transform.childCount, Is.EqualTo(Ar520BodyCount));
                Assert.That(scene.SpawnedNodes, Is.EqualTo(NoNodes));
                Assert.That(scene.MergedChunks, Is.EqualTo(NoChunks));
                Assert.That(scene.Meshes, Has.Count.EqualTo(Ar520GeometryMeshCount));
                Assert.That(scene.Materials, Has.Count.EqualTo(Ar520MaterialCount));
                Assert.That(scene.Materials.All(material => material.enableInstancing), Is.True);
            });
        }

        [Test]
        public void Build_Ar520MergedWholeModel_ParentsOneChunkPerMaterialBucketUnderTheRoot()
        {
            FragmentSceneBuildResult scene = BuildAr520(FragmentImportMode.MergedWholeModel);

            Assert.Multiple(() =>
            {
                Assert.That(FragmentSampleModels.CountObjects(scene.Root), Is.EqualTo(Ar520MergedObjectCount));
                Assert.That(scene.Root.transform.childCount, Is.EqualTo(Ar520MergedChunkCount));
                Assert.That(scene.SpawnedNodes, Is.EqualTo(NoNodes));
                Assert.That(scene.MergedChunks, Is.EqualTo(Ar520MergedChunkCount));
                Assert.That(scene.Meshes, Has.Count.EqualTo(Ar520MergedChunkCount));
                Assert.That(scene.Materials, Has.Count.EqualTo(Ar520MaterialCount));
            });
        }

        [Test]
        public void Build_JoysonHierarchyPerElement_SpawnsTheSecondModelWithEverySurfaceMaterial()
        {
            FragmentSceneBuildResult scene = FragmentSampleModels.BuildOrIgnore(
                FragmentSampleModels.JoysonFileName, FragmentImportMode.HierarchyPerElement);

            Assert.Multiple(() =>
            {
                Assert.That(FragmentSampleModels.CountObjects(scene.Root), Is.EqualTo(JoysonPerElementObjectCount));
                Assert.That(scene.SpawnedNodes, Is.EqualTo(JoysonPerElementNodeCount));
                Assert.That(scene.MergedChunks, Is.EqualTo(JoysonPerElementChunkCount));
                Assert.That(scene.Meshes, Has.Count.EqualTo(JoysonPerElementMeshCount));
                Assert.That(scene.Materials, Has.Count.EqualTo(JoysonPerElementMaterialCount));
            });
        }

        [Test]
        public void Build_EveryMode_RegistersEveryRenderedMeshAsASubAssetExactlyOnce(
            [Values] FragmentImportMode mode)
        {
            FragmentSceneBuildResult scene = BuildAr520(mode);

            Mesh[] rendered = RenderedMeshes(scene);

            Assert.Multiple(() =>
            {
                Assert.That(rendered, Is.Not.Empty);
                Assert.That(rendered, Has.None.Null);
                Assert.That(scene.Meshes, Is.Unique);
                Assert.That(rendered.Distinct(), Is.EquivalentTo(scene.Meshes));
            });
        }

        [Test]
        public void Build_EveryMode_RegistersEveryRenderedMaterialAsASubAssetExactlyOnce(
            [Values] FragmentImportMode mode)
        {
            FragmentSceneBuildResult scene = BuildAr520(mode);

            Material[] rendered = RenderedMaterials(scene);

            Assert.Multiple(() =>
            {
                Assert.That(rendered, Is.Not.Empty);
                Assert.That(rendered, Has.None.Null);
                Assert.That(scene.Materials, Is.Unique);
                Assert.That(rendered.Distinct(), Is.EquivalentTo(scene.Materials));
            });
        }

        [Test]
        public void Build_JoysonHierarchyPerElement_RegistersEverySubAssetOfTheSecondModelExactlyOnce()
        {
            FragmentSceneBuildResult scene = FragmentSampleModels.BuildOrIgnore(
                FragmentSampleModels.JoysonFileName, FragmentImportMode.HierarchyPerElement);

            Assert.Multiple(() =>
            {
                Assert.That(scene.Meshes, Is.Unique);
                Assert.That(RenderedMeshes(scene).Distinct(), Is.EquivalentTo(scene.Meshes));
                Assert.That(scene.Materials, Is.Unique);
                Assert.That(RenderedMaterials(scene).Distinct(), Is.EquivalentTo(scene.Materials));
            });
        }

        [Test]
        public void Build_EveryModeWithPickingEnabled_GivesEveryRenderedMeshACollider(
            [Values] FragmentImportMode mode)
        {
            FragmentSceneBuildResult scene = BuildAr520(mode);

            MeshFilter[] filters = scene.Root.GetComponentsInChildren<MeshFilter>(true);
            MeshCollider[] colliders = scene.Root.GetComponentsInChildren<MeshCollider>(true);

            Assert.Multiple(() =>
            {
                Assert.That(colliders, Has.Length.EqualTo(filters.Length));
                Assert.That(colliders.Select(collider => collider.sharedMesh),
                    Is.EqualTo(filters.Select(filter => filter.sharedMesh)));
            });
        }

        [Test]
        public void Build_EveryModeWithPickingDisabled_LeavesNoColliderAnywhere(
            [Values] FragmentImportMode mode)
        {
            FragmentSceneBuildResult scene = FragmentSampleModels.BuildWithoutPickingOrIgnore(
                FragmentSampleModels.Ar520FileName, mode);

            Assert.Multiple(() =>
            {
                Assert.That(scene.Root.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(scene.Root.GetComponentsInChildren<MeshFilter>(true), Is.Not.Empty);
            });
        }

        private static Mesh[] RenderedMeshes(FragmentSceneBuildResult scene)
        {
            return scene.Root.GetComponentsInChildren<MeshFilter>(true)
                .Select(filter => filter.sharedMesh)
                .ToArray();
        }

        private static Material[] RenderedMaterials(FragmentSceneBuildResult scene)
        {
            return scene.Root.GetComponentsInChildren<MeshRenderer>(true)
                .Select(renderer => renderer.sharedMaterial)
                .ToArray();
        }

        private static FragmentSceneBuildResult BuildAr520(FragmentImportMode mode)
        {
            return FragmentSampleModels.BuildOrIgnore(FragmentSampleModels.Ar520FileName, mode);
        }
    }
}
