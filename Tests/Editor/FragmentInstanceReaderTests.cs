using System;
using System.Collections.Generic;
using System.Numerics;
using Google.FlatBuffers;
using NUnit.Framework;

namespace FragmentsUnity.Tests
{
    [TestFixture]
    public sealed class FragmentInstanceReaderTests
    {
        private const int BuilderInitialBytes = 1024;
        private const float UnitScale = 1f;
        private const uint FirstLocalId = 11;
        private const uint SecondLocalId = 22;
        private const uint OutOfRangeItemIndex = 5;
        private const uint OutOfRangeMaterialIndex = 5;
        private const uint ShellRepresentationId = 3;
        private const uint CircleExtrusionRepresentationId = 9;
        private const double PositionTolerance = 1e-9;
        private const double HandDerivedPositionTolerance = 1e-6;
        private const float RotationTolerance = 1e-6f;
        private const string SkippedWarningFragment = "Skipped 1 sample(s)";
        private const string CircleExtrusionInfoFragment = "1 circle-extrusion";

        [Test]
        public void BuildInstances_ItemIndexPastMeshesItems_SkipsSampleWithWarning()
        {
            Schema.Meshes meshes = BuildMeshes(
                new[] { FirstLocalId },
                new[]
                {
                    (Item: 0u, Material: 0u, Representation: 0u, LocalTransform: 0u),
                    (Item: OutOfRangeItemIndex, Material: 0u, Representation: 0u, LocalTransform: 0u)
                });

            (FragmentImportResult result, List<(FragmentImportSeverity Severity, string Message)> logs) =
                RunBuildInstances(meshes);

            Assert.That(result.Instances, Has.Count.EqualTo(1));
            Assert.That(result.Instances[0].LocalId, Is.EqualTo((int)FirstLocalId));
            Assert.That(result.TotalInstances, Is.EqualTo(1));
            Assert.That(logs, Has.Count.EqualTo(1));
            Assert.That(logs[0].Severity, Is.EqualTo(FragmentImportSeverity.Warning));
            Assert.That(logs[0].Message, Does.Contain(SkippedWarningFragment));
        }

        [Test]
        public void BuildInstances_RepresentationClasses_AssignShellGeometryAndSkipCircleExtrusion()
        {
            Schema.Meshes meshes = BuildMeshes(
                new[] { FirstLocalId, SecondLocalId },
                new[]
                {
                    (Item: 0u, Material: 0u, Representation: 0u, LocalTransform: 0u),
                    (Item: 1u, Material: 0u, Representation: 1u, LocalTransform: 0u)
                },
                new[]
                {
                    (Id: ShellRepresentationId, Class: Schema.RepresentationClass.SHELL),
                    (Id: CircleExtrusionRepresentationId, Class: Schema.RepresentationClass.CIRCLE_EXTRUSION)
                });

            (FragmentImportResult result, List<(FragmentImportSeverity Severity, string Message)> logs) =
                RunBuildInstances(meshes);

            Assert.That(result.Instances, Has.Count.EqualTo(2));
            Assert.That(result.Instances[0].GeometryIndex, Is.EqualTo((int)ShellRepresentationId));
            Assert.That(result.Instances[1].GeometryIndex, Is.EqualTo(-1));
            Assert.That(logs, Has.Count.EqualTo(1));
            Assert.That(logs[0].Severity, Is.EqualTo(FragmentImportSeverity.Info));
            Assert.That(logs[0].Message, Does.Contain(CircleExtrusionInfoFragment));
        }

        [Test]
        public void BuildInstances_MaterialIndex_AssignsFromParallelListsOrDefaults()
        {
            Schema.Meshes meshes = BuildMeshes(
                new[] { FirstLocalId },
                new[]
                {
                    (Item: 0u, Material: 0u, Representation: 0u, LocalTransform: 0u),
                    (Item: 0u, Material: OutOfRangeMaterialIndex, Representation: 0u, LocalTransform: 0u)
                });
            var expectedColor = new Vector4(0.25f, 0.5f, 0.75f, 0.5f);

            (FragmentImportResult result, _) = RunBuildInstances(meshes, new[] { expectedColor }, new[] { true });

            FragmentInstance colored = result.Instances[0];
            Assert.That(colored.MaterialIndex, Is.EqualTo(0));
            Assert.That(colored.Color, Is.EqualTo(expectedColor));
            Assert.That(colored.Opacity, Is.EqualTo(expectedColor.W));
            Assert.That(colored.DoubleSided, Is.True);

            FragmentInstance defaulted = result.Instances[1];
            Assert.That(defaulted.MaterialIndex, Is.EqualTo((int)OutOfRangeMaterialIndex));
            Assert.That(defaulted.Color, Is.EqualTo(Vector4.One));
            Assert.That(defaulted.Opacity, Is.EqualTo(1f));
            Assert.That(defaulted.DoubleSided, Is.False);
        }

