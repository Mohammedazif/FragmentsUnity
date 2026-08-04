using System.Linq;
using NUnit.Framework;

namespace FragmentsUnity.Tests
{
    [TestFixture]
    public sealed class FragmentModelInfoBuilderTests
    {
        private const string HeaderTimestamp = "2024-12-06T17:49:38+04:00";
        private const string FullHeaderJson =
            "{\"schema\":\"IFC4\","
            + "\"names\":[\"combined.ifc\",\"" + HeaderTimestamp + "\",\"Ann\",\"Acme\","
            + "\"ODA SDAI 22.12\",\"Autodesk Revit\",\"AuthKey\",\"Spare\"],"
            + "\"descriptions\":[\"ViewDefinition [CoordinationView_V2.0]\"],"
            + "\"crs\":\"EPSG:4326\"}";
        private const string TimestampOnlyHeaderJson =
            "{\"names\":[\"combined.ifc\",\"" + HeaderTimestamp + "\"]}";
        private const string NotJsonHeader = "IFC2X3 header text that is not JSON";

        private const string ModelName = "AR520";
        private const string ModelGuid = "2ff8631c-31be-4f34-95fc-efb1decc84f1";
        private const string ProjectItemName = "37464650";
        private const string SiteItemName = "Default";
        private const string BuildingItemName = "Tower A";

        private const string UnitsRelationName = "Units";
        private const string UnitsSetName = "Units";
        private const string UnitTypeAttributeName = "UnitType";
        private const string PrefixAttributeName = "Prefix";
        private const string LengthUnitType = "LENGTHUNIT";
        private const string MilliPrefix = "MILLI";
        private const string MetreUnitName = "METRE";
        private const string MillimetreValue = "MILLIMETRE";
        private const string SiUnitCategory = "IFCSIUNIT";
        private const string DerivedUnitCategory = "IFCDERIVEDUNIT";
        private const string AreaUnitType = "AREAUNIT";

        private const string AddressRelationName = "BuildingAddress";
        private const string AddressSetName = "Address";
        private const string PostalAddressCategory = "IFCPOSTALADDRESS";

        private const int ModelInfoLocalId = -1;
        private const string ModelInfoCategory = "IfcProject";

        [Test]
        public void BuildModelInfo_EmptySource_TakesProjectIdentityFromModel()
        {
            var source = new FragmentImportResult { ModelName = ModelName, ModelGuid = ModelGuid };
            var info = new FragmentItemMetadata();

            FragmentModelInfoBuilder.BuildModelInfo(source, info);

            Assert.That(info.LocalId, Is.EqualTo(ModelInfoLocalId));
            Assert.That(info.Category, Is.EqualTo(ModelInfoCategory));
            Assert.That(info.Name, Is.EqualTo(ModelName));
            Assert.That(info.GlobalId, Is.EqualTo(ModelGuid));
            Assert.That(info.Attributes, Is.Empty);
            Assert.That(info.PropertySets, Is.Empty);
        }

        [Test]
        public void BuildModelInfo_FullHeaderJson_LabelsAttributesInOrder()
        {
            var source = new FragmentImportResult { Metadata = FullHeaderJson };
            var info = new FragmentItemMetadata();

            FragmentModelInfoBuilder.BuildModelInfo(source, info);

            Assert.That(info.Attributes.Select(attribute => attribute.Name), Is.EqualTo(new[]
            {
                "IFC Schema", "File Name", "Exported", "Author", "Organization",
                "Preprocessor", "Authoring Tool", "Authorization", "Header",
                "Description", "Coordinate Reference"
            }));
            Assert.That(info.Attributes.Select(attribute => attribute.Value), Is.EqualTo(new[]
            {
                "IFC4", "combined.ifc", HeaderTimestamp, "Ann", "Acme",
                "ODA SDAI 22.12", "Autodesk Revit", "AuthKey", "Spare",
                "ViewDefinition [CoordinationView_V2.0]", "EPSG:4326"
            }));
        }

        [Test]
        public void BuildModelInfo_UnparsableHeader_FallsBackToRawHeaderAttribute()
        {
            var source = new FragmentImportResult { Metadata = NotJsonHeader };
            var info = new FragmentItemMetadata();

            FragmentModelInfoBuilder.BuildModelInfo(source, info);

            Assert.That(info.Attributes, Has.Count.EqualTo(1));
            Assert.That(info.Attributes[0].Name, Is.EqualTo("Header"));
            Assert.That(info.Attributes[0].Value, Is.EqualTo(NotJsonHeader));
        }

        [Test]
        public void BuildModelInfo_IsoTimestampInNames_StaysByteIdentical()
        {
            var source = new FragmentImportResult { Metadata = TimestampOnlyHeaderJson };
            var info = new FragmentItemMetadata();

            FragmentModelInfoBuilder.BuildModelInfo(source, info);

            FragmentAttribute exported = info.FindAttribute("Exported");
            Assert.That(exported, Is.Not.Null);
            Assert.That(exported.Value, Is.EqualTo(HeaderTimestamp));
        }

