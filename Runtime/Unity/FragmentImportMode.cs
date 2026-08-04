namespace FragmentsUnity
{
    /// <summary>How a parsed model becomes GameObjects; UE's Procedural mode is absent because Unity builds every mesh through the same Mesh path (UNITY_PORT.md section 6).</summary>
    public enum FragmentImportMode
    {
        HierarchyPerBody,
        HierarchyPerElement,
        HierarchyPerStorey,
        Instanced,
        MergedWholeModel
    }
}
