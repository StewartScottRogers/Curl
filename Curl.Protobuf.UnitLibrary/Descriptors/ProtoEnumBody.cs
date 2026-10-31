namespace Curl.Protobuf.Descriptors;

/// <summary>What <see cref="ProtoEnumParser" /> has read of an enum so far.</summary>
internal sealed class ProtoEnumBody : ProtoReservedBody
{
    /// <summary>The enum's values.</summary>
    public List<ProtoEnumValueDescriptor> Values { get; } = [];

    /// <summary>The number token of the first value that repeats an earlier value's number, or null.</summary>
    public ProtoToken? FirstAlias { get; set; }
}
