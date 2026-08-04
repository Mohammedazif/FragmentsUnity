using System;
using System.Collections.Generic;
using System.Numerics;
using Google.FlatBuffers;
using NUnit.Framework;

namespace FragmentsUnity.Tests
{
    [TestFixture]
    public sealed class FragmentGeometryBuilderTests
    {
        private const int BuilderInitialBytes = 4096;
        private const float UnitScale = 1f;
        private const int SquareVertexCount = 4;
        private const int SquareTriangleCount = 2;
        private const int SquareIndexCount = 6;
        private const int HoledSquareVertexCount = 8;
        private const int HoledSquareTriangleCount = 8;
        private const int HoledSquareIndexCount = 24;
        private const int MergedCreaseVertexCount = 4;
        private const int UnmergedFoldVertexCount = 6;
        private const int SharedEdgeTriangleCount = 2;
        private const int SharedEdgeIndexCount = 6;
        private const int OversizedRingIndexCount = (int)FragmentImportLimits.MaxFaceRingVertices + 1;
        private const string NonFiniteWarningFragment = "not finite";
        private const string VertexLimitWarningFragment = "vertex limit";

        [Test]
        public void BuildShellGeometries_SquareProfile_EmitsMirroredQuad()
        {
            (FragmentImportResult result, List<(FragmentImportSeverity Severity, string Message)> logs) =
                BuildGeometries(BuildSquareMeshes());

            Assert.That(logs, Is.Empty);
            Assert.That(result.Geometries, Has.Count.EqualTo(1));
            FragmentGeometry geometry = result.Geometries[0];
            Assert.That(geometry.GeometryIndex, Is.EqualTo(0));
            Assert.That(geometry.Positions, Is.EqualTo(new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(-1f, 0f, 0f),
                new Vector3(-1f, 1f, 0f),
                new Vector3(0f, 1f, 0f)
            }));
            Assert.That(geometry.Indices, Has.Count.EqualTo(SquareIndexCount));
            Assert.That(geometry.HasBounds, Is.True);
            Assert.That(geometry.BoundsMin, Is.EqualTo(new Vector3(-1f, 0f, 0f)));
            Assert.That(geometry.BoundsMax, Is.EqualTo(new Vector3(0f, 1f, 0f)));
            Assert.That(result.TotalVertices, Is.EqualTo(SquareVertexCount));
            Assert.That(result.TotalTriangles, Is.EqualTo(SquareTriangleCount));
        }

        [Test]
        public void BuildShellGeometries_SquareProfile_EmittedWindingOpposesStoredNormals()
        {
            (FragmentImportResult result, _) = BuildGeometries(BuildSquareMeshes());

            FragmentGeometry geometry = result.Geometries[0];
            Assert.That(geometry.Normals, Has.Count.EqualTo(SquareVertexCount));
            Assert.That(geometry.Normals, Is.All.EqualTo(new Vector3(0f, 0f, 1f)));
            for (int i = 0; i + 2 < geometry.Indices.Count; i += 3)
            {
                Vector3 first = geometry.Positions[geometry.Indices[i]];
                Vector3 second = geometry.Positions[geometry.Indices[i + 1]];
                Vector3 third = geometry.Positions[geometry.Indices[i + 2]];
                Vector3 windingCross = Vector3.Cross(second - first, third - first);
                Vector3 storedNormal = geometry.Normals[geometry.Indices[i]];
                Assert.That(Vector3.Dot(windingCross, storedNormal), Is.GreaterThan(0f), $"triangle at index {i}");
            }
        }

        [Test]
        public void BuildShellGeometries_BigShellSquare_MatchesRegularShellExactly()
        {
            (FragmentImportResult regular, _) = BuildGeometries(BuildSquareMeshes());
            (FragmentImportResult big, _) = BuildGeometries(BuildBigSquareMeshes());

            AssertGeometriesIdentical(regular.Geometries[0], big.Geometries[0]);
        }

