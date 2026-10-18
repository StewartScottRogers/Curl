using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary><c>lib5000.c</c>: <see cref="LibHttpSignatureRequest"/> as a plain GET with the default covered components.</summary>
public sealed class Lib5000() : LibHttpSignatureRequest(5000)
{
    /// <inheritdoc/>
    protected override IEnumerable<(CurlOption Option, object? Value)> ProgramOptions() => [];
}
