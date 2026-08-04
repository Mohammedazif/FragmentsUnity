using Google.FlatBuffers;
using NUnit.Framework;

namespace FragmentsUnity.Tests
{
    [TestFixture]
    public sealed class FragmentNameExtractorTests
    {
        private const string PlainNameTuple = "[\"Name\",\"Wall A\"]";
        private const string EscapedNameTuple = "[\"Name\",\"Say \\\"hi\\\"\"]";
        private const string CategoryOnlyTuple = "[\"Category\",\"IFCWALL\"]";
        private const string PlainName = "Wall A";
        private const string EscapedName = "Say \"hi\"";
        private const string ModelGuid = "synthetic-model";
        private const int PlainNameLocalId = 0;
        private const int EscapedNameLocalId = 1;
        private const int NamelessLocalId = 2;
        private const int OutOfRangeLocalId = 3;
        private const int NegativeLocalId = -1;
        private const int BuilderInitialBytes = 1024;

        [Test]
        public void ExtractName_PlainNameTuple_ReturnsValue()
        {
            FragmentNameExtractor extractor = CreateExtractor();

            Assert.That(extractor.ExtractName(PlainNameLocalId), Is.EqualTo(PlainName));
        }

        [Test]
        public void ExtractName_EscapedQuotesInName_UnescapesValue()
        {
            FragmentNameExtractor extractor = CreateExtractor();

            Assert.That(extractor.ExtractName(EscapedNameLocalId), Is.EqualTo(EscapedName));
        }

        [Test]
        public void ExtractName_ItemWithoutNameTuple_ReturnsEmpty()
        {
            FragmentNameExtractor extractor = CreateExtractor();

            Assert.That(extractor.ExtractName(NamelessLocalId), Is.Empty);
        }

        [Test]
        public void ExtractName_LocalIdPastAttributesVector_ReturnsEmpty()
        {
            FragmentNameExtractor extractor = CreateExtractor();

            Assert.That(extractor.ExtractName(OutOfRangeLocalId), Is.Empty);
        }

        [Test]
        public void ExtractName_NegativeLocalId_ReturnsEmpty()
        {
            FragmentNameExtractor extractor = CreateExtractor();

            Assert.That(extractor.ExtractName(NegativeLocalId), Is.Empty);
        }

        private static FragmentNameExtractor CreateExtractor()
        {
            Schema.Model model = BuildModelWithAttributes();
            return new FragmentNameExtractor(model, new FragmentImportResult());
        }

        private static Schema.Model BuildModelWithAttributes()
        {
            var builder = new FlatBufferBuilder(BuilderInitialBytes);

            Offset<Schema.Attribute>[] attributes =
            {
                BuildAttribute(builder, PlainNameTuple),
                BuildAttribute(builder, EscapedNameTuple),
                BuildAttribute(builder, CategoryOnlyTuple)
            };
            VectorOffset attributesVector = Schema.Model.CreateAttributesVector(builder, attributes);

            VectorOffset guids = Schema.Model.CreateGuidsVector(builder, new StringOffset[0]);
            VectorOffset guidsItems = Schema.Model.CreateGuidsItemsVector(builder, new uint[0]);
            VectorOffset localIds = Schema.Model.CreateLocalIdsVector(builder, new uint[0]);
            VectorOffset categories = Schema.Model.CreateCategoriesVector(builder, new StringOffset[0]);
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
                guidOffset: guid);
            builder.Finish(model.Value);

            return Schema.Model.GetRootAsModel(builder.DataBuffer);
        }

        private static Offset<Schema.Attribute> BuildAttribute(FlatBufferBuilder builder, string tuple)
        {
            StringOffset tupleOffset = builder.CreateString(tuple);
            VectorOffset data = Schema.Attribute.CreateDataVector(builder, new[] { tupleOffset });
            return Schema.Attribute.CreateAttribute(builder, data);
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
