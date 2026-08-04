namespace FragmentsUnity
{
    /// <summary>Hardening budgets defending against hostile or corrupt .frag files; values mirror FragParser.cpp.</summary>
    public static class FragmentImportLimits
    {
        public const long MinInflateAllowanceBytes = 256L * 1024 * 1024;
        public const long MaxInflateAllowanceBytes = 1536L * 1024 * 1024;
        public const int InflateAllowancePerInputByte = 64;
        public const int InflateOutputPresizePerInputByte = 4;
        public const int InflateChunkBytes = 1024 * 1024;
        public const int MetadataDecompressChunkBytes = 1024 * 1024;

        public const int VerifierTableBudgetBytesPerTable = 4;
        public const int VerifierMinTableBudget = 1000000;
        public const int VerifierMaxTableBudget = 100000000;

        public const uint MaxModelItems = 8 * 1000 * 1000;

        public const uint MaxGlobalIdBytes = 64;
        public const uint MaxCategoryBytes = 64;
        public const uint MaxModelHeaderBytes = 1024 * 1024;
        public const uint MaxItemAttributes = 256;

        public const uint MaxShellPoints = 4 * 1000 * 1000;
        public const uint MaxFaceRingVertices = 8192;
        public const long GeometryVertexBudget = 32 * 1000 * 1000;
        public const long ShellPointBudget = 64 * 1000 * 1000;
        public const long EarcutWorkBudget = 2000L * 1000 * 1000;

        public const float CreaseAngleMinimumNormalDot = 0.7f;

        public const float MinimumSafeNormalLengthSquared = 1e-8f;

        public const int MaxSpawnedObjects = 250000;

        // mirrors MaxAssetNameChars at FragAssetFactory.cpp:20
        public const int MaxExtractedAssetNameChars = 64;

        // mirrors the base-path ceiling at FragAssetFactory.cpp:72
        public const int MaxExtractedFolderPathChars = 200;

        // mirrors GMaxMergedVertices at FragmentsActor.cpp:560
        public const int MaxMergedVertices = 500000;

        // mirrors GMaxMergedIndices at FragmentsActor.cpp:563
        public const long MaxMergedIndices = 4000000;

        public const int MergeBucketColorLevels = 255;
        public const int MergeBucketOpacityLevels = 100;

        public const long NameScanByteBudget = 64L * 1024 * 1024;
        public const uint MaxNameTupleBytes = 4096;

        public const uint MaxTupleBytes = 4 * 1024 * 1024;
        public const int MaxTupleTokens = 1024 * 1024;
        public const long MinTupleByteBudgetBytes = 256L * 1024 * 1024;
        public const int TupleByteBudgetPerInputByte = 4;

        public const int MaxPropertyNameChars = 256;
        public const int MaxPropertyValueChars = 4096;
        public const int MaxTypeAttributes = 256;
        public const int MaxItemRelations = 256;
        public const int MaxRelationTargets = 65536;

        public const int MetadataBudgetCharsPerUnit = 64;

        public const int PropertyWalkMaxDepth = 2;
        public const int MaxPropertyUnitsPerItem = 8192;
        public const int PropertyWalkModelItemCap = 1024 * 1024;
        public const int PropertyWalkUnitsPerModelItem = 512;

        public const int AssociationWalkMaxDepth = 4;
        public const int MaxAssociationNodesPerItem = 512;
        public const int MaxAssociationTextChars = 256;
        public const int AssociationWalkModelItemCap = 1024 * 1024;
        public const int AssociationNodesPerModelItem = 128;

        public const int StoreyAncestorSearchLimit = 8;

        // mirrors MaxRowsPerGroup at FragmentsMetadataDetails.cpp:51
        public const int MaxInspectorRowsPerSection = 200;

        // mirrors MaxPropertySetGroups at FragmentsMetadataDetails.cpp:52
        public const int MaxInspectorPropertySets = 100;

        public const int MaxInspectorChunkElements = 200;

        public const float VertexColorGammaExponent = 2.2f;

        // mirrors the surface heuristic at FragmentsActor.cpp:603-613
        public const float GlassBlueDominanceOverRed = 0.15f;
        public const float GlassBlueDominanceOverGreen = 0.05f;
        public const float OpaqueOpacityThreshold = 0.99f;
        public const float GlassOpacityThreshold = 0.5f;
        public const float ForcedGlassOpacity = 0.5f;

        // mirrors M_FragBase_Glass Roughness 0.1 at FragmentsUEEditorModule.cpp:272
        public const float GlassSmoothness = 0.9f;

        // mirrors the non-glass instance Roughness 0.65 at FragmentsActor.cpp:80
        public const float NonGlassSmoothness = 0.35f;
    }
}
