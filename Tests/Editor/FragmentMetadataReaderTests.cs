using Google.FlatBuffers;
using NUnit.Framework;

namespace FragmentsUnity.Tests
{
    [TestFixture]
    public sealed class FragmentMetadataReaderTests
    {
        private const int BuilderInitialBytes = 2048;
        private const string ModelGuid = "synthetic-model";

        private const uint BeamExpressId = 100;
        private const uint ColumnExpressId = 200;
        private const uint WallExpressId = 300;
        private const int BeamLocalId = 0;
        private const int ColumnLocalId = 1;
        private const int WallLocalId = 2;
        private const int ItemCount = 3;

        private const string BeamCategory = "IFCBEAM";
        private const string ColumnCategory = "IFCCOLUMN";
        private const string WallCategory = "IFCWALL";

        private const string BeamName = "Beam A";
        private const string BeamNameTuple = "[\"Name\",\"Beam A\"]";
        private const string BeamTagTuple = "[\"Tag\",\"T1\",\"IFCIDENTIFIER\"]";
        private const string ColumnNameTuple = "[\"Name\",\"Column B\"]";
        private const string WallNameTuple = "[\"Name\",\"Wall C\"]";

        private const string DefinedByRelationName = "IsDefinedBy";
        private const string DefinedByWallTuple = "[\"IsDefinedBy\",\"300\"]";
        private const string DefinedByUnknownTuple = "[\"IsDefinedBy\",\"999999\"]";

        private const string BeamGlobalId = "0synthetic0beam0guid00";

        [Test]
        public void LoadFromBuffer_SyntheticModel_ReadsOneItemPerLocalId()
        {
            FragmentImportResult result = LoadSyntheticModel();

            Assert.That(result.Success, Is.True, result.ErrorMessage);
            Assert.That(result.Items, Has.Count.EqualTo(ItemCount));
        }

        [Test]
        public void LoadFromBuffer_AttributeTuples_CarryNameValueAndType()
        {
            FragmentItemMetadata beam = LoadSyntheticModel().Items[BeamLocalId];

            Assert.That(beam.Attributes, Has.Count.EqualTo(2));
            Assert.That(beam.Attributes[0].Name, Is.EqualTo("Name"));
            Assert.That(beam.Attributes[0].Value, Is.EqualTo(BeamName));
            Assert.That(beam.Attributes[0].Type, Is.Empty);
            Assert.That(beam.Attributes[1].Name, Is.EqualTo("Tag"));
            Assert.That(beam.Attributes[1].Value, Is.EqualTo("T1"));
            Assert.That(beam.Attributes[1].Type, Is.EqualTo("IFCIDENTIFIER"));
        }

        [Test]
        public void LoadFromBuffer_NameTuple_FillsItemName()
        {
            FragmentImportResult result = LoadSyntheticModel();

            Assert.That(result.Items[BeamLocalId].Name, Is.EqualTo(BeamName));
        }

        [Test]
        public void LoadFromBuffer_RelationTargets_ResolveToDenseLocalIds()
        {
            FragmentItemMetadata beam = LoadSyntheticModel().Items[BeamLocalId];

            Assert.That(beam.Relations, Has.Count.EqualTo(1));
            Assert.That(beam.Relations[0].Name, Is.EqualTo(DefinedByRelationName));
            Assert.That(beam.Relations[0].RelatedLocalIds, Is.EqualTo(new[] { WallLocalId }));
        }

        [Test]
        public void LoadFromBuffer_RelationToUnknownExpressId_YieldsNoTargets()
        {
            FragmentItemMetadata column = LoadSyntheticModel().Items[ColumnLocalId];

            Assert.That(column.Relations, Has.Count.EqualTo(1));
            Assert.That(column.Relations[0].Name, Is.EqualTo(DefinedByRelationName));
            Assert.That(column.Relations[0].RelatedLocalIds, Is.Empty);
        }

        [Test]
        public void LoadFromBuffer_CategoriesVector_LandsOnItems()
        {
            FragmentImportResult result = LoadSyntheticModel();

            Assert.That(result.Items[BeamLocalId].Category, Is.EqualTo(BeamCategory));
            Assert.That(result.Items[ColumnLocalId].Category, Is.EqualTo(ColumnCategory));
            Assert.That(result.Items[WallLocalId].Category, Is.EqualTo(WallCategory));
        }

        [Test]
        public void LoadFromBuffer_GuidsPairing_FillsGlobalIdOfThePairedItemOnly()
        {
            FragmentImportResult result = LoadSyntheticModel();

            Assert.That(result.Items[BeamLocalId].GlobalId, Is.EqualTo(BeamGlobalId));
            Assert.That(result.Items[ColumnLocalId].GlobalId, Is.Empty);
            Assert.That(result.Items[WallLocalId].GlobalId, Is.Empty);
        }

        [Test]
        public void LoadFromBuffer_LocalIdsVector_MapsDenseIndexToExpressId()
        {
            FragmentImportResult result = LoadSyntheticModel();

            Assert.That(result.Items[BeamLocalId].LocalId, Is.EqualTo(BeamLocalId));
            Assert.That(result.Items[BeamLocalId].ExpressId, Is.EqualTo(BeamExpressId));
            Assert.That(result.Items[ColumnLocalId].ExpressId, Is.EqualTo(ColumnExpressId));
            Assert.That(result.Items[WallLocalId].ExpressId, Is.EqualTo(WallExpressId));
        }

