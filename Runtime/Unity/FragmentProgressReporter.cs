namespace FragmentsUnity
{
    /// <summary>Progress callback a scene build reports through; returning false cancels the build.</summary>
    public delegate bool FragmentProgressReporter(string stage, float normalizedProgress);
}
