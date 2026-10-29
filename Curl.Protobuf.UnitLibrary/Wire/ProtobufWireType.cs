namespace Curl.Protobuf.Wire;

/// <summary>The wire type in the low three bits of a Protocol Buffers field tag.</summary>
public enum ProtobufWireType
{
    /// <summary>A base-128 varint (int32, int64, uint32, uint64, sint32, sint64, bool, enum).</summary>
    Varint = 0,

    /// <summary>Eight little-endian bytes (fixed64, sfixed64, double).</summary>
    Fixed64 = 1,

    /// <summary>A varint length then that many bytes (string, bytes, message, packed repeated).</summary>
    LengthDelimited = 2,

    /// <summary>The start of a group, closed by an <see cref="EndGroup" /> tag of the same field number.</summary>
    StartGroup = 3,

    /// <summary>The end of the group opened by a <see cref="StartGroup" /> tag of the same field number.</summary>
    EndGroup = 4,

    /// <summary>Four little-endian bytes (fixed32, sfixed32, float).</summary>
    Fixed32 = 5,
}
