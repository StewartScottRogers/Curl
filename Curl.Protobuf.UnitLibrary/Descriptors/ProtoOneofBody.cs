namespace Curl.Protobuf.Descriptors;

/// <summary>A oneof being read: the message its fields go into, its name and its options.</summary>
internal sealed class ProtoOneofBody(ProtoMessageBody message, string name)
{
    /// <summary>The message the oneof is in.</summary>
    public ProtoMessageBody Message { get; } = message;

    /// <summary>The oneof's name.</summary>
    public string Name { get; } = name;

    /// <summary>The oneof's options.</summary>
    public List<ProtoOption> Options { get; } = [];
}
