using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;

namespace FragmentsUnity
{
    /// <summary>Builds placement instances from mesh samples and stamps them with item identity.</summary>
    internal static class FragmentInstanceReader
    {
        internal static void BuildInstances(
            Schema.Meshes meshes,
            float scaleFactor,
            IReadOnlyList<Vector4> materialColors,
            IReadOnlyList<bool> materialDoubleSided,
            IReadOnlyDictionary<int, string> localIdToGlobalId,
            FragmentImportResult result,
            Action<FragmentImportSeverity, string> log)
        {
            // Verifier enforces both required vectors; empty meshes_items falls through to the per-sample check (FragParser.cpp:1811).
            if (meshes.SamplesLength == 0)
            {
                return;
            }

            int meshesItemCount = meshes.MeshesItemsLength;
            int representationCount = meshes.RepresentationsLength;
            int localTransformCount = meshes.LocalTransformsLength;
            int globalTransformCount = meshes.GlobalTransformsLength;

            int skippedSamples = 0;
            int skippedCircleExtrusions = 0;

            int sampleCount = Math.Min(meshes.SamplesLength, (int)FragmentImportLimits.MaxModelItems);
            if (meshes.SamplesLength > sampleCount)
            {
                log?.Invoke(FragmentImportSeverity.Error, string.Format(
                    CultureInfo.InvariantCulture,
                    "Model declares {0} samples; building the first {1}. The rest of the geometry will be missing.",
                    meshes.SamplesLength, sampleCount));
            }
            result.Instances.Capacity = sampleCount;

            for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
            {
                Schema.Sample sample = meshes.Samples(sampleIndex).Value;
                var instance = new FragmentInstance();

                uint itemIndex = sample.Item;
                if (itemIndex >= (uint)meshesItemCount)
                {
                    skippedSamples++;
                    continue;
                }

                // A 2 GB buffer cannot hold 2^31 ids, so a larger value is corrupt (mirrors FragParser.cpp:1843-1849).
                uint rawLocalId = meshes.MeshesItems((int)itemIndex);
                if (rawLocalId > int.MaxValue)
                {
                    skippedSamples++;
                    continue;
                }

                instance.LocalId = (int)rawLocalId;

                uint representationIndex = sample.Representation;
                if (representationIndex < (uint)representationCount)
                {
                    Schema.Representation representation = meshes.Representations((int)representationIndex).Value;
                    if (representation.RepresentationClass == Schema.RepresentationClass.SHELL)
                    {
                        instance.GeometryIndex = (int)representation.Id;
                    }
                    else if (representation.RepresentationClass == Schema.RepresentationClass.CIRCLE_EXTRUSION)
                    {
                        // TODO(CircleExtrusion) - skipped to match FragParser.cpp:1862
                        skippedCircleExtrusions++;
                    }
                }

                instance.MaterialIndex = (int)sample.Material;
                if (instance.MaterialIndex >= 0 && instance.MaterialIndex < materialColors.Count)
                {
                    Vector4 color = materialColors[instance.MaterialIndex];
                    instance.Color = color;
                    instance.Opacity = color.W;
                    instance.DoubleSided = materialDoubleSided[instance.MaterialIndex];
                }

                FragmentTransform localTransform = FragmentTransform.Identity;
                FragmentTransform globalTransform = FragmentTransform.Identity;

                uint localTransformIndex = sample.LocalTransform;
                if (localTransformIndex < (uint)localTransformCount)
                {
                    localTransform = ConvertTransform(meshes.LocalTransforms((int)localTransformIndex).Value, scaleFactor);
                }

                // Global transform is indexed by the sample's item index, not the sample index (mirrors FragParser.cpp:1888).
                if (itemIndex < (uint)globalTransformCount)
                {
                    globalTransform = ConvertTransform(meshes.GlobalTransforms((int)itemIndex).Value, scaleFactor);
                }

                instance.Transform = FragmentTransform.Compose(localTransform, globalTransform);

                if (localIdToGlobalId.TryGetValue(instance.LocalId, out string globalId))
                {
                    instance.GlobalId = globalId;
                }

                result.Instances.Add(instance);
            }

            result.TotalInstances = result.Instances.Count;
            if (skippedSamples > 0)
            {
                log?.Invoke(FragmentImportSeverity.Warning, string.Format(
                    CultureInfo.InvariantCulture,
                    "Skipped {0} sample(s): item index does not resolve to a local id",
                    skippedSamples));
            }
            if (skippedCircleExtrusions > 0)
            {
                log?.Invoke(FragmentImportSeverity.Info, string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} circle-extrusion (rebar) representation(s) skipped - not implemented",
                    skippedCircleExtrusions));
            }
        }

        internal static void EnrichInstances(FragmentImportResult result, FragmentNameExtractor nameExtractor)
        {
            foreach (FragmentInstance instance in result.Instances)
            {
                FragmentItemMetadata item = result.FindItem(instance.LocalId);
                if (item != null)
                {
                    instance.Category = item.Category;
                    instance.Name = item.Name;
                    if (!string.IsNullOrEmpty(item.GlobalId))
                    {
                        instance.GlobalId = item.GlobalId;
                    }
                    continue;
                }

                if (instance.LocalId >= 0 && instance.LocalId < result.Categories.Count)
                {
                    instance.Category = result.Categories[instance.LocalId];
                }
                instance.Name = nameExtractor.ExtractName(instance.LocalId);
            }
        }

        private static FragmentTransform ConvertTransform(Schema.Transform transform, float scaleFactor)
        {
            Schema.DoubleVector position = transform.Position;
            Schema.FloatVector xDirection = transform.XDirection;
            Schema.FloatVector yDirection = transform.YDirection;
            return FragmentCoordinateConverter.BuildTransform(
                position.X, position.Y, position.Z,
                xDirection.X, xDirection.Y, xDirection.Z,
                yDirection.X, yDirection.Y, yDirection.Z,
                scaleFactor);
        }
    }
}
