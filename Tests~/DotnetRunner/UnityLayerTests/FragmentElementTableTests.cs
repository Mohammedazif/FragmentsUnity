using NUnit.Framework;
using UnityEngine;

namespace FragmentsUnity.Tests
{
    [TestFixture]
    public sealed class FragmentElementTableTests
    {
        private const string TableObjectName = "MergedChunk";
        private const int UnknownLocalId = -1;

        private static readonly int[] TriangleStarts = { 2, 5, 9 };
        private static readonly int[] LocalIds = { 40, 41, 42 };

        [Test]
        public void FindLocalId_TriangleAtAPartStart_ReturnsThatPart()
        {
            Assert.That(Table().FindLocalId(5), Is.EqualTo(41));
        }

        [Test]
        public void FindLocalId_TriangleInsideAPart_ReturnsTheStartingPart()
        {
            Assert.That(Table().FindLocalId(7), Is.EqualTo(41));
        }

        [Test]
        public void FindLocalId_TriangleBeyondTheLastStart_ReturnsTheLastPart()
        {
            Assert.That(Table().FindLocalId(1000), Is.EqualTo(42));
        }

        [Test]
        public void FindLocalId_TriangleBeforeTheFirstStart_IsUnknown()
        {
            Assert.That(Table().FindLocalId(1), Is.EqualTo(UnknownLocalId));
        }

        [Test]
        public void FindLocalId_NegativeTriangle_IsUnknown()
        {
            Assert.That(Table().FindLocalId(-1), Is.EqualTo(UnknownLocalId));
        }

        [Test]
        public void FindLocalId_TableNeverSet_IsUnknown()
        {
            Assert.That(NewTable().FindLocalId(0), Is.EqualTo(UnknownLocalId));
        }

        [Test]
        public void FindLocalId_NullArrays_IsUnknown()
        {
            FragmentElementTable table = NewTable();
            table.SetTable(null, null);

            Assert.That(table.FindLocalId(0), Is.EqualTo(UnknownLocalId));
        }

        [Test]
        public void FindLocalId_FewerLocalIdsThanStarts_IsUnknown()
        {
            FragmentElementTable table = NewTable();
            table.SetTable(TriangleStarts, new[] { 40 });

            Assert.That(table.FindLocalId(9), Is.EqualTo(UnknownLocalId));
        }

        private static FragmentElementTable Table()
        {
            FragmentElementTable table = NewTable();
            table.SetTable(TriangleStarts, LocalIds);
            return table;
        }

        private static FragmentElementTable NewTable()
        {
            return new GameObject(TableObjectName).AddComponent<FragmentElementTable>();
        }
    }
}
