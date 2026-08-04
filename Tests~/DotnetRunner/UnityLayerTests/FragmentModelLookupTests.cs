using NUnit.Framework;

namespace FragmentsUnity.Tests
{
    [TestFixture]
    public sealed class FragmentModelLookupTests
    {
        private const string WallCategory = "IFCWALL";
        private const string LowerCaseWallCategory = "ifcwall";
        private const string MixedCaseWallCategory = "IfcWall";
        private const string DoorCategory = "IFCDOOR";
        private const string StoreyCategory = "IFCBUILDINGSTOREY";
        private const string UnknownCategory = "IFCBEAM";
        private const string PartialCategory = "WALL";

        private const string FirstWallName = "Wall A";
        private const string SecondWallName = "Wall B";
        private const string DoorName = "Door A";

        private const string FirstStorey = "Level 1";
        private const string SecondStorey = "Level 2";
        private const string LowerCaseFirstStorey = "level 1";
        private const string UnknownStorey = "Level 9";

        private const string SharedGlobalId = "0aB1cD2eF3gH4iJ5kL6mN7";
        private const string SharedGlobalIdOtherCase = "0Ab1Cd2Ef3Gh4Ij5Kl6Mn7";
        private const string DoorGlobalId = "9xY8wV7uT6sR5qP4oN3mL2";
        private const string UnknownGlobalId = "zzzzzzzzzzzzzzzzzzzzzz";

        private const int FirstWallLocalId = 5;
        private const int DoorLocalId = 17;
        private const int SecondWallLocalId = 200;
        private const int StoreyLocalId = 42;
        private const int UnknownLocalId = 3;

        [Test]
        public void FindByLocalId_KnownId_ReturnsTheItem()
        {
            FragmentModel model = SampleModel();

            Assert.That(model.FindByLocalId(SecondWallLocalId).Name, Is.EqualTo(SecondWallName));
        }

        [Test]
        public void FindByLocalId_UnknownId_ReturnsNull()
        {
            FragmentModel model = SampleModel();

            Assert.That(model.FindByLocalId(UnknownLocalId), Is.Null);
        }

        [Test]
        public void FindByGlobalId_DifferentCase_ReturnsTheItem()
        {
            FragmentModel model = SampleModel();

            Assert.That(model.FindByGlobalId(DoorGlobalId.ToLowerInvariant()).Name, Is.EqualTo(DoorName));
        }

        [Test]
        public void FindByGlobalId_TwoItemsShareAnId_ReturnsTheLastOne()
        {
            FragmentModel model = SampleModel();

            Assert.That(model.FindByGlobalId(SharedGlobalId).LocalId, Is.EqualTo(SecondWallLocalId));
        }

        [Test]
        public void FindByGlobalId_UnknownOrEmptyId_ReturnsNull()
        {
            FragmentModel model = SampleModel();

            Assert.Multiple(() =>
            {
                Assert.That(model.FindByGlobalId(UnknownGlobalId), Is.Null);
                Assert.That(model.FindByGlobalId(string.Empty), Is.Null);
                Assert.That(model.FindByGlobalId(null), Is.Null);
            });
        }

        [Test]
        public void FindByCategory_DifferentCase_ReturnsEveryMatchInItemOrder()
        {
            FragmentModel model = SampleModel();

            Assert.That(model.FindByCategory(MixedCaseWallCategory),
                Is.EqualTo(new[] { FirstWallLocalId, SecondWallLocalId }));
        }

        [Test]
        public void FindByCategory_PartialCategoryName_MatchesNothing()
        {
            FragmentModel model = SampleModel();

            Assert.That(model.FindByCategory(PartialCategory), Is.Empty);
        }

        [Test]
        public void FindByCategory_UnknownCategory_ReturnsEmptyList()
        {
            FragmentModel model = SampleModel();

            Assert.That(model.FindByCategory(UnknownCategory), Is.Not.Null.And.Empty);
        }

        [Test]
        public void FindByStorey_MatchesContainedItemsAndTheStoreyItself()
        {
            FragmentModel model = SampleModel();

            Assert.That(model.FindByStorey(LowerCaseFirstStorey),
                Is.EqualTo(new[] { FirstWallLocalId, DoorLocalId, StoreyLocalId }));
        }

