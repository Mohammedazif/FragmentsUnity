namespace FragmentsUnity
{
    /// <summary>Model-wide geometry budgets and diagnostics counters shared across every shell.</summary>
    internal sealed class FragmentShellBuildState
    {
        public long VertexBudget = FragmentImportLimits.GeometryVertexBudget;
        public long PointBudget = FragmentImportLimits.ShellPointBudget;
        public long EarcutWorkBudget = FragmentImportLimits.EarcutWorkBudget;
        public int NonFinitePointCount;
        public int DroppedShellPointCount;
        public int OversizedFaceCount;
        public int BudgetedOutFaceCount;
        public int UntriangulatedFaceCount;
    }
}
