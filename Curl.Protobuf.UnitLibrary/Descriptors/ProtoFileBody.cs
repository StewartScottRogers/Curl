namespace Curl.Protobuf.Descriptors;

/// <summary>What <see cref="ProtoFileParser" /> has read of a file so far.</summary>
internal sealed class ProtoFileBody
{
    /// <summary>The file's package, null until its <c>package</c> statement.</summary>
    public string? Package { get; set; }

    /// <summary>The file's imports.</summary>
    public List<ProtoImportDescriptor> Imports { get; } = [];

    /// <summary>The file's services.</summary>
    public List<ProtoServiceDescriptor> Services { get; } = [];

    /// <summary>The file's options.</summary>
    public List<ProtoOption> Options { get; } = [];

    /// <summary>The file's top-level messages.</summary>
    public List<ProtoMessageDescriptor> Messages { get; } = [];

    /// <summary>The file's top-level enums.</summary>
    public List<ProtoEnumDescriptor> Enums { get; } = [];
}
