using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEngine;
using CompressionLevel = System.IO.Compression.CompressionLevel;

namespace FragmentsUnity
{
    /// <summary>Sub-asset persisting an imported model's metadata as compressed JSON, deserialized on demand.</summary>
    public sealed class FragmentModelAsset : ScriptableObject
    {
        private const string AssetName = "ModelData";

        // Host projects may set JsonConvert.DefaultSettings; pinning them keeps the payload readable back.
        private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver(),
            NullValueHandling = NullValueHandling.Include,
            TypeNameHandling = TypeNameHandling.None,
            DateParseHandling = DateParseHandling.None,
            Culture = CultureInfo.InvariantCulture,
            FloatParseHandling = FloatParseHandling.Double
        };

        [HideInInspector]
        [SerializeField] private byte[] _compressedMetadata = Array.Empty<byte>();

        public static FragmentModelAsset Create(FragmentImportResult result)
        {
            var data = new FragmentModelData
            {
                ModelName = result.ModelName,
                ModelGuid = result.ModelGuid,
                Metadata = result.Metadata,
                ModelInfo = result.ModelInfo
            };

            // Only items carrying geometry are queryable, deduped in instance order; mirrors FragmentsActor.cpp:781
            var seenLocalIds = new HashSet<int>();
            foreach (FragmentInstance instance in result.Instances)
            {
                if (!seenLocalIds.Add(instance.LocalId))
                {
                    continue;
                }
                FragmentItemMetadata item = result.FindItem(instance.LocalId);
                if (item != null)
                {
                    data.Items.Add(item);
                }
            }

            var asset = CreateInstance<FragmentModelAsset>();
            asset.name = AssetName;
            asset._compressedMetadata = Compress(JsonConvert.SerializeObject(data, SerializerSettings));
            return asset;
        }

        /// <summary>Deserializes the stored payload; a missing or corrupt payload yields empty data, never null.</summary>
        public FragmentModelData Load()
        {
            if (_compressedMetadata == null || _compressedMetadata.Length == 0)
            {
                return new FragmentModelData();
            }

            try
            {
                return JsonConvert.DeserializeObject<FragmentModelData>(
                    Decompress(_compressedMetadata), SerializerSettings) ?? new FragmentModelData();
            }
            catch (Exception exception) when (exception is JsonException || exception is InvalidDataException)
            {
                // A hand-edited or corrupt asset must not break metadata queries.
                Debug.LogWarning("[FragmentsUnity] Model metadata failed to load; queries will see an empty model.", this);
                return new FragmentModelData();
            }
        }

        private static byte[] Compress(string json)
        {
            byte[] payload = Encoding.UTF8.GetBytes(json);
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
            {
                gzip.Write(payload, 0, payload.Length);
            }
            return output.ToArray();
        }

        private static string Decompress(byte[] compressed)
        {
            using var input = new MemoryStream(compressed, writable: false);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();

            var chunk = new byte[FragmentImportLimits.MetadataDecompressChunkBytes];
            int bytesRead;
            while ((bytesRead = gzip.Read(chunk, 0, chunk.Length)) > 0)
            {
                output.Write(chunk, 0, bytesRead);
            }

            return Encoding.UTF8.GetString(output.ToArray());
        }
    }
}
