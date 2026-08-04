using System;
using System.Collections.Generic;
using System.Numerics;

namespace FragmentsUnity
{
    internal static class FragmentGeometryBuilder
    {
        internal static void BuildShellGeometries(
            Schema.Meshes meshes,
            float scaleFactor,
            FragmentImportResult result,
            Action<FragmentImportSeverity, string> log)
        {
            var state = new FragmentShellBuildState();
            int shellCount = meshes.ShellsLength;
            for (int shellIndex = 0; shellIndex < shellCount; shellIndex++)
            {
                Schema.Shell shell = meshes.Shells(shellIndex).Value;
                var geometry = new FragmentGeometry { GeometryIndex = shellIndex };
                bool isBigShell = shell.Type == Schema.ShellType.BIG;

                List<Vector3> rawPoints = ReadShellPoints(shell, state);
                var profiles = FragmentShellRingSet.ForProfiles(shell, isBigShell);
                var holes = FragmentShellRingSet.ForHoles(shell, isBigShell);
                Dictionary<uint, List<uint>> holesByProfile = GroupHolesByProfile(holes);

                TriangulateProfiles(profiles, holes, holesByProfile, rawPoints, scaleFactor, geometry, state);
                FinalizeShellGeometry(geometry, result);
            }

            ReportShellDiagnostics(state, log);
        }

        private static List<Vector3> ReadShellPoints(Schema.Shell shell, FragmentShellBuildState state)
        {
            // Triangulation runs in raw right-handed source space; mirrors FragParser.cpp:1479.
            int pointCount = state.PointBudget > 0
                ? Math.Min(shell.PointsLength, (int)FragmentImportLimits.MaxShellPoints)
                : 0;
            if (shell.PointsLength > pointCount)
            {
                state.DroppedShellPointCount++;
            }
            state.PointBudget -= pointCount;

            var rawPoints = new List<Vector3>(pointCount);
            for (int pointIndex = 0; pointIndex < pointCount; pointIndex++)
            {
                Schema.FloatVector point = shell.Points(pointIndex).Value;
                float x = point.X;
                float y = point.Y;
                float z = point.Z;
                if (HasNonFiniteComponent(x, y, z))
                {
                    // A non-finite component poisons the Newell normal and earcut; mirrors FragParser.cpp:1499.
                    state.NonFinitePointCount++;
                    rawPoints.Add(Vector3.Zero);
                }
                else
                {
                    rawPoints.Add(new Vector3(x, y, z));
                }
            }
            return rawPoints;
        }

        private static Dictionary<uint, List<uint>> GroupHolesByProfile(FragmentShellRingSet holes)
        {
            var holesByProfile = new Dictionary<uint, List<uint>>();
            int holeCount = holes.RingCount;
            for (int holeIndex = 0; holeIndex < holeCount; holeIndex++)
            {
                uint profileId = holes.GetProfileId(holeIndex);
                if (!holesByProfile.TryGetValue(profileId, out List<uint> holeIds))
                {
                    holeIds = new List<uint>();
                    holesByProfile.Add(profileId, holeIds);
                }
                holeIds.Add((uint)holeIndex);
            }
            return holesByProfile;
        }