        [Test]
        public void BuildShellGeometries_SquareWithCenteredHole_EmitsEightTrianglesOverEightVertices()
        {
            (FragmentImportResult result, List<(FragmentImportSeverity Severity, string Message)> logs) =
                BuildGeometries(BuildHoledSquareMeshes());

            Assert.That(logs, Is.Empty);
            FragmentGeometry geometry = result.Geometries[0];
            Assert.That(geometry.Positions, Has.Count.EqualTo(HoledSquareVertexCount));
            Assert.That(geometry.Indices, Has.Count.EqualTo(HoledSquareIndexCount));
            Assert.That(result.TotalVertices, Is.EqualTo(HoledSquareVertexCount));
            Assert.That(result.TotalTriangles, Is.EqualTo(HoledSquareTriangleCount));
        }

        [Test]
        public void BuildShellGeometries_BigShellWithCenteredHole_MatchesRegularShellExactly()
        {
            (FragmentImportResult regular, _) = BuildGeometries(BuildHoledSquareMeshes());
            (FragmentImportResult big, _) = BuildGeometries(BuildBigHoledSquareMeshes());

            AssertGeometriesIdentical(regular.Geometries[0], big.Geometries[0]);
        }

        [Test]
        public void BuildShellGeometries_NonFinitePoint_QuarantinedToOriginWithWarning()
        {
            Schema.Meshes meshes = BuildShellMeshes(builder => new[]
            {
                BuildRegularShell(builder, new[]
                {
                    new Vector3(float.NaN, 0f, 0f),
                    new Vector3(1f, 0f, 0f),
                    new Vector3(0f, 1f, 0f)
                }, new[] { new ushort[] { 0, 1, 2 } })
            });

            (FragmentImportResult result, List<(FragmentImportSeverity Severity, string Message)> logs) =
                BuildGeometries(meshes);

            FragmentGeometry geometry = result.Geometries[0];
            Assert.That(geometry.Positions, Has.Count.EqualTo(3));
            Assert.That(geometry.Positions[0], Is.EqualTo(Vector3.Zero));
            Assert.That(logs, Has.Count.EqualTo(1));
            Assert.That(logs[0].Severity, Is.EqualTo(FragmentImportSeverity.Warning));
            Assert.That(logs[0].Message, Does.Contain(NonFiniteWarningFragment));
        }

        [Test]
        public void BuildShellGeometries_RingPastVertexLimit_DroppedWithWarning()
        {
            Schema.Meshes meshes = BuildShellMeshes(builder => new[]
            {
                BuildRegularShell(builder, SquarePoints(), new[] { new ushort[OversizedRingIndexCount] })
            });

            (FragmentImportResult result, List<(FragmentImportSeverity Severity, string Message)> logs) =
                BuildGeometries(meshes);

            FragmentGeometry geometry = result.Geometries[0];
            Assert.That(geometry.Positions, Is.Empty);
            Assert.That(geometry.Indices, Is.Empty);
            Assert.That(geometry.HasBounds, Is.False);
            Assert.That(result.TotalVertices, Is.EqualTo(0));
            Assert.That(result.TotalTriangles, Is.EqualTo(0));
            Assert.That(logs, Has.Count.EqualTo(1));
            Assert.That(logs[0].Severity, Is.EqualTo(FragmentImportSeverity.Warning));
            Assert.That(logs[0].Message, Does.Contain(VertexLimitWarningFragment));
        }

        [Test]
        public void BuildShellGeometries_TwoIndexProfile_SkippedSilentlyKeepingShellAlignment()
        {
            Schema.Meshes meshes = BuildShellMeshes(builder => new[]
            {
                BuildRegularShell(builder, SquarePoints(), new[] { new ushort[] { 0, 1 } }),
                BuildRegularShell(builder, SquarePoints(), new[] { SquareRing() })
            });

            (FragmentImportResult result, List<(FragmentImportSeverity Severity, string Message)> logs) =
                BuildGeometries(meshes);

            Assert.That(logs, Is.Empty);
            Assert.That(result.Geometries, Has.Count.EqualTo(2));
            Assert.That(result.Geometries[0].GeometryIndex, Is.EqualTo(0));
            Assert.That(result.Geometries[0].Positions, Is.Empty);
            Assert.That(result.Geometries[0].Indices, Is.Empty);
            Assert.That(result.Geometries[1].GeometryIndex, Is.EqualTo(1));
            Assert.That(result.Geometries[1].Positions, Has.Count.EqualTo(SquareVertexCount));
            Assert.That(result.TotalVertices, Is.EqualTo(SquareVertexCount));
            Assert.That(result.TotalTriangles, Is.EqualTo(SquareTriangleCount));
        }

