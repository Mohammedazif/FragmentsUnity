using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FragmentsUnity.Editor
{
    /// <summary>Promotes an imported model's mesh and material sub-assets into standalone assets a project can edit and reference.</summary>
    public static class FragmentAssetExtractor
    {
        private const string MenuPath = "Assets/Fragments/Extract Meshes and Materials";
        private const string LogPrefix = "[FragmentsUnity] ";

        /// <summary>Writes one asset per distinct mesh under the model; returns how many were written.</summary>
        public static int ExtractMeshes(FragmentModel model, string targetFolder)
        {
            if (model == null)
            {
                return 0;
            }

            List<Mesh> meshes = CollectMeshes(model);
            var plan = new FragmentAssetExtractionPlan(targetFolder, model.gameObject.name);
            if (meshes.Count == 0 || !PrepareFolder(plan, plan.MeshFolder))
            {
                return 0;
            }

            int written = 0;
            foreach (Mesh mesh in meshes)
            {
                if (WriteCopy(mesh, plan.MeshPath(mesh.name)))
                {
                    written++;
                }
            }

            if (written > 0)
            {
                SaveAssets();
            }
            return written;
        }

        /// <summary>Writes one asset per distinct material under the model; returns how many were written.</summary>
        public static int ExtractMaterials(FragmentModel model, string targetFolder)
        {
            if (model == null)
            {
                return 0;
            }

            List<Material> materials = CollectMaterials(model);
            var plan = new FragmentAssetExtractionPlan(targetFolder, model.gameObject.name);
            if (materials.Count == 0 || !PrepareFolder(plan, plan.MaterialFolder))
            {
                return 0;
            }

            int written = 0;
            foreach (Material material in materials)
            {
                if (WriteCopy(material, plan.MaterialPath(material.name)))
                {
                    written++;
                }
            }

            if (written > 0)
            {
                SaveAssets();
            }
            return written;
        }

        [MenuItem(MenuPath)]
        private static void ExtractSelectedModel()
        {
            FragmentModel model = SelectedModel();
            if (model == null)
            {
                return;
            }

            string targetFolder = FragmentAssetExtractionPlan.DefaultTargetFolder;
            int meshes = ExtractMeshes(model, targetFolder);
            int materials = ExtractMaterials(model, targetFolder);

            Debug.Log(
                $"{LogPrefix}Extracted {meshes} meshes and {materials} materials from '{model.gameObject.name}' into '{targetFolder}'.");
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateExtractSelectedModel()
        {
            return SelectedModel() != null;
        }

        private static FragmentModel SelectedModel()
        {
            GameObject selected = Selection.activeGameObject;
            return selected != null ? selected.GetComponent<FragmentModel>() : null;
        }

        private static List<Mesh> CollectMeshes(FragmentModel model)
        {
            var meshes = new List<Mesh>();
            var seen = new HashSet<Mesh>();

            // Filtering may have deactivated whole storeys, and their meshes are part of the model all the same.
            foreach (MeshFilter filter in model.gameObject.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh != null && seen.Add(filter.sharedMesh))
                {
                    meshes.Add(filter.sharedMesh);
                }
            }

            return meshes;
        }

        private static List<Material> CollectMaterials(FragmentModel model)
        {
            var materials = new List<Material>();
            var seen = new HashSet<Material>();

            foreach (MeshRenderer renderer in model.gameObject.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.sharedMaterial != null && seen.Add(renderer.sharedMaterial))
                {
                    materials.Add(renderer.sharedMaterial);
                }
            }

            return materials;
        }

        private static bool PrepareFolder(FragmentAssetExtractionPlan plan, string folder)
        {
            if (!plan.IsValid)
            {
                // mirrors the over-long-path refusal at FragAssetFactory.cpp:74-78
                Debug.LogError(
                    $"{LogPrefix}Asset path '{plan.ModelFolder}' is {plan.ModelFolder.Length} characters, which leaves no room for asset names — nothing was extracted.");
                return false;
            }

            return EnsureFolder(folder);
        }

        private static bool EnsureFolder(string folder)
        {
            foreach (string step in FragmentAssetExtractionPlan.FolderChain(folder))
            {
                if (AssetDatabase.IsValidFolder(step))
                {
                    continue;
                }

                int leafStart = step.LastIndexOf(FragmentAssetExtractionPlan.FolderSeparator) + 1;
                string parent = step.Substring(0, leafStart - 1);
                if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, step.Substring(leafStart))))
                {
                    Debug.LogError($"{LogPrefix}Could not create folder '{step}' — nothing was extracted.");
                    return false;
                }
            }

            return true;
        }

        private static bool WriteCopy(Object source, string assetPath)
        {
            string uniquePath = AssetDatabase.GenerateUniqueAssetPath(assetPath);

            // CreateAsset refuses an object that already belongs to the imported .frag, so the copy is what gets saved.
            Object copy = Object.Instantiate(source);
            copy.name = Path.GetFileNameWithoutExtension(uniquePath);
            AssetDatabase.CreateAsset(copy, uniquePath);

            // mirrors counting only the assets that actually landed at FragAssetFactory.cpp:253-260
            return AssetDatabase.LoadAssetAtPath<Object>(uniquePath) != null;
        }

        private static void SaveAssets()
        {
            AssetDatabase.SaveAssets();
        }
    }
}
