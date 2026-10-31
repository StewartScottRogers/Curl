namespace Curl.Protobuf.Descriptors;

/// <summary>
/// Carries a <see cref="ProtoFileError" /> up to <c>ProtoFileSetReader.TryRead</c>, which
/// catches it: it never escapes the library.
/// </summary>
internal sealed class ProtoFileException(ProtoFileError error) : Exception(error.Message)
{
    /// <summary>The error to report.</summary>
    public ProtoFileError Error { get; } = error;
}