        [Test]
        public void BuildShellGeometries_NearCoplanarSharedEdge_MergesSharedVertices()
        {
            (FragmentImportResult result, _) =
                BuildGeometries(BuildSharedEdgeMeshes(new Vector3(0.5f, -1f, 0.25f)));

            FragmentGeometry geometry = result.Geometries[0];
            Assert.That(geometry.Positions, Has.Count.EqualTo(MergedCreaseVertexCount));
            Assert.That(geometry.Indices, Has.Count.EqualTo(SharedEdgeIndexCount));
            Assert.That(result.TotalVertices, Is.EqualTo(MergedCreaseVertexCount));
            Assert.That(result.TotalTriangles, Is.EqualTo(SharedEdgeTriangleCount));
        }

        [Test]
        public void BuildShellGeometries_PerpendicularFoldSharedEdge_KeepsVerticesSeparate()
        {
            (FragmentImportResult result, _) =
                BuildGeometries(BuildSharedEdgeMeshes(new Vector3(0.5f, 0f, -1f)));

            FragmentGeometry geometry = result.Geometries[0];
            Assert.That(geometry.Positions, Has.Count.EqualTo(UnmergedFoldVertexCount));
            Assert.That(geometry.Indices, Has.Count.EqualTo(SharedEdgeIndexCount));
            Assert.That(result.TotalVertices, Is.EqualTo(UnmergedFoldVertexCount));
            Assert.That(result.TotalTriangles, Is.EqualTo(SharedEdgeTriangleCount));
        }

        [Test]
        public void BuildShellGeometries_BowtieProfile_StillEmitsWholeTriangles()
        {
            Schema.Meshes meshes = BuildShellMeshes(builder => new[]
            {
                BuildRegularShell(builder, new[]
                {
                    new Vector3(0f, 0f, 0f),
                    new Vector3(1f, 1f, 0f),
                    new Vector3(1f, 0f, 0f),
                    new Vector3(0f, 1f, 0f)
                }, new[] { new ushort[] { 0, 1, 2, 3 } })
            });

            (FragmentImportResult result, _) = BuildGeometries(meshes);

            FragmentGeometry geometry = result.Geometries[0];
            Assert.That(geometry.Positions, Has.Count.EqualTo(4));
            Assert.That(geometry.Indices.Count % 3, Is.EqualTo(0));
            Assert.That(geometry.Indices.Count, Is.GreaterThanOrEqualTo(3));
            Assert.That(geometry.Indices, Is.All.InRange(0, 3));
            Assert.That(result.TotalTriangles, Is.EqualTo(geometry.Indices.Count / 3));
        }

        private static void AssertGeometriesIdentical(FragmentGeometry expected, FragmentGeometry actual)
        {
            Assert.That(actual.Positions, Is.EqualTo(expected.Positions));
            Assert.That(actual.Indices, Is.EqualTo(expected.Indices));
            Assert.That(actual.Normals, Is.EqualTo(expected.Normals));
        }

        private static (FragmentImportResult Result, List<(FragmentImportSeverity Severity, string Message)> Logs)
            BuildGeometries(Schema.Meshes meshes)
        {
            var result = new FragmentImportResult();
            var logs = new List<(FragmentImportSeverity Severity, string Message)>();
            FragmentGeometryBuilder.BuildShellGeometries(
                meshes, UnitScale, result, (severity, message) => logs.Add((severity, message)));
            return (result, logs);
        }