        private static void TriangulateProfiles(
            FragmentShellRingSet profiles,
            FragmentShellRingSet holes,
            Dictionary<uint, List<uint>> holesByProfile,
            List<Vector3> rawPoints,
            float scaleFactor,
            FragmentGeometry geometry,
            FragmentShellBuildState state)
        {
            var rawIndexToGeometryIndices = new Dictionary<int, List<int>>();
            int profileCount = profiles.RingCount;
            for (int profileIndex = 0; profileIndex < profileCount; profileIndex++)
            {
                int faceVertexCount = profiles.GetIndexCount(profileIndex);
                if (faceVertexCount < 3)
                {
                    continue;
                }
                if (faceVertexCount > (int)FragmentImportLimits.MaxFaceRingVertices)
                {
                    state.OversizedFaceCount++;
                    continue;
                }
                if (state.VertexBudget <= 0)
                {
                    state.BudgetedOutFaceCount++;
                    continue;
                }
                state.VertexBudget -= faceVertexCount;

                var facePositions = new List<Vector3>(faceVertexCount);
                var faceRawIndices = new List<int>(faceVertexCount);
                for (int vertexIndex = 0; vertexIndex < faceVertexCount; vertexIndex++)
                {
                    int rawIndex = profiles.GetRawIndex(profileIndex, vertexIndex);
                    faceRawIndices.Add(rawIndex);
                    facePositions.Add(GetRawPointOrZero(rawPoints, rawIndex));
                }

                (double normalX, double normalY, double normalZ) = ComputeFaceNormal(facePositions);
                (int dimension0, int dimension1) = SelectProjectionAxes(normalX, normalY, normalZ);

                var rings = new List<List<double>>();
                var outerRing = new List<double>(faceVertexCount * 2);
                foreach (Vector3 position in facePositions)
                {
                    outerRing.Add(GetCoordinate(position, dimension0));
                    outerRing.Add(GetCoordinate(position, dimension1));
                }
                rings.Add(outerRing);

                if (holesByProfile.TryGetValue((uint)profileIndex, out List<uint> holeIds))
                {
                    AppendHoleRings(
                        holes, holeIds, rawPoints, dimension0, dimension1,
                        facePositions, faceRawIndices, rings, state);
                }

                List<int> triangleIndices =
                    Earcut.Triangulate(rings, ref state.EarcutWorkBudget, out bool budgetExhausted);
                if (budgetExhausted)
                {
                    state.UntriangulatedFaceCount++;
                }

                if (triangleIndices.Count == 0 && faceVertexCount >= 3)
                {
                    // Fan fallback over the outer ring only; mirrors FragParser.cpp:1683.
                    for (int vertexIndex = 1; vertexIndex + 1 < faceVertexCount; vertexIndex++)
                    {
                        triangleIndices.Add(0);
                        triangleIndices.Add(vertexIndex);
                        triangleIndices.Add(vertexIndex + 1);
                    }
                }

                // Unity fronts are wound opposite to UE, so FragParser.cpp:1697's negation must not carry over.
                Vector3 unityNormal = FragmentCoordinateConverter.ConvertPosition(
                    (float)normalX, (float)normalY, (float)normalZ, 1f);

                List<int> faceGeometryIndices = MergeFaceVertices(
                    facePositions, faceRawIndices, unityNormal, scaleFactor, geometry, rawIndexToGeometryIndices);

                // The mirror conversion flips handedness, so winding is reversed; mirrors FragParser.cpp:1740.
                for (int i = 0; i + 2 < triangleIndices.Count; i += 3)
                {
                    geometry.Indices.Add(faceGeometryIndices[triangleIndices[i]]);
                    geometry.Indices.Add(faceGeometryIndices[triangleIndices[i + 2]]);
                    geometry.Indices.Add(faceGeometryIndices[triangleIndices[i + 1]]);
                }
            }
        }

        private static void AppendHoleRings(
            FragmentShellRingSet holes,
            List<uint> holeIds,
            List<Vector3> rawPoints,
            int dimension0,
            int dimension1,
            List<Vector3> facePositions,
            List<int> faceRawIndices,
            List<List<double>> rings,
            FragmentShellBuildState state)
        {
            foreach (uint holeId in holeIds)
            {
                int holeVertexCount = holes.GetIndexCount((int)holeId);
                if (holeVertexCount < 3)
                {
                    continue;
                }
                if (holeVertexCount > (int)FragmentImportLimits.MaxFaceRingVertices || state.VertexBudget <= 0)
                {
                    // Budget exhaustion counts as an oversized face here too; mirrors FragParser.cpp:1646.
                    state.OversizedFaceCount++;
                    continue;
                }
                state.VertexBudget -= holeVertexCount;

                var holeRing = new List<double>(holeVertexCount * 2);
                for (int vertexIndex = 0; vertexIndex < holeVertexCount; vertexIndex++)
                {
                    int rawIndex = holes.GetRawIndex((int)holeId, vertexIndex);
                    faceRawIndices.Add(rawIndex);
                    Vector3 holePosition = GetRawPointOrZero(rawPoints, rawIndex);
                    // Appended to the flat face arrays so earcut indices land there; mirrors FragParser.cpp:1664.
                    facePositions.Add(holePosition);
                    holeRing.Add(GetCoordinate(holePosition, dimension0));
                    holeRing.Add(GetCoordinate(holePosition, dimension1));
                }
                rings.Add(holeRing);
            }
        }

