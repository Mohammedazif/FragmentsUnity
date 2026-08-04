using NUnit.Framework;

namespace FragmentsUnity.Tests
{
    [TestFixture]
    public sealed class FragmentModelAttributeSearchTests
    {
        private const bool ExactMatch = true;
        private const bool LooseMatch = false;

        private const string WallCategory = "IFCWALL";
        private const string DoorCategory = "IFCDOOR";

        private const int NamedWallLocalId = 1;
        private const int PropertySetWallLocalId = 2;
        private const int PlainDoorLocalId = 3;

        private const string NameAttribute = "Name";
        private const string LowerCaseNameAttribute = "name";
        private const string WallLongName = "Basic Wall:Generic - 200mm";
        private const string NamePart = "generic";
        private const string NamePrefix = "Basic Wall";

        private const string WallSetName = "Pset_WallCommon";
        private const string IsExternalProperty = "IsExternal";
        private const string TrueValue = "TRUE";
        private const string FalseValue = "FALSE";
        private const string LowerCaseTrueValue = "true";
        private const string FireRatingProperty = "FireRating";
        private const string FireRatingValue = "60";

        private const string UnknownAttribute = "IFCUnknownAttribute";
        private const string UnknownValue = "no such value";

        [Test]
        public void FindByAttribute_ExactMatch_ComparesValuesIgnoringCase()
        {
            FragmentModel model = SampleModel();

            Assert.That(model.FindByAttribute(IsExternalProperty, LowerCaseTrueValue, ExactMatch),
                Is.EqualTo(new[] { PropertySetWallLocalId }));
        }

        [Test]
        public void FindByAttribute_ExactMatch_RejectsPartialValues()
        {
            FragmentModel model = SampleModel();

            Assert.That(model.FindByAttribute(NameAttribute, NamePrefix, ExactMatch), Is.Empty);
        }

        [Test]
        public void FindByAttribute_LooseMatch_AcceptsSubstringsIgnoringCase()
        {
            FragmentModel model = SampleModel();

            Assert.That(model.FindByAttribute(LowerCaseNameAttribute, NamePart, LooseMatch),
                Is.EqualTo(new[] { NamedWallLocalId }));
        }

        [Test]
        public void FindByAttribute_ExactMatchOnTheWholeValue_FindsTheItem()
        {
            FragmentModel model = SampleModel();

            Assert.That(model.FindByAttribute(NameAttribute, WallLongName, ExactMatch),
                Is.EqualTo(new[] { NamedWallLocalId }));
        }

        [TestCase(null)]
        [TestCase("")]
        public void FindByAttribute_NoValue_MatchesEveryItemCarryingTheAttribute(string attributeValue)
        {
            FragmentModel model = SampleModel();

            Assert.That(model.FindByAttribute(IsExternalProperty, attributeValue, ExactMatch),
                Is.EqualTo(new[] { PropertySetWallLocalId, PlainDoorLocalId }));
        }

        [Test]
        public void FindByAttribute_NameOnlyPresentInAPropertySet_StillMatches()
        {
            FragmentModel model = SampleModel();

            Assert.That(model.FindByAttribute(FireRatingProperty, FireRatingValue, ExactMatch),
                Is.EqualTo(new[] { PropertySetWallLocalId }));
        }

        [Test]
        public void FindByAttribute_AttributeAndPropertyShareAName_MatchesOnlyTheMatchingValue()
        {
            FragmentModel model = SampleModel();

            Assert.That(model.FindByAttribute(IsExternalProperty, FalseValue, ExactMatch),
                Is.EqualTo(new[] { PlainDoorLocalId }));
        }

        [Test]
        public void FindByAttribute_UnknownAttribute_ReturnsEmptyList()
        {
            FragmentModel model = SampleModel();

            Assert.Multiple(() =>
            {
                Assert.That(model.FindByAttribute(UnknownAttribute, UnknownValue, ExactMatch),
                    Is.Not.Null.And.Empty);
                Assert.That(model.FindByAttribute(UnknownAttribute, string.Empty, LooseMatch),
                    Is.Not.Null.And.Empty);
            });
        }

        [Test]
        public void FindByAttribute_KnownAttributeWithAnUnknownValue_ReturnsEmptyList()
        {
            FragmentModel model = SampleModel();

            Assert.That(model.FindByAttribute(NameAttribute, UnknownValue, LooseMatch), Is.Empty);
        }

        private static FragmentModel SampleModel()
        {
            FragmentItemMetadata namedWall =
                FragmentModelFixture.Item(NamedWallLocalId, WallCategory, WallLongName);
            namedWall.Attributes.Add(new FragmentAttribute(NameAttribute, WallLongName, string.Empty));

            FragmentItemMetadata propertySetWall =
                FragmentModelFixture.Item(PropertySetWallLocalId, WallCategory, string.Empty);
            var set = new FragmentPropertySet { Name = WallSetName };
            set.Properties.Add(new FragmentAttribute(IsExternalProperty, TrueValue, string.Empty));
            set.Properties.Add(new FragmentAttribute(FireRatingProperty, FireRatingValue, string.Empty));
            propertySetWall.PropertySets.Add(set);

            FragmentItemMetadata plainDoor =
                FragmentModelFixture.Item(PlainDoorLocalId, DoorCategory, string.Empty);
            plainDoor.Attributes.Add(new FragmentAttribute(IsExternalProperty, FalseValue, string.Empty));

            return FragmentModelFixture.ModelWith(namedWall, propertySetWall, plainDoor);
        }
    }
}
