using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace FragmentsUnity.Tests
{
    [TestFixture]
    public sealed class FragmentModelSummaryTests
    {
        private const string WallCategory = "IFCWALL";
        private const string LowerCaseWallCategory = "ifcwall";
        private const string DoorCategory = "IFCDOOR";
        private const string MixedCaseWallCategory = "IfcWall";

        private const string FirstStorey = "Level 1";
        private const string LowerCaseFirstStorey = "level 1";
        private const string SecondStorey = "Level 2";

        private const int FirstWallLocalId = 1;
        private const int SecondWallLocalId = 2;
        private const int DoorLocalId = 3;
        private const int UnplacedWallLocalId = 4;

        private const int WallCount = 3;
        private const int DoorCount = 1;
        private const int FirstStoreyCount = 2;
        private const int SecondStoreyCount = 1;
        private const int DistinctCategoryCount = 2;
        private const int DistinctStoreyCount = 2;

        [Test]
        public void GetCategoryCounts_FoldsCaseWhenCounting()
        {
            Dictionary<string, int> counts = SampleModel().GetCategoryCounts();

            Assert.Multiple(() =>
            {
                Assert.That(counts, Has.Count.EqualTo(DistinctCategoryCount));
                Assert.That(counts[MixedCaseWallCategory], Is.EqualTo(WallCount));
                Assert.That(counts[DoorCategory], Is.EqualTo(DoorCount));
            });
        }

        [Test]
        public void GetCategoryCounts_KeepsTheCasingOfTheFirstItemSeen()
        {
            List<string> keys = SampleModel().GetCategoryCounts().Keys.ToList();

            Assert.Multiple(() =>
            {
                Assert.That(keys, Has.Member(WallCategory));
                Assert.That(keys, Has.No.Member(LowerCaseWallCategory));
            });
        }

        [Test]
        public void GetStoreyCounts_CountsItemsPerStoreyIgnoringCase()
        {
            Dictionary<string, int> counts = SampleModel().GetStoreyCounts();

            Assert.Multiple(() =>
            {
                Assert.That(counts, Has.Count.EqualTo(DistinctStoreyCount));
                Assert.That(counts[LowerCaseFirstStorey], Is.EqualTo(FirstStoreyCount));
                Assert.That(counts[SecondStorey], Is.EqualTo(SecondStoreyCount));
            });
        }

        [Test]
        public void GetStoreyCounts_SkipsItemsWithoutAStorey()
        {
            Dictionary<string, int> counts = SampleModel().GetStoreyCounts();

            Assert.That(counts.Values.Sum(), Is.EqualTo(FirstStoreyCount + SecondStoreyCount));
        }

        [Test]
        public void GetCategoryCounts_ModelWithoutItems_ReturnsEmptyDictionary()
        {
            FragmentModel model = FragmentModelFixture.ModelWith();

            Assert.Multiple(() =>
            {
                Assert.That(model.GetCategoryCounts(), Is.Not.Null.And.Empty);
                Assert.That(model.GetStoreyCounts(), Is.Not.Null.And.Empty);
            });
        }

        private static FragmentModel SampleModel()
        {
            FragmentItemMetadata firstWall =
                FragmentModelFixture.Item(FirstWallLocalId, WallCategory, string.Empty);
            firstWall.StoreyName = FirstStorey;

            FragmentItemMetadata secondWall =
                FragmentModelFixture.Item(SecondWallLocalId, LowerCaseWallCategory, string.Empty);
            secondWall.StoreyName = LowerCaseFirstStorey;

            FragmentItemMetadata door = FragmentModelFixture.Item(DoorLocalId, DoorCategory, string.Empty);
            door.StoreyName = SecondStorey;

            FragmentItemMetadata unplacedWall =
                FragmentModelFixture.Item(UnplacedWallLocalId, WallCategory, string.Empty);

            return FragmentModelFixture.ModelWith(firstWall, secondWall, door, unplacedWall);
        }
    }
}
