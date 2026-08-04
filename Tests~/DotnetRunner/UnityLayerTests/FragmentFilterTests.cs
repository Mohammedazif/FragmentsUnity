using NUnit.Framework;
using UnityEngine;

namespace FragmentsUnity.Tests
{
    [TestFixture]
    public sealed class FragmentFilterTests
    {
        private const int WallLocalId = 7;
        private const int DoorLocalId = 8;
        private const int StoreyLocalId = 9;
        private const string WallCategory = "IfcWall";
        private const string DoorCategory = "IfcDoor";
        private const string StoreyName = "Level 1";
        private const string FireRatingName = "FireRating";
        private const string FireRatingValue = "REI 60";
        private const string MergedWarningFragment = "merged mode, so filtering can only isolate whole chunks";
        private const string ConsolePrefix = "[FragmentsUnity] ";
        private const int OneWarning = 1;

        private GameObject _root;
        private FragmentFilter _filter;
        private FragmentVisibilityIndex _index;
        private GameObject _wall;
        private GameObject _door;

        [SetUp]
        public void SetUp()
        {
            Debug.capturedWarnings.Clear();

            _root = new GameObject("Model");
            _root.AddComponent<FragmentModel>().SetAsset(FragmentModelAsset.Create(BuildResult()));
            _index = _root.AddComponent<FragmentVisibilityIndex>();
            _index.SetElementGranular(true);
            _filter = _root.AddComponent<FragmentFilter>();

            _wall = Element("Wall", WallLocalId);
            _door = Element("Door", DoorLocalId);
        }

        [Test]
        public void IsolateByCategory_HidesEveryOtherCategory()
        {
            _filter.IsolateByCategory(WallCategory);

            Assert.That(IsVisible(_wall), Is.True);
            Assert.That(IsVisible(_door), Is.False);
        }

        [Test]
        public void IsolateByStorey_KeepsTheStoreyContents()
        {
            _filter.IsolateByStorey(StoreyName);

            Assert.That(IsVisible(_wall), Is.True);
            Assert.That(IsVisible(_door), Is.False);
        }

        [Test]
        public void IsolateByAttribute_KeepsTheMatchingElements()
        {
            _filter.IsolateByAttribute(FireRatingName, FireRatingValue, true);

            Assert.That(IsVisible(_wall), Is.False);
            Assert.That(IsVisible(_door), Is.True);
        }

        [Test]
        public void IsolateLocalIds_KeepsOnlyThoseIds()
        {
            _filter.IsolateLocalIds(new[] { DoorLocalId });

            Assert.That(IsVisible(_wall), Is.False);
            Assert.That(IsVisible(_door), Is.True);
        }

        [Test]
        public void SetVisibleLocalIds_False_HidesOnlyThoseIds()
        {
            _filter.SetVisibleLocalIds(new[] { WallLocalId }, false);

            Assert.That(IsVisible(_wall), Is.False);
            Assert.That(IsVisible(_door), Is.True);
        }

        [Test]
        public void SetVisibleLocalIds_True_ShowsThemAgain()
        {
            _filter.SetVisibleLocalIds(new[] { WallLocalId }, false);
            _filter.SetVisibleLocalIds(new[] { WallLocalId }, true);

            Assert.That(IsVisible(_wall), Is.True);
            Assert.That(_index.IsFilterActive, Is.False);
        }

        [Test]
        public void SetVisibleLocalIds_OnAMergedScene_WarnsThatOnlyWholeChunksHide()
        {
            _index.SetElementGranular(false);
            Debug.capturedWarnings.Clear();

            _filter.SetVisibleLocalIds(new[] { WallLocalId }, false);

            Assert.That(Debug.capturedWarnings, Has.Count.EqualTo(OneWarning));
        }

        [Test]
        public void IsolateByStorey_OnAMergedScene_WarnsOncePerCallThatOnlyWholeChunksIsolate()
        {
            _index.SetElementGranular(false);
            Debug.capturedWarnings.Clear();

            _filter.IsolateByStorey(StoreyName);

            Assert.That(Debug.capturedWarnings, Has.Count.EqualTo(OneWarning));
            Assert.That(Debug.capturedWarnings[0], Does.StartWith(ConsolePrefix));
            Assert.That(Debug.capturedWarnings[0], Does.Contain(MergedWarningFragment));
        }