        [Test]
        public void BuildInstances_LocalAndGlobalTransforms_ComposeInUnitySpace()
        {
            Schema.Meshes meshes = BuildMeshes(
                new[] { FirstLocalId },
                new[] { (Item: 0u, Material: 0u, Representation: 0u, LocalTransform: 0u) },
                localTransforms: new[] { TransformSeed.Translation(1.0, 0.0, 0.0) },
                globalTransforms: new[] { new TransformSeed(10.0, 0.0, 0.0, 0f, 0f, -1f, 0f, 1f, 0f) });

            (FragmentImportResult result, _) = RunBuildInstances(meshes);

            FragmentTransform local = FragmentCoordinateConverter.BuildTransform(
                1.0, 0.0, 0.0, 1f, 0f, 0f, 0f, 1f, 0f, UnitScale);
            FragmentTransform global = FragmentCoordinateConverter.BuildTransform(
                10.0, 0.0, 0.0, 0f, 0f, -1f, 0f, 1f, 0f, UnitScale);
            FragmentTransform expected = FragmentTransform.Compose(local, global);
            FragmentTransform actual = result.Instances[0].Transform;
            Assert.That(actual.PositionX, Is.EqualTo(expected.PositionX).Within(PositionTolerance));
            Assert.That(actual.PositionY, Is.EqualTo(expected.PositionY).Within(PositionTolerance));
            Assert.That(actual.PositionZ, Is.EqualTo(expected.PositionZ).Within(PositionTolerance));
            Assert.That(actual.Rotation.X, Is.EqualTo(expected.Rotation.X).Within(RotationTolerance));
            Assert.That(actual.Rotation.Y, Is.EqualTo(expected.Rotation.Y).Within(RotationTolerance));
            Assert.That(actual.Rotation.Z, Is.EqualTo(expected.Rotation.Z).Within(RotationTolerance));
            Assert.That(actual.Rotation.W, Is.EqualTo(expected.Rotation.W).Within(RotationTolerance));
            Assert.That(actual.PositionX, Is.EqualTo(-10.0).Within(HandDerivedPositionTolerance));
            Assert.That(actual.PositionY, Is.EqualTo(0.0).Within(HandDerivedPositionTolerance));
            Assert.That(actual.PositionZ, Is.EqualTo(-1.0).Within(HandDerivedPositionTolerance));
        }

        [Test]
        public void BuildInstances_GlobalTransform_IndexedBySampleItemNotSampleOrder()
        {
            Schema.Meshes meshes = BuildMeshes(
                new[] { FirstLocalId, SecondLocalId },
                new[]
                {
                    (Item: 1u, Material: 0u, Representation: 0u, LocalTransform: 0u),
                    (Item: 0u, Material: 0u, Representation: 0u, LocalTransform: 0u)
                },
                localTransforms: new[] { TransformSeed.Translation(0.0, 0.0, 0.0) },
                globalTransforms: new[]
                {
                    TransformSeed.Translation(100.0, 0.0, 0.0),
                    TransformSeed.Translation(0.0, 0.0, 200.0)
                });

            (FragmentImportResult result, _) = RunBuildInstances(meshes);

            Assert.That(result.Instances, Has.Count.EqualTo(2));
            FragmentTransform secondItemTransform = result.Instances[0].Transform;
            Assert.That(result.Instances[0].LocalId, Is.EqualTo((int)SecondLocalId));
            Assert.That(secondItemTransform.PositionX, Is.EqualTo(0.0).Within(PositionTolerance));
            Assert.That(secondItemTransform.PositionZ, Is.EqualTo(200.0).Within(PositionTolerance));
            FragmentTransform firstItemTransform = result.Instances[1].Transform;
            Assert.That(result.Instances[1].LocalId, Is.EqualTo((int)FirstLocalId));
            Assert.That(firstItemTransform.PositionX, Is.EqualTo(-100.0).Within(PositionTolerance));
            Assert.That(firstItemTransform.PositionZ, Is.EqualTo(0.0).Within(PositionTolerance));
        }

        private static (FragmentImportResult Result, List<(FragmentImportSeverity Severity, string Message)> Logs)
            RunBuildInstances(
                Schema.Meshes meshes,
                IReadOnlyList<Vector4> materialColors = null,
                IReadOnlyList<bool> materialDoubleSided = null)
        {
            var result = new FragmentImportResult();
            var logs = new List<(FragmentImportSeverity Severity, string Message)>();
            FragmentInstanceReader.BuildInstances(
                meshes,
                UnitScale,
                materialColors ?? Array.Empty<Vector4>(),
                materialDoubleSided ?? Array.Empty<bool>(),
                new Dictionary<int, string>(),
                result,
                (severity, message) => logs.Add((severity, message)));
            return (result, logs);
        }

