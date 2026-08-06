using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;

namespace FragmentsUnity
{
    /// <summary>Inflates zlib-wrapped .frag payloads under a decompression-bomb ceiling.</summary>
    public static class FragmentDecompressor
    {
        private const byte ZlibMagicByte = 0x78;
        private const int ZlibHeaderBytes = 2;
        private const int ZlibHeaderChecksumDivisor = 31;
        private const byte ZlibPresetDictionaryFlag = 0x20;
        private const int ZlibTrailerBytes = 4;
        private const uint Adler32Modulus = 65521;
        private const int Adler32BatchBytes = 5552;
        private const double BytesPerMegabyte = 1024.0 * 1024.0;

        /// <summary>False means the data is not usable zlib; the caller treats the buffer as uncompressed.</summary>
        public static bool TryDecompress(byte[] data, out byte[] decompressed, Action<FragmentImportSeverity, string> log)
        {
            decompressed = null;

            if (data == null || data.Length < ZlibHeaderBytes)
                return false;

            if (data[0] != ZlibMagicByte)
                return false;

            // A FLG whitelist misses 0x5E.
            if ((((uint)data[0] << 8) | data[1]) % ZlibHeaderChecksumDivisor != 0)
                return false;

            // FDICT: a preset dictionary we have no way to supply.
            if ((data[1] & ZlibPresetDictionaryFlag) != 0)
                return false;

            long inflateAllowance = Math.Clamp(
                (long)data.Length * FragmentImportLimits.InflateAllowancePerInputByte,
                FragmentImportLimits.MinInflateAllowanceBytes,
                FragmentImportLimits.MaxInflateAllowanceBytes);

            // The 32-bit product wraps negative past a 512 MB input.
            int presizeBytes = (int)Math.Min(
                (long)data.Length * FragmentImportLimits.InflateOutputPresizePerInputByte, inflateAllowance);

            using var compressedStream = new MemoryStream(data, ZlibHeaderBytes, data.Length - ZlibHeaderBytes, writable: false);
            using var inflateStream = new DeflateStream(compressedStream, CompressionMode.Decompress);
            using var output = new MemoryStream(presizeBytes);

            var chunk = new byte[FragmentImportLimits.InflateChunkBytes];
            long totalInflated = 0;
            uint runningAdler = 1;

            try
            {
                int bytesInflated;
                // DeflateStream never consumes the trailing adler32.
                while ((bytesInflated = inflateStream.Read(chunk, 0, chunk.Length)) > 0)
                {
                    totalInflated += bytesInflated;
                    if (totalInflated > inflateAllowance)
                    {
                        log?.Invoke(FragmentImportSeverity.Error, string.Format(
                            CultureInfo.InvariantCulture,
                            "Refusing to decompress further: {0:F1} MB of input has expanded past the {1:F1} MB ceiling. The file is corrupt or is a decompression bomb.",
                            data.Length / BytesPerMegabyte,
                            inflateAllowance / BytesPerMegabyte));
                        return false;
                    }

                    runningAdler = UpdateAdler32(runningAdler, chunk, bytesInflated);
                    output.Write(chunk, 0, bytesInflated);
                }
            }
            catch (InvalidDataException exception)
            {
                log?.Invoke(FragmentImportSeverity.Error, $"zlib inflate failed: {exception.Message}");
                return false;
            }

            // Checking the adler32 here rejects truncated input that would otherwise pass as partial output.
            if (data.Length < ZlibHeaderBytes + ZlibTrailerBytes || runningAdler != ReadTrailerAdler32(data))
            {
                log?.Invoke(FragmentImportSeverity.Error, "zlib inflate failed: adler32 checksum mismatch");
                return false;
            }

            decompressed = output.ToArray();
            return true;
        }

        private static uint UpdateAdler32(uint adler, byte[] buffer, int count)
        {
            uint low = adler & 0xFFFF;
            uint high = (adler >> 16) & 0xFFFF;

            int offset = 0;
            while (offset < count)
            {
                int batchEnd = Math.Min(offset + Adler32BatchBytes, count);
                for (int i = offset; i < batchEnd; i++)
                {
                    low += buffer[i];
                    high += low;
                }
                low %= Adler32Modulus;
                high %= Adler32Modulus;
                offset = batchEnd;
            }

            return (high << 16) | low;
        }

        private static uint ReadTrailerAdler32(byte[] data)
        {
            int start = data.Length - ZlibTrailerBytes;
            return ((uint)data[start] << 24)
                | ((uint)data[start + 1] << 16)
                | ((uint)data[start + 2] << 8)
                | data[start + 3];
        }
    }
}
