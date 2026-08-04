using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace FragmentsUnity.Tests
{
    internal static class FragmentSampleModels
    {
        internal const string Ar520FileName = "AR520.frag";
        internal const string JoysonFileName = "Joyson Model.frag";

        private const string SampleDirEnvironmentVariable = "FRAGMENTSUNITY_SAMPLE_DIR";

        private static readonly Dictionary<string, FragmentImportResult> ParsedSamples =
            new Dictionary<string, FragmentImportResult>();

        private static readonly Dictionary<string, FragmentModelAsset> MetadataAssets =
            new Dictionary<string, FragmentModelAsset>();

        internal static FragmentImportResult LoadOrIgnore(string fileName)
        {
            if (ParsedSamples.TryGetValue(fileName, out FragmentImportResult cached))
            {
                return cached;
            }

            FragmentImportResult result = FragmentParser.LoadFromFile(ResolveSamplePathOrIgnore(fileName));
            Assert.That(result.Success, Is.True, result.ErrorMessage);

            ParsedSamples[fileName] = result;
            return result;
        }

        internal static FragmentSceneBuildResult BuildOrIgnore(string fileName, FragmentImportMode mode)
        {
            return Build(LoadOrIgnore(fileName), mode, true);
        }

        internal static FragmentSceneBuildResult BuildWithoutPickingOrIgnore(string fileName, FragmentImportMode mode)
        {
            return Build(LoadOrIgnore(fileName), mode, false);
        }

        internal static FragmentSceneBuildResult BuildAsImportedOrIgnore(string fileName, FragmentImportMode mode)
        {
            FragmentSceneBuildResult scene = BuildOrIgnore(fileName, mode);
            scene.Root.AddComponent<FragmentModel>().SetAsset(MetadataAssetOrIgnore(fileName));
            scene.Root.AddComponent<FragmentFilter>();
            return scene;
        }

        internal static int CountObjects(GameObject root)
        {
            int count = 1;
            for (int child = 0; child < root.transform.childCount; child++)
            {
                count += CountObjects(root.transform.GetChild(child).gameObject);
            }
            return count;
        }

        internal static int CountTriangles(GameObject target)
        {
            return target.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3;
        }

        internal static RaycastHit Hit(Collider collider, int triangleIndex)
        {
            return new RaycastHit { collider = collider, triangleIndex = triangleIndex };
        }

        private static FragmentModelAsset MetadataAssetOrIgnore(string fileName)
        {
            if (MetadataAssets.TryGetValue(fileName, out FragmentModelAsset cached))
            {
                return cached;
            }

            FragmentModelAsset asset = FragmentModelAsset.Create(LoadOrIgnore(fileName));
            MetadataAssets[fileName] = asset;
            return asset;
        }

        private static FragmentSceneBuildResult Build(
            FragmentImportResult result, FragmentImportMode mode, bool enablePicking)
        {
            var options = new FragmentSceneBuildOptions { Mode = mode, EnablePicking = enablePicking };
            FragmentSceneBuildResult scene = FragmentSceneBuilder.Build(result, options, null);
            Assert.That(scene.Root, Is.Not.Null);
            return scene;
        }

        private static string ResolveSamplePathOrIgnore(string fileName)
        {
            string sampleDir = Environment.GetEnvironmentVariable(SampleDirEnvironmentVariable);
            if (string.IsNullOrEmpty(sampleDir))
            {
                Assert.Ignore(
                    $"Set {SampleDirEnvironmentVariable} to the directory holding sample .frag files to run this test.");
            }

            string path = Path.Combine(sampleDir, fileName);
            if (!File.Exists(path))
            {
                Assert.Ignore($"Sample file '{fileName}' not found in {sampleDir}.");
            }

            return path;
        }
    }
}
