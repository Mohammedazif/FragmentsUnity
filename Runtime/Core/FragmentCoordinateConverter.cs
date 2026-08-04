using System.Numerics;

namespace FragmentsUnity
{
    /// <summary>Converts Fragments source space (right-handed, Y-up, meters) to Unity space (left-handed, Y-up, meters).</summary>
    public static class FragmentCoordinateConverter
    {
        /// <summary>Single-axis mirror; callers must reverse triangle winding. Mirrors FragParser.cpp:2057.</summary>
        public static Vector3 ConvertPosition(float x, float y, float z, float scale)
        {
            return new Vector3(-x * scale, y * scale, z * scale);
        }

        /// <summary>Reconstructs z = cross(x, y), conjugates the basis by the mirror; mirrors FragParser.cpp:2076.</summary>
        public static FragmentTransform BuildTransform(
            double positionX, double positionY, double positionZ,
            float xDirectionX, float xDirectionY, float xDirectionZ,
            float yDirectionX, float yDirectionY, float yDirectionZ,
            float scale)
        {
            var sourceX = new Vector3(xDirectionX, xDirectionY, xDirectionZ);
            var sourceY = new Vector3(yDirectionX, yDirectionY, yDirectionZ);
            Vector3 sourceZ = Vector3.Cross(sourceX, sourceY);

            Vector3 xAxisImage = SafeNormalize(new Vector3(sourceX.X, -sourceX.Y, -sourceX.Z));
            Vector3 yAxisImage = SafeNormalize(new Vector3(-sourceY.X, sourceY.Y, sourceY.Z));
            Vector3 zAxisImage = SafeNormalize(new Vector3(-sourceZ.X, sourceZ.Y, sourceZ.Z));

            var rotationMatrix = new Matrix4x4(
                xAxisImage.X, xAxisImage.Y, xAxisImage.Z, 0f,
                yAxisImage.X, yAxisImage.Y, yAxisImage.Z, 0f,
                zAxisImage.X, zAxisImage.Y, zAxisImage.Z, 0f,
                0f, 0f, 0f, 1f);

            return new FragmentTransform
            {
                PositionX = -positionX * scale,
                PositionY = positionY * scale,
                PositionZ = positionZ * scale,
                Rotation = Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(rotationMatrix))
            };
        }

        private static Vector3 SafeNormalize(Vector3 value)
        {
            float lengthSquared = value.LengthSquared();
            return lengthSquared < FragmentImportLimits.MinimumSafeNormalLengthSquared
                ? Vector3.Zero
                : value / (float)System.Math.Sqrt(lengthSquared);
        }
    }
}
