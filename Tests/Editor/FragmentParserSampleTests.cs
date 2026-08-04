using System;
using System.IO;
using NUnit.Framework;

namespace FragmentsUnity.Tests
{
    [TestFixture]
    public sealed class FragmentParserSampleTests
    {
        private const string SampleDirEnvironmentVariable = "FRAGMENTSUNITY_SAMPLE_DIR";
        private const string Ar520FileName = "AR520.frag";
        private const string JoysonFileName = "Joyson Model.frag";

        private const int Ar520InstanceCount = 1158;
        private const int Ar520GeometryCount = 128;
        private const int Ar520VertexCount = 10080;
        private const int Ar520TriangleCount = 7536;
        private const int Ar520ElementCount = 8209;
        private const int Ar520CategoryCount = 12952;
        private const string Ar520ModelGuid = "2ff8631c-31be-4f34-95fc-efb1decc84f1";
        private const int Ar520SpatialRootChildCount = 1;

        private const int JoysonInstanceCount = 14257;
        private const int JoysonGeometryCount = 1044;
        private const int JoysonVertexCount = 112030;
        private const int JoysonTriangleCount = 123034;

        private const string ArchitectureFileName = "20210219Architecture.frag";
        private const int ArchitectureInstanceCount = 32555;
        private const int ArchitectureGeometryCount = 4075;
        private const int ArchitectureVertexCount = 390236;
        private const int ArchitectureTriangleCount = 371252;
        private const int ArchitectureElementCount = 101407;
        private const int ArchitectureCategoryCount = 180399;
        private const string ArchitectureModelGuid = "96e64653-4bd1-468d-85ce-208eaf0cfa96";
        private const int ArchitectureSpatialRootChildCount = 1;

        private const int GarbageSeed = 20260803;
        private const int GarbageByteCount = 4096;

        [Test]
        public void LoadFromFile_Ar520Sample_MatchesReferencePins()
        {
            string path = ResolveSamplePathOrIgnore(Ar520FileName);

            FragmentImportResult result = FragmentParser.LoadFromFile(path);

            Assert.That(result.Success, Is.True, result.ErrorMessage);
            Assert.That(result.Instances, Has.Count.EqualTo(Ar520InstanceCount));
            Assert.That(result.Geometries, Has.Count.EqualTo(Ar520GeometryCount));
            Assert.That(result.TotalVertices, Is.EqualTo(Ar520VertexCount));
            Assert.That(result.TotalTriangles, Is.EqualTo(Ar520TriangleCount));
            Assert.That(result.TotalElements, Is.EqualTo(Ar520ElementCount));
            Assert.That(result.Categories, Has.Count.EqualTo(Ar520CategoryCount));
            Assert.That(result.ModelGuid, Is.EqualTo(Ar520ModelGuid));
            Assert.That(result.SpatialRoot.Children, Has.Count.EqualTo(Ar520SpatialRootChildCount));
        }

        [Test]
        public void LoadFromFile_JoysonSample_MatchesReferencePins()
        {
            string path = ResolveSamplePathOrIgnore(JoysonFileName);

            FragmentImportResult result = FragmentParser.LoadFromFile(path);

            Assert.That(result.Success, Is.True, result.ErrorMessage);
            Assert.That(result.Instances, Has.Count.EqualTo(JoysonInstanceCount));
            Assert.That(result.Geometries, Has.Count.EqualTo(JoysonGeometryCount));
            Assert.That(result.TotalVertices, Is.EqualTo(JoysonVertexCount));
            Assert.That(result.TotalTriangles, Is.EqualTo(JoysonTriangleCount));
        }

        [Test]
        public void LoadFromFile_ArchitectureSample_MatchesReferencePins()
        {
            string path = ResolveSamplePathOrIgnore(ArchitectureFileName);

            FragmentImportResult result = FragmentParser.LoadFromFile(path);

            Assert.That(result.Success, Is.True, result.ErrorMessage);
            Assert.That(result.Instances, Has.Count.EqualTo(ArchitectureInstanceCount));
            Assert.That(result.Geometries, Has.Count.EqualTo(ArchitectureGeometryCount));
            Assert.That(result.TotalVertices, Is.EqualTo(ArchitectureVertexCount));
            Assert.That(result.TotalTriangles, Is.EqualTo(ArchitectureTriangleCount));
            Assert.That(result.TotalElements, Is.EqualTo(ArchitectureElementCount));
            Assert.That(result.Categories, Has.Count.EqualTo(ArchitectureCategoryCount));
            Assert.That(result.ModelGuid, Is.EqualTo(ArchitectureModelGuid));
            Assert.That(result.SpatialRoot.Children, Has.Count.EqualTo(ArchitectureSpatialRootChildCount));
        }

        [Test]
        public void LoadFromBuffer_EmptyArray_FailsWithEmptyBufferMessage()
        {
            FragmentImportResult result = FragmentParser.LoadFromBuffer(Array.Empty<byte>());

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Is.EqualTo("Empty buffer"));
        }

        [Test]
        public void LoadFromBuffer_RandomGarbage_FailsFlatBuffersVerification()
        {
            var garbage = new byte[GarbageByteCount];
            new Random(GarbageSeed).NextBytes(garbage);

            FragmentImportResult result = FragmentParser.LoadFromBuffer(garbage);

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.StartWith("FlatBuffers verification failed"));
        }

        [Test]
        public void LoadFromFile_NonexistentPath_FailsWithReadErrorPrefix()
        {
            string missingPath = Path.Combine(
                Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".frag");

            FragmentImportResult result = FragmentParser.LoadFromFile(missingPath);

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.StartWith("Failed to read file"));
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
