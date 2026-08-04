using System.Collections.Generic;
using NUnit.Framework;

namespace FragmentsUnity.Tests
{
    [TestFixture]
    public sealed class FragmentMetadataFormatterTests
    {
        private const string WallCategory = "IFCWALL";
        private const string WallName = "Basic Wall:Exterior - Brick on CMU:314159";
        private const string WallGlobalId = "3DqaUydM5KV8VVBLpZ1eDq";
        private const string WallTypeName = "Basic Wall:Exterior - Brick on CMU";
        private const string ContainerName = "Level 1";
        private const string ContainerCategory = "IFCBUILDINGSTOREY";
        private const string StoreyName = "Level 2";
        private const int WallLocalId = 42;
        private const long WallExpressId = 3141;
        private const int ContainerLocalId = 7;
        private const int StoreyLocalId = 9;

        private const string LoadBearingAttribute = "LoadBearing";
        private const string LoadBearingValue = "T";
        private const string CommonSetName = "Pset_WallCommon";
        private const string TypeSetName = "Pset_WallCommonFromType";
        private const string FireRatingProperty = "FireRating";
        private const string FireRatingValue = "REI 60";
        private const string ThermalProperty = "ThermalTransmittance";
        private const string ThermalValue = "0.28";
        private const string ClassificationName = "Uniclass";
        private const string ClassificationValue = "EF_25_10";
        private const string RelationName = "ContainedInStructure";

        private const string ConcreteMaterial = "Concrete";
        private const string InsulationMaterial = "Insulation";
        private const string GypsumMaterial = "Gypsum Board";
        private const string UpperCaseConcreteMaterial = "CONCRETE";
        private const string LayerSetName = "Exterior Wall Layers";
        private const float ConcreteThickness = 0.2f;
        private const int WallLayerCount = 7;
        private const int DistinctMaterialCount = 3;
        private const int CommonSetPropertyCount = 2;
        private const int RelatedLocalId = 11;

        [Test]
        public void ToDisplayString_WritesTheIdentityHeaderInSourceOrder()
        {
            string[] lines = FragmentMetadataFormatter.ToDisplayString(SampleWall()).Split('\n');

            Assert.Multiple(() =>
            {
                Assert.That(lines[0], Is.EqualTo("Category: " + WallCategory));
                Assert.That(lines[1], Is.EqualTo("Name: " + WallName));
                Assert.That(lines[2], Is.EqualTo("GlobalId: " + WallGlobalId));
                Assert.That(lines[3], Is.EqualTo("Type: " + WallTypeName));
                Assert.That(lines[4], Is.EqualTo($"Contained In: {ContainerName} ({ContainerCategory})"));
                Assert.That(lines[5], Is.EqualTo("Storey: " + StoreyName));
                Assert.That(lines[6], Is.EqualTo($"LocalId: {WallLocalId} (Express #{WallExpressId})"));
            });
        }

        [Test]
        public void ToDisplayString_OrdersAttributesThenSetsThenMaterialsThenClassification()
        {
            string text = FragmentMetadataFormatter.ToDisplayString(SampleWall());

            Assert.Multiple(() =>
            {
                Assert.That(text.IndexOf("\nAttributes\n"), Is.GreaterThan(text.IndexOf("LocalId: ")));
                Assert.That(text.IndexOf("\n" + CommonSetName + "\n"), Is.GreaterThan(text.IndexOf("\nAttributes\n")));
                Assert.That(text.IndexOf("\nMaterials\n"), Is.GreaterThan(text.IndexOf("\n" + CommonSetName + "\n")));
                Assert.That(text.IndexOf("\nClassification\n"), Is.GreaterThan(text.IndexOf("\nMaterials\n")));
            });
        }

        [Test]
        public void ToDisplayString_IndentsEveryValueAndMarksTypeInheritedSets()
        {
            string text = FragmentMetadataFormatter.ToDisplayString(SampleWall());

            Assert.Multiple(() =>
            {
                Assert.That(text, Does.Contain($"  {LoadBearingAttribute}: {LoadBearingValue}\n"));
                Assert.That(text, Does.Contain($"  {FireRatingProperty}: {FireRatingValue}\n"));
                Assert.That(text, Does.Contain($"\n{TypeSetName} (from type)\n"));
                Assert.That(text, Does.Contain($"  {ClassificationName}: {ClassificationValue}\n"));
            });
        }

        [Test]
        public void ToDisplayString_PrefixesThickLayersWithTheirLayerSetAndPlainMaterialsWithNothing()
        {
            string text = FragmentMetadataFormatter.ToDisplayString(SampleWall());

            Assert.Multiple(() =>
            {
                Assert.That(text, Does.Contain($"  {LayerSetName}: {ConcreteMaterial} (0.2 thick)\n"));
                Assert.That(text, Does.Contain($"  {GypsumMaterial}\n"));
            });
        }

