namespace Curl.Protobuf.Descriptors;

/// <summary>What <see cref="ProtoMessageParser" /> has read of a message so far.</summary>
internal sealed class ProtoMessageBody : ProtoReservedBody
{
    /// <summary>The message's fields, a oneof's among them.</summary>
    public List<ProtoFieldDescriptor> Fields { get; } = [];

    /// <summary>The message's oneofs.</summary>
    public List<ProtoOneofDescriptor> Oneofs { get; } = [];

    /// <summary>The messages nested in it.</summary>
    public List<ProtoMessageDescriptor> Messages { get; } = [];

    /// <summary>The enums nested in it.</summary>
    public List<ProtoEnumDescriptor> Enums { get; } = [];
}
