namespace Curl.Protobuf.Descriptors;

/// <summary>
/// Carries a <see cref="ProtoSyntaxError" /> from deep in the tokenizer or parser up to
/// <c>ProtoParser.TryParse</c>, which catches it: it never escapes the library.
/// </summary>
internal sealed class ProtoSyntaxException(ProtoSyntaxError error) : Exception(error.Message)
{
    /// <summary>The error to report.</summary>
    public ProtoSyntaxError Error { get; } = error;
}