        [Test]
        public void FindByStorey_StoreyWithoutOwnItem_StillMatchesContainedItems()
        {
            FragmentModel model = SampleModel();

            Assert.That(model.FindByStorey(SecondStorey), Is.EqualTo(new[] { SecondWallLocalId }));
        }

        [Test]
        public void FindByStorey_UnknownStorey_ReturnsEmptyList()
        {
            FragmentModel model = SampleModel();

            Assert.That(model.FindByStorey(UnknownStorey), Is.Not.Null.And.Empty);
        }

        [Test]
        public void ModelWithoutAsset_AnswersEveryQueryWithEmptyResults()
        {
            var model = new FragmentModel();

            Assert.Multiple(() =>
            {
                Assert.That(model.ModelAsset, Is.Null);
                Assert.That(model.Data, Is.Not.Null);
                Assert.That(model.Data.Items, Is.Empty);
                Assert.That(model.FindByLocalId(FirstWallLocalId), Is.Null);
                Assert.That(model.FindByGlobalId(SharedGlobalId), Is.Null);
                Assert.That(model.FindByCategory(WallCategory), Is.Not.Null.And.Empty);
                Assert.That(model.FindByStorey(FirstStorey), Is.Not.Null.And.Empty);
                Assert.That(model.GetCategoryCounts(), Is.Not.Null.And.Empty);
                Assert.That(model.GetFlattenedValues(FirstWallLocalId), Is.Not.Null.And.Empty);
            });
        }

        [Test]
        public void Data_IsCachedAcrossQueries()
        {
            FragmentModel model = SampleModel();

            Assert.That(model.Data, Is.SameAs(model.Data));
        }

        [Test]
        public void Data_AfterTheAssetFieldIsReassigned_IsReloadedRatherThanServedFromTheCache()
        {
            FragmentModel model = SampleModel();
            FragmentModelData loadedFromTheFirstAsset = model.Data;

            model.SetAsset(FragmentModelAsset.Create(FragmentModelFixture.ResultWith(
                new[] { FragmentModelFixture.Item(UnknownLocalId, DoorCategory, DoorName) })));

            Assert.That(model.Data, Is.Not.SameAs(loadedFromTheFirstAsset));
        }

        [Test]
        public void SetAsset_AfterQueriesRan_DropsTheCachedDataAndIndexes()
        {
            FragmentModel model = SampleModel();
            Assert.That(model.FindByGlobalId(DoorGlobalId), Is.Not.Null);

            model.SetAsset(FragmentModelAsset.Create(FragmentModelFixture.ResultWith(
                new[] { FragmentModelFixture.Item(UnknownLocalId, DoorCategory, DoorName) })));

            Assert.Multiple(() =>
            {
                Assert.That(model.FindByGlobalId(DoorGlobalId), Is.Null);
                Assert.That(model.FindByLocalId(DoorLocalId), Is.Null);
                Assert.That(model.FindByLocalId(UnknownLocalId), Is.Not.Null);
            });
        }

        private static FragmentModel SampleModel()
        {
            FragmentItemMetadata firstWall =
                FragmentModelFixture.Item(FirstWallLocalId, WallCategory, FirstWallName);
            firstWall.GlobalId = SharedGlobalId;
            firstWall.StoreyName = FirstStorey;

            FragmentItemMetadata door = FragmentModelFixture.Item(DoorLocalId, DoorCategory, DoorName);
            door.GlobalId = DoorGlobalId;
            door.StoreyName = FirstStorey;

            FragmentItemMetadata secondWall =
                FragmentModelFixture.Item(SecondWallLocalId, LowerCaseWallCategory, SecondWallName);
            secondWall.GlobalId = SharedGlobalIdOtherCase;
            secondWall.StoreyName = SecondStorey;

            FragmentItemMetadata storey =
                FragmentModelFixture.Item(StoreyLocalId, StoreyCategory, FirstStorey);

            return FragmentModelFixture.ModelWith(firstWall, door, secondWall, storey);
        }
    }
}