        private static FragmentImportResult LoadSyntheticModel()
        {
            return FragmentParser.LoadFromBuffer(BuildModelBuffer());
        }

        private static byte[] BuildModelBuffer()
        {
            var builder = new FlatBufferBuilder(BuilderInitialBytes);

            Offset<Schema.Attribute>[] attributes =
            {
                BuildAttribute(builder, BeamNameTuple, BeamTagTuple),
                BuildAttribute(builder, ColumnNameTuple),
                BuildAttribute(builder, WallNameTuple)
            };
            VectorOffset attributesVector = Schema.Model.CreateAttributesVector(builder, attributes);

            Offset<Schema.Relation>[] relations =
            {
                BuildRelation(builder, DefinedByWallTuple),
                BuildRelation(builder, DefinedByUnknownTuple)
            };
            VectorOffset relationsVector = Schema.Model.CreateRelationsVector(builder, relations);
            VectorOffset relationsItems = Schema.Model.CreateRelationsItemsVector(
                builder, new[] { (int)BeamExpressId, (int)ColumnExpressId });

            VectorOffset guids = Schema.Model.CreateGuidsVector(
                builder, new[] { builder.CreateString(BeamGlobalId) });
            VectorOffset guidsItems = Schema.Model.CreateGuidsItemsVector(builder, new[] { BeamExpressId });
            VectorOffset localIds = Schema.Model.CreateLocalIdsVector(
                builder, new[] { BeamExpressId, ColumnExpressId, WallExpressId });
            VectorOffset categories = Schema.Model.CreateCategoriesVector(builder, new[]
            {
                builder.CreateString(BeamCategory),
                builder.CreateString(ColumnCategory),
                builder.CreateString(WallCategory)
            });

            Offset<Schema.Meshes> meshes = BuildEmptyMeshes(builder);
            StringOffset guid = builder.CreateString(ModelGuid);

            Offset<Schema.Model> model = Schema.Model.CreateModel(
                builder,
                guidsOffset: guids,
                guids_itemsOffset: guidsItems,
                local_idsOffset: localIds,
                categoriesOffset: categories,
                meshesOffset: meshes,
                attributesOffset: attributesVector,
                relationsOffset: relationsVector,
                relations_itemsOffset: relationsItems,
                guidOffset: guid);
            builder.Finish(model.Value);

            return builder.SizedByteArray();
        }

        private static Offset<Schema.Attribute> BuildAttribute(FlatBufferBuilder builder, params string[] tuples)
        {
            var tupleOffsets = new StringOffset[tuples.Length];
            for (int i = 0; i < tuples.Length; i++)
            {
                tupleOffsets[i] = builder.CreateString(tuples[i]);
            }
            return Schema.Attribute.CreateAttribute(
                builder, Schema.Attribute.CreateDataVector(builder, tupleOffsets));
        }

        private static Offset<Schema.Relation> BuildRelation(FlatBufferBuilder builder, params string[] tuples)
        {
            var tupleOffsets = new StringOffset[tuples.Length];
            for (int i = 0; i < tuples.Length; i++)
            {
                tupleOffsets[i] = builder.CreateString(tuples[i]);
            }
            return Schema.Relation.CreateRelation(
                builder, Schema.Relation.CreateDataVector(builder, tupleOffsets));
        }

        private static Offset<Schema.Meshes> BuildEmptyMeshes(FlatBufferBuilder builder)
        {
            VectorOffset meshesItems = Schema.Meshes.CreateMeshesItemsVector(builder, new uint[0]);
            Schema.Meshes.StartSamplesVector(builder, 0);
            VectorOffset samples = builder.EndVector();
            Schema.Meshes.StartRepresentationsVector(builder, 0);
            VectorOffset representations = builder.EndVector();
            Schema.Meshes.StartMaterialsVector(builder, 0);
            VectorOffset materials = builder.EndVector();
            VectorOffset circleExtrusions = Schema.Meshes.CreateCircleExtrusionsVector(
                builder, new Offset<Schema.CircleExtrusion>[0]);
            VectorOffset shells = Schema.Meshes.CreateShellsVector(builder, new Offset<Schema.Shell>[0]);
            Schema.Meshes.StartLocalTransformsVector(builder, 0);
            VectorOffset localTransforms = builder.EndVector();
            Schema.Meshes.StartGlobalTransformsVector(builder, 0);
            VectorOffset globalTransforms = builder.EndVector();

            Schema.Meshes.StartMeshes(builder);
            Offset<Schema.Transform> coordinates = Schema.Transform.CreateTransform(
                builder, 0.0, 0.0, 0.0, 1.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f);
            Schema.Meshes.AddCoordinates(builder, coordinates);
            Schema.Meshes.AddMeshesItems(builder, meshesItems);
            Schema.Meshes.AddSamples(builder, samples);
            Schema.Meshes.AddRepresentations(builder, representations);
            Schema.Meshes.AddMaterials(builder, materials);
            Schema.Meshes.AddCircleExtrusions(builder, circleExtrusions);
            Schema.Meshes.AddShells(builder, shells);
            Schema.Meshes.AddLocalTransforms(builder, localTransforms);
            Schema.Meshes.AddGlobalTransforms(builder, globalTransforms);
            return Schema.Meshes.EndMeshes(builder);
        }
    }
}
