using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using Google.FlatBuffers;

namespace FragmentsUnity
{
    /// <summary>Parses ThatOpen Fragments 2.0 (.frag) data into an engine-neutral FragmentImportResult.</summary>
    public static class FragmentParser
    {
        private const float ByteRangeColorChannelDivisor = 255.0f;
        private const float UnitRangeColorChannelMax = 1.0f;

        public static FragmentImportResult LoadFromFile(string filePath, FragmentImportOptions options = null)
        {
            options ??= new FragmentImportOptions();

            byte[] rawData;
            try
            {
                rawData = File.ReadAllBytes(filePath);
            }
            catch (Exception exception) when (
                exception is IOException
                || exception is UnauthorizedAccessException
                || exception is ArgumentException
                || exception is NotSupportedException
                || exception is System.Security.SecurityException)
            {
                var failed = new FragmentImportResult { ErrorMessage = $"Failed to read file: {filePath}" };
                options.Log?.Invoke(FragmentImportSeverity.Error, failed.ErrorMessage);
                return failed;
            }

            FragmentImportResult result = LoadFromBuffer(rawData, options);

            string baseName = Path.GetFileNameWithoutExtension(filePath).Replace(" ", "_");
            result.ModelName = baseName;
            result.ModelInfo.Name = baseName;

            return result;
        }

        /// <summary>Accepts either a zlib-compressed or a raw FlatBuffers .frag payload.</summary>
        public static FragmentImportResult LoadFromBuffer(byte[] data, FragmentImportOptions options = null)
        {
            options ??= new FragmentImportOptions();
            Action<FragmentImportSeverity, string> log = options.Log;

            if (data == null || data.Length == 0)
            {
                return new FragmentImportResult { ErrorMessage = "Empty buffer" };
            }

            byte[] buffer = FragmentDecompressor.TryDecompress(data, out byte[] decompressed, log)
                ? decompressed
                : data;

            var byteBuffer = new ByteBuffer(buffer);
            var verifierOptions = new Options
            {
                maxTables = (int)Math.Clamp(
                    (long)buffer.Length / FragmentImportLimits.VerifierTableBudgetBytesPerTable,
                    FragmentImportLimits.VerifierMinTableBudget,
                    FragmentImportLimits.VerifierMaxTableBudget)
            };

            var verifier = new Verifier(byteBuffer, verifierOptions);
            // ThatOpen writes no file identifier; null skips the identifier check (mirrors FragParser.cpp:64-70).
            if (!verifier.VerifyBuffer(null, false, Schema.ModelVerify.Verify))
            {
                var failed = new FragmentImportResult
                {
                    ErrorMessage = "FlatBuffers verification failed — invalid or corrupt .frag data"
                };
                log?.Invoke(FragmentImportSeverity.Error, failed.ErrorMessage);
                return failed;
            }

            return ParseModel(Schema.Model.GetRootAsModel(byteBuffer), options, buffer.Length);
        }

        private static FragmentImportResult ParseModel(Schema.Model model, FragmentImportOptions options, int bufferSize)
        {
            var result = new FragmentImportResult();
            Action<FragmentImportSeverity, string> log = options.Log;
            float scaleFactor = options.ScaleFactor;

            int rejectedStrings = 0;

            result.ModelGuid = ReadBoundedString(model.Guid, FragmentImportLimits.MaxGlobalIdBytes, ref rejectedStrings);
            result.Metadata = ReadBoundedString(model.Metadata, FragmentImportLimits.MaxModelHeaderBytes, ref rejectedStrings);

            // Categories run parallel to local_ids; mirrors FragParser.cpp:1323.
            int categoryCount = Math.Min(
                Math.Min(model.CategoriesLength, model.LocalIdsLength),
                (int)FragmentImportLimits.MaxModelItems);
            result.Categories.Capacity = categoryCount;
            for (int i = 0; i < categoryCount; i++)
            {
                result.Categories.Add(
                    ReadBoundedString(model.Categories(i), FragmentImportLimits.MaxCategoryBytes, ref rejectedStrings));
            }

            // local_ids[dense_index] = ifc_express_id; the reverse map turns express ids into dense ones.
            int localIdCount = model.LocalIdsLength;
            if (localIdCount > FragmentImportLimits.MaxModelItems)
            {
                log?.Invoke(FragmentImportSeverity.Error, string.Format(
                    CultureInfo.InvariantCulture,
                    "Model declares {0} items, past the {1} this importer supports — truncating. Elements past the cut will not import.",
                    localIdCount, FragmentImportLimits.MaxModelItems));
                localIdCount = (int)FragmentImportLimits.MaxModelItems;
            }

            var localIds = new List<uint>(localIdCount);
            var expressIdToDenseIndex = new Dictionary<uint, int>(localIdCount);
            for (int i = 0; i < localIdCount; i++)
            {
                uint expressId = model.LocalIds(i);
                localIds.Add(expressId);
                expressIdToDenseIndex[expressId] = i;
            }

            // guids_items[k] holds the express id owning guids[k], not a dense index into guids.
            var localIdToGlobalId = new Dictionary<int, string>();
            if (model.GetGuidsItemsBytes() != null)
            {
                int pairCount = Math.Min(
                    Math.Min(model.GuidsLength, model.GuidsItemsLength),
                    (int)FragmentImportLimits.MaxModelItems);
                for (int k = 0; k < pairCount; k++)
                {
                    if (expressIdToDenseIndex.TryGetValue(model.GuidsItems(k), out int denseIndex))
                    {
                        string globalId = ReadBoundedString(model.Guids(k), FragmentImportLimits.MaxGlobalIdBytes, ref rejectedStrings);
                        if (globalId.Length > 0)
                        {
                            localIdToGlobalId[denseIndex] = globalId;
                        }
                    }
                }

                result.TotalElements = model.GuidsLength;
            }

            if (rejectedStrings > 0)
            {
                log?.Invoke(FragmentImportSeverity.Warning, string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} string(s) — model guid, header, category or element GlobalId — were declared far longer than that kind of value can be, and were dropped.",
                    rejectedStrings));
            }