        [Test]
        public void BuildModelInfo_SpatialCategories_ProduceProjectSiteAndBuildingAttributes()
        {
            var source = new FragmentImportResult();
            AddItem(source, "IFCPROJECT", ProjectItemName);
            AddItem(source, "IFCSITE", SiteItemName);
            AddItem(source, "IFCBUILDING", BuildingItemName);
            var info = new FragmentItemMetadata();

            FragmentModelInfoBuilder.BuildModelInfo(source, info);

            Assert.That(info.Attributes.Select(attribute => attribute.Name),
                Is.EqualTo(new[] { "Project", "Site", "Building" }));
            Assert.That(info.Attributes.Select(attribute => attribute.Value),
                Is.EqualTo(new[] { ProjectItemName, SiteItemName, BuildingItemName }));
        }

        [Test]
        public void BuildModelInfo_UnitsRelation_PrependsPrefixToUnitName()
        {
            var source = new FragmentImportResult();
            FragmentItemMetadata project = AddItem(source, "IFCPROJECT", ProjectItemName);
            FragmentItemMetadata metre = AddItem(source, SiUnitCategory, MetreUnitName);
            metre.Attributes.Add(new FragmentAttribute(UnitTypeAttributeName, LengthUnitType, string.Empty));
            metre.Attributes.Add(new FragmentAttribute(PrefixAttributeName, MilliPrefix, string.Empty));
            AddRelation(project, UnitsRelationName, metre.LocalId);
            var info = new FragmentItemMetadata();

            FragmentModelInfoBuilder.BuildModelInfo(source, info);

            FragmentPropertySet units = info.FindPropertySet(UnitsSetName);
            Assert.That(units, Is.Not.Null);
            Assert.That(units.Properties, Has.Count.EqualTo(1));
            Assert.That(units.Properties[0].Name, Is.EqualTo(LengthUnitType));
            Assert.That(units.Properties[0].Value, Is.EqualTo(MillimetreValue));
            Assert.That(units.Properties[0].Type, Is.EqualTo(SiUnitCategory));
        }

        [Test]
        public void BuildModelInfo_UnnamedUnit_IsSkipped()
        {
            var source = new FragmentImportResult();
            FragmentItemMetadata project = AddItem(source, "IFCPROJECT", ProjectItemName);
            FragmentItemMetadata metre = AddItem(source, SiUnitCategory, MetreUnitName);
            metre.Attributes.Add(new FragmentAttribute(UnitTypeAttributeName, LengthUnitType, string.Empty));
            FragmentItemMetadata derived = AddItem(source, DerivedUnitCategory, string.Empty);
            derived.Attributes.Add(new FragmentAttribute(UnitTypeAttributeName, AreaUnitType, string.Empty));
            AddRelation(project, UnitsRelationName, metre.LocalId, derived.LocalId);
            var info = new FragmentItemMetadata();

            FragmentModelInfoBuilder.BuildModelInfo(source, info);

            FragmentPropertySet units = info.FindPropertySet(UnitsSetName);
            Assert.That(units, Is.Not.Null);
            Assert.That(units.Properties.Select(property => property.Name), Is.EqualTo(new[] { LengthUnitType }));
        }

        [Test]
        public void BuildModelInfo_BuildingAddressRelation_FillsAddressSet()
        {
            var source = new FragmentImportResult();
            FragmentItemMetadata building = AddItem(source, "IFCBUILDING", BuildingItemName);
            FragmentItemMetadata postal = AddItem(source, PostalAddressCategory, string.Empty);
            postal.Attributes.Add(new FragmentAttribute("Country", "US", string.Empty));
            postal.Attributes.Add(new FragmentAttribute("Town", "Springfield", string.Empty));
            AddRelation(building, AddressRelationName, postal.LocalId);
            var info = new FragmentItemMetadata();

            FragmentModelInfoBuilder.BuildModelInfo(source, info);

            FragmentPropertySet address = info.FindPropertySet(AddressSetName);
            Assert.That(address, Is.Not.Null);
            Assert.That(address.Properties.Select(property => property.Name), Is.EqualTo(new[] { "Country", "Town" }));
            Assert.That(address.Properties.Select(property => property.Value), Is.EqualTo(new[] { "US", "Springfield" }));
        }

        private static FragmentItemMetadata AddItem(FragmentImportResult source, string category, string name)
        {
            var item = new FragmentItemMetadata
            {
                LocalId = source.Items.Count,
                Category = category,
                Name = name
            };
            source.Items.Add(item);
            return item;
        }

        private static void AddRelation(FragmentItemMetadata item, string name, params int[] targetLocalIds)
        {
            var relation = new FragmentRelation { Name = name };
            relation.RelatedLocalIds.AddRange(targetLocalIds);
            item.Relations.Add(relation);
        }
    }
}
