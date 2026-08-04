using System.Collections.Generic;
using FragmentsUnity.Editor;
using NUnit.Framework;

namespace FragmentsUnity.Tests
{
    [TestFixture]
    public sealed class FragmentAssetExtractionPlanTests
    {
        private const string TargetFolder = "Assets/Extracted";
        private const string ModelName = "AR520";
        private const string MeshName = "Wall";

        [Test]
        public void ModelFolder_IsOneFolderPerModelUnderTheTarget()
        {
            var plan = new FragmentAssetExtractionPlan(TargetFolder, ModelName);

            Assert.Multiple(() =>
            {
                Assert.That(plan.ModelFolder, Is.EqualTo("Assets/Extracted/AR520"));
                Assert.That(plan.MeshFolder, Is.EqualTo("Assets/Extracted/AR520/Meshes"));
                Assert.That(plan.MaterialFolder, Is.EqualTo("Assets/Extracted/AR520/Materials"));
                Assert.That(plan.IsValid, Is.True);
            });
        }

        [Test]
        public void EmptyTargetFolder_FallsBackToTheDefaultRoot()
        {
            var plan = new FragmentAssetExtractionPlan(string.Empty, ModelName);

            Assert.That(plan.ModelFolder, Is.EqualTo("Assets/Fragments/AR520"));
        }

        [Test]
        public void NullTargetFolder_FallsBackToTheDefaultRoot()
        {
            var plan = new FragmentAssetExtractionPlan(null, ModelName);

            Assert.That(plan.ModelFolder, Is.EqualTo("Assets/Fragments/AR520"));
        }

        [Test]
        public void TargetFolderOutsideAssets_IsMovedUnderAssets()
        {
            var plan = new FragmentAssetExtractionPlan("Baked/Models", ModelName);

            Assert.That(plan.ModelFolder, Is.EqualTo("Assets/Baked/Models/AR520"));
        }

