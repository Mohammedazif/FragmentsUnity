using System.Linq;
using NUnit.Framework;

namespace FragmentsUnity.Tests
{
    [TestFixture]
    public sealed class FragmentModelDataTests
    {
        private const string WallCategory = "IFCWALL";
        private const string FirstName = "Wall A";
        private const string SecondName = "Wall B";
        private const string ThirdName = "Wall C";

        private const int FirstLocalId = 5;
        private const int SecondLocalId = 17;
        private const int ThirdLocalId = 200;
        private const int UnknownLocalId = 1;
        private const int FirstPosition = 0;
        private const int SecondPosition = 1;
        private const int ThirdPosition = 2;

        [Test]
        public void NewData_HasEmptyItemsAndNonNullModelInfo()
        {
            var data = new FragmentModelData();

            Assert.Multiple(() =>
            {
                Assert.That(data.Items, Is.Empty);
                Assert.That(data.ModelInfo, Is.Not.Null);
                Assert.That(data.ModelName, Is.Empty);
                Assert.That(data.FindItem(FirstLocalId), Is.Null);
            });
        }

        [Test]
        public void FindItem_NonContiguousLocalIds_ResolvesByLocalIdNotByPosition()
        {
            FragmentModelData data = NonContiguousData();

            Assert.Multiple(() =>
            {
                Assert.That(data.FindItem(FirstLocalId).Name, Is.EqualTo(FirstName));
                Assert.That(data.FindItem(SecondLocalId).Name, Is.EqualTo(SecondName));
                Assert.That(data.FindItem(ThirdLocalId).Name, Is.EqualTo(ThirdName));
            });
        }

        [Test]
        public void FindItem_PositionOfAnItem_IsNotTreatedAsItsLocalId()
        {
            FragmentModelData data = NonContiguousData();

            Assert.Multiple(() =>
            {
                Assert.That(data.FindItem(FirstPosition), Is.Null);
                Assert.That(data.FindItem(SecondPosition), Is.Null);
                Assert.That(data.FindItem(ThirdPosition), Is.Null);
            });
        }

        [Test]
        public void FindItem_NegativeLocalId_ReturnsNull()
        {
            FragmentModelData data = NonContiguousData();

            Assert.That(data.FindItem(-1), Is.Null);
        }

        [Test]
        public void FindItem_AfterRoundTripThroughTheAsset_StillResolvesNonContiguousLocalIds()
        {
            FragmentModelData data = FragmentModelFixture.LoadedDataFor(
                FragmentModelFixture.Item(FirstLocalId, WallCategory, FirstName),
                FragmentModelFixture.Item(SecondLocalId, WallCategory, SecondName),
                FragmentModelFixture.Item(ThirdLocalId, WallCategory, ThirdName));

            Assert.Multiple(() =>
            {
                Assert.That(data.Items.Select(item => item.LocalId),
                    Is.EqualTo(new[] { FirstLocalId, SecondLocalId, ThirdLocalId }));
                Assert.That(data.FindItem(SecondLocalId).Name, Is.EqualTo(SecondName));
                Assert.That(data.FindItem(UnknownLocalId), Is.Null);
            });
        }

        private static FragmentModelData NonContiguousData()
        {
            var data = new FragmentModelData();
            data.Items.Add(FragmentModelFixture.Item(FirstLocalId, WallCategory, FirstName));
            data.Items.Add(FragmentModelFixture.Item(SecondLocalId, WallCategory, SecondName));
            data.Items.Add(FragmentModelFixture.Item(ThirdLocalId, WallCategory, ThirdName));
            return data;
        }
    }
}
