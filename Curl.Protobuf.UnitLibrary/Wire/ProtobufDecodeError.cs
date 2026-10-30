namespace Curl.Protobuf.Wire;

/// <summary>Why a <see cref="ProtobufReader" /> could not read the next value from its input.</summary>
public enum ProtobufDecodeError
{
    /// <summary>No error: the value was read.</summary>
    None,

    /// <summary>The input ends inside a varint (its last byte still has the continuation bit set).</summary>
    TruncatedVarint,

    /// <summary>A varint's tenth byte still has the continuation bit set: no varint is longer than 10 bytes.</summary>
    VarintTooLong,

    /// <summary>The input ends inside a fixed32 or fixed64 value.</summary>
    TruncatedFixed,

    /// <summary>A length-delimited field's length runs past the end of the input.</summary>
    LengthPastEnd,

    /// <summary>A tag carries wire type 6 or 7, which the encoding does not define.</summary>
    InvalidWireType,

    /// <summary>A tag carries field number 0 or one above 536,870,911 (2^29 - 1).</summary>
    InvalidFieldNumber,

    /// <summary>An end-group tag closes no open group, or closes a group of another field number.</summary>
    UnmatchedEndGroup,

    /// <summary>The input ends inside a group that no end-group tag closed.</summary>
    UnterminatedGroup,

    /// <summary>Groups or messages are nested deeper than <see cref="ProtobufReader.MaximumDepth" />.</summary>
    NestingTooDeep,

    /// <summary>A <c>string</c> field's bytes are not valid UTF-8, which proto3 requires.</summary>
    InvalidUtf8,
}
