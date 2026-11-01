namespace Curl.Extended.Protocol.Amqp1.Types;

/// <summary>An AMQP symbol (section 1.6.21): ASCII text from a constrained domain, distinct from a string.</summary>
internal readonly record struct Amqp1Symbol(string Value);

/// <summary>An AMQP timestamp (section 1.6.17): signed milliseconds since the Unix epoch.</summary>
internal readonly record struct Amqp1Timestamp(long MillisecondsSinceUnixEpoch);

/// <summary>
/// An AMQP described type (section 1.3): a descriptor, in practice a <see cref="Amqp1Symbol"/> or a numeric
/// <see cref="ulong"/>, and the value it describes.
/// </summary>
internal sealed record Amqp1Described(object? Descriptor, object? Value);

/// <summary>An AMQP map (section 1.6.23): its key and value pairs in wire order.</summary>
internal sealed record Amqp1Map(IReadOnlyList<KeyValuePair<object?, object?>> Entries);

/// <summary>
/// An AMQP array (sections 1.6.24 and 1.6.25): elements that share one element constructor, the format code
/// <paramref name="ElementFormatCode"/>, described by <paramref name="ElementDescriptor"/> when it is not null.
/// </summary>
internal sealed record Amqp1Array(byte ElementFormatCode, IReadOnlyList<object?> Elements, object? ElementDescriptor = null);

/// <summary>
/// An AMQP decimal32, decimal64 or decimal128 (sections 1.6.13 to 1.6.15), kept as its raw IEEE 754 decimal bytes.
/// </summary>
internal sealed class Amqp1Decimal
{
    /// <summary>Wraps 4, 8 or 16 raw bytes.</summary>
    public Amqp1Decimal(byte[] bytes)
    {
        FormatCode = bytes.Length switch
        {
            4 => Amqp1FormatCode.Decimal32,
            8 => Amqp1FormatCode.Decimal64,
            16 => Amqp1FormatCode.Decimal128,
            _ => throw new Amqp1MalformedEncodingException($"An AMQP decimal is 4, 8 or 16 bytes, not {bytes.Length}."),
        };
        Bytes = bytes;
    }

    /// <summary>The raw bytes, in wire order.</summary>
    public byte[] Bytes { get; }

    /// <summary>The format code for the width: decimal32, decimal64 or decimal128.</summary>
    public byte FormatCode { get; }
}
