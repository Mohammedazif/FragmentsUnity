using System.Collections.Generic;
using NUnit.Framework;

namespace FragmentsUnity.Tests
{
    [TestFixture]
    public sealed class FragmentModelFlattenedValuesTests
    {
        private const int WallLocalId = 5;
        private const int BareWallLocalId = 6;
        private const int UnknownLocalId = 99;

        private const string WallCategory = "IFCWALL";
        private const string WallName = "Wall A";

        private const string NameAttribute = "Name";
        private const string NameValue = "Wall A";
        private const string RepeatedAttribute = "Reference";
        private const string FirstReferenceValue = "first";
        private const string LastReferenceValue = "last";

        private const string SetName = "Pset_WallCommon";
        private const string PropertyName = "IsExternal";
        private const string PropertyValue = "TRUE";
        private const string PropertyKey = "Pset_WallCommon.IsExternal";
        private const string LowerCasePropertyKey = "pset_wallcommon.isexternal";

        private const string ClassificationName = "Uniclass";
        private const string ClassificationValue = "EF_25_10";
        private const string ClassificationKey = "Classification.Uniclass";

        private const string FirstMaterialName = "Concrete";
        private const string SecondMaterialName = "Insulation";
        private const string FirstMaterialKey = "Material.0";
        private const string SecondMaterialKey = "Material.1";

        private const string TypeNameKey = "TypeName";
        private const string ContainedInKey = "ContainedIn";
        private const string StoreyKey = "Storey";
        private const string TypeName = "Basic Wall:Generic - 200mm";
        private const string ContainerName = "Office 12";
        private const string StoreyName = "Level 1";

        [Test]
        public void GetFlattenedValues_IncludesEveryAttributeByName()
        {
            Dictionary<string, string> values = FlattenedWall();

            Assert.That(values[NameAttribute], Is.EqualTo(NameValue));
        }

        [Test]
        public void GetFlattenedValues_PrefixesPropertiesWithTheirSetName()
        {
            Dictionary<string, string> values = FlattenedWall();

            Assert.That(values[PropertyKey], Is.EqualTo(PropertyValue));
        }

        [Test]
        public void GetFlattenedValues_PrefixesClassificationsAndIndexesMaterials()
        {
            Dictionary<string, string> values = FlattenedWall();

            Assert.Multiple(() =>
            {
                Assert.That(values[ClassificationKey], Is.EqualTo(ClassificationValue));
                Assert.That(values[FirstMaterialKey], Is.EqualTo(FirstMaterialName));
                Assert.That(values[SecondMaterialKey], Is.EqualTo(SecondMaterialName));
            });
        }

        [Test]
        public void GetFlattenedValues_AddsTypeContainerAndStoreyEntries()
        {
            Dictionary<string, string> values = FlattenedWall();

            Assert.Multiple(() =>
            {
                Assert.That(values[TypeNameKey], Is.EqualTo(TypeName));
                Assert.That(values[ContainedInKey], Is.EqualTo(ContainerName));
                Assert.That(values[StoreyKey], Is.EqualTo(StoreyName));
            });
        }

        [Test]
        public void GetFlattenedValues_KeysFoldCase()
        {
            Dictionary<string, string> values = FlattenedWall();

            Assert.That(values[LowerCasePropertyKey], Is.EqualTo(PropertyValue));
        }

        [Test]
        public void GetFlattenedValues_RepeatedName_KeepsTheLastValue()
        {
            Dictionary<string, string> values = FlattenedWall();

            Assert.That(values[RepeatedAttribute], Is.EqualTo(LastReferenceValue));
        }

        [Test]
        public void GetFlattenedValues_ItemWithoutPlacement_OmitsThosePlacementKeys()
        {
            Dictionary<string, string> values = SampleModel().GetFlattenedValues(BareWallLocalId);

            Assert.Multiple(() =>
            {
                Assert.That(values, Is.Empty);
                Assert.That(values.ContainsKey(TypeNameKey), Is.False);
                Assert.That(values.ContainsKey(ContainedInKey), Is.False);
                Assert.That(values.ContainsKey(StoreyKey), Is.False);
            });
        }

        [Test]
        public void GetFlattenedValues_UnknownLocalId_ReturnsEmptyDictionary()
        {
            Dictionary<string, string> values = SampleModel().GetFlattenedValues(UnknownLocalId);

            Assert.That(values, Is.Not.Null.And.Empty);
        }

        private static Dictionary<string, string> FlattenedWall()
        {
            return SampleModel().GetFlattenedValues(WallLocalId);
        }

        private static FragmentModel SampleModel()
        {
            FragmentItemMetadata wall = FragmentModelFixture.Item(WallLocalId, WallCategory, WallName);
            wall.Attributes.Add(new FragmentAttribute(NameAttribute, NameValue, string.Empty));
            wall.Attributes.Add(new FragmentAttribute(RepeatedAttribute, FirstReferenceValue, string.Empty));
            wall.Attributes.Add(new FragmentAttribute(RepeatedAttribute, LastReferenceValue, string.Empty));

            var set = new FragmentPropertySet { Name = SetName };
            set.Properties.Add(new FragmentAttribute(PropertyName, PropertyValue, string.Empty));
            wall.PropertySets.Add(set);

            wall.Classifications.Add(
                new FragmentAttribute(ClassificationName, ClassificationValue, string.Empty));
            wall.Materials.Add(new FragmentMaterial { Name = FirstMaterialName });
            wall.Materials.Add(new FragmentMaterial { Name = SecondMaterialName });

            wall.TypeName = TypeName;
            wall.ContainerName = ContainerName;
            wall.StoreyName = StoreyName;

            FragmentItemMetadata bareWall =
                FragmentModelFixture.Item(BareWallLocalId, WallCategory, string.Empty);

            return FragmentModelFixture.ModelWith(wall, bareWall);
        }
    }
}