        private static Vector3[] SquarePoints() => new[]
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(1f, 0f, 0f),
            new Vector3(1f, 1f, 0f),
            new Vector3(0f, 1f, 0f)
        };

        private static ushort[] SquareRing() => new ushort[] { 0, 1, 2, 3 };

        private static Vector3[] HoledSquarePoints() => new[]
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(4f, 0f, 0f),
            new Vector3(4f, 4f, 0f),
            new Vector3(0f, 4f, 0f),
            new Vector3(1f, 1f, 0f),
            new Vector3(1f, 3f, 0f),
            new Vector3(3f, 3f, 0f),
            new Vector3(3f, 1f, 0f)
        };

        private static Vector3[] SharedEdgePoints(Vector3 fourthPoint) => new[]
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(1f, 0f, 0f),
            new Vector3(0.5f, 1f, 0f),
            fourthPoint
        };

        private static Schema.Meshes BuildSquareMeshes()
        {
            return BuildShellMeshes(builder => new[]
            {
                BuildRegularShell(builder, SquarePoints(), new[] { SquareRing() })
            });
        }

        private static Schema.Meshes BuildBigSquareMeshes()
        {
            return BuildShellMeshes(builder => new[]
            {
                BuildBigShell(builder, SquarePoints(), new[] { new uint[] { 0, 1, 2, 3 } })
            });
        }

        private static Schema.Meshes BuildHoledSquareMeshes()
        {
            return BuildShellMeshes(builder => new[]
            {
                BuildRegularShell(
                    builder,
                    HoledSquarePoints(),
                    new[] { SquareRing() },
                    new[] { (new ushort[] { 4, 5, 6, 7 }, (ushort)0) })
            });
        }

        private static Schema.Meshes BuildBigHoledSquareMeshes()
        {
            return BuildShellMeshes(builder => new[]
            {
                BuildBigShell(
                    builder,
                    HoledSquarePoints(),
                    new[] { new uint[] { 0, 1, 2, 3 } },
                    new[] { (new uint[] { 4, 5, 6, 7 }, (ushort)0) })
            });
        }

        private static Schema.Meshes BuildSharedEdgeMeshes(Vector3 fourthPoint)
        {
            return BuildShellMeshes(builder => new[]
            {
                BuildRegularShell(builder, SharedEdgePoints(fourthPoint), new[]
                {
                    new ushort[] { 0, 1, 2 },
                    new ushort[] { 0, 3, 1 }
                })
            });
        }

        private static Schema.Meshes BuildShellMeshes(Func<FlatBufferBuilder, Offset<Schema.Shell>[]> buildShells)
        {
            var builder = new FlatBufferBuilder(BuilderInitialBytes);
            Offset<Schema.Shell>[] shellOffsets = buildShells(builder);
            VectorOffset meshesItems = Schema.Meshes.CreateMeshesItemsVector(builder, Array.Empty<uint>());
            Schema.Meshes.StartSamplesVector(builder, 0);
            VectorOffset samples = builder.EndVector();
            Schema.Meshes.StartRepresentationsVector(builder, 0);
            VectorOffset representations = builder.EndVector();
            Schema.Meshes.StartMaterialsVector(builder, 0);
            VectorOffset materials = builder.EndVector();
            VectorOffset circleExtrusions = Schema.Meshes.CreateCircleExtrusionsVector(
                builder, Array.Empty<Offset<Schema.CircleExtrusion>>());
            VectorOffset shells = Schema.Meshes.CreateShellsVector(builder, shellOffsets);
            Schema.Meshes.StartLocalTransformsVector(builder, 0);
            VectorOffset localTransforms = builder.EndVector();
            Schema.Meshes.StartGlobalTransformsVector(builder, 0);
            VectorOffset globalTransforms = builder.EndVector();

            Schema.Meshes.StartMeshes(builder);
            Offset<Schema.Transform> coordinates = Schema.Transform.CreateTransform(
                builder, 0.0, 0.0, 0.0, 1f, 0f, 0f, 0f, 1f, 0f);
            Schema.Meshes.AddCoordinates(builder, coordinates);
            Schema.Meshes.AddMeshesItems(builder, meshesItems);
            Schema.Meshes.AddSamples(builder, samples);
            Schema.Meshes.AddRepresentations(builder, representations);
            Schema.Meshes.AddMaterials(builder, materials);
            Schema.Meshes.AddCircleExtrusions(builder, circleExtrusions);
            Schema.Meshes.AddShells(builder, shells);
            Schema.Meshes.AddLocalTransforms(builder, localTransforms);
            Schema.Meshes.AddGlobalTransforms(builder, globalTransforms);
            Offset<Schema.Meshes> meshes = Schema.Meshes.EndMeshes(builder);
            builder.Finish(meshes.Value);
            return Schema.Meshes.GetRootAsMeshes(builder.DataBuffer);
        }

        private static Offset<Schema.Shell> BuildRegularShell(
            FlatBufferBuilder builder,
            Vector3[] points,
            ushort[][] profileRings,
            (ushort[] Indices, ushort ProfileId)[] holeRings = null)
        {
            holeRings ??= Array.Empty<(ushort[] Indices, ushort ProfileId)>();
            var profileOffsets = new Offset<Schema.ShellProfile>[profileRings.Length];
            for (int i = 0; i < profileRings.Length; i++)
            {
                VectorOffset indices = Schema.ShellProfile.CreateIndicesVector(builder, profileRings[i]);
                profileOffsets[i] = Schema.ShellProfile.CreateShellProfile(builder, indices);
            }
            var holeOffsets = new Offset<Schema.ShellHole>[holeRings.Length];
            for (int i = 0; i < holeRings.Length; i++)
            {
                VectorOffset indices = Schema.ShellHole.CreateIndicesVector(builder, holeRings[i].Indices);
                holeOffsets[i] = Schema.ShellHole.CreateShellHole(builder, indices, holeRings[i].ProfileId);
            }
            VectorOffset profiles = Schema.Shell.CreateProfilesVector(builder, profileOffsets);
            VectorOffset holes = Schema.Shell.CreateHolesVector(builder, holeOffsets);
            VectorOffset pointsVector = BuildPointsVector(builder, points);
            VectorOffset bigProfiles = Schema.Shell.CreateBigProfilesVector(
                builder, Array.Empty<Offset<Schema.BigShellProfile>>());
            VectorOffset bigHoles = Schema.Shell.CreateBigHolesVector(
                builder, Array.Empty<Offset<Schema.BigShellHole>>());
            VectorOffset faceIds = Schema.Shell.CreateProfilesFaceIdsVector(builder, Array.Empty<ushort>());
            return Schema.Shell.CreateShell(
                builder, profiles, holes, pointsVector, bigProfiles, bigHoles, Schema.ShellType.NONE, faceIds);
        }

        private static Offset<Schema.Shell> BuildBigShell(
            FlatBufferBuilder builder,
            Vector3[] points,
            uint[][] profileRings,
            (uint[] Indices, ushort ProfileId)[] holeRings = null)
        {
            holeRings ??= Array.Empty<(uint[] Indices, ushort ProfileId)>();
            var profileOffsets = new Offset<Schema.BigShellProfile>[profileRings.Length];
            for (int i = 0; i < profileRings.Length; i++)
            {
                VectorOffset indices = Schema.BigShellProfile.CreateIndicesVector(builder, profileRings[i]);
                profileOffsets[i] = Schema.BigShellProfile.CreateBigShellProfile(builder, indices);
            }
            var holeOffsets = new Offset<Schema.BigShellHole>[holeRings.Length];
            for (int i = 0; i < holeRings.Length; i++)
            {
                VectorOffset indices = Schema.BigShellHole.CreateIndicesVector(builder, holeRings[i].Indices);
                holeOffsets[i] = Schema.BigShellHole.CreateBigShellHole(builder, indices, holeRings[i].ProfileId);
            }
            VectorOffset profiles = Schema.Shell.CreateProfilesVector(
                builder, Array.Empty<Offset<Schema.ShellProfile>>());
            VectorOffset holes = Schema.Shell.CreateHolesVector(
                builder, Array.Empty<Offset<Schema.ShellHole>>());
            VectorOffset pointsVector = BuildPointsVector(builder, points);
            VectorOffset bigProfiles = Schema.Shell.CreateBigProfilesVector(builder, profileOffsets);
            VectorOffset bigHoles = Schema.Shell.CreateBigHolesVector(builder, holeOffsets);
            VectorOffset faceIds = Schema.Shell.CreateProfilesFaceIdsVector(builder, Array.Empty<ushort>());
            return Schema.Shell.CreateShell(
                builder, profiles, holes, pointsVector, bigProfiles, bigHoles, Schema.ShellType.BIG, faceIds);
        }

        private static VectorOffset BuildPointsVector(FlatBufferBuilder builder, Vector3[] points)
        {
            Schema.Shell.StartPointsVector(builder, points.Length);
            for (int i = points.Length - 1; i >= 0; i--)
            {
                Schema.FloatVector.CreateFloatVector(builder, points[i].X, points[i].Y, points[i].Z);
            }
            return builder.EndVector();
        }
    }
}
