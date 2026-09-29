using System.Buffers.Binary;
using System.IO.Compression;

namespace Curl.Tls;

/// <summary>
/// Compresses a Certificate body for a test server's CompressedCertificate: zlib through
/// <see cref="ZLibStream" />, brotli through <see cref="BrotliEncoder" />, and zstd as a
/// single frame of raw blocks (RFC 8878 section 3.1.1), since the BCL has no Zstandard
/// encoder and the decoder under test reads any valid frame.
/// </summary>
internal static class TestCertificateCompressor
{
    private const int ZstandardMaxBlockSize = 128 * 1024;

    public static byte[] Compress(ushort algorithm, byte[] data) => algorithm switch
    {
        CertificateCompressionAlgorithm.Zlib => CompressZlib(data),
        CertificateCompressionAlgorithm.Brotli => CompressBrotli(data),
        _ => CompressZstandardRaw(data),
    };

    /// <summary>Returns a CompressedCertificate carrying <paramref name="certificateBody" /> compressed with <paramref name="algorithm" />, its true length declared.</summary>
    public static CompressedCertificate Wrap(ushort algorithm, byte[] certificateBody) =>
        new(algorithm, certificateBody.Length, Compress(algorithm, certificateBody));

    private static byte[] CompressZlib(byte[] data)
    {
        using MemoryStream output = new();
        using (ZLibStream stream = new(output, CompressionLevel.Optimal))
        {
            stream.Write(data);
        }

        return output.ToArray();
    }

    private static byte[] CompressBrotli(byte[] data)
    {
        byte[] output = new byte[BrotliEncoder.GetMaxCompressedLength(data.Length)];
        Assert.IsTrue(BrotliEncoder.TryCompress(data, output, out int written));
        return output[..written];
    }

    private static byte[] CompressZstandardRaw(byte[] data)
    {
        List<byte> frame = [0x28, 0xB5, 0x2F, 0xFD];

        // Frame header descriptor: a 4-byte content size, single segment, no checksum, no dictionary.
        frame.Add(0xA0);
        byte[] contentSize = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(contentSize, (uint)data.Length);
        frame.AddRange(contentSize);
        int position = 0;
        do
        {
            int size = Math.Min(ZstandardMaxBlockSize, data.Length - position);
            bool last = position + size == data.Length;

            // Block header: last-block bit, block type 0 (raw), then the size, 3 bytes little-endian.
            int header = (size << 3) | (last ? 1 : 0);
            frame.AddRange([(byte)header, (byte)(header >> 8), (byte)(header >> 16)]);
            frame.AddRange(data.AsSpan(position, size));
            position += size;
        }
        while (position < data.Length);

        return [.. frame];
    }
}