            if (options.ImportMetadata)
            {
                FragmentMetadataReader.BuildItemMetadata(
                    model, localIds, expressIdToDenseIndex, localIdToGlobalId, options, bufferSize, result, log);

                var modelInfo = new FragmentItemMetadata();
                FragmentModelInfoBuilder.BuildModelInfo(result, modelInfo);
                result.ModelInfo = modelInfo;
            }

            Schema.Meshes? meshesTable = model.Meshes;
            if (!meshesTable.HasValue)
            {
                result.ErrorMessage = "Model has no meshes";
                return result;
            }

            Schema.Meshes meshes = meshesTable.Value;

            Schema.Transform? coordinates = meshes.Coordinates;
            if (options.ImportMetadata && coordinates.HasValue)
            {
                Schema.DoubleVector origin = coordinates.Value.Position;
                result.ModelInfo.Attributes.Add(new FragmentAttribute(
                    "Model Origin",
                    string.Format(CultureInfo.InvariantCulture, "{0:F6}, {1:F6}, {2:F6}", origin.X, origin.Y, origin.Z),
                    "metres, source coordinate system"));
            }

            // Material is a 6-byte struct, so nothing but the buffer bounds this vector's length.
            int materialCount = Math.Min(meshes.MaterialsLength, (int)FragmentImportLimits.MaxModelItems);
            if (meshes.MaterialsLength > materialCount)
            {
                log?.Invoke(FragmentImportSeverity.Error, string.Format(
                    CultureInfo.InvariantCulture,
                    "Model declares {0} materials; reading the first {1}.",
                    meshes.MaterialsLength, materialCount));
            }

            var materialColors = new List<Vector4>(materialCount);
            var materialDoubleSided = new List<bool>(materialCount);
            for (int i = 0; i < materialCount; i++)
            {
                Schema.Material material = meshes.Materials(i).Value;
                float r = material.R;
                float g = material.G;
                float b = material.B;
                float a = material.A;

                // Color channels may be [0, 1] or [0, 255] depending on exporter version.
                float channelDivisor =
                    r > UnitRangeColorChannelMax || g > UnitRangeColorChannelMax
                    || b > UnitRangeColorChannelMax || a > UnitRangeColorChannelMax
                        ? ByteRangeColorChannelDivisor
                        : UnitRangeColorChannelMax;

                materialColors.Add(new Vector4(r / channelDivisor, g / channelDivisor, b / channelDivisor, a / channelDivisor));
                materialDoubleSided.Add(material.RenderedFaces == Schema.RenderedFaces.TWO);
            }

            FragmentGeometryBuilder.BuildShellGeometries(meshes, scaleFactor, result, log);
            FragmentInstanceReader.BuildInstances(
                meshes, scaleFactor, materialColors, materialDoubleSided, localIdToGlobalId, result, log);

            var nameExtractor = new FragmentNameExtractor(model, result);
            FragmentSpatialStructureReader.BuildSpatialStructure(model, expressIdToDenseIndex, nameExtractor, result);
            FragmentInstanceReader.EnrichInstances(result, nameExtractor);

            result.Success = true;
            return result;
        }

        // Bounds the decoded char count, not the UTF-8 byte count of FragParser.cpp:183 — accepted divergence.
        private static string ReadBoundedString(string value, uint maxBytes, ref int rejectedStrings)
        {
            if (value == null)
            {
                return string.Empty;
            }

            if (value.Length > maxBytes)
            {
                rejectedStrings++;
                return string.Empty;
            }

            // FString(UTF8_TO_TCHAR(c_str())) stops at the first NUL; mirrors FragParser.cpp:199.
            int nulIndex = value.IndexOf('\0');
            return nulIndex < 0 ? value : value.Substring(0, nulIndex);
        }
    }
}
