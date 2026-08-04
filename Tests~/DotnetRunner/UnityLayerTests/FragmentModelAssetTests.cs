using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace FragmentsUnity.Tests
{
    [TestFixture]
    public sealed class FragmentModelAssetTests
    {
        private const string CompressedMetadataFieldName = "_compressedMetadata";
        private const string ExpectedAssetName = "ModelData";

        private const string ModelName = "AR520";
        private const string ModelGuid = "2ff8631c-31be-4f34-95fc-efb1decc84f1";
        private const string HeaderJson = "{\"schema\":\"IFC4\",\"names\":[\"combined.ifc\"]}";
        private const string ModelInfoCategory = "IfcProject";
        private const string ModelInfoName = "37464650";

        private const string WallCategory = "IFCWALL";
        private const string DoorCategory = "IFCDOOR";
        private const string WallName = "Basic Wall";
        private const string SecondWallName = "Party Wall";
        private const string ThirdWallName = "Curtain Wall";
        private const string DoorName = "Single Door";
        private const string SecondDoorName = "Double Door";

        private const int WallLocalId = 5;
        private const int LocalIdWithoutItem = 9;
        private const string WallGlobalId = "1hqRfV3mv2rP3RJ8AZ$Zk4";
        private const long WallExpressId = 37464650L;

        private const string TypeName = "Basic Wall:Generic - 200mm";
        private const int TypeLocalId = 61;
        private const string ContainerName = "Office 12";
        private const string ContainerCategory = "IFCSPACE";
        private const int ContainerLocalId = 62;
        private const string StoreyName = "Level 1";
        private const int StoreyLocalId = 63;

        private const string AttributeName = "Description";
        private const string AttributeValue = "Exterior wall";
        private const string AttributeType = "IFCTEXT";
        private const string SetName = "Pset_WallCommon";
        private const int SetLocalId = 71;
        private const string PropertyName = "IsExternal";
        private const string PropertyValue = "TRUE";
        private const string PropertyType = "IFCBOOLEAN";
        private const string TypeSetName = "Pset_WallCommonFromType";
        private const string TypePropertyName = "LoadBearing";
        private const string TypePropertyValue = "FALSE";

        private const string MaterialName = "Concrete";
        private const string LayerSetName = "Generic - 200mm";
        private const float MaterialThickness = 0.25f;
        private const int MaterialLocalId = 81;
        private const string ClassificationName = "Uniclass";
        private const string ClassificationValue = "EF_25_10";
        private const string ClassificationType = "IFCCLASSIFICATIONREFERENCE";

        private const string RelationName = "ContainedInStructure";
        private const int FirstRelatedLocalId = 91;
        private const int SecondRelatedLocalId = 92;

        private const string NotJsonPayload = "IFC2X3 header text that is not JSON";
        private static readonly byte[] CorruptPayload = { 0x46, 0x52, 0x41, 0x47 };

        [Test]
        public void Create_PersistsOnlyInstancedItemsDedupedInFirstInstanceOrder()
        {
            var result = new FragmentImportResult();
            result.Items.Add(FragmentModelFixture.Item(0, WallCategory, WallName));
            result.Items.Add(FragmentModelFixture.Item(1, WallCategory, SecondWallName));
            result.Items.Add(FragmentModelFixture.Item(2, WallCategory, ThirdWallName));
            result.Items.Add(FragmentModelFixture.Item(3, DoorCategory, DoorName));
            result.Items.Add(FragmentModelFixture.Item(4, DoorCategory, SecondDoorName));
            AddInstance(result, 3);
            AddInstance(result, 1);
            AddInstance(result, 3);
            AddInstance(result, LocalIdWithoutItem);
            AddInstance(result, 0);

            FragmentModelData data = FragmentModelAsset.Create(result).Load();

            Assert.That(data.Items.Select(item => item.LocalId), Is.EqualTo(new[] { 3, 1, 0 }));
            Assert.That(data.Items.Select(item => item.Name),
                Is.EqualTo(new[] { DoorName, SecondWallName, WallName }));
        }

        [Test]
        public void Create_ResultWithoutInstances_PersistsNoItems()
        {
            var result = new FragmentImportResult();
            result.Items.Add(FragmentModelFixture.Item(0, WallCategory, WallName));

            FragmentModelData data = FragmentModelAsset.Create(result).Load();

            Assert.That(data.Items, Is.Empty);
        }

        [Test]
        public void Create_NamesTheSubAssetModelData()
        {
            FragmentModelAsset asset = FragmentModelAsset.Create(new FragmentImportResult());

            Assert.That(asset.name, Is.EqualTo(ExpectedAssetName));
        }

        [Test]
        public void Create_RoundTripsModelIdentityAndRawHeader()
        {
            var result = new FragmentImportResult
            {
                ModelName = ModelName,
                ModelGuid = ModelGuid,
                Metadata = HeaderJson,
                ModelInfo = FragmentModelFixture.Item(-1, ModelInfoCategory, ModelInfoName)
            };
            result.ModelInfo.Attributes.Add(new FragmentAttribute(AttributeName, AttributeValue, AttributeType));

            FragmentModelData data = FragmentModelAsset.Create(result).Load();

            Assert.Multiple(() =>
            {
                Assert.That(data.ModelName, Is.EqualTo(ModelName));
                Assert.That(data.ModelGuid, Is.EqualTo(ModelGuid));
                Assert.That(data.Metadata, Is.EqualTo(HeaderJson));
                Assert.That(data.ModelInfo.Category, Is.EqualTo(ModelInfoCategory));
                Assert.That(data.ModelInfo.Name, Is.EqualTo(ModelInfoName));
                Assert.That(data.ModelInfo.Attributes.Single().Value, Is.EqualTo(AttributeValue));
            });
        }

        [Test]
        public void Create_RoundTripsItemIdentityAndPlacement()
        {
            FragmentItemMetadata loaded = LoadSingle(RichWall());

            Assert.Multiple(() =>
            {
                Assert.That(loaded.LocalId, Is.EqualTo(WallLocalId));
                Assert.That(loaded.GlobalId, Is.EqualTo(WallGlobalId));
                Assert.That(loaded.ExpressId, Is.EqualTo(WallExpressId));
                Assert.That(loaded.Category, Is.EqualTo(WallCategory));
                Assert.That(loaded.Name, Is.EqualTo(WallName));
                Assert.That(loaded.TypeName, Is.EqualTo(TypeName));
                Assert.That(loaded.TypeLocalId, Is.EqualTo(TypeLocalId));
                Assert.That(loaded.ContainerName, Is.EqualTo(ContainerName));
                Assert.That(loaded.ContainerCategory, Is.EqualTo(ContainerCategory));
                Assert.That(loaded.ContainerLocalId, Is.EqualTo(ContainerLocalId));
                Assert.That(loaded.StoreyName, Is.EqualTo(StoreyName));
                Assert.That(loaded.StoreyLocalId, Is.EqualTo(StoreyLocalId));
            });
        }

        [Test]
        public void Create_RoundTripsAttributesAndPropertySets()
        {
            FragmentItemMetadata loaded = LoadSingle(RichWall());

            FragmentAttribute attribute = loaded.Attributes.Single();
            FragmentPropertySet instanceSet = loaded.PropertySets[0];
            FragmentPropertySet typeSet = loaded.PropertySets[1];

            Assert.Multiple(() =>
            {
                Assert.That(attribute.Name, Is.EqualTo(AttributeName));
                Assert.That(attribute.Value, Is.EqualTo(AttributeValue));
                Assert.That(attribute.Type, Is.EqualTo(AttributeType));
                Assert.That(instanceSet.Name, Is.EqualTo(SetName));
                Assert.That(instanceSet.LocalId, Is.EqualTo(SetLocalId));
                Assert.That(instanceSet.FromType, Is.False);
                Assert.That(instanceSet.Properties.Single().Name, Is.EqualTo(PropertyName));
                Assert.That(instanceSet.Properties.Single().Value, Is.EqualTo(PropertyValue));
                Assert.That(instanceSet.Properties.Single().Type, Is.EqualTo(PropertyType));
                Assert.That(typeSet.Name, Is.EqualTo(TypeSetName));
                Assert.That(typeSet.FromType, Is.True);
                Assert.That(typeSet.Properties.Single().Value, Is.EqualTo(TypePropertyValue));
            });
        }

        [Test]
        public void Create_RoundTripsMaterialsAndClassifications()
        {
            FragmentItemMetadata loaded = LoadSingle(RichWall());

            FragmentMaterial material = loaded.Materials.Single();
            FragmentAttribute classification = loaded.Classifications.Single();

            Assert.Multiple(() =>
            {
                Assert.That(material.Name, Is.EqualTo(MaterialName));
                Assert.That(material.LayerSetName, Is.EqualTo(LayerSetName));
                Assert.That(material.Thickness, Is.EqualTo(MaterialThickness));
                Assert.That(material.LocalId, Is.EqualTo(MaterialLocalId));
                Assert.That(classification.Name, Is.EqualTo(ClassificationName));
                Assert.That(classification.Value, Is.EqualTo(ClassificationValue));
                Assert.That(classification.Type, Is.EqualTo(ClassificationType));
            });
        }

        [Test]
        public void Create_RoundTripsRelationsWithRelatedLocalIds()
        {
            FragmentItemMetadata loaded = LoadSingle(RichWall());

            FragmentRelation relation = loaded.Relations.Single();

            Assert.That(relation.Name, Is.EqualTo(RelationName));
            Assert.That(relation.RelatedLocalIds,
                Is.EqualTo(new[] { FirstRelatedLocalId, SecondRelatedLocalId }));
        }

        [Test]
        public void Load_AssetThatWasNeverCreated_ReturnsEmptyData()
        {
            FragmentModelData data = ScriptableObject.CreateInstance<FragmentModelAsset>().Load();

            Assert.Multiple(() =>
            {
                Assert.That(data, Is.Not.Null);
                Assert.That(data.Items, Is.Empty);
                Assert.That(data.ModelName, Is.Empty);
                Assert.That(data.ModelGuid, Is.Empty);
                Assert.That(data.Metadata, Is.Empty);
                Assert.That(data.ModelInfo, Is.Not.Null);
                Assert.That(data.FindItem(WallLocalId), Is.Null);
            });
        }

        [Test]
        public void Load_CalledTwice_ReturnsIndependentInstances()
        {
            FragmentModelAsset asset = FragmentModelAsset.Create(
                FragmentModelFixture.ResultWith(new[] { RichWall() }));

            FragmentModelData first = asset.Load();
            FragmentModelData second = asset.Load();

            Assert.That(second, Is.Not.SameAs(first));
            Assert.That(second.Items.Single().GlobalId, Is.EqualTo(first.Items.Single().GlobalId));
        }

        [Test]
        public void Load_PayloadThatIsNotGzip_ReturnsEmptyDataInsteadOfThrowing()
        {
            FragmentModelAsset asset = AssetWithOneWall();
            OverwritePayload(asset, CorruptPayload);

            FragmentModelData data = asset.Load();

            Assert.Multiple(() =>
            {
                Assert.That(data, Is.Not.Null);
                Assert.That(data.Items, Is.Empty);
                Assert.That(data.ModelInfo, Is.Not.Null);
            });
        }

        [Test]
        public void Load_PayloadThatIsNotJson_ReturnsEmptyDataInsteadOfThrowing()
        {
            FragmentModelAsset asset = AssetWithOneWall();
            OverwritePayload(asset, Gzip(NotJsonPayload));

            FragmentModelData data = asset.Load();

            Assert.Multiple(() =>
            {
                Assert.That(data, Is.Not.Null);
                Assert.That(data.Items, Is.Empty);
            });
        }

        [Test]
        public void Create_RepeatedFromTheSameResult_ProducesIdenticalPayloads()
        {
            FragmentImportResult result = FragmentModelFixture.ResultWith(new[] { RichWall() });
            result.ModelName = ModelName;
            result.Metadata = HeaderJson;

            byte[] first = CompressedPayloadOf(FragmentModelAsset.Create(result));
            byte[] second = CompressedPayloadOf(FragmentModelAsset.Create(result));

            Assert.That(first, Is.Not.Empty);
            Assert.That(second, Is.EqualTo(first));
        }

        private static FragmentItemMetadata RichWall()
        {
            FragmentItemMetadata item = FragmentModelFixture.Item(WallLocalId, WallCategory, WallName);
            item.GlobalId = WallGlobalId;
            item.ExpressId = WallExpressId;
            item.TypeName = TypeName;
            item.TypeLocalId = TypeLocalId;
            item.ContainerName = ContainerName;
            item.ContainerCategory = ContainerCategory;
            item.ContainerLocalId = ContainerLocalId;
            item.StoreyName = StoreyName;
            item.StoreyLocalId = StoreyLocalId;

            item.Attributes.Add(new FragmentAttribute(AttributeName, AttributeValue, AttributeType));

            var instanceSet = new FragmentPropertySet { Name = SetName, LocalId = SetLocalId, FromType = false };
            instanceSet.Properties.Add(new FragmentAttribute(PropertyName, PropertyValue, PropertyType));
            item.PropertySets.Add(instanceSet);

            var typeSet = new FragmentPropertySet { Name = TypeSetName, FromType = true };
            typeSet.Properties.Add(new FragmentAttribute(TypePropertyName, TypePropertyValue, PropertyType));
            item.PropertySets.Add(typeSet);

            item.Materials.Add(new FragmentMaterial
            {
                Name = MaterialName,
                LayerSetName = LayerSetName,
                Thickness = MaterialThickness,
                LocalId = MaterialLocalId
            });
            item.Classifications.Add(
                new FragmentAttribute(ClassificationName, ClassificationValue, ClassificationType));

            var relation = new FragmentRelation { Name = RelationName };
            relation.RelatedLocalIds.Add(FirstRelatedLocalId);
            relation.RelatedLocalIds.Add(SecondRelatedLocalId);
            item.Relations.Add(relation);

            return item;
        }

        private static FragmentItemMetadata LoadSingle(FragmentItemMetadata item)
        {
            return FragmentModelFixture.LoadedDataFor(item).Items.Single();
        }

        private static FragmentModelAsset AssetWithOneWall()
        {
            return FragmentModelAsset.Create(FragmentModelFixture.ResultWith(new[] { RichWall() }));
        }

        private static byte[] Gzip(string text)
        {
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
            {
                byte[] payload = Encoding.UTF8.GetBytes(text);
                gzip.Write(payload, 0, payload.Length);
            }
            return output.ToArray();
        }

        private static void AddInstance(FragmentImportResult result, int localId)
        {
            result.Instances.Add(new FragmentInstance { LocalId = localId });
        }

        private static byte[] CompressedPayloadOf(FragmentModelAsset asset)
        {
            return (byte[])CompressedMetadataField().GetValue(asset);
        }

        private static void OverwritePayload(FragmentModelAsset asset, byte[] payload)
        {
            CompressedMetadataField().SetValue(asset, payload);
        }

        private static FieldInfo CompressedMetadataField()
        {
            FieldInfo field = typeof(FragmentModelAsset).GetField(
                CompressedMetadataFieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return field;
        }
    }
}
