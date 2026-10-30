namespace Curl.Protobuf.Descriptors;

/// <summary>A <c>.proto</c> file and every file it imports, with each named type reference resolved.</summary>
public sealed class ProtoFileSet
{
    private readonly IReadOnlyDictionary<ProtoTypeReference, ProtoNamedType> resolved;

    internal ProtoFileSet(
        IReadOnlyList<ProtoFile> files,
        IReadOnlyDictionary<string, ProtoNamedType> types,
        IReadOnlyDictionary<ProtoTypeReference, ProtoNamedType> resolved)
    {
        Files = files;
        Types = types;
        this.resolved = resolved;
    }

    /// <summary>The file named to read, the last of <see cref="Files" />.</summary>
    public ProtoFile Root => Files[^1];

    /// <summary>Every file read, each after the files it imports.</summary>
    public IReadOnlyList<ProtoFile> Files { get; }

    /// <summary>Every message and enum of every file, by fully qualified name with a leading '.'.</summary>
    public IReadOnlyDictionary<string, ProtoNamedType> Types { get; }

    /// <summary>
    /// The message or enum a named type reference of one of <see cref="Files" /> resolved to
    /// (a field's type or map value, a method's request or response); null for any other reference.
    /// </summary>
    public ProtoNamedType? FindType(ProtoTypeReference reference) => resolved.GetValueOrDefault(reference);
}
