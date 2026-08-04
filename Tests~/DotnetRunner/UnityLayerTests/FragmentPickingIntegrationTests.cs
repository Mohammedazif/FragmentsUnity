using NUnit.Framework;
using UnityEngine;

namespace FragmentsUnity.Tests
{
    [TestFixture]
    public sealed class FragmentPickingIntegrationTests
    {
        private const int Ar520BodyColliderCount = 1158;
        private const int FirstTriangle = 0;
        private const int ArbitraryTriangle = 7;
        private const int FarTriangle = 100000;
        private const int NoNextPart = -1;

        [Test]
        public void Picking_MergedMode_ResolvesEveryTriangleOfAPartToThatPartsElement(
            [Values(
                FragmentImportMode.MergedWholeModel,
                FragmentImportMode.HierarchyPerStorey,
                FragmentImportMode.HierarchyPerElement)]
            FragmentImportMode mode)
        {
            AssertMergedPartPicking(FragmentSampleModels.Ar520FileName, mode);
        }

        [Test]
        public void Picking_JoysonMergedWholeModel_ResolvesEveryTriangleOfAPartToThatPartsElement()
        {
            AssertMergedPartPicking(FragmentSampleModels.JoysonFileName, FragmentImportMode.MergedWholeModel);
        }

        [Test]
        public void Picking_PerObjectMode_ResolvesEveryColliderToItsOwnElementMetadata(
            [Values(FragmentImportMode.HierarchyPerBody, FragmentImportMode.Instanced)]
            FragmentImportMode mode)
        {
            FragmentSceneBuildResult scene = FragmentSampleModels.BuildAsImportedOrIgnore(
                FragmentSampleModels.Ar520FileName, mode);
            FragmentImportResult parsed = FragmentSampleModels.LoadOrIgnore(FragmentSampleModels.Ar520FileName);

            MeshCollider[] colliders = scene.Root.GetComponentsInChildren<MeshCollider>(true);
            int resolvedToParsedMetadata = 0;
            foreach (MeshCollider collider in colliders)
            {
                if (ResolvesToItsOwnParsedElement(collider, parsed))
                {
                    resolvedToParsedMetadata++;
                }
            }

            Assert.Multiple(() =>
            {
                Assert.That(scene.Root.GetComponentsInChildren<FragmentElementTable>(true), Is.Empty);
                Assert.That(colliders, Has.Length.EqualTo(Ar520BodyColliderCount));
                Assert.That(resolvedToParsedMetadata, Is.EqualTo(Ar520BodyColliderCount));
            });
        }

        [Test]
        public void Picking_PerObjectMode_ResolvesTheSameElementWhicheverTriangleWasHit(
            [Values(FragmentImportMode.HierarchyPerBody, FragmentImportMode.Instanced)]
            FragmentImportMode mode)
        {
            FragmentSceneBuildResult scene = FragmentSampleModels.BuildAsImportedOrIgnore(
                FragmentSampleModels.Ar520FileName, mode);

            MeshCollider collider = scene.Root.GetComponentsInChildren<MeshCollider>(true)[0];
            int localId = collider.gameObject.GetComponent<FragmentElementReference>().LocalId;

            Assert.Multiple(() =>
            {
                Assert.That(ResolveLocalId(collider, FirstTriangle), Is.EqualTo(localId));
                Assert.That(ResolveLocalId(collider, ArbitraryTriangle), Is.EqualTo(localId));
                Assert.That(ResolveLocalId(collider, FarTriangle), Is.EqualTo(localId));
            });
        }

