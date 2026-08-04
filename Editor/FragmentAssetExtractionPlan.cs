using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FragmentsUnity.Editor
{
    /// <summary>Computes the folders and collision-free asset paths an extraction writes; mirrors FFragAssetFactory's layout.</summary>
    public sealed class FragmentAssetExtractionPlan
    {
        // mirrors the /Game/Fragments default root at FragAssetFactory.cpp:60
        public const string DefaultTargetFolder = "Assets/Fragments";

        public const string MeshFolderName = "Meshes";
        public const string MaterialFolderName = "Materials";
        public const string MeshAssetExtension = ".asset";
        public const string MaterialAssetExtension = ".mat";

        public const int MaxAssetNameChars = FragmentImportLimits.MaxExtractedAssetNameChars;

        public const int MaxFolderPathChars = FragmentImportLimits.MaxExtractedFolderPathChars;

        public const char FolderSeparator = '/';

        private const string AssetsRootFolder = "Assets";
        private const string FallbackAssetName = "Asset";
        private const string FallbackModelName = "Model";
        private const char WindowsFolderSeparator = '\\';
        private const char NameSeparator = '_';
        private const char LeadingDigitPrefix = 'A';

        private readonly HashSet<string> _usedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public FragmentAssetExtractionPlan(string targetFolder, string modelName)
        {
            string root = NormalizeFolder(targetFolder);
            string model = SanitizeAssetName(string.IsNullOrEmpty(modelName) ? FallbackModelName : modelName);

            ModelFolder = root + FolderSeparator + model;
            MeshFolder = ModelFolder + FolderSeparator + MeshFolderName;
            MaterialFolder = ModelFolder + FolderSeparator + MaterialFolderName;
            IsValid = MaterialFolder.Length <= MaxFolderPathChars;
        }

        /// <summary>One folder per model under the target root; mirrors BasePath at FragAssetFactory.cpp:69.</summary>
        public string ModelFolder { get; }

        public string MeshFolder { get; }

        public string MaterialFolder { get; }

        /// <summary>False when the folder path leaves no room for asset names; mirrors FragAssetFactory.cpp:72-79.</summary>
        public bool IsValid { get; }

        /// <summary>Every folder that must exist before writing into <paramref name="folder"/>, outermost first.</summary>
        public static IReadOnlyList<string> FolderChain(string folder)
        {
            var chain = new List<string>();
            if (string.IsNullOrEmpty(folder))
            {
                return chain;
            }

            string[] segments = folder.Split(FolderSeparator);
            var path = new StringBuilder(segments[0]);
            for (int index = 1; index < segments.Length; index++)
            {
                path.Append(FolderSeparator).Append(segments[index]);
                chain.Add(path.ToString());
            }
            return chain;
        }

        /// <summary>Folds a source name into a file-system-safe asset name; mirrors SanitizeAssetName at FragAssetFactory.cpp:22.</summary>
        public static string SanitizeAssetName(string name)
        {
            int copyLength = name == null ? 0 : Math.Min(name.Length, MaxAssetNameChars);
            var sanitized = new StringBuilder(copyLength);

            for (int index = 0; index < copyLength; index++)
            {
                char character = name[index];
                sanitized.Append(IsAsciiLetterOrDigit(character) || character == NameSeparator ? character : NameSeparator);
            }

            if (sanitized.Length == 0)
            {
                return FallbackAssetName;
            }

            if (char.IsDigit(sanitized[0]))
            {
                sanitized.Insert(0, LeadingDigitPrefix);
            }

            if (sanitized.Length > MaxAssetNameChars)
            {
                sanitized.Length = MaxAssetNameChars;
            }

            return sanitized.ToString();
        }

        /// <summary>The path to write one extracted mesh to; never repeats a path this plan already handed out.</summary>
        public string MeshPath(string desiredName)
        {
            return UniquePath(MeshFolder, desiredName, MeshAssetExtension);
        }

        /// <summary>The path to write one extracted material to; never repeats a path this plan already handed out.</summary>
        public string MaterialPath(string desiredName)
        {
            return UniquePath(MaterialFolder, desiredName, MaterialAssetExtension);
        }

        private string UniquePath(string folder, string desiredName, string extension)
        {
            string sanitized = SanitizeAssetName(desiredName);
            string path = folder + FolderSeparator + sanitized + extension;

            // Reusing a path would overwrite the asset written moments ago; mirrors FragAssetFactory.cpp:112-118
            int suffix = 1;
            while (!_usedPaths.Add(path))
            {
                path = folder + FolderSeparator + sanitized + NameSeparator
                    + suffix.ToString(CultureInfo.InvariantCulture) + extension;
                suffix++;
            }

            return path;
        }

        private static string NormalizeFolder(string targetFolder)
        {
            string folder = (targetFolder ?? string.Empty).Replace(WindowsFolderSeparator, FolderSeparator).Trim();
            folder = folder.Trim(FolderSeparator);

            if (folder.Length == 0)
            {
                return DefaultTargetFolder;
            }

            // AssetDatabase only writes under the project's Assets root; mirrors the root fix-up at FragAssetFactory.cpp:62-65
            if (folder != AssetsRootFolder && !folder.StartsWith(AssetsRootFolder + FolderSeparator, StringComparison.Ordinal))
            {
                folder = AssetsRootFolder + FolderSeparator + folder;
            }

            return folder;
        }

        private static bool IsAsciiLetterOrDigit(char character)
        {
            return (character >= '0' && character <= '9')
                || (character >= 'A' && character <= 'Z')
                || (character >= 'a' && character <= 'z');
        }
    }
}
