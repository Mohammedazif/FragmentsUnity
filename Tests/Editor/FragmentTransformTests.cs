using System;
using System.Numerics;
using NUnit.Framework;

namespace FragmentsUnity.Tests
{
    [TestFixture]
    public sealed class FragmentTransformTests
    {
        private const double PositionTolerance = 1e-9;
        private const float RotationComponentTolerance = 1e-6f;
        private const float RotatedAxisTolerance = 1e-5f;
        private const double ComposedPositionTolerance = 1e-4;
        private const double LargeTranslationTolerance = 1e-6;

        [Test]
        public void Compose_WithIdentityOnEitherSide_ReturnsOriginal()
        {
            var transform = new FragmentTransform
            {
                PositionX = 1.5,
                PositionY = -2.5,
                PositionZ = 3.5,
                Rotation = Quaternion.CreateFromAxisAngle(Vector3.Normalize(new Vector3(1f, 2f, 3f)), 0.7f)
            };

            FragmentTransform identityFirst = FragmentTransform.Compose(FragmentTransform.Identity, transform);
            FragmentTransform identitySecond = FragmentTransform.Compose(transform, FragmentTransform.Identity);

            AssertTransformNear(transform, identityFirst);
            AssertTransformNear(transform, identitySecond);
        }

        [Test]
        public void Compose_TranslationsOnly_AddsPositions()
        {
            var first = new FragmentTransform
            {
                PositionX = 1.5,
                PositionY = 2.25,
                PositionZ = -3.75,
                Rotation = Quaternion.Identity
            };
            var second = new FragmentTransform
            {
                PositionX = 10.0,
                PositionY = 20.0,
                PositionZ = 30.0,
                Rotation = Quaternion.Identity
            };

            FragmentTransform composed = FragmentTransform.Compose(first, second);

            Assert.AreEqual(11.5, composed.PositionX, PositionTolerance);
            Assert.AreEqual(22.25, composed.PositionY, PositionTolerance);
            Assert.AreEqual(26.25, composed.PositionZ, PositionTolerance);
            Assert.AreEqual(1f, Math.Abs(composed.Rotation.W), RotationComponentTolerance);
        }

        [Test]
        public void Compose_AppliesFirstTransformThenSecond()
        {
            var local = new FragmentTransform
            {
                PositionX = 1.0,
                PositionY = 2.0,
                PositionZ = 3.0,
                Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI / 2f)
            };
            var global = new FragmentTransform
            {
                PositionX = 10.0,
                PositionY = 20.0,
                PositionZ = 30.0,
                Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2f)
            };

            FragmentTransform composed = FragmentTransform.Compose(local, global);

            Assert.AreEqual(13.0, composed.PositionX, ComposedPositionTolerance);
            Assert.AreEqual(22.0, composed.PositionY, ComposedPositionTolerance);
            Assert.AreEqual(29.0, composed.PositionZ, ComposedPositionTolerance);
            AssertRotatedAxis(new Vector3(0f, 0f, -1f), Vector3.UnitX, composed.Rotation);
            AssertRotatedAxis(new Vector3(1f, 0f, 0f), Vector3.UnitY, composed.Rotation);
            AssertRotatedAxis(new Vector3(0f, -1f, 0f), Vector3.UnitZ, composed.Rotation);

            FragmentTransform reversed = FragmentTransform.Compose(global, local);

            Assert.AreEqual(11.0, reversed.PositionX, ComposedPositionTolerance);
            Assert.AreEqual(-28.0, reversed.PositionY, ComposedPositionTolerance);
            Assert.AreEqual(23.0, reversed.PositionZ, ComposedPositionTolerance);
        }

        [Test]
        public void Compose_LargeGlobalTranslation_PreservesSmallLocalOffsetInDouble()
        {
            var local = new FragmentTransform
            {
                PositionX = 0.001,
                Rotation = Quaternion.Identity
            };
            var global = new FragmentTransform
            {
                PositionX = 1e8,
                Rotation = Quaternion.Identity
            };

            FragmentTransform composed = FragmentTransform.Compose(local, global);

            Assert.Greater(composed.PositionX, 1e8);
            Assert.AreEqual(0.001, composed.PositionX - 1e8, LargeTranslationTolerance);
        }

        [Test]
        public void Compose_RotationOnly_MatchesVector3Transform()
        {
            var rotations = new[]
            {
                Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2f),
                Quaternion.CreateFromAxisAngle(Vector3.Normalize(new Vector3(1f, 1f, 0f)), 1.234f),
                Quaternion.CreateFromAxisAngle(Vector3.Normalize(new Vector3(-1f, 2f, 0.5f)), 2.5f),
                Quaternion.CreateFromYawPitchRoll(0.3f, -0.8f, 1.7f)
            };
            var points = new[]
            {
                new Vector3(1f, 2f, 3f),
                new Vector3(-0.5f, 0.25f, -0.125f),
                new Vector3(10f, -20f, 30f)
            };

            foreach (Quaternion rotation in rotations)
            {
                var second = new FragmentTransform { Rotation = rotation };
                foreach (Vector3 point in points)
                {
                    var first = new FragmentTransform
                    {
                        PositionX = point.X,
                        PositionY = point.Y,
                        PositionZ = point.Z,
                        Rotation = Quaternion.Identity
                    };

                    FragmentTransform composed = FragmentTransform.Compose(first, second);

                    Vector3 expected = Vector3.Transform(point, rotation);
                    string context = $"rotation {rotation} point {point}";
                    Assert.AreEqual(expected.X, composed.PositionX, ComposedPositionTolerance, context + " X");
                    Assert.AreEqual(expected.Y, composed.PositionY, ComposedPositionTolerance, context + " Y");
                    Assert.AreEqual(expected.Z, composed.PositionZ, ComposedPositionTolerance, context + " Z");
                }
            }
        }

        private static void AssertTransformNear(in FragmentTransform expected, in FragmentTransform actual)
        {
            Assert.AreEqual(expected.PositionX, actual.PositionX, PositionTolerance);
            Assert.AreEqual(expected.PositionY, actual.PositionY, PositionTolerance);
            Assert.AreEqual(expected.PositionZ, actual.PositionZ, PositionTolerance);
            Assert.AreEqual(expected.Rotation.X, actual.Rotation.X, RotationComponentTolerance);
            Assert.AreEqual(expected.Rotation.Y, actual.Rotation.Y, RotationComponentTolerance);
            Assert.AreEqual(expected.Rotation.Z, actual.Rotation.Z, RotationComponentTolerance);
            Assert.AreEqual(expected.Rotation.W, actual.Rotation.W, RotationComponentTolerance);
        }

        private static void AssertRotatedAxis(Vector3 expected, Vector3 axis, Quaternion rotation)
        {
            Vector3 actual = Vector3.Transform(axis, rotation);
            Assert.AreEqual(expected.X, actual.X, RotatedAxisTolerance, $"axis {axis} X");
            Assert.AreEqual(expected.Y, actual.Y, RotatedAxisTolerance, $"axis {axis} Y");
            Assert.AreEqual(expected.Z, actual.Z, RotatedAxisTolerance, $"axis {axis} Z");
        }
    }
}
