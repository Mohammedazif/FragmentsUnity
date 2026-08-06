using System.Numerics;

namespace FragmentsUnity
{
    /// <summary>A rigid transform in Unity space.</summary>
    public struct FragmentTransform
    {
        public double PositionX;
        public double PositionY;
        public double PositionZ;
        public Quaternion Rotation;

        public static FragmentTransform Identity => new FragmentTransform
        {
            Rotation = Quaternion.Identity
        };

        /// <summary>Composes so the result applies <paramref name="first"/> then <paramref name="second"/>.</summary>
        public static FragmentTransform Compose(in FragmentTransform first, in FragmentTransform second)
        {
            RotatePosition(second.Rotation,
                first.PositionX, first.PositionY, first.PositionZ,
                out double rotatedX, out double rotatedY, out double rotatedZ);

            return new FragmentTransform
            {
                PositionX = rotatedX + second.PositionX,
                PositionY = rotatedY + second.PositionY,
                PositionZ = rotatedZ + second.PositionZ,
                Rotation = Quaternion.Normalize(Quaternion.Concatenate(first.Rotation, second.Rotation))
            };
        }

        private static void RotatePosition(
            Quaternion rotation,
            double x, double y, double z,
            out double outX, out double outY, out double outZ)
        {
            double qx = rotation.X;
            double qy = rotation.Y;
            double qz = rotation.Z;
            double qw = rotation.W;

            double tx = 2.0 * (qy * z - qz * y);
            double ty = 2.0 * (qz * x - qx * z);
            double tz = 2.0 * (qx * y - qy * x);

            outX = x + qw * tx + (qy * tz - qz * ty);
            outY = y + qw * ty + (qz * tx - qx * tz);
            outZ = z + qw * tz + (qx * ty - qy * tx);
        }
    }
}
