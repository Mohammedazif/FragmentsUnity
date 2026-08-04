namespace FragmentsUnity
{
    internal static class FragmentUnityMath
    {
        internal static UnityEngine.Vector3 ToUnityVector(System.Numerics.Vector3 value)
        {
            return new UnityEngine.Vector3(value.X, value.Y, value.Z);
        }

        internal static UnityEngine.Quaternion ToUnityQuaternion(System.Numerics.Quaternion value)
        {
            return new UnityEngine.Quaternion(value.X, value.Y, value.Z, value.W);
        }

        internal static UnityEngine.Vector3 ToUnityPosition(in FragmentTransform transform)
        {
            return new UnityEngine.Vector3(
                (float)transform.PositionX,
                (float)transform.PositionY,
                (float)transform.PositionZ);
        }
    }
}
