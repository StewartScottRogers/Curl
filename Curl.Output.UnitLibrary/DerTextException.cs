using Curl.Api.Codes;

namespace Curl.Output;

/// <summary>
/// Thrown by <see cref="DerText" /> for a value curl cannot print, carrying the
/// <see cref="CurlCode" /> curl 8.22.0's <c>lib/vtls/x509asn1.c</c> returns for it:
/// <see cref="CurlCode.WeirdServerReply" /> for a character past what UTF-8 encodes,
/// <see cref="CurlCode.BadFunctionArgument" /> for anything else.
/// </summary>
/// <param name="code">The code curl returns.</param>
internal sealed class DerTextException(CurlCode code)
    : FormatException("The certificate holds a value curl cannot print.")
{
    /// <summary>Gets the code curl returns.</summary>
    internal CurlCode Code { get; } = code;
}