        [Test]
        public void TargetFolderWithSeparatorNoise_IsNormalized()
        {
            var plan = new FragmentAssetExtractionPlan(@"\Assets\Extracted\", ModelName);

            Assert.That(plan.ModelFolder, Is.EqualTo("Assets/Extracted/AR520"));
        }

        [Test]
        public void EmptyModelName_FallsBackToAModelFolder()
        {
            var plan = new FragmentAssetExtractionPlan(TargetFolder, string.Empty);

            Assert.That(plan.ModelFolder, Is.EqualTo("Assets/Extracted/Model"));
        }

        [Test]
        public void ModelNameWithIllegalCharacters_IsSanitizedIntoTheFolder()
        {
            var plan = new FragmentAssetExtractionPlan(TargetFolder, "Level 1: Slab/Wall.frag");

            Assert.That(plan.ModelFolder, Is.EqualTo("Assets/Extracted/Level_1__Slab_Wall_frag"));
        }

        [Test]
        public void OverLongFolderPath_IsRejected()
        {
            var plan = new FragmentAssetExtractionPlan(
                "Assets/" + new string('d', FragmentAssetExtractionPlan.MaxFolderPathChars), ModelName);

            Assert.That(plan.IsValid, Is.False);
        }

        [Test]
        public void MeshPath_UsesTheMeshFolderAndAssetExtension()
        {
            var plan = new FragmentAssetExtractionPlan(TargetFolder, ModelName);

            Assert.That(plan.MeshPath(MeshName), Is.EqualTo("Assets/Extracted/AR520/Meshes/Wall.asset"));
        }

        [Test]
        public void MaterialPath_UsesTheMaterialFolderAndMaterialExtension()
        {
            var plan = new FragmentAssetExtractionPlan(TargetFolder, ModelName);

            Assert.That(plan.MaterialPath(MeshName), Is.EqualTo("Assets/Extracted/AR520/Materials/Wall.mat"));
        }

        [Test]
        public void RepeatedName_GetsANumberedSuffix()
        {
            var plan = new FragmentAssetExtractionPlan(TargetFolder, ModelName);

            Assert.Multiple(() =>
            {
                Assert.That(plan.MeshPath(MeshName), Does.EndWith("/Wall.asset"));
                Assert.That(plan.MeshPath(MeshName), Does.EndWith("/Wall_1.asset"));
                Assert.That(plan.MeshPath(MeshName), Does.EndWith("/Wall_2.asset"));
            });
        }

        [Test]
        public void NamesDifferingOnlyByCase_AreTreatedAsCollisions()
        {
            var plan = new FragmentAssetExtractionPlan(TargetFolder, ModelName);

            Assert.Multiple(() =>
            {
                Assert.That(plan.MeshPath("wall"), Does.EndWith("/wall.asset"));
                Assert.That(plan.MeshPath("WALL"), Does.EndWith("/WALL_1.asset"));
            });
        }

        [Test]
        public void MeshesAndMaterials_DoNotCollideWithEachOther()
        {
            var plan = new FragmentAssetExtractionPlan(TargetFolder, ModelName);

            Assert.Multiple(() =>
            {
                Assert.That(plan.MeshPath(MeshName), Does.EndWith("/Meshes/Wall.asset"));
                Assert.That(plan.MaterialPath(MeshName), Does.EndWith("/Materials/Wall.mat"));
            });
        }

        [Test]
        public void SanitizeAssetName_ReplacesEverythingButLettersDigitsAndUnderscores()
        {
            Assert.That(
                FragmentAssetExtractionPlan.SanitizeAssetName("Basic Wall:N-EXT 3 5/8\" Finish"),
                Is.EqualTo("Basic_Wall_N_EXT_3_5_8__Finish"));
        }

        [Test]
        public void SanitizeAssetName_EmptyOrNull_FallsBackToAName()
        {
            Assert.Multiple(() =>
            {
                Assert.That(FragmentAssetExtractionPlan.SanitizeAssetName(null), Is.EqualTo("Asset"));
                Assert.That(FragmentAssetExtractionPlan.SanitizeAssetName(string.Empty), Is.EqualTo("Asset"));
            });
        }

        [Test]
        public void SanitizeAssetName_LeadingDigit_IsPrefixed()
        {
            Assert.That(FragmentAssetExtractionPlan.SanitizeAssetName("520_Wall"), Is.EqualTo("A520_Wall"));
        }

        [Test]
        public void SanitizeAssetName_ClampsToTheNameCeiling()
        {
            string clamped = FragmentAssetExtractionPlan.SanitizeAssetName(
                new string('w', FragmentAssetExtractionPlan.MaxAssetNameChars * 2));

            Assert.That(clamped.Length, Is.EqualTo(FragmentAssetExtractionPlan.MaxAssetNameChars));
        }

        [Test]
        public void SanitizeAssetName_LeadingDigitOnAClampedName_StaysWithinTheCeiling()
        {
            string clamped = FragmentAssetExtractionPlan.SanitizeAssetName(
                new string('7', FragmentAssetExtractionPlan.MaxAssetNameChars * 2));

            Assert.Multiple(() =>
            {
                Assert.That(clamped.Length, Is.EqualTo(FragmentAssetExtractionPlan.MaxAssetNameChars));
                Assert.That(clamped, Does.StartWith("A7"));
            });
        }

        [Test]
        public void FolderChain_ListsEveryFolderToCreateOutermostFirst()
        {
            IReadOnlyList<string> chain = FragmentAssetExtractionPlan.FolderChain("Assets/Fragments/AR520/Meshes");

            Assert.That(chain, Is.EqualTo(new[]
            {
                "Assets/Fragments", "Assets/Fragments/AR520", "Assets/Fragments/AR520/Meshes"
            }));
        }

        [Test]
        public void FolderChain_OfTheAssetsRoot_IsEmpty()
        {
            Assert.Multiple(() =>
            {
                Assert.That(FragmentAssetExtractionPlan.FolderChain("Assets"), Is.Empty);
                Assert.That(FragmentAssetExtractionPlan.FolderChain(string.Empty), Is.Empty);
            });
        }
    }
}
