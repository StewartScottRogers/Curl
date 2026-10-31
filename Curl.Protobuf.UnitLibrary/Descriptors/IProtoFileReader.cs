using System.Diagnostics.CodeAnalysis;

namespace Curl.Protobuf.Descriptors;

/// <summary>Reads <c>.proto</c> files by import path, so <see cref="ProtoFileSetReader" /> never touches the file system itself.</summary>
public interface IProtoFileReader
{
    /// <summary>Reads the text of the file at an import path; false when there is no such file.</summary>
    bool TryReadFile(string path, [NotNullWhen(true)] out string? text);
}
