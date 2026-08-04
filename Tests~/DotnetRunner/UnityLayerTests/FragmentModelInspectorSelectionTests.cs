using FragmentsUnity.Editor;
using NUnit.Framework;
using UnityEngine;

namespace FragmentsUnity.Tests
{
    [TestFixture]
    public sealed class FragmentModelInspectorSelectionTests
    {
        private const string WallCategory = "IFCWALL";
        private const string WallName = "Exterior Wall";
        private const int WallLocalId = 5;
        private const int UnknownLocalId = 99;

        [Test]
        public void ResolveSelection_ElementUnderTheModel_ReturnsItsMetadata()
        {
            FragmentModel model = ModelWithWall(out GameObject element);

            FragmentItemMetadata item = FragmentModelInspector.ResolveSelection(element, model);

            Assert.Multiple(() =>
            {
                Assert.That(item, Is.Not.Null);
                Assert.That(item.LocalId, Is.EqualTo(WallLocalId));
                Assert.That(item.Name, Is.EqualTo(WallName));
            });
        }

        [Test]
        public void ResolveSelection_MeshChildOfTheElement_WalksUpToTheElement()
        {
            FragmentModel model = ModelWithWall(out GameObject element);
            var body = new GameObject("body");
            body.transform.SetParent(element.transform);

            Assert.That(FragmentModelInspector.ResolveSelection(body, model)?.LocalId, Is.EqualTo(WallLocalId));
        }

        [Test]
        public void ResolveSelection_DeactivatedElement_StillResolves()
        {
            FragmentModel model = ModelWithWall(out GameObject element);
            element.SetActive(false);

            Assert.That(FragmentModelInspector.ResolveSelection(element, model), Is.Not.Null);
        }

        [Test]
        public void ResolveSelection_ElementOfAnotherModel_ReturnsNull()
        {
            FragmentModel first = ModelWithWall(out GameObject element);
            ModelWithWall(out GameObject otherElement);

            Assert.Multiple(() =>
            {
                Assert.That(FragmentModelInspector.ResolveSelection(otherElement, first), Is.Null);
                Assert.That(FragmentModelInspector.ResolveSelection(element, first), Is.Not.Null);
            });
        }

        [Test]
        public void ResolveSelection_ObjectWithoutAnElementReference_ReturnsNull()
        {
            FragmentModel model = ModelWithWall(out GameObject element);

            Assert.That(FragmentModelInspector.ResolveSelection(element.transform.root.gameObject, model), Is.Null);
        }

        [Test]
        public void ResolveSelection_ElementIdMissingFromTheModel_ReturnsNull()
        {
            FragmentModel model = ModelWithWall(out GameObject element);
            element.GetComponent<FragmentElementReference>().LocalId = UnknownLocalId;

            Assert.That(FragmentModelInspector.ResolveSelection(element, model), Is.Null);
        }

        [Test]
        public void ResolveSelection_NullArguments_ReturnNull()
        {
            FragmentModel model = ModelWithWall(out GameObject element);

            Assert.Multiple(() =>
            {
                Assert.That(FragmentModelInspector.ResolveSelection(null, model), Is.Null);
                Assert.That(FragmentModelInspector.ResolveSelection(element, null), Is.Null);
            });
        }

        private static FragmentModel ModelWithWall(out GameObject element)
        {
            FragmentItemMetadata wall = FragmentModelFixture.Item(WallLocalId, WallCategory, WallName);

            var root = new GameObject("model");
            FragmentModel model = root.AddComponent<FragmentModel>();
            model.SetAsset(FragmentModelAsset.Create(FragmentModelFixture.ResultWith(new[] { wall })));

            element = new GameObject(WallName);
            element.transform.SetParent(root.transform);
            element.AddComponent<FragmentElementReference>().LocalId = WallLocalId;

            return model;
        }
    }
}