        private static List<int> MergeFaceVertices(
            List<Vector3> facePositions,
            List<int> faceRawIndices,
            Vector3 unityNormal,
            float scaleFactor,
            FragmentGeometry geometry,
            Dictionary<int, List<int>> rawIndexToGeometryIndices)
        {
            var faceGeometryIndices = new List<int>(facePositions.Count);
            for (int faceVertexIndex = 0; faceVertexIndex < facePositions.Count; faceVertexIndex++)
            {
                int rawIndex = faceRawIndices[faceVertexIndex];
                Vector3 rawPosition = facePositions[faceVertexIndex];
                int geometryIndex = -1;

                if (rawIndexToGeometryIndices.TryGetValue(rawIndex, out List<int> existingIndices))
                {
                    foreach (int existingIndex in existingIndices)
                    {
                        Vector3 existingNormal = NormalizeOrZero(geometry.Normals[existingIndex]);
                        if (Vector3.Dot(existingNormal, unityNormal)
                            >= FragmentImportLimits.CreaseAngleMinimumNormalDot)
                        {
                            geometryIndex = existingIndex;
                            break;
                        }
                    }
                }

                if (geometryIndex != -1)
                {
                    geometry.Normals[geometryIndex] += unityNormal;
                }
                else
                {
                    Vector3 unityPosition = FragmentCoordinateConverter.ConvertPosition(
                        rawPosition.X, rawPosition.Y, rawPosition.Z, scaleFactor);
                    geometryIndex = geometry.Positions.Count;
                    geometry.Positions.Add(unityPosition);
                    geometry.Normals.Add(unityNormal);
                    if (!rawIndexToGeometryIndices.TryGetValue(rawIndex, out List<int> geometryIndices))
                    {
                        geometryIndices = new List<int>();
                        rawIndexToGeometryIndices.Add(rawIndex, geometryIndices);
                    }
                    geometryIndices.Add(geometryIndex);
                }
                faceGeometryIndices.Add(geometryIndex);
            }
            return faceGeometryIndices;
        }

        private static (double X, double Y, double Z) ComputeFaceNormal(List<Vector3> facePositions)
        {
            double normalX = 0.0;
            double normalY = 0.0;
            double normalZ = 0.0;
            int vertexCount = facePositions.Count;
            for (int i = 0; i < vertexCount; i++)
            {
                Vector3 current = facePositions[i];
                Vector3 next = facePositions[(i + 1) % vertexCount];
                normalX += ((double)current.Y - next.Y) * ((double)current.Z + next.Z);
                normalY += ((double)current.Z - next.Z) * ((double)current.X + next.X);
                normalZ += ((double)current.X - next.X) * ((double)current.Y + next.Y);
            }

            double lengthSquared = normalX * normalX + normalY * normalY + normalZ * normalZ;
            if (lengthSquared < FragmentImportLimits.MinimumSafeNormalLengthSquared)
            {
                return (0.0, 0.0, 1.0);
            }
            double inverseLength = 1.0 / Math.Sqrt(lengthSquared);
            return (normalX * inverseLength, normalY * inverseLength, normalZ * inverseLength);
        }

        private static (int Dimension0, int Dimension1) SelectProjectionAxes(
            double normalX, double normalY, double normalZ)
        {
            double absX = Math.Abs(normalX);
            double absY = Math.Abs(normalY);
            double absZ = Math.Abs(normalZ);

            // Axes swap on a negative dominant component to preserve winding; mirrors FragParser.cpp:1592.
            if (absZ > absX && absZ > absY)
            {
                return normalZ > 0.0 ? (0, 1) : (1, 0);
            }
            if (absY > absX && absY > absZ)
            {
                return normalY > 0.0 ? (2, 0) : (0, 2);
            }
            return normalX > 0.0 ? (1, 2) : (2, 1);
        }

