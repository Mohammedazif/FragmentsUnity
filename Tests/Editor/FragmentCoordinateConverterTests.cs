using System.Numerics;
using NUnit.Framework;

namespace FragmentsUnity.Tests
{
    [TestFixture]
    public sealed class FragmentCoordinateConverterTests
    {
        private const float BasisImageTolerance = 1e-5f;
        private const float RotationComponentTolerance = 1e-6f;
        private const double PositionTolerance = 1e-9;

        [Test]
        public void ConvertPosition_UnitScale_NegatesXKeepsYZ()
        {
            Assert.AreEqual(new Vector3(-1f, 2f, 3f), FragmentCoordinateConverter.ConvertPosition(1f, 2f, 3f, 1f));
            Assert.AreEqual(new Vector3(4.5f, 5.25f, -6.75f), FragmentCoordinateConverter.ConvertPosition(-4.5f, 5.25f, -6.75f, 1f));
            Assert.AreEqual(Vector3.Zero, FragmentCoordinateConverter.ConvertPosition(0f, 0f, 0f, 1f));
        }

        [Test]
        public void ConvertPosition_NonUnitScale_ScalesAllComponents()
        {
            Assert.AreEqual(new Vector3(-0.5f, 1f, 1.5f), FragmentCoordinateConverter.ConvertPosition(1f, 2f, 3f, 0.5f));
            Assert.AreEqual(new Vector3(-200f, -300f, 400f), FragmentCoordinateConverter.ConvertPosition(2f, -3f, 4f, 100f));
        }

        [Test]
        public void ConvertPosition_AppliedTwiceWithInverseScale_RecoversOriginal()
        {
            var original = new Vector3(1.5f, -2.25f, 3.125f);

            Vector3 mirrored = FragmentCoordinateConverter.ConvertPosition(original.X, original.Y, original.Z, 1f);
            Vector3 restored = FragmentCoordinateConverter.ConvertPosition(mirrored.X, mirrored.Y, mirrored.Z, 1f);
            Assert.AreEqual(original, restored);

            Vector3 scaled = FragmentCoordinateConverter.ConvertPosition(original.X, original.Y, original.Z, 4f);
            Vector3 restoredScaled = FragmentCoordinateConverter.ConvertPosition(scaled.X, scaled.Y, scaled.Z, 0.25f);
            Assert.AreEqual(original, restoredScaled);
        }

        [Test]
        public void BuildTransform_IdentityBasis_YieldsIdentityRotationAndMirroredPosition()
        {
            FragmentTransform transform = FragmentCoordinateConverter.BuildTransform(
                1.0, 2.0, 3.0, 1f, 0f, 0f, 0f, 1f, 0f, 1f);

            Assert.AreEqual(-1.0, transform.PositionX, PositionTolerance);
            Assert.AreEqual(2.0, transform.PositionY, PositionTolerance);
            Assert.AreEqual(3.0, transform.PositionZ, PositionTolerance);
            Assert.AreEqual(0f, transform.Rotation.X, RotationComponentTolerance);
            Assert.AreEqual(0f, transform.Rotation.Y, RotationComponentTolerance);
            Assert.AreEqual(0f, transform.Rotation.Z, RotationComponentTolerance);
            Assert.AreEqual(1f, System.Math.Abs(transform.Rotation.W), RotationComponentTolerance);

            FragmentTransform doubled = FragmentCoordinateConverter.BuildTransform(
                1.0, 2.0, 3.0, 1f, 0f, 0f, 0f, 1f, 0f, 2f);

            Assert.AreEqual(-2.0, doubled.PositionX, PositionTolerance);
            Assert.AreEqual(4.0, doubled.PositionY, PositionTolerance);
            Assert.AreEqual(6.0, doubled.PositionZ, PositionTolerance);
        }

