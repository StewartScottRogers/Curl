using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins CGR-071 of the 2026-10-25 gap report: curl 8.22.0 writes every byte a gzip member or
/// zlib stream decoded to before failing its trailer check with zlib's own text, where
/// <see cref="HttpContentCodingDecoder" /> used to drop what the failing read decoded and give
/// the generic exit 61 message (measured, BL-3600 Notes).
/// </summary>
[TestClass]
public sealed class Cgr071Tests
{
    private const string Body = "hello compressed world\n";

    private const string Generic = "Unrecognized or bad HTTP Content or Transfer-Encoding";

    private static readonly int[] ChunkSizes = [1, 3, 5, 7, 11, 16, 17, 65536];

    [TestMethod]
    [DataRow("gzip", -8, "Error while processing content unencoding: incorrect data check", DisplayName = "gzip: CRC-32 byte flipped")]
    [DataRow("gzip", -5, "Error while processing content unencoding: incorrect data check", DisplayName = "gzip: last CRC-32 byte flipped")]
    [DataRow("gzip", -4, "Error while processing content unencoding: incorrect length check", DisplayName = "gzip: first size byte flipped")]
    [DataRow("gzip", -1, "Error while processing content unencoding: incorrect length check", DisplayName = "gzip: last size byte flipped")]
    [DataRow("deflate", -4, "Error while processing content unencoding: incorrect data check", DisplayName = "zlib: first Adler-32 byte flipped")]
    [DataRow("deflate", -1, "Error while processing content unencoding: incorrect data check", DisplayName = "zlib: last Adler-32 byte flipped")]
    public void Decode_TrailerCheckFails_WritesEveryDecodedByteThenThrowsZlibsText(string coding, int flipped, string message)
    {
        byte[] body = Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat(Body, 50)));
        byte[] encoded = Encode(coding, body);
        encoded[encoded.Length + flipped] ^= 0xFF;
        foreach (int chunkSize in ChunkSizes)
        {
            List<byte> decoded = [];
            HttpTransferException thrown = Assert.ThrowsExactly<HttpTransferException>(() => DecodeInto(decoded, coding, encoded, chunkSize));

            Assert.AreEqual(CurlExitCode.BadContentEncoding, thrown.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(message, thrown.Message, $"Chunk size {chunkSize}");
            CollectionAssert.AreEqual(body, decoded.ToArray(), $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    [DataRow("gzip", "1F8B080000000000000AFFFFFFFFFFFFFFFFFFFF", DisplayName = "gzip: corrupt data read together")]
    [DataRow("deflate", "000100FEFF4107", DisplayName = "deflate: raw block after decoded output")]
    public void Decode_CorruptDeflateData_ThrowsTheGenericMessage(string coding, string hex)
    {
        byte[] encoded = Convert.FromHexString(hex);

        HttpTransferException thrown = Assert.ThrowsExactly<HttpTransferException>(() => DecodeInto([], coding, encoded, encoded.Length));

        Assert.AreEqual(Generic, thrown.Message);
    }

    [TestMethod]
    public void Decode_IntactGzipInEveryChunkSize_DecodesTheBody()
    {
        byte[] body = Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat(Body, 50)));
        byte[] encoded = Encode("gzip", body);
        foreach (int chunkSize in ChunkSizes)
        {
            List<byte> decoded = [];
            DecodeInto(decoded, "gzip", encoded, chunkSize);

            CollectionAssert.AreEqual(body, decoded.ToArray(), $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    [DataRow(true, 3, DisplayName = "gzip: too soon after output for the CRC-32")]
    [DataRow(true, 8, DisplayName = "gzip: too late for the CRC-32, size not after a matching CRC-32")]
    [DataRow(true, 12, DisplayName = "gzip: too late for the size")]
    [DataRow(false, 8, DisplayName = "zlib: has no size")]
    public void CheckFailed_FailureNotAtAMismatchedField_IsNull(bool isGzip, long readSinceOutput)
    {
        HttpContentChecksumTrailer trailer = new(isGzip);
        trailer.Append("A"u8);

        Assert.IsNull(trailer.CheckFailed(new byte[16], readSinceOutput));
    }

    [TestMethod]
    public void CheckFailed_ChecksumMatchesWhereItEnds_IsNull()
    {
        HttpContentChecksumTrailer trailer = new(isGzip: true);
        trailer.Append("A"u8);
        byte[] read = new byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(read.AsSpan(4), Crc32Of("A"u8));

        Assert.IsNull(trailer.CheckFailed(read, 4));
    }

    [TestMethod]
    [DataRow(4L, "Error while processing content unencoding: incorrect data check")]
    [DataRow(7L, "Error while processing content unencoding: incorrect data check")]
    [DataRow(8L, "Error while processing content unencoding: incorrect length check")]
    [DataRow(11L, "Error while processing content unencoding: incorrect length check")]
    public void CheckFailed_GzipFailureAtAField_NamesIt(long readSinceOutput, string message)
    {
        HttpContentChecksumTrailer trailer = new(isGzip: true);
        trailer.Append("A"u8);
        byte[] read = new byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(read, readSinceOutput >= 8 ? Crc32Of("A"u8) : 0);
        BinaryPrimitives.WriteUInt32LittleEndian(read.AsSpan(4), readSinceOutput >= 8 ? 2u : 0);

        Assert.AreEqual(message, trailer.CheckFailed(read, readSinceOutput));
    }

    private static uint Crc32Of(ReadOnlySpan<byte> bytes)
    {
        uint crc = uint.MaxValue;
        foreach (byte value in bytes)
        {
            crc ^= value;
            for (int bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
            }
        }

        return ~crc;
    }

    private static void DecodeInto(List<byte> decoded, string coding, byte[] encoded, int chunkSize)
    {
        using HttpContentCodingDecoder decoder = new(coding == "gzip" ? HttpContentCoding.Gzip : HttpContentCoding.Deflate);
        for (int offset = 0; offset < encoded.Length; offset += chunkSize)
        {
            foreach (ReadOnlyMemory<byte> piece in decoder.Decode(encoded.AsMemory(offset, Math.Min(chunkSize, encoded.Length - offset))))
            {
                decoded.AddRange(piece.ToArray());
            }
        }
    }

    private static byte[] Encode(string coding, byte[] body)
    {
        MemoryStream encoded = new();
        using (Stream compressor = coding == "gzip" ? new GZipStream(encoded, CompressionLevel.Optimal) : new ZLibStream(encoded, CompressionLevel.Optimal))
        {
            compressor.Write(body);
        }

        return encoded.ToArray();
    }
}