        private static void FinalizeShellGeometry(FragmentGeometry geometry, FragmentImportResult result)
        {
            if (geometry.Positions.Count > 0 && geometry.Indices.Count >= 3)
            {
                for (int i = 0; i < geometry.Normals.Count; i++)
                {
                    Vector3 normal = geometry.Normals[i];
                    float lengthSquared = normal.LengthSquared();
                    // Near-zero normals stay unnormalized like FVector::Normalize; mirrors FragParser.cpp:1763.
                    if (lengthSquared >= FragmentImportLimits.MinimumSafeNormalLengthSquared)
                    {
                        geometry.Normals[i] = normal / MathF.Sqrt(lengthSquared);
                    }
                }

                Vector3 boundsMin = geometry.Positions[0];
                Vector3 boundsMax = geometry.Positions[0];
                foreach (Vector3 position in geometry.Positions)
                {
                    boundsMin = Vector3.Min(boundsMin, position);
                    boundsMax = Vector3.Max(boundsMax, position);
                }
                geometry.BoundsMin = boundsMin;
                geometry.BoundsMax = boundsMax;
                geometry.HasBounds = true;

                result.TotalVertices += geometry.Positions.Count;
                result.TotalTriangles += geometry.Indices.Count / 3;
            }

            // Added even when empty so the geometry index tracks the shell index; mirrors FragParser.cpp:1775.
            result.Geometries.Add(geometry);
        }

        private static void ReportShellDiagnostics(FragmentShellBuildState state, Action<FragmentImportSeverity, string> log)
        {
            if (state.NonFinitePointCount > 0)
            {
                log?.Invoke(FragmentImportSeverity.Warning,
                    $"{state.NonFinitePointCount} vertex position(s) were not finite and were moved to the origin; that geometry is wrong.");
            }
            if (state.DroppedShellPointCount > 0)
            {
                log?.Invoke(FragmentImportSeverity.Warning,
                    $"{state.DroppedShellPointCount} shell(s) declared more than {FragmentImportLimits.MaxShellPoints} points; the remainder were dropped.");
            }
            if (state.OversizedFaceCount > 0)
            {
                log?.Invoke(FragmentImportSeverity.Warning,
                    $"Dropped {state.OversizedFaceCount} face(s)/hole(s) past the {FragmentImportLimits.MaxFaceRingVertices}-vertex limit for a single ring.");
            }
            if (state.BudgetedOutFaceCount > 0)
            {
                log?.Invoke(FragmentImportSeverity.Error,
                    $"Geometry allowance spent: {state.BudgetedOutFaceCount} face(s) were not built. The model is larger than this importer will load in one pass.");
            }
            if (state.UntriangulatedFaceCount > 0)
            {
                log?.Invoke(FragmentImportSeverity.Error,
                    $"Triangulation allowance spent on {state.UntriangulatedFaceCount} face(s); they fell back to a fan or were left empty. This usually means self-intersecting or degenerate profiles.");
            }
        }

        private static Vector3 GetRawPointOrZero(List<Vector3> rawPoints, int rawIndex)
        {
            // Out-of-range raw indices contribute the origin; mirrors FragParser.cpp:1565.
            return rawIndex >= 0 && rawIndex < rawPoints.Count ? rawPoints[rawIndex] : Vector3.Zero;
        }

        private static double GetCoordinate(Vector3 position, int dimension)
        {
            return dimension == 0 ? position.X : dimension == 1 ? position.Y : position.Z;
        }

        private static bool HasNonFiniteComponent(float x, float y, float z)
        {
            return float.IsNaN(x) || float.IsInfinity(x)
                || float.IsNaN(y) || float.IsInfinity(y)
                || float.IsNaN(z) || float.IsInfinity(z);
        }

        private static Vector3 NormalizeOrZero(Vector3 value)
        {
            float lengthSquared = value.LengthSquared();
            return lengthSquared < FragmentImportLimits.MinimumSafeNormalLengthSquared
                ? Vector3.Zero
                : value / MathF.Sqrt(lengthSquared);
        }
    }
}