        [Test]
        public void IsolateByStorey_OnAnElementGranularScene_WarnsAboutNothing()
        {
            _filter.IsolateByStorey(StoreyName);

            Assert.That(Debug.capturedWarnings, Is.Empty);
        }

        [Test]
        public void SetCategoryVisible_False_HidesThatCategoryOnly()
        {
            _filter.SetCategoryVisible(WallCategory, false);

            Assert.That(IsVisible(_wall), Is.False);
            Assert.That(IsVisible(_door), Is.True);
        }

        [Test]
        public void SetStoreyVisible_False_HidesTheStoreyContents()
        {
            _filter.SetStoreyVisible(StoreyName, false);

            Assert.That(IsVisible(_wall), Is.False);
            Assert.That(_index.IsHidden(WallLocalId), Is.True);
        }

        [Test]
        public void ClearFilter_RestoresEverything()
        {
            _filter.IsolateByCategory(WallCategory);
            _filter.ClearFilter();

            Assert.That(IsVisible(_wall), Is.True);
            Assert.That(IsVisible(_door), Is.True);
            Assert.That(_index.IsFilterActive, Is.False);
        }

        [Test]
        public void IsolateByCategory_UnknownCategory_HidesEverything()
        {
            _filter.IsolateByCategory("IfcBeam");

            Assert.That(IsVisible(_wall), Is.False);
            Assert.That(IsVisible(_door), Is.False);
        }

        [Test]
        public void IsolateByCategory_WithoutAnIndex_ChangesNothing()
        {
            var bare = new GameObject("Model");
            bare.AddComponent<FragmentModel>();

            Assert.DoesNotThrow(() => bare.AddComponent<FragmentFilter>().IsolateByCategory(WallCategory));
        }

        [Test]
        public void IsolateByCategory_WithoutAModel_ChangesNothing()
        {
            var bare = new GameObject("Model");
            bare.AddComponent<FragmentVisibilityIndex>().Register(WallLocalId, _wall);

            bare.AddComponent<FragmentFilter>().IsolateByCategory(WallCategory);

            Assert.That(IsVisible(_wall), Is.True);
        }

        [Test]
        public void ClearFilter_WithoutAnIndex_ChangesNothing()
        {
            var bare = new GameObject("Model");

            Assert.DoesNotThrow(() => bare.AddComponent<FragmentFilter>().ClearFilter());
        }

        private static FragmentImportResult BuildResult()
        {
            var result = new FragmentImportResult();

            var wall = new FragmentItemMetadata
            {
                LocalId = WallLocalId,
                Category = WallCategory,
                Name = "Wall",
                StoreyName = StoreyName
            };

            var door = new FragmentItemMetadata
            {
                LocalId = DoorLocalId,
                Category = DoorCategory,
                Name = "Door"
            };
            door.Attributes.Add(new FragmentAttribute(FireRatingName, FireRatingValue, "IfcLabel"));

            var storey = new FragmentItemMetadata
            {
                LocalId = StoreyLocalId,
                Category = "IFCBUILDINGSTOREY",
                Name = StoreyName
            };

            AddItem(result, wall);
            AddItem(result, door);
            AddItem(result, storey);
            return result;
        }

        private static void AddItem(FragmentImportResult result, FragmentItemMetadata item)
        {
            while (result.Items.Count <= item.LocalId)
            {
                result.Items.Add(null);
            }
            result.Items[item.LocalId] = item;
            result.Instances.Add(new FragmentInstance { LocalId = item.LocalId });
        }

        private static bool IsVisible(GameObject element)
        {
            return !element.GetComponent<MeshRenderer>().forceRenderingOff;
        }

        private GameObject Element(string label, int localId)
        {
            var element = new GameObject(label);
            element.transform.SetParent(_root.transform, false);
            element.AddComponent<FragmentElementReference>().LocalId = localId;
            element.AddComponent<MeshRenderer>();
            element.AddComponent<MeshCollider>();
            _index.Register(localId, element);
            return element;
        }
    }
}