        private static Schema.Meshes BuildMeshes(
            uint[] meshesItems,
            (uint Item, uint Material, uint Representation, uint LocalTransform)[] samples,
            (uint Id, Schema.RepresentationClass Class)[] representations = null,
            TransformSeed[] localTransforms = null,
            TransformSeed[] globalTransforms = null)
        {
            representations ??= Array.Empty<(uint Id, Schema.RepresentationClass Class)>();
            localTransforms ??= Array.Empty<TransformSeed>();
            globalTransforms ??= Array.Empty<TransformSeed>();

            var builder = new FlatBufferBuilder(BuilderInitialBytes);
            VectorOffset meshesItemsVector = Schema.Meshes.CreateMeshesItemsVector(builder, meshesItems);
            Schema.Meshes.StartSamplesVector(builder, samples.Length);
            for (int i = samples.Length - 1; i >= 0; i--)
            {
                Schema.Sample.CreateSample(
                    builder, samples[i].Item, samples[i].Material, samples[i].Representation,
                    samples[i].LocalTransform);
            }
            VectorOffset samplesVector = builder.EndVector();
            Schema.Meshes.StartRepresentationsVector(builder, representations.Length);
            for (int i = representations.Length - 1; i >= 0; i--)
            {
                Schema.Representation.CreateRepresentation(
                    builder, representations[i].Id, 0f, 0f, 0f, 0f, 0f, 0f, representations[i].Class);
            }
            VectorOffset representationsVector = builder.EndVector();
            Schema.Meshes.StartMaterialsVector(builder, 0);
            VectorOffset materials = builder.EndVector();
            VectorOffset circleExtrusions = Schema.Meshes.CreateCircleExtrusionsVector(
                builder, Array.Empty<Offset<Schema.CircleExtrusion>>());
            VectorOffset shells = Schema.Meshes.CreateShellsVector(builder, Array.Empty<Offset<Schema.Shell>>());
            Schema.Meshes.StartLocalTransformsVector(builder, localTransforms.Length);
            for (int i = localTransforms.Length - 1; i >= 0; i--)
            {
                AddTransform(builder, localTransforms[i]);
            }
            VectorOffset localTransformsVector = builder.EndVector();
            Schema.Meshes.StartGlobalTransformsVector(builder, globalTransforms.Length);
            for (int i = globalTransforms.Length - 1; i >= 0; i--)
            {
                AddTransform(builder, globalTransforms[i]);
            }
            VectorOffset globalTransformsVector = builder.EndVector();

            Schema.Meshes.StartMeshes(builder);
            Offset<Schema.Transform> coordinates = Schema.Transform.CreateTransform(
                builder, 0.0, 0.0, 0.0, 1f, 0f, 0f, 0f, 1f, 0f);
            Schema.Meshes.AddCoordinates(builder, coordinates);
            Schema.Meshes.AddMeshesItems(builder, meshesItemsVector);
            Schema.Meshes.AddSamples(builder, samplesVector);
            Schema.Meshes.AddRepresentations(builder, representationsVector);
            Schema.Meshes.AddMaterials(builder, materials);
            Schema.Meshes.AddCircleExtrusions(builder, circleExtrusions);
            Schema.Meshes.AddShells(builder, shells);
            Schema.Meshes.AddLocalTransforms(builder, localTransformsVector);
            Schema.Meshes.AddGlobalTransforms(builder, globalTransformsVector);
            Offset<Schema.Meshes> meshes = Schema.Meshes.EndMeshes(builder);
            builder.Finish(meshes.Value);
            return Schema.Meshes.GetRootAsMeshes(builder.DataBuffer);
        }

        private static void AddTransform(FlatBufferBuilder builder, TransformSeed seed)
        {
            Schema.Transform.CreateTransform(
                builder,
                seed.PositionX, seed.PositionY, seed.PositionZ,
                seed.XDirectionX, seed.XDirectionY, seed.XDirectionZ,
                seed.YDirectionX, seed.YDirectionY, seed.YDirectionZ);
        }

        private readonly struct TransformSeed
        {
            public readonly double PositionX;
            public readonly double PositionY;
            public readonly double PositionZ;
            public readonly float XDirectionX;
            public readonly float XDirectionY;
            public readonly float XDirectionZ;
            public readonly float YDirectionX;
            public readonly float YDirectionY;
            public readonly float YDirectionZ;

            public TransformSeed(
                double positionX, double positionY, double positionZ,
                float xDirectionX, float xDirectionY, float xDirectionZ,
                float yDirectionX, float yDirectionY, float yDirectionZ)
            {
                PositionX = positionX;
                PositionY = positionY;
                PositionZ = positionZ;
                XDirectionX = xDirectionX;
                XDirectionY = xDirectionY;
                XDirectionZ = xDirectionZ;
                YDirectionX = yDirectionX;
                YDirectionY = yDirectionY;
                YDirectionZ = yDirectionZ;
            }

            public static TransformSeed Translation(double x, double y, double z)
            {
                return new TransformSeed(x, y, z, 1f, 0f, 0f, 0f, 1f, 0f);
            }
        }
    }
}
