using System.Diagnostics.CodeAnalysis;
using Curl.Protobuf.Descriptors;

namespace Curl.Extension.Protocol.Grpc.Client;

/// <summary>Reads <c>.proto</c> files by import path from one directory: the directory of the <c>--grpc-proto</c> file (ADR-1147).</summary>
/// <param name="directory">The directory import paths are relative to.</param>
internal sealed class ProtoFileDirectoryReader(string directory) : IProtoFileReader
{
    /// <inheritdoc />
    public bool TryReadFile(string path, [NotNullWhen(true)] out string? text)
    {
        string fullPath = Path.Combine(directory, path);
        if (!File.Exists(fullPath))
        {
            text = null;
            return false;
        }

        text = File.ReadAllText(fullPath);
        return true;
    }
}
