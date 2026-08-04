using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using NUnit.Framework;

namespace FragmentsUnity.Tests
{
    [TestFixture]
    public sealed class FragmentSurfaceMergeTests
    {
        private const string BuilderTypeName = "FragmentsUnity.FragmentMergedMeshBuilder";
        private const string BuildChunksMethodName = "BuildChunks";
        private const string NamePrefix = "Model";
        private const string WallCategory = "IFCWALL";

        private const int GeometryIndex = 0;
        private const int MaterialIndex = 0;
        private const int OpaqueLocalId = 10;
        private const int TranslucentLocalId = 11;
        private const int GlassLocalId = 12;

        private const float NeutralChannel = 0.5f;
        private const float OpaqueOpacity = FragmentImportLimits.OpaqueOpacityThreshold;
        private const float TranslucentOpacity = 0.7f;
        private const float GlassOpacity = 0.2f;
        private const float ColorTolerance = 1e-4f;

        // sits inside the same 1/MergeBucketOpacityLevels step as OpaqueOpacityThreshold but classifies translucent
        private const float TranslucentOpacityInTheOpaqueQuantizationStep =
            FragmentImportLimits.OpaqueOpacityThreshold - 0.25f / FragmentImportLimits.MergeBucketOpacityLevels;

        [Test]
        public void BuildChunks_SurfacesSharingCategoryColourAndQuantizedOpacity_StillSplitByKind()
        {
            FragmentImportResult result = ResultWithTriangle();
            result.Instances.Add(Surface(OpaqueLocalId, NeutralChannel, OpaqueOpacity));
            result.Instances.Add(Surface(TranslucentLocalId, NeutralChannel, TranslucentOpacityInTheOpaqueQuantizationStep));

            List<object> chunks = BuildChunks(result);

            Assert.Multiple(() =>
            {
                Assert.That(chunks, Has.Count.EqualTo(2));
                Assert.That(chunks.Select(SurfaceKind),
                    Is.EqualTo(new[] { FragmentSurfaceKind.Opaque, FragmentSurfaceKind.Translucent }));
            });
        }

        [Test]
        public void BuildChunks_MixedSurfacesOfOneCategory_GiveEveryChunkASingleSurfaceKind()
        {
            FragmentImportResult result = ResultWithTriangle();
            result.Instances.Add(Surface(OpaqueLocalId, NeutralChannel, OpaqueOpacity));
            result.Instances.Add(Surface(TranslucentLocalId, NeutralChannel, TranslucentOpacity));
            result.Instances.Add(Surface(GlassLocalId, NeutralChannel, GlassOpacity));

            List<object> chunks = BuildChunks(result);

            Assert.Multiple(() =>
            {
                Assert.That(chunks.Select(SurfaceKind),
                    Is.EqualTo(new[]
                    {
                        FragmentSurfaceKind.Opaque,
                        FragmentSurfaceKind.Translucent,
                        FragmentSurfaceKind.Glass
                    }));
                Assert.That(chunks.Select(PartLocalIds),
                    Is.EqualTo(new[]
                    {
                        new[] { OpaqueLocalId },
                        new[] { TranslucentLocalId },
                        new[] { GlassLocalId }
                    }));
            });
        }

        [Test]
        public void BuildChunks_HueClassifiedGlass_BakesTheForcedOpacityIntoTheVertexAlpha()
        {
            FragmentImportResult result = ResultWithTriangle();
            FragmentInstance instance = Surface(GlassLocalId, NeutralChannel, OpaqueOpacity + ColorTolerance);
            instance.Color = BlueishColor();
            result.Instances.Add(instance);

            object chunk = BuildChunks(result)[0];

            Assert.Multiple(() =>
            {
                Assert.That(SurfaceKind(chunk), Is.EqualTo(FragmentSurfaceKind.Glass));
                Assert.That(Mesh(chunk).colors[0].a,
                    Is.EqualTo(FragmentImportLimits.ForcedGlassOpacity).Within(ColorTolerance));
            });
        }

        [Test]
        public void BuildChunks_TranslucentSurface_KeepsItsOwnOpacityInTheVertexAlpha()
        {
            FragmentImportResult result = ResultWithTriangle();
            result.Instances.Add(Surface(TranslucentLocalId, NeutralChannel, TranslucentOpacity));

            object chunk = BuildChunks(result)[0];

            Assert.Multiple(() =>
            {
                Assert.That(SurfaceKind(chunk), Is.EqualTo(FragmentSurfaceKind.Translucent));
                Assert.That(Mesh(chunk).colors[0].a, Is.EqualTo(TranslucentOpacity).Within(ColorTolerance));
            });
        }

