using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace FragmentsUnity.Tests
{
    [TestFixture]
    public sealed class FragmentVisibilityIndexTests
    {
        private const int WallLocalId = 7;
        private const int DoorLocalId = 8;
        private const int SpaceLocalId = 9;

        private const string LocalIdsFieldName = "_localIds";
        private const string TargetsFieldName = "_targets";

        private GameObject _root;
        private FragmentVisibilityIndex _index;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Model");
            _index = _root.AddComponent<FragmentVisibilityIndex>();
        }

        [Test]
        public void HasEntries_NothingRegistered_IsFalse()
        {
            Assert.That(_index.HasEntries, Is.False);
        }

        [Test]
        public void HasEntries_AfterRegistering_IsTrue()
        {
            _index.Register(WallLocalId, Element("Wall"));

            Assert.That(_index.HasEntries, Is.True);
        }

        [Test]
        public void SupportsElementFiltering_UntilABuildRecordsOtherwise_IsFalse()
        {
            Assert.That(_index.SupportsElementFiltering, Is.False);
        }

        [Test]
        public void SetElementGranular_True_ReportsElementFilteringSupport()
        {
            _index.SetElementGranular(true);

            Assert.That(_index.SupportsElementFiltering, Is.True);
        }

        [Test]
        public void Register_NegativeLocalId_IsIgnored()
        {
            _index.Register(-1, Element("Wall"));

            Assert.That(_index.HasEntries, Is.False);
        }

        [Test]
        public void Register_NullTarget_IsIgnored()
        {
            _index.Register(WallLocalId, null);

            Assert.That(_index.HasEntries, Is.False);
        }

        [Test]
        public void SetVisible_False_StopsTheRendererDrawingAndDisablesTheCollider()
        {
            GameObject wall = Element("Wall");
            _index.Register(WallLocalId, wall);

            _index.SetVisible(new[] { WallLocalId }, false);

            Assert.That(wall.GetComponent<MeshRenderer>().forceRenderingOff, Is.True);
            Assert.That(wall.GetComponent<MeshCollider>().enabled, Is.False);
        }

        [Test]
        public void SetVisible_False_LeavesTheObjectActiveSoMetadataStillResolves()
        {
            GameObject wall = Element("Wall");
            _index.Register(WallLocalId, wall);

            _index.SetVisible(new[] { WallLocalId }, false);

            Assert.That(wall.activeSelf, Is.True);
        }

        [Test]
        public void SetVisible_True_RestoresRendererAndCollider()
        {
            GameObject wall = Element("Wall");
            _index.Register(WallLocalId, wall);

            _index.SetVisible(new[] { WallLocalId }, false);
            _index.SetVisible(new[] { WallLocalId }, true);

            Assert.That(wall.GetComponent<MeshRenderer>().forceRenderingOff, Is.False);
            Assert.That(wall.GetComponent<MeshCollider>().enabled, Is.True);
        }

        [Test]
        public void SetVisible_True_LeavesARendererThatImportedDisabledAlone()
        {
            GameObject space = Element("IfcSpace");
            space.GetComponent<MeshRenderer>().enabled = false;
            _index.Register(SpaceLocalId, space);

            _index.SetVisible(new[] { SpaceLocalId }, false);
            _index.SetVisible(new[] { SpaceLocalId }, true);

            Assert.That(space.GetComponent<MeshRenderer>().enabled, Is.False);
        }

        [Test]
        public void SetVisible_False_TracksTheHiddenLocalId()
        {
            _index.Register(WallLocalId, Element("Wall"));

            _index.SetVisible(new[] { WallLocalId }, false);

            Assert.That(_index.IsHidden(WallLocalId), Is.True);
            Assert.That(_index.IsFilterActive, Is.True);
        }

        [Test]
        public void SetVisible_UnknownLocalId_IsIgnored()
        {
            _index.Register(WallLocalId, Element("Wall"));

            _index.SetVisible(new[] { DoorLocalId }, false);

            Assert.That(_index.IsHidden(DoorLocalId), Is.False);
        }

        [Test]
        public void SetVisible_NullIds_IsIgnored()
        {
            _index.Register(WallLocalId, Element("Wall"));

            _index.SetVisible(null, false);

            Assert.That(_index.IsFilterActive, Is.False);
        }

        [Test]
        public void SetVisible_ElementWithManyObjects_HidesAllOfThem()
        {
            GameObject firstBody = Element("Wall_body1");
            GameObject secondBody = Element("Wall_body2");
            _index.Register(WallLocalId, firstBody);
            _index.Register(WallLocalId, secondBody);

            _index.SetVisible(new[] { WallLocalId }, false);

            Assert.That(firstBody.GetComponent<MeshRenderer>().forceRenderingOff, Is.True);
            Assert.That(secondBody.GetComponent<MeshRenderer>().forceRenderingOff, Is.True);
        }

        [Test]
        public void SetVisible_HierarchyNode_HidesTheRenderersBelowIt()
        {
            GameObject storey = new GameObject("Storey");
            storey.transform.SetParent(_root.transform, false);
            GameObject wall = Element("Wall");
            wall.transform.SetParent(storey.transform, false);
            _index.Register(WallLocalId, storey);

            _index.SetVisible(new[] { WallLocalId }, false);

            Assert.That(wall.GetComponent<MeshRenderer>().forceRenderingOff, Is.True);
            Assert.That(storey.activeSelf, Is.True);
        }

        [Test]
        public void SetVisible_NodeWithoutAnyRendererOrCollider_FallsBackToDeactivation()
        {
            var emptyNode = new GameObject("Storey");
            emptyNode.transform.SetParent(_root.transform, false);
            _index.Register(WallLocalId, emptyNode);

            _index.SetVisible(new[] { WallLocalId }, false);

            Assert.That(emptyNode.activeSelf, Is.False);
        }

        [Test]
        public void SetVisible_True_ReactivatesADeactivatedNode()
        {
            var emptyNode = new GameObject("Storey");
            emptyNode.transform.SetParent(_root.transform, false);
            _index.Register(WallLocalId, emptyNode);

            _index.SetVisible(new[] { WallLocalId }, false);
            _index.SetVisible(new[] { WallLocalId }, true);

            Assert.That(emptyNode.activeSelf, Is.True);
        }

        [Test]
        public void Isolate_HidesEverythingElse()
        {
            GameObject wall = Element("Wall");
            GameObject door = Element("Door");
            _index.Register(WallLocalId, wall);
            _index.Register(DoorLocalId, door);

            _index.Isolate(new[] { WallLocalId });

            Assert.That(wall.GetComponent<MeshRenderer>().forceRenderingOff, Is.False);
            Assert.That(door.GetComponent<MeshRenderer>().forceRenderingOff, Is.True);
            Assert.That(_index.IsHidden(DoorLocalId), Is.True);
            Assert.That(_index.IsHidden(WallLocalId), Is.False);
        }

        [Test]
        public void Isolate_ElementSharingAMergedChunk_KeepsTheChunkVisible()
        {
            GameObject chunk = Element("MergedChunk");
            _index.Register(WallLocalId, chunk);
            _index.Register(DoorLocalId, chunk);

            _index.Isolate(new[] { WallLocalId });

            Assert.That(chunk.GetComponent<MeshRenderer>().forceRenderingOff, Is.False);
        }

        [Test]
        public void Isolate_EmptySelection_HidesEverything()
        {
            GameObject wall = Element("Wall");
            _index.Register(WallLocalId, wall);

            _index.Isolate(null);

            Assert.That(wall.GetComponent<MeshRenderer>().forceRenderingOff, Is.True);
        }

        [Test]
        public void Isolate_AfterAnEarlierIsolate_ForgetsTheOldHiddenSet()
        {
            _index.Register(WallLocalId, Element("Wall"));
            _index.Register(DoorLocalId, Element("Door"));

            _index.Isolate(new[] { WallLocalId });
            _index.Isolate(new[] { DoorLocalId });

            Assert.That(_index.IsHidden(WallLocalId), Is.True);
            Assert.That(_index.IsHidden(DoorLocalId), Is.False);
        }

        [Test]
        public void Clear_RestoresEverythingItHid()
        {
            GameObject wall = Element("Wall");
            GameObject door = Element("Door");
            _index.Register(WallLocalId, wall);
            _index.Register(DoorLocalId, door);

            _index.Isolate(new[] { WallLocalId });
            _index.Clear();

            Assert.That(door.GetComponent<MeshRenderer>().forceRenderingOff, Is.False);
            Assert.That(door.GetComponent<MeshCollider>().enabled, Is.True);
            Assert.That(_index.IsFilterActive, Is.False);
        }

        [Test]
        public void Clear_AfterTheRuntimeStateWasLost_StillRestoresTheRegisteredTargets()
        {
            GameObject wall = Element("Wall");
            _index.Register(WallLocalId, wall);
            _index.SetVisible(new[] { WallLocalId }, false);

            Deserialized(WallLocalId, wall).Clear();

            Assert.That(wall.GetComponent<MeshRenderer>().forceRenderingOff, Is.False);
            Assert.That(wall.GetComponent<MeshCollider>().enabled, Is.True);
        }

        [Test]
        public void Clear_LeavesARendererThatImportedDisabledAlone()
        {
            GameObject space = Element("IfcSpace");
            space.GetComponent<MeshRenderer>().enabled = false;
            _index.Register(SpaceLocalId, space);

            _index.Isolate(new[] { WallLocalId });
            _index.Clear();

            Assert.That(space.GetComponent<MeshRenderer>().enabled, Is.False);
        }

        [Test]
        public void Clear_WithNothingHidden_IsANoOp()
        {
            GameObject wall = Element("Wall");
            _index.Register(WallLocalId, wall);

            _index.Clear();

            Assert.That(wall.GetComponent<MeshRenderer>().forceRenderingOff, Is.False);
            Assert.That(wall.GetComponent<MeshRenderer>().enabled, Is.True);
            Assert.That(_index.IsFilterActive, Is.False);
        }

        [Test]
        public void Register_AfterTheLookupWasBuilt_StillFiltersTheNewObject()
        {
            _index.Register(WallLocalId, Element("Wall"));
            _index.SetVisible(new[] { WallLocalId }, false);

            GameObject lateBody = Element("Wall_body2");
            _index.Register(WallLocalId, lateBody);
            _index.SetVisible(new[] { WallLocalId }, false);

            Assert.That(lateBody.GetComponent<MeshRenderer>().forceRenderingOff, Is.True);
        }

        // A domain reload keeps only the serialized fields, so the restore path is exercised without any runtime state.
        private static FragmentVisibilityIndex Deserialized(int localId, GameObject target)
        {
            var reloaded = new GameObject("Model").AddComponent<FragmentVisibilityIndex>();
            SerializedField(LocalIdsFieldName).SetValue(reloaded, new List<int> { localId });
            SerializedField(TargetsFieldName).SetValue(reloaded, new List<GameObject> { target });
            return reloaded;
        }

        private static FieldInfo SerializedField(string fieldName)
        {
            return typeof(FragmentVisibilityIndex).GetField(
                fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        }

        private GameObject Element(string label)
        {
            var element = new GameObject(label);
            element.transform.SetParent(_root.transform, false);
            element.AddComponent<MeshRenderer>();
            element.AddComponent<MeshCollider>();
            return element;
        }
    }
}
