using System.Buffers.Binary;

namespace Curl.Zstandard;

/// <summary>
/// Hand-assembles RFC 8878 frames for the decoder tests: a frame header, raw, RLE and
/// other blocks, a content checksum, and skippable frames.
/// </summary>
internal static class ZstandardTestFrames
{
    public const int RawBlockType = 0;

    public const int RleBlockType = 1;

    public const int CompressedBlockType = 2;

    public const int ReservedBlockType = 3;

    /// <summary>A <c>Window_Descriptor</c> of 0: a 1 KiB window.</summary>
    public const byte OneKibibyteWindow = 0x00;

    private static ReadOnlySpan<byte> ZstandardMagic => [0x28, 0xB5, 0x2F, 0xFD];

    /// <summary>The magic and a <c>Frame_Header_Descriptor</c>, followed by <paramref name="fields" />.</summary>
    public static byte[] FrameHeader(byte descriptor, params byte[] fields) => [.. ZstandardMagic, descriptor, .. fields];

    /// <summary>A three-byte block header.</summary>
    public static byte[] BlockHeader(bool last, int blockType, int blockSize)
    {
        var value = (blockSize << 3) | (blockType << 1) | (last ? 1 : 0);
        return [(byte)value, (byte)(value >> 8), (byte)(value >> 16)];
    }

    public static byte[] RawBlock(bool last, byte[] content) => [.. BlockHeader(last, RawBlockType, content.Length), .. content];

    public static byte[] RleBlock(bool last, byte value, int size) => [.. BlockHeader(last, RleBlockType, size), value];

    /// <summary>The <c>Content_Checksum</c> of <paramref name="content" />: the low 32 bits of its XXH64, little-endian.</summary>
    public static byte[] Checksum(byte[] content)
    {
        var checksum = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(checksum, (uint)XxHash64.Hash(content));
        return checksum;
    }

    /// <summary>A skippable frame with magic <c>0x184D2A5<paramref name="nibble" /></c> wrapping <paramref name="content" />.</summary>
    public static byte[] SkippableFrame(int nibble, byte[] content)
    {
        var header = new byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(header, 0x184D2A50u | (uint)nibble);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4), (uint)content.Length);
        return [.. header, .. content];
    }

    public static byte[] Concatenate(params byte[][] parts) => [.. parts.SelectMany(part => part)];
}
