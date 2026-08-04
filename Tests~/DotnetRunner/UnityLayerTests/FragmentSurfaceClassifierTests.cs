using System.Numerics;
using NUnit.Framework;

namespace FragmentsUnity.Tests
{
    [TestFixture]
    public sealed class FragmentSurfaceClassifierTests
    {
        private const float FullOpacity = 1.0f;
        private const float TranslucentOpacity = 0.6f;
        private const float NearlyInvisibleOpacity = 0.3f;
        private const float JustBelowOpaqueOpacity = 0.98f;

        private const float GreyChannel = 0.5f;
        private const float NoChannel = 0.0f;
        private const float DimRedChannel = 0.2f;
        private const float DimGreenChannel = 0.3f;
        private const float StrongBlueChannel = 0.9f;
        private const float PastTheThreshold = 0.01f;

        [Test]
        public void Classify_AnOpaqueGrey_IsOpaque()
        {
            FragmentSurfaceKind kind = FragmentSurfaceClassifier.Classify(Grey(), FullOpacity);

            Assert.That(kind, Is.EqualTo(FragmentSurfaceKind.Opaque));
        }

        [Test]
        public void Classify_AGreyAtSixTenthsOpacity_IsTranslucent()
        {
            FragmentSurfaceKind kind = FragmentSurfaceClassifier.Classify(Grey(), TranslucentOpacity);

            Assert.That(kind, Is.EqualTo(FragmentSurfaceKind.Translucent));
        }

        [Test]
        public void Classify_AFullyOpaqueBlue_IsGlassByHueAlone()
        {
            var blue = new Vector4(DimRedChannel, DimGreenChannel, StrongBlueChannel, FullOpacity);

            FragmentSurfaceKind kind = FragmentSurfaceClassifier.Classify(blue, FullOpacity);

            Assert.That(kind, Is.EqualTo(FragmentSurfaceKind.Glass));
        }

        [Test]
        public void Classify_ALowOpacityNonBlue_IsGlass()
        {
            FragmentSurfaceKind kind = FragmentSurfaceClassifier.Classify(Grey(), NearlyInvisibleOpacity);

            Assert.That(kind, Is.EqualTo(FragmentSurfaceKind.Glass));
        }

        [Test]
        public void Classify_OpacityExactlyAtTheOpaqueThreshold_IsOpaque()
        {
            FragmentSurfaceKind kind =
                FragmentSurfaceClassifier.Classify(Grey(), FragmentImportLimits.OpaqueOpacityThreshold);

            Assert.That(kind, Is.EqualTo(FragmentSurfaceKind.Opaque));
        }

        [Test]
        public void Classify_OpacityJustBelowTheOpaqueThreshold_IsTranslucent()
        {
            FragmentSurfaceKind kind = FragmentSurfaceClassifier.Classify(Grey(), JustBelowOpaqueOpacity);

            Assert.That(kind, Is.EqualTo(FragmentSurfaceKind.Translucent));
        }

        [Test]
        public void Classify_OpacityExactlyAtTheGlassThreshold_IsTranslucent()
        {
            FragmentSurfaceKind kind =
                FragmentSurfaceClassifier.Classify(Grey(), FragmentImportLimits.GlassOpacityThreshold);

            Assert.That(kind, Is.EqualTo(FragmentSurfaceKind.Translucent));
        }

        [Test]
        public void Classify_BlueExactlyOneDominanceStepOverRed_IsNotGlass()
        {
            float blueChannel = DimRedChannel + FragmentImportLimits.GlassBlueDominanceOverRed;
            var color = new Vector4(DimRedChannel, NoChannel, blueChannel, FullOpacity);

            FragmentSurfaceKind kind = FragmentSurfaceClassifier.Classify(color, FullOpacity);

            Assert.That(kind, Is.EqualTo(FragmentSurfaceKind.Opaque));
        }

        [Test]
        public void Classify_BluePastTheRedDominanceStep_IsGlass()
        {
            float blueChannel = DimRedChannel + FragmentImportLimits.GlassBlueDominanceOverRed + PastTheThreshold;
            var color = new Vector4(DimRedChannel, NoChannel, blueChannel, FullOpacity);

            FragmentSurfaceKind kind = FragmentSurfaceClassifier.Classify(color, FullOpacity);

            Assert.That(kind, Is.EqualTo(FragmentSurfaceKind.Glass));
        }

        [Test]
        public void Classify_BlueExactlyOneDominanceStepOverGreen_IsNotGlass()
        {
            float blueChannel = GreyChannel + FragmentImportLimits.GlassBlueDominanceOverGreen;
            var color = new Vector4(NoChannel, GreyChannel, blueChannel, FullOpacity);

            FragmentSurfaceKind kind = FragmentSurfaceClassifier.Classify(color, FullOpacity);

            Assert.That(kind, Is.EqualTo(FragmentSurfaceKind.Opaque));
        }

        [Test]
        public void Classify_BluePastTheGreenDominanceStep_IsGlass()
        {
            float blueChannel = GreyChannel + FragmentImportLimits.GlassBlueDominanceOverGreen + PastTheThreshold;
            var color = new Vector4(NoChannel, GreyChannel, blueChannel, FullOpacity);

            FragmentSurfaceKind kind = FragmentSurfaceClassifier.Classify(color, FullOpacity);

            Assert.That(kind, Is.EqualTo(FragmentSurfaceKind.Glass));
        }

        [Test]
        public void ResolveOpacity_GlassThatIsStillFullyOpaque_IsForcedTransparent()
        {
            float opacity = FragmentSurfaceClassifier.ResolveOpacity(FragmentSurfaceKind.Glass, FullOpacity);

            Assert.That(opacity, Is.EqualTo(FragmentImportLimits.ForcedGlassOpacity));
        }

        [Test]
        public void ResolveOpacity_GlassExactlyAtTheOpaqueThreshold_KeepsItsOpacity()
        {
            float opacity = FragmentSurfaceClassifier.ResolveOpacity(
                FragmentSurfaceKind.Glass, FragmentImportLimits.OpaqueOpacityThreshold);

            Assert.That(opacity, Is.EqualTo(FragmentImportLimits.OpaqueOpacityThreshold));
        }

        [Test]
        public void ResolveOpacity_GlassThatIsAlreadyTransparent_KeepsItsOpacity()
        {
            float opacity = FragmentSurfaceClassifier.ResolveOpacity(
                FragmentSurfaceKind.Glass, NearlyInvisibleOpacity);

            Assert.That(opacity, Is.EqualTo(NearlyInvisibleOpacity));
        }

        [Test]
        public void ResolveOpacity_OpaqueAndTranslucentSurfaces_KeepTheirOpacity()
        {
            Assert.Multiple(() =>
            {
                Assert.That(
                    FragmentSurfaceClassifier.ResolveOpacity(FragmentSurfaceKind.Opaque, FullOpacity),
                    Is.EqualTo(FullOpacity));
                Assert.That(
                    FragmentSurfaceClassifier.ResolveOpacity(FragmentSurfaceKind.Translucent, TranslucentOpacity),
                    Is.EqualTo(TranslucentOpacity));
            });
        }

        private static Vector4 Grey()
        {
            return new Vector4(GreyChannel, GreyChannel, GreyChannel, FullOpacity);
        }
    }
}