        private static void AssertMergedPartPicking(string fileName, FragmentImportMode mode)
        {
            FragmentSceneBuildResult scene = FragmentSampleModels.BuildAsImportedOrIgnore(fileName, mode);
            FragmentImportResult parsed = FragmentSampleModels.LoadOrIgnore(fileName);

            foreach (FragmentElementTable table in scene.Root.GetComponentsInChildren<FragmentElementTable>(true))
            {
                int partLocalId = table.FindLocalId(FirstTriangle);
                int nextPartFirstTriangle = FindNextPartFirstTriangle(
                    table, FragmentSampleModels.CountTriangles(table.gameObject), partLocalId);
                if (nextPartFirstTriangle == NoNextPart)
                {
                    continue;
                }

                int nextPartLocalId = table.FindLocalId(nextPartFirstTriangle);
                FragmentItemMetadata part = parsed.FindItem(partLocalId);
                FragmentItemMetadata nextPart = parsed.FindItem(nextPartLocalId);
                if (part == null || nextPart == null)
                {
                    continue;
                }

                Collider collider = table.gameObject.GetComponent<MeshCollider>();
                int lastTriangleOfPart = nextPartFirstTriangle - 1;
                int middleTriangleOfPart = lastTriangleOfPart / 2;

                Assert.Multiple(() =>
                {
                    Assert.That(nextPartLocalId, Is.Not.EqualTo(partLocalId));
                    Assert.That(ResolveLocalId(collider, middleTriangleOfPart), Is.EqualTo(partLocalId));
                    Assert.That(ResolveLocalId(collider, FirstTriangle), Is.EqualTo(partLocalId));
                    Assert.That(ResolveLocalId(collider, lastTriangleOfPart), Is.EqualTo(partLocalId));
                    Assert.That(ResolveLocalId(collider, nextPartFirstTriangle), Is.EqualTo(nextPartLocalId));
                    AssertMetadataMatchesParser(collider, middleTriangleOfPart, part);
                    AssertMetadataMatchesParser(collider, nextPartFirstTriangle, nextPart);
                });
                return;
            }

            Assert.Fail($"No merged chunk of '{fileName}' in {mode} holds two neighbouring elements to pick between.");
        }

        private static void AssertMetadataMatchesParser(
            Collider collider, int triangleIndex, FragmentItemMetadata parsedItem)
        {
            bool found = FragmentPicker.TryGetMetadata(
                FragmentSampleModels.Hit(collider, triangleIndex), out FragmentItemMetadata metadata);

            Assert.That(found, Is.True,
                $"Triangle {triangleIndex} of {collider.gameObject.name} resolved to no metadata.");
            Assert.That(metadata?.LocalId, Is.EqualTo(parsedItem.LocalId));
            Assert.That(metadata?.GlobalId, Is.EqualTo(parsedItem.GlobalId));
            Assert.That(metadata?.Category, Is.EqualTo(parsedItem.Category));
        }

        private static bool ResolvesToItsOwnParsedElement(MeshCollider collider, FragmentImportResult parsed)
        {
            FragmentElementReference reference = collider.gameObject.GetComponent<FragmentElementReference>();
            if (reference == null || ResolveLocalId(collider, ArbitraryTriangle) != reference.LocalId)
            {
                return false;
            }

            FragmentItemMetadata parsedItem = parsed.FindItem(reference.LocalId);
            return parsedItem != null
                && FragmentPicker.TryGetMetadata(
                    FragmentSampleModels.Hit(collider, ArbitraryTriangle), out FragmentItemMetadata metadata)
                && metadata.LocalId == parsedItem.LocalId
                && metadata.GlobalId == parsedItem.GlobalId
                && metadata.Category == parsedItem.Category;
        }

        private static int FindNextPartFirstTriangle(
            FragmentElementTable table, int triangleCount, int partLocalId)
        {
            for (int triangle = FirstTriangle + 1; triangle < triangleCount; triangle++)
            {
                if (table.FindLocalId(triangle) != partLocalId)
                {
                    return triangle;
                }
            }
            return NoNextPart;
        }

        private static int ResolveLocalId(Collider collider, int triangleIndex)
        {
            FragmentPicker.TryGetLocalId(FragmentSampleModels.Hit(collider, triangleIndex), out int localId);
            return localId;
        }
    }
}
