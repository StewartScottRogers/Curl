namespace Curl.Conformance.Extended.WebTransport.Server;

/// <summary>
/// Bytes that arrived on one stream, read from the front: QUIC variable-length integers (RFC 9000
/// section 16) and type-length-value items (HTTP/3 frames and capsules). A read that needs bytes
/// not yet here takes nothing.
/// </summary>
internal sealed class WebTransportServerBuffer
{
    private readonly List<byte> bytes = [];

    private int position;

    internal int Position => position;

    internal static void WriteVariableLengthInteger(List<byte> output, long value)
    {
        (int length, int prefix) = value switch
        {
            < 1L << 6 => (1, 0),
            < 1L << 14 => (2, 1),
            < 1L << 30 => (4, 2),
            _ => (8, 3),
        };
        output.Add((byte)((value >> (8 * (length - 1))) | ((long)prefix << 6)));
        for (int index = length - 2; index >= 0; index--)
        {
            output.Add((byte)(value >> (8 * index)));
        }
    }

    internal void Append(ReadOnlySpan<byte> arrived) => bytes.AddRange(arrived);

    internal bool TryReadVariableLengthInteger(out long value)
    {
        value = 0;
        if (position >= bytes.Count || position + (1 << (bytes[position] >> 6)) > bytes.Count)
        {
            return false;
        }

        int length = 1 << (bytes[position] >> 6);
        value = bytes[position] & 0x3f;
        for (var index = 1; index < length; index++)
        {
            value = (value << 8) | bytes[position + index];
        }

        position += length;
        return true;
    }

    internal bool TryReadTypeLengthValue(out long type, out byte[] value)
    {
        int mark = position;
        value = [];
        if (TryReadVariableLengthInteger(out type) && TryReadVariableLengthInteger(out long length) && position + length <= bytes.Count)
        {
            value = [.. bytes.GetRange(position, (int)length)];
            position += (int)length;
            return true;
        }

        position = mark;
        return false;
    }

    internal void Rewind(int mark) => position = mark;

    internal byte[] TakeRest()
    {
        byte[] rest = [.. bytes.GetRange(position, bytes.Count - position)];
        position = bytes.Count;
        return rest;
    }
}