        [Test]
        public void ToDisplayString_OmitsTheStoreyWhenTheContainerIsTheStorey()
        {
            FragmentItemMetadata wall = SampleWall();
            wall.StoreyLocalId = wall.ContainerLocalId;

            Assert.That(FragmentMetadataFormatter.ToDisplayString(wall), Does.Not.Contain("Storey: "));
        }

        [Test]
        public void ToDisplayString_OmitsEveryOptionalLineAndHeadingForABareItem()
        {
            string text = FragmentMetadataFormatter.ToDisplayString(new FragmentItemMetadata());

            Assert.That(text, Is.EqualTo("Category: \nName: \nLocalId: -1 (Express #0)\n"));
        }

        [Test]
        public void ToDisplayString_NullItem_ReturnsEmptyString()
        {
            Assert.That(FragmentMetadataFormatter.ToDisplayString(null), Is.Empty);
        }

        [Test]
        public void GetMaterialNames_SevenLayersOverThreeMaterials_YieldsThreeNames()
        {
            FragmentItemMetadata wall = SampleWall();
            List<string> names = FragmentMetadataFormatter.GetMaterialNames(wall);

            Assert.Multiple(() =>
            {
                Assert.That(wall.Materials, Has.Count.EqualTo(WallLayerCount));
                Assert.That(names, Has.Count.EqualTo(DistinctMaterialCount));
                Assert.That(names, Is.EqualTo(new[] { ConcreteMaterial, InsulationMaterial, GypsumMaterial }));
            });
        }

        [Test]
        public void GetMaterialNames_FoldsCaseAndKeepsTheFirstSpelling()
        {
            var item = new FragmentItemMetadata();
            item.Materials.Add(new FragmentMaterial { Name = ConcreteMaterial });
            item.Materials.Add(new FragmentMaterial { Name = UpperCaseConcreteMaterial });

            Assert.That(
                FragmentMetadataFormatter.GetMaterialNames(item),
                Is.EqualTo(new[] { ConcreteMaterial }));
        }

        [Test]
        public void GetMaterialNames_NullItem_ReturnsEmptyList()
        {
            Assert.That(FragmentMetadataFormatter.GetMaterialNames(null), Is.Not.Null.And.Empty);
        }

        [Test]
        public void GetPropertySetNames_ReturnsEverySetInFileOrder()
        {
            Assert.That(
                FragmentMetadataFormatter.GetPropertySetNames(SampleWall()),
                Is.EqualTo(new[] { CommonSetName, TypeSetName }));
        }

        [Test]
        public void GetPropertiesInSet_MatchesTheSetNameIgnoringCase()
        {
            List<FragmentAttribute> properties =
                FragmentMetadataFormatter.GetPropertiesInSet(SampleWall(), CommonSetName.ToLowerInvariant());

            Assert.Multiple(() =>
            {
                Assert.That(properties, Has.Count.EqualTo(CommonSetPropertyCount));
                Assert.That(properties[0].Name, Is.EqualTo(FireRatingProperty));
                Assert.That(properties[0].Value, Is.EqualTo(FireRatingValue));
                Assert.That(properties[1].Name, Is.EqualTo(ThermalProperty));
            });
        }

        [Test]
        public void GetPropertiesInSet_UnknownSet_ReturnsEmptyList()
        {
            Assert.That(
                FragmentMetadataFormatter.GetPropertiesInSet(SampleWall(), "Pset_NotHere"),
                Is.Not.Null.And.Empty);
        }

        [Test]
        public void GetPropertiesInSet_ReturnsACopyTheCallerCannotUseToEditTheItem()
        {
            FragmentItemMetadata wall = SampleWall();
            FragmentMetadataFormatter.GetPropertiesInSet(wall, CommonSetName).Clear();

            Assert.That(wall.PropertySets[0].Properties, Has.Count.EqualTo(CommonSetPropertyCount));
        }

        [Test]
        public void HasDisplayableMetadata_IsFalseForNothingAndTrueForAnyIdentity()
        {
            var categoryOnly = new FragmentItemMetadata { Category = WallCategory };

            Assert.Multiple(() =>
            {
                Assert.That(FragmentMetadataFormatter.HasDisplayableMetadata(null), Is.False);
                Assert.That(FragmentMetadataFormatter.HasDisplayableMetadata(new FragmentItemMetadata()), Is.False);
                Assert.That(FragmentMetadataFormatter.HasDisplayableMetadata(categoryOnly), Is.True);
                Assert.That(FragmentMetadataFormatter.HasDisplayableMetadata(SampleWall()), Is.True);
            });
        }

        [Test]
        public void DescribeMaterial_AppendsTheThicknessOnlyForLayers()
        {
            var layer = new FragmentMaterial { Name = ConcreteMaterial, Thickness = ConcreteThickness };
            var plain = new FragmentMaterial { Name = GypsumMaterial };

            Assert.Multiple(() =>
            {
                Assert.That(FragmentMetadataFormatter.DescribeMaterial(layer), Is.EqualTo("Concrete  —  0.2 thick"));
                Assert.That(FragmentMetadataFormatter.DescribeMaterial(plain), Is.EqualTo(GypsumMaterial));
                Assert.That(FragmentMetadataFormatter.DescribeMaterial(null), Is.Empty);
            });
        }

