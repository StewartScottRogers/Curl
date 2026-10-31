namespace Curl.Protobuf.Descriptors;

/// <summary>What <see cref="ProtoServiceParser" /> has read of a service so far.</summary>
internal sealed class ProtoServiceBody
{
    /// <summary>The service's methods.</summary>
    public List<ProtoMethodDescriptor> Methods { get; } = [];

    /// <summary>The service's options.</summary>
    public List<ProtoOption> Options { get; } = [];
}
