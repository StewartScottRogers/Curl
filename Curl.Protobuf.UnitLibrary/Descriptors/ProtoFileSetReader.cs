using System.Diagnostics.CodeAnalysis;

namespace Curl.Protobuf.Descriptors;

/// <summary>Reads a <c>.proto</c> file and, through an <see cref="IProtoFileReader" />, every file it imports.</summary>
public static class ProtoFileSetReader
{
    /// <summary>
    /// Reads the file at the path given and every file it imports into a <see cref="ProtoFileSet" />
    /// whose named types all resolve; false, with the error and where it is, when a file is
    /// missing or malformed, an import forms a cycle, or a type does not resolve. Never throws on bad input.
    /// </summary>
    public static bool TryRead(
        string path,
        IProtoFileReader reader,
        [NotNullWhen(true)] out ProtoFileSet? files,
        [NotNullWhen(false)] out ProtoFileError? error)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(reader);
        try
        {
            files = new ProtoFileSetBuilder(reader).Build(path);
            error = null;
            return true;
        }
        catch (ProtoFileException exception)
        {
            files = null;
            error = exception.Error;
            return false;
        }
    }
}
