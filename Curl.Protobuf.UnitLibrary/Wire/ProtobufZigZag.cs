namespace Curl.Protobuf.Wire;

/// <summary>
/// ZigZag encoding of the <c>sint32</c> and <c>sint64</c> types: maps signed integers to
/// unsigned ones so values of small magnitude, negative or positive, encode as short varints
/// (0 to 0, -1 to 1, 1 to 2, -2 to 3, and so on).
/// </summary>
public static class ProtobufZigZag
{
    /// <summary>Encodes a <c>sint32</c> value.</summary>
    public static uint Encode32(int value) => (uint)((value << 1) ^ (value >> 31));

    /// <summary>Decodes a <c>sint32</c> value.</summary>
    public static int Decode32(uint value) => (int)(value >> 1) ^ -(int)(value & 1);

    /// <summary>Encodes a <c>sint64</c> value.</summary>
    public static ulong Encode64(long value) => (ulong)((value << 1) ^ (value >> 63));

    /// <summary>Decodes a <c>sint64</c> value.</summary>
    public static long Decode64(ulong value) => (long)(value >> 1) ^ -(long)(value & 1);
}
