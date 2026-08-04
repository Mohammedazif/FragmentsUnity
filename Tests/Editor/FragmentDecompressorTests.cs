using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using NUnit.Framework;

namespace FragmentsUnity.Tests
{
    [TestFixture]
    public sealed class FragmentDecompressorTests
    {
        private const byte ZlibCmfByte = 0x78;
        private const byte ZlibFlgDefaultCompression = 0x9C;
        private const byte ZlibFlgBreakingChecksum = 0x9D;
        private const byte GzipLeadingByte = 0x1F;
        private const byte DeflateReservedBlockTypeByte = 0x07;
        private const int ZlibHeaderChecksumDivisor = 31;
        private const byte ZlibPresetDictionaryFlag = 0x20;
        private const int ZlibCmfBitShift = 8;
        private const int RoundTripPayloadByteCount = 2048;
        private const int RoundTripPayloadByteModulus = 251;

        [Test]
        public void TryDecompress_InputShorterThanZlibHeader_RejectsWithoutLogging()
        {
            AssertSilentReject(Array.Empty<byte>());
            AssertSilentReject(new byte[] { ZlibCmfByte });
        }

        [Test]
        public void TryDecompress_LeadingByteNotZlibMagic_RejectsWithoutLogging()
        {
            AssertSilentReject(new byte[] { GzipLeadingByte, ZlibFlgDefaultCompression });
        }

        [Test]
        public void TryDecompress_HeaderWordFailsChecksum_RejectsWithoutLogging()
        {
            AssertSilentReject(new byte[] { ZlibCmfByte, ZlibFlgBreakingChecksum });
        }

        [Test]
        public void TryDecompress_PresetDictionaryHeader_RejectsWithoutLogging()
        {
            byte presetDictionaryFlg = FindChecksumValidPresetDictionaryFlg();

            AssertSilentReject(new byte[] { ZlibCmfByte, presetDictionaryFlg });
        }

        [Test]
        public void TryDecompress_CorruptDeflateBody_FailsWithSingleErrorLog()
        {
            var data = new byte[]
            {
                ZlibCmfByte, ZlibFlgDefaultCompression, DeflateReservedBlockTypeByte, 0x00, 0x00, 0x00
            };
            var logs = new List<(FragmentImportSeverity Severity, string Message)>();

            bool succeeded = FragmentDecompressor.TryDecompress(
                data, out byte[] decompressed, (severity, message) => logs.Add((severity, message)));

            Assert.That(succeeded, Is.False);
            Assert.That(decompressed, Is.Null);
            Assert.That(logs, Has.Count.EqualTo(1));
            Assert.That(logs[0].Severity, Is.EqualTo(FragmentImportSeverity.Error));
            Assert.That(logs[0].Message, Does.StartWith("zlib inflate failed"));
        }

        [Test]
        public void TryDecompress_ValidZlibStream_RoundTripsPayloadWithoutLogging()
        {
            byte[] payload = BuildDeterministicPayload();
            byte[] data = CompressWithZlibHeader(payload);
            var logs = new List<(FragmentImportSeverity Severity, string Message)>();

            bool succeeded = FragmentDecompressor.TryDecompress(
                data, out byte[] decompressed, (severity, message) => logs.Add((severity, message)));

            Assert.That(succeeded, Is.True);
            Assert.That(decompressed, Is.EqualTo(payload));
            Assert.That(logs, Is.Empty);
        }

        [Test]
        public void TryDecompress_CorruptedAdlerTrailer_FailsWithChecksumError()
        {
            byte[] data = CompressWithZlibHeader(BuildDeterministicPayload());
            data[data.Length - 1] ^= 0xFF;
            var logs = new List<(FragmentImportSeverity Severity, string Message)>();

            bool succeeded = FragmentDecompressor.TryDecompress(
                data, out byte[] decompressed, (severity, message) => logs.Add((severity, message)));

            Assert.That(succeeded, Is.False);
            Assert.That(decompressed, Is.Null);
            Assert.That(logs, Has.Count.EqualTo(1));
            Assert.That(logs[0].Message, Does.Contain("adler32"));
        }

        [Test]
        public void TryDecompress_TruncatedStream_FailsWithChecksumError()
        {
            byte[] data = CompressWithZlibHeader(BuildDeterministicPayload());
            var truncated = new byte[data.Length / 2];
            Array.Copy(data, truncated, truncated.Length);
            var logs = new List<(FragmentImportSeverity Severity, string Message)>();

            bool succeeded = FragmentDecompressor.TryDecompress(
                truncated, out byte[] decompressed, (severity, message) => logs.Add((severity, message)));

            Assert.That(succeeded, Is.False);
            Assert.That(decompressed, Is.Null);
            Assert.That(logs, Has.Count.EqualTo(1));
            Assert.That(logs[0].Severity, Is.EqualTo(FragmentImportSeverity.Error));
        }

        private static void AssertSilentReject(byte[] data)
        {
            var logs = new List<(FragmentImportSeverity Severity, string Message)>();

            bool succeeded = FragmentDecompressor.TryDecompress(
                data, out byte[] decompressed, (severity, message) => logs.Add((severity, message)));

            Assert.That(succeeded, Is.False);
            Assert.That(decompressed, Is.Null);
            Assert.That(logs, Is.Empty);
        }

        private static byte FindChecksumValidPresetDictionaryFlg()
        {
            for (int flg = byte.MinValue; flg <= byte.MaxValue; flg++)
            {
                uint headerWord = ((uint)ZlibCmfByte << ZlibCmfBitShift) | (uint)flg;
                bool checksumValid = headerWord % ZlibHeaderChecksumDivisor == 0;
                bool presetDictionarySet = (flg & ZlibPresetDictionaryFlag) != 0;
                if (checksumValid && presetDictionarySet)
                {
                    return (byte)flg;
                }
            }

            throw new InvalidOperationException("No FLG byte combines a valid zlib checksum with the FDICT bit.");
        }

        private static byte[] BuildDeterministicPayload()
        {
            var payload = new byte[RoundTripPayloadByteCount];
            for (int i = 0; i < payload.Length; i++)
            {
                payload[i] = (byte)(i % RoundTripPayloadByteModulus);
            }

            return payload;
        }

        private static byte[] CompressWithZlibHeader(byte[] payload)
        {
            using var buffer = new MemoryStream();
            buffer.WriteByte(ZlibCmfByte);
            buffer.WriteByte(ZlibFlgDefaultCompression);
            using (var deflate = new DeflateStream(buffer, CompressionLevel.Optimal, leaveOpen: true))
            {
                deflate.Write(payload, 0, payload.Length);
            }

            uint adler = ComputeAdler32(payload);
            buffer.WriteByte((byte)(adler >> 24));
            buffer.WriteByte((byte)(adler >> 16));
            buffer.WriteByte((byte)(adler >> 8));
            buffer.WriteByte((byte)adler);
            return buffer.ToArray();
        }

        private static uint ComputeAdler32(byte[] payload)
        {
            const uint modulus = 65521;
            uint low = 1;
            uint high = 0;
            foreach (byte value in payload)
            {
                low = (low + value) % modulus;
                high = (high + low) % modulus;
            }

            return (high << 16) | low;
        }
    }
}