        [Test]
        public void Build_ModelWithEverySurfaceKind_GivesEachKindItsOwnMaterial()
        {
            FragmentImportResult result = ResultWithTriangle();
            result.Instances.Add(Surface(OpaqueLocalId, NeutralChannel, OpaqueOpacity));
            result.Instances.Add(Surface(TranslucentLocalId, NeutralChannel, TranslucentOpacity));
            result.Instances.Add(Surface(GlassLocalId, NeutralChannel, GlassOpacity));

            FragmentSceneBuildResult scene = FragmentSceneBuilder.Build(
                result, FragmentSceneFixture.Options(FragmentImportMode.MergedWholeModel), null);

            Assert.Multiple(() =>
            {
                Assert.That(scene.MergedChunks, Is.EqualTo(3));
                Assert.That(scene.Materials, Has.Count.EqualTo(3));
                Assert.That(RenderedMaterials(scene), Is.Unique);
            });
        }

        [Test]
        public void Build_SurfacesOfOneKind_ShareOneMaterialAcrossEveryChunk()
        {
            FragmentImportResult result = ResultWithTriangle();
            result.Instances.Add(Categorized(Surface(OpaqueLocalId, NeutralChannel, OpaqueOpacity), WallCategory));
            result.Instances.Add(Surface(TranslucentLocalId, NeutralChannel, OpaqueOpacity));

            FragmentSceneBuildResult scene = FragmentSceneBuilder.Build(
                result, FragmentSceneFixture.Options(FragmentImportMode.MergedWholeModel), null);

            Assert.Multiple(() =>
            {
                Assert.That(scene.MergedChunks, Is.EqualTo(2));
                Assert.That(scene.Materials, Has.Count.EqualTo(1));
            });
        }

        private static UnityEngine.Material[] RenderedMaterials(FragmentSceneBuildResult scene)
        {
            return scene.Root.GetComponentsInChildren<UnityEngine.MeshRenderer>(true)
                .Select(renderer => renderer.sharedMaterial)
                .ToArray();
        }

        private static FragmentImportResult ResultWithTriangle()
        {
            var result = new FragmentImportResult();
            result.Geometries.Add(FragmentSceneFixture.Triangle(GeometryIndex));
            return result;
        }

        private static FragmentInstance Surface(int localId, float channel, float opacity)
        {
            FragmentInstance instance = FragmentSceneFixture.Instance(localId, GeometryIndex, MaterialIndex);
            instance.Color = new Vector4(channel, channel, channel, 1f);
            instance.Opacity = opacity;
            return instance;
        }

        private static FragmentInstance Categorized(FragmentInstance instance, string category)
        {
            instance.Category = category;
            return instance;
        }

        private static Vector4 BlueishColor()
        {
            return new Vector4(
                NeutralChannel,
                NeutralChannel,
                NeutralChannel + FragmentImportLimits.GlassBlueDominanceOverRed + ColorTolerance,
                1f);
        }

        private static List<object> BuildChunks(FragmentImportResult result)
        {
            Type builderType = typeof(FragmentSceneBuilder).Assembly.GetType(BuilderTypeName);
            MethodInfo method = builderType.GetMethod(
                BuildChunksMethodName, BindingFlags.NonPublic | BindingFlags.Static);
            object chunks = method.Invoke(null, new object[] { result.Instances, result, NamePrefix });
            return ((IEnumerable)chunks).Cast<object>().ToList();
        }

        private static FragmentSurfaceKind SurfaceKind(object chunk)
        {
            return Field<FragmentSurfaceKind>(chunk, "SurfaceKind");
        }

        private static int[] PartLocalIds(object chunk)
        {
            return Field<int[]>(chunk, "PartLocalIds");
        }

        private static UnityEngine.Mesh Mesh(object chunk)
        {
            return Field<UnityEngine.Mesh>(chunk, "Mesh");
        }

        private static T Field<T>(object chunk, string fieldName)
        {
            return (T)chunk.GetType().GetField(fieldName).GetValue(chunk);
        }
    }
}