        [Test]
        public void ToClipboardText_CarriesTheGlobalIdAPropertyValueAndAMaterialName()
        {
            string text = FragmentMetadataFormatter.ToClipboardText(SampleWall());

            Assert.Multiple(() =>
            {
                Assert.That(text, Does.Contain(WallGlobalId));
                Assert.That(text, Does.Contain(FireRatingValue));
                Assert.That(text, Does.Contain(ThermalValue));
                Assert.That(text, Does.Contain(ConcreteMaterial));
                Assert.That(text, Does.Contain(ClassificationValue));
            });
        }

        [Test]
        public void ToClipboardText_AddsTheRelationsThatToDisplayStringDrops()
        {
            FragmentItemMetadata wall = SampleWall();

            Assert.Multiple(() =>
            {
                Assert.That(FragmentMetadataFormatter.ToDisplayString(wall), Does.Not.Contain("\nRelations\n"));
                Assert.That(
                    FragmentMetadataFormatter.ToClipboardText(wall),
                    Does.Contain($"\nRelations\n  {RelationName}: {RelatedLocalId}\n"));
            });
        }

        [Test]
        public void ToClipboardText_NullItem_ReturnsEmptyString()
        {
            Assert.That(FragmentMetadataFormatter.ToClipboardText(null), Is.Empty);
        }

        [Test]
        public void ToClipboardText_SampleModelItem_CarriesItsGlobalIdAndEverySetName()
        {
            FragmentImportResult result = FragmentSampleModels.LoadOrIgnore(FragmentSampleModels.Ar520FileName);
            FragmentItemMetadata item = FirstItemWithPropertySets(result);
            string text = FragmentMetadataFormatter.ToClipboardText(item);

            Assert.Multiple(() =>
            {
                Assert.That(text, Does.Contain(item.GlobalId));
                foreach (string setName in FragmentMetadataFormatter.GetPropertySetNames(item))
                {
                    Assert.That(text, Does.Contain("\n" + setName));
                }
            });
        }

        private static FragmentItemMetadata FirstItemWithPropertySets(FragmentImportResult result)
        {
            foreach (FragmentItemMetadata item in result.Items)
            {
                if (item != null && item.PropertySets.Count > 0 && !string.IsNullOrEmpty(item.GlobalId))
                {
                    return item;
                }
            }

            Assert.Ignore("The sample model carries no item with property sets.");
            return null;
        }

        private static FragmentItemMetadata SampleWall()
        {
            var wall = new FragmentItemMetadata
            {
                LocalId = WallLocalId,
                ExpressId = WallExpressId,
                GlobalId = WallGlobalId,
                Category = WallCategory,
                Name = WallName,
                TypeName = WallTypeName,
                ContainerName = ContainerName,
                ContainerCategory = ContainerCategory,
                ContainerLocalId = ContainerLocalId,
                StoreyName = StoreyName,
                StoreyLocalId = StoreyLocalId
            };

            wall.Attributes.Add(new FragmentAttribute(LoadBearingAttribute, LoadBearingValue, "IfcBoolean"));

            var commonSet = new FragmentPropertySet { Name = CommonSetName };
            commonSet.Properties.Add(new FragmentAttribute(FireRatingProperty, FireRatingValue, "IfcLabel"));
            commonSet.Properties.Add(new FragmentAttribute(ThermalProperty, ThermalValue, "IfcReal"));
            wall.PropertySets.Add(commonSet);

            var typeSet = new FragmentPropertySet { Name = TypeSetName, FromType = true };
            typeSet.Properties.Add(new FragmentAttribute(FireRatingProperty, FireRatingValue, "IfcLabel"));
            wall.PropertySets.Add(typeSet);

            AddLayer(wall, ConcreteMaterial);
            AddLayer(wall, InsulationMaterial);
            wall.Materials.Add(new FragmentMaterial { Name = GypsumMaterial });
            AddLayer(wall, InsulationMaterial);
            AddLayer(wall, ConcreteMaterial);
            wall.Materials.Add(new FragmentMaterial { Name = GypsumMaterial });
            AddLayer(wall, InsulationMaterial);

            wall.Classifications.Add(new FragmentAttribute(ClassificationName, ClassificationValue, string.Empty));

            var relation = new FragmentRelation { Name = RelationName };
            relation.RelatedLocalIds.Add(RelatedLocalId);
            wall.Relations.Add(relation);

            return wall;
        }

        private static void AddLayer(FragmentItemMetadata wall, string materialName)
        {
            wall.Materials.Add(new FragmentMaterial
            {
                Name = materialName,
                LayerSetName = LayerSetName,
                Thickness = ConcreteThickness
            });
        }
    }
}
