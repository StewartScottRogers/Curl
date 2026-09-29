using System.Buffers.Binary;

namespace Curl.Zstandard;

/// <summary>
/// A Zstandard frame header (RFC 8878 section 3.1.1.1): the <c>Frame_Header_Descriptor</c>
/// byte and the fields it says follow it - <c>Window_Descriptor</c>, <c>Dictionary_ID</c>
/// and <c>Frame_Content_Size</c>.
/// </summary>
internal readonly record struct ZstandardFrameHeader(ulong WindowSize, uint DictionaryId, ulong? ContentSize, bool HasChecksum)
{
    private const int SingleSegmentBit = 0x20;

    private const int ReservedBit = 0x08;

    private const int ChecksumBit = 0x04;

    private static ReadOnlySpan<byte> DictionaryIdFieldSizes => [0, 1, 2, 4];

    private static ReadOnlySpan<byte> ContentSizeFieldSizes => [0, 2, 4, 8];

    /// <summary>Returns whether <paramref name="descriptor" /> sets the bit RFC 8878 reserves, which must be 0.</summary>
    public static bool HasReservedBit(byte descriptor) => (descriptor & ReservedBit) != 0;

    /// <summary>Returns how many header bytes follow <paramref name="descriptor" />.</summary>
    public static int FieldsLength(byte descriptor) =>
        WindowDescriptorSize(descriptor) + DictionaryIdFieldSizes[descriptor & 3] + ContentSizeFieldSize(descriptor);

    /// <summary>Reads the header from <paramref name="descriptor" /> and the <see cref="FieldsLength" /> bytes of <paramref name="fields" />.</summary>
    public static ZstandardFrameHeader Read(byte descriptor, ReadOnlySpan<byte> fields)
    {
        var windowDescriptorSize = WindowDescriptorSize(descriptor);
        var dictionaryIdSize = DictionaryIdFieldSizes[descriptor & 3];
        var dictionaryId = (uint)ReadLittleEndian(fields.Slice(windowDescriptorSize, dictionaryIdSize));
        var contentSize = ReadContentSize(fields[(windowDescriptorSize + dictionaryIdSize)..]);
        var windowSize = windowDescriptorSize == 0 ? contentSize.GetValueOrDefault() : WindowSizeOf(fields[0]);
        return new ZstandardFrameHeader(windowSize, dictionaryId, contentSize, (descriptor & ChecksumBit) != 0);
    }

    private static int WindowDescriptorSize(byte descriptor) => (descriptor & SingleSegmentBit) != 0 ? 0 : 1;

    private static int ContentSizeFieldSize(byte descriptor)
    {
        var flag = descriptor >> 6;
        return flag == 0 && (descriptor & SingleSegmentBit) != 0 ? 1 : ContentSizeFieldSizes[flag];
    }

    private static ulong? ReadContentSize(ReadOnlySpan<byte> field) => field.Length switch
    {
        0 => null,
        2 => BinaryPrimitives.ReadUInt16LittleEndian(field) + 256UL,
        _ => ReadLittleEndian(field),
    };

    /// <summary>RFC 8878 section 3.1.1.1.2: <c>Exponent</c> in the top five bits, <c>Mantissa</c> in the low three.</summary>
    private static ulong WindowSizeOf(byte windowDescriptor)
    {
        var windowBase = 1UL << (10 + (windowDescriptor >> 3));
        return windowBase + ((windowBase / 8) * (ulong)(windowDescriptor & 7));
    }

    private static ulong ReadLittleEndian(ReadOnlySpan<byte> field)
    {
        ulong value = 0;
        for (var index = field.Length - 1; index >= 0; index--)
        {
            value = (value << 8) | field[index];
        }

        return value;
    }
}
