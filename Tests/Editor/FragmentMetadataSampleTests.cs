using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace FragmentsUnity.Tests
{
    [TestFixture]
    public sealed class FragmentMetadataSampleTests
    {
        private const string SampleDirEnvironmentVariable = "FRAGMENTSUNITY_SAMPLE_DIR";
        private const string Ar520FileName = "AR520.frag";
        private const string JoysonFileName = "Joyson Model.frag";

        private const string Ar520DoorGlobalId = "1MXYWRlOjEm8W0fkpMpNTl";
        private const string Ar520DoorCategory = "IFCDOOR";
        private const string Ar520DoorNamePrefix = "DOR - Curtain-Wall-Double";
        private const string Ar520StoreyName = "GF-GROUND FLOOR - FFL";
        private const string Ar520DoorContainerCategory = "IFCCURTAINWALL";
        private const string DoorCommonSetName = "Pset_DoorCommon";
        private const string IsExternalPropertyName = "IsExternal";
        private const string IsExternalTrueValue = "true";
        private const string AluminumMaterialName = "Aluminum";
        private const string GlassMaterialName = "Glass";

        private const string Ar520SlabGlobalId = "3X5KAguVb6zxUkPAK059OM";
        private const string Ar520SlabCategory = "IFCSLAB";
        private const string TilesMaterialName = "Tiles";
        private const string TilesLayerSetName = "Floor:Floor Finish - 400mm";
        private const float TilesThickness = 400f;

        private const string SchemaAttributeName = "IFC Schema";
        private const string Ar520SchemaValue = "IFC2X3";
        private const string UnitsSetName = "Units";
        private const string LengthUnitPropertyName = "LENGTHUNIT";
        private const string Ar520LengthUnitValue = "MILLIMETRE";

        private const int Ar520ItemCount = 12952;
        private const int Ar520ItemsWithPropertySets = 1299;
        private const int Ar520PropertySetCount = 15657;
        private const int Ar520PropertyCount = 34110;
        private const int Ar520ItemsWithMaterials = 1276;
        private const int Ar520ItemsWithStorey = 1117;

        private const string JoysonWallGlobalId = "1zzhLY2$PAnwlHvBOzeJsG";
        private const string JoysonWallCategory = "IFCWALLSTANDARDCASE";
        private const string JoysonWallStoreyName = "Level 1";
        private const string RigidInsulationMaterialName = "Rigid Insulation";
        private const float RigidInsulationThickness = 0.0833333f;
        private const float ThicknessTolerance = 1e-4f;

        private static readonly Dictionary<string, FragmentImportResult> LoadedSamples =
            new Dictionary<string, FragmentImportResult>();

        [Test]
        public void LoadFromFile_Ar520Door_MatchesIdentityAndContainment()
        {
            FragmentItemMetadata door = FindByGlobalId(LoadSampleOrIgnore(Ar520FileName), Ar520DoorGlobalId);

            Assert.That(door.Category, Is.EqualTo(Ar520DoorCategory));
            Assert.That(door.Name, Does.StartWith(Ar520DoorNamePrefix));
            Assert.That(door.StoreyName, Is.EqualTo(Ar520StoreyName));
            Assert.That(door.ContainerCategory, Is.EqualTo(Ar520DoorContainerCategory));
        }

        [Test]
        public void LoadFromFile_Ar520Door_CarriesDoorCommonIsExternal()
        {
            FragmentItemMetadata door = FindByGlobalId(LoadSampleOrIgnore(Ar520FileName), Ar520DoorGlobalId);

            Assert.That(door.FindPropertySet(DoorCommonSetName), Is.Not.Null);
            FragmentAttribute isExternal = door.FindProperty(DoorCommonSetName, IsExternalPropertyName);
            Assert.That(isExternal, Is.Not.Null);
            Assert.That(isExternal.Value, Is.EqualTo(IsExternalTrueValue));
        }

        [Test]
        public void LoadFromFile_Ar520Door_CarriesAluminumAndGlassMaterials()
        {
            FragmentItemMetadata door = FindByGlobalId(LoadSampleOrIgnore(Ar520FileName), Ar520DoorGlobalId);

            List<string> materialNames = door.Materials.Select(material => material.Name).ToList();
            Assert.That(materialNames, Contains.Item(AluminumMaterialName));
            Assert.That(materialNames, Contains.Item(GlassMaterialName));
        }

        [Test]
        public void LoadFromFile_Ar520Slab_CarriesTilesLayerMaterial()
        {
            FragmentItemMetadata slab = FindByGlobalId(LoadSampleOrIgnore(Ar520FileName), Ar520SlabGlobalId);

            Assert.That(slab.Category, Is.EqualTo(Ar520SlabCategory));
            FragmentMaterial tiles = slab.Materials.FirstOrDefault(
                material => material.Name.Equals(TilesMaterialName, StringComparison.Ordinal));
            Assert.That(tiles, Is.Not.Null);
            Assert.That(tiles.LayerSetName, Is.EqualTo(TilesLayerSetName));
            Assert.That(tiles.Thickness, Is.EqualTo(TilesThickness).Within(ThicknessTolerance));
        }

        [Test]
        public void LoadFromFile_Ar520ModelInfo_ReportsSchemaAndLengthUnit()
        {
            FragmentImportResult result = LoadSampleOrIgnore(Ar520FileName);

            FragmentAttribute schema = result.ModelInfo.FindAttribute(SchemaAttributeName);
            Assert.That(schema, Is.Not.Null);
            Assert.That(schema.Value, Is.EqualTo(Ar520SchemaValue));

            Assert.That(result.ModelInfo.FindPropertySet(UnitsSetName), Is.Not.Null);
            FragmentAttribute lengthUnit = result.ModelInfo.FindProperty(UnitsSetName, LengthUnitPropertyName);
            Assert.That(lengthUnit, Is.Not.Null);
            Assert.That(lengthUnit.Value, Is.EqualTo(Ar520LengthUnitValue));
        }

        [Test]
        public void LoadFromFile_Ar520_MatchesMetadataAggregatePins()
        {
            FragmentImportResult result = LoadSampleOrIgnore(Ar520FileName);

            Assert.That(result.Items, Has.Count.EqualTo(Ar520ItemCount));
            Assert.That(result.Items.Count(item => item.PropertySets.Count > 0),
                Is.EqualTo(Ar520ItemsWithPropertySets));
            Assert.That(result.Items.Sum(item => item.PropertySets.Count),
                Is.EqualTo(Ar520PropertySetCount));
            Assert.That(result.Items.Sum(item => item.PropertySets.Sum(set => set.Properties.Count)),
                Is.EqualTo(Ar520PropertyCount));
            Assert.That(result.Items.Count(item => item.Materials.Count > 0),
                Is.EqualTo(Ar520ItemsWithMaterials));
            Assert.That(result.Items.Count(item => item.StoreyLocalId >= 0),
                Is.EqualTo(Ar520ItemsWithStorey));
        }

        [Test]
        public void LoadFromFile_JoysonWall_MatchesStoreyAndInsulationThickness()
        {
            FragmentItemMetadata wall = FindByGlobalId(LoadSampleOrIgnore(JoysonFileName), JoysonWallGlobalId);

            Assert.That(wall.Category, Is.EqualTo(JoysonWallCategory));
            Assert.That(wall.StoreyName, Is.EqualTo(JoysonWallStoreyName));
            FragmentMaterial insulation = wall.Materials.FirstOrDefault(
                material => material.Name.Equals(RigidInsulationMaterialName, StringComparison.Ordinal));
            Assert.That(insulation, Is.Not.Null);
            Assert.That(insulation.Thickness, Is.EqualTo(RigidInsulationThickness).Within(ThicknessTolerance));
        }

        private static FragmentItemMetadata FindByGlobalId(FragmentImportResult result, string globalId)
        {
            FragmentItemMetadata found = result.Items.FirstOrDefault(
                item => item.GlobalId.Equals(globalId, StringComparison.Ordinal));

            Assert.That(found, Is.Not.Null, $"No item with GlobalId '{globalId}'.");
            return found;
        }

        private static FragmentImportResult LoadSampleOrIgnore(string fileName)
        {
            if (LoadedSamples.TryGetValue(fileName, out FragmentImportResult cached))
            {
                return cached;
            }

            FragmentImportResult result = FragmentParser.LoadFromFile(ResolveSamplePathOrIgnore(fileName));
            Assert.That(result.Success, Is.True, result.ErrorMessage);

            LoadedSamples[fileName] = result;
            return result;
        }

        private static string ResolveSamplePathOrIgnore(string fileName)
        {
            string sampleDir = Environment.GetEnvironmentVariable(SampleDirEnvironmentVariable);
            if (string.IsNullOrEmpty(sampleDir))
            {
                Assert.Ignore(
                    $"Set {SampleDirEnvironmentVariable} to the directory holding sample .frag files to run this test.");
            }

            string path = Path.Combine(sampleDir, fileName);
            if (!File.Exists(path))
            {
                Assert.Ignore($"Sample file '{fileName}' not found in {sampleDir}.");
            }

            return path;
        }
    }
}