        [Test]
        public void BuildTransform_QuarterTurnAboutSourceY_MapsUnityXAxisToPositiveZ()
        {
            FragmentTransform transform = FragmentCoordinateConverter.BuildTransform(
                0.0, 0.0, 0.0, 0f, 0f, -1f, 0f, 1f, 0f, 1f);

            AssertVectorNear(new Vector3(0f, 0f, 1f), Vector3.Transform(Vector3.UnitX, transform.Rotation), "x axis");
            AssertVectorNear(new Vector3(0f, 1f, 0f), Vector3.Transform(Vector3.UnitY, transform.Rotation), "y axis");
            AssertVectorNear(new Vector3(-1f, 0f, 0f), Vector3.Transform(Vector3.UnitZ, transform.Rotation), "z axis");
        }

        [Test]
        public void BuildTransform_OrthonormalBases_RotateUnitAxesToConjugatedImages()
        {
            var seedPairs = new[]
            {
                (new Vector3(1f, 2f, 3f), new Vector3(0f, 1f, 0f)),
                (new Vector3(-2f, 0.5f, 1f), new Vector3(1f, 1f, 1f)),
                (new Vector3(0.3f, -0.7f, 0.2f), new Vector3(-0.5f, 0.1f, 0.9f)),
                (new Vector3(5f, -1f, 2f), new Vector3(0f, 0f, 1f))
            };

            foreach ((Vector3 xSeed, Vector3 ySeed) in seedPairs)
            {
                Vector3 x = Vector3.Normalize(xSeed);
                Vector3 y = OrthonormalizeAgainst(x, ySeed);
                Vector3 z = Vector3.Cross(x, y);

                FragmentTransform transform = FragmentCoordinateConverter.BuildTransform(
                    0.0, 0.0, 0.0, x.X, x.Y, x.Z, y.X, y.Y, y.Z, 1f);

                string context = $"seeds {xSeed} {ySeed}";
                AssertVectorNear(
                    new Vector3(x.X, -x.Y, -x.Z),
                    Vector3.Transform(Vector3.UnitX, transform.Rotation),
                    context + " x image");
                AssertVectorNear(
                    new Vector3(-y.X, y.Y, y.Z),
                    Vector3.Transform(Vector3.UnitY, transform.Rotation),
                    context + " y image");
                AssertVectorNear(
                    new Vector3(-z.X, z.Y, z.Z),
                    Vector3.Transform(Vector3.UnitZ, transform.Rotation),
                    context + " z image");
            }
        }

        [Test]
        public void BuildTransform_ZeroDirections_ProducesFiniteUnitRotation()
        {
            FragmentTransform transform = FragmentCoordinateConverter.BuildTransform(
                1.0, 2.0, 3.0, 0f, 0f, 0f, 0f, 0f, 0f, 1f);

            Quaternion rotation = transform.Rotation;
            Assert.IsFalse(float.IsNaN(rotation.X), "rotation X is NaN");
            Assert.IsFalse(float.IsNaN(rotation.Y), "rotation Y is NaN");
            Assert.IsFalse(float.IsNaN(rotation.Z), "rotation Z is NaN");
            Assert.IsFalse(float.IsNaN(rotation.W), "rotation W is NaN");
            Assert.AreEqual(1f, rotation.Length(), RotationComponentTolerance);
            Assert.AreEqual(-1.0, transform.PositionX, PositionTolerance);
            Assert.AreEqual(2.0, transform.PositionY, PositionTolerance);
            Assert.AreEqual(3.0, transform.PositionZ, PositionTolerance);
        }

        private static Vector3 OrthonormalizeAgainst(Vector3 axis, Vector3 seed)
        {
            return Vector3.Normalize(seed - Vector3.Dot(seed, axis) * axis);
        }

        private static void AssertVectorNear(Vector3 expected, Vector3 actual, string context)
        {
            Assert.AreEqual(expected.X, actual.X, BasisImageTolerance, context + " X");
            Assert.AreEqual(expected.Y, actual.Y, BasisImageTolerance, context + " Y");
            Assert.AreEqual(expected.Z, actual.Z, BasisImageTolerance, context + " Z");
        }
    }
}
