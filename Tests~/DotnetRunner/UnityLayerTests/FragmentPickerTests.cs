using NUnit.Framework;
using UnityEngine;

namespace FragmentsUnity.Tests
{
    [TestFixture]
    public sealed class FragmentPickerTests
    {
        private const int WallLocalId = 7;
        private const int DoorLocalId = 8;
        private const int UnknownLocalId = -1;
        private const string WallCategory = "IfcWall";

        private static readonly int[] TriangleStarts = { 0, 12 };
        private static readonly int[] PartLocalIds = { WallLocalId, DoorLocalId };

        [Test]
        public void TryGetLocalId_MergedChunk_ReadsTheTriangleIndex()
        {
            Collider chunk = MergedChunk(ModelRoot());

            bool found = FragmentPicker.TryGetLocalId(Hit(chunk, 20), out int localId);

            Assert.That(found, Is.True);
            Assert.That(localId, Is.EqualTo(DoorLocalId));
        }

        [Test]
        public void TryGetLocalId_MergedChunkTriangleOutsideTheTable_Fails()
        {
            GameObject root = ModelRoot();
            GameObject node = ElementObject(root, WallLocalId);
            Collider chunk = MergedChunk(node);
            chunk.gameObject.GetComponent<FragmentElementTable>().SetTable(new[] { 4 }, new[] { DoorLocalId });

            bool found = FragmentPicker.TryGetLocalId(Hit(chunk, 0), out int localId);

            Assert.That(found, Is.False);
            Assert.That(localId, Is.EqualTo(UnknownLocalId));
        }

        [Test]
        public void TryGetLocalId_ElementReferenceOnTheCollider_ReturnsItsId()
        {
            Collider element = WithCollider(ElementObject(ModelRoot(), WallLocalId));

            bool found = FragmentPicker.TryGetLocalId(Hit(element, 3), out int localId);

            Assert.That(found, Is.True);
            Assert.That(localId, Is.EqualTo(WallLocalId));
        }

        [Test]
        public void TryGetLocalId_ElementReferenceOnAnInactiveParent_ReturnsItsId()
        {
            GameObject element = ElementObject(ModelRoot(), WallLocalId);
            var body = new GameObject("Wall_body1");
            body.transform.SetParent(element.transform, false);
            element.SetActive(false);

            bool found = FragmentPicker.TryGetLocalId(Hit(WithCollider(body), 3), out int localId);

            Assert.That(found, Is.True);
            Assert.That(localId, Is.EqualTo(WallLocalId));
        }

        [Test]
        public void TryGetLocalId_UnreferencedElement_Fails()
        {
            GameObject element = ElementObject(ModelRoot(), WallLocalId);
            element.GetComponent<FragmentElementReference>().LocalId = UnknownLocalId;

            bool found = FragmentPicker.TryGetLocalId(Hit(WithCollider(element), 3), out int localId);

            Assert.That(found, Is.False);
            Assert.That(localId, Is.EqualTo(UnknownLocalId));
        }

        [Test]
        public void TryGetLocalId_HitWithoutACollider_Fails()
        {
            bool found = FragmentPicker.TryGetLocalId(default, out int localId);

            Assert.That(found, Is.False);
            Assert.That(localId, Is.EqualTo(UnknownLocalId));
        }

        [Test]
        public void TryGetLocalId_ColliderOutsideAnyModel_Fails()
        {
            Collider stray = WithCollider(new GameObject("Floor"));

            bool found = FragmentPicker.TryGetLocalId(Hit(stray, 3), out int localId);

            Assert.That(found, Is.False);
        }

        [Test]
        public void TryGetMetadata_ElementUnderAModel_ResolvesTheItem()
        {
            GameObject root = ModelRoot();
            Collider element = WithCollider(ElementObject(root, WallLocalId));

            bool found = FragmentPicker.TryGetMetadata(Hit(element, 3), out FragmentItemMetadata metadata);

            Assert.That(found, Is.True);
            Assert.That(metadata.Category, Is.EqualTo(WallCategory));
        }

        [Test]
        public void TryGetMetadata_MergedChunk_ResolvesThePartUnderTheTriangle()
        {
            Collider chunk = MergedChunk(ModelRoot());

            bool found = FragmentPicker.TryGetMetadata(Hit(chunk, 12), out FragmentItemMetadata metadata);

            Assert.That(found, Is.True);
            Assert.That(metadata.LocalId, Is.EqualTo(DoorLocalId));
        }

        [Test]
        public void TryGetMetadata_HiddenElement_StillResolves()
        {
            GameObject root = ModelRoot();
            GameObject element = ElementObject(root, WallLocalId);
            Collider collider = WithCollider(element);
            root.AddComponent<FragmentVisibilityIndex>().Register(WallLocalId, element);
            root.GetComponent<FragmentVisibilityIndex>().SetVisible(new[] { WallLocalId }, false);

            bool found = FragmentPicker.TryGetMetadata(Hit(collider, 3), out FragmentItemMetadata metadata);

            Assert.That(found, Is.True);
            Assert.That(metadata.LocalId, Is.EqualTo(WallLocalId));
        }

        [Test]
        public void TryGetMetadata_WithoutAModelAbove_FailsWithNull()
        {
            var element = new GameObject("Wall");
            element.AddComponent<FragmentElementReference>().LocalId = WallLocalId;

            bool found = FragmentPicker.TryGetMetadata(Hit(WithCollider(element), 3), out FragmentItemMetadata metadata);

            Assert.That(found, Is.False);
            Assert.That(metadata, Is.Null);
        }

        [Test]
        public void TryGetMetadata_LocalIdMissingFromTheModel_FailsWithNull()
        {
            GameObject root = ModelRoot();
            GameObject element = ElementObject(root, WallLocalId);
            element.GetComponent<FragmentElementReference>().LocalId = 4242;

            bool found = FragmentPicker.TryGetMetadata(Hit(WithCollider(element), 3), out FragmentItemMetadata metadata);

            Assert.That(found, Is.False);
            Assert.That(metadata, Is.Null);
        }

        private static GameObject ModelRoot()
        {
            var root = new GameObject("Model");
            root.AddComponent<FragmentModel>().SetAsset(FragmentModelAsset.Create(ResultWithWallAndDoor()));
            return root;
        }

        private static FragmentImportResult ResultWithWallAndDoor()
        {
            var result = new FragmentImportResult();
            AddItem(result, WallLocalId, WallCategory);
            AddItem(result, DoorLocalId, "IfcDoor");
            return result;
        }

        private static void AddItem(FragmentImportResult result, int localId, string category)
        {
            while (result.Items.Count <= localId)
            {
                result.Items.Add(null);
            }
            result.Items[localId] = new FragmentItemMetadata { LocalId = localId, Category = category };
            result.Instances.Add(new FragmentInstance { LocalId = localId });
        }

        private static GameObject ElementObject(GameObject root, int localId)
        {
            var element = new GameObject("Element");
            element.transform.SetParent(root.transform, false);
            element.AddComponent<FragmentElementReference>().LocalId = localId;
            return element;
        }

        private static Collider MergedChunk(GameObject parent)
        {
            var chunk = new GameObject("MergedChunk");
            chunk.transform.SetParent(parent.transform, false);
            chunk.AddComponent<FragmentElementTable>().SetTable(TriangleStarts, PartLocalIds);
            return WithCollider(chunk);
        }

        private static Collider WithCollider(GameObject target)
        {
            return target.AddComponent<MeshCollider>();
        }

        private static RaycastHit Hit(Collider collider, int triangleIndex)
        {
            return new RaycastHit { collider = collider, triangleIndex = triangleIndex };
        }
    }
}
