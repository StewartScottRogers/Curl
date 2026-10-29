namespace Curl.Protobuf.Wire;

/// <summary>
/// One field read from the wire whatever its type, kept so it can be written back unchanged:
/// how a message keeps the unknown fields it does not recognise.
/// </summary>
/// <param name="FieldNumber">The field number from the tag.</param>
/// <param name="WireType">The wire type from the tag; never <see cref="ProtobufWireType.EndGroup" />.</param>
/// <param name="Scalar">The value of a varint, fixed32 or fixed64 field; 0 otherwise.</param>
/// <param name="Payload">The bytes of a length-delimited field; empty otherwise.</param>
/// <param name="GroupFields">The fields inside a group; empty otherwise.</param>
public sealed record ProtobufField(
    int FieldNumber,
    ProtobufWireType WireType,
    ulong Scalar,
    ReadOnlyMemory<byte> Payload,
    IReadOnlyList<ProtobufField> GroupFields);
