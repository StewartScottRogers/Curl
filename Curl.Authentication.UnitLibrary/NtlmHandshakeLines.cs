using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <summary>
/// The <c>-v</c> lines curl writes when its NTLM handshake goes wrong, from <c>lib/http_ntlm.c</c>'s
/// <c>Curl_input_ntlm</c> and <c>lib/http.c</c>, measured with curl 8.21.0 (Schannel, SSPI) on
/// Windows and curl 8.18.0 (OpenSSL, curl's own NTLM) on Ubuntu (BL-848 Notes). Each is written
/// without the <c>* </c> prefix.
/// </summary>
internal static class NtlmHandshakeLines
{
    /// <summary>A bare <c>NTLM</c> challenge to a request that sent Type 3.</summary>
    internal const string Rejected = "NTLM handshake rejected";

    /// <summary>A bare <c>NTLM</c> challenge to a Type 1 that answered a challenge.</summary>
    internal const string InternalError = "NTLM handshake failure (internal error)";

    /// <summary>A Type 2 message curl's own NTLM cannot read.</summary>
    internal const string BadType2 = "NTLM handshake failure (bad type-2 message)";

    /// <summary>
    /// What curl's own NTLM writes before <see cref="BadType2" /> for a Type 2 message whose
    /// target information lies past its end or starts inside its 48-byte header, from curl
    /// 8.21.0's <c>lib/vauth/ntlm.c</c> <c>ntlm_decode_type2_target</c> (BL-1226). The SSPI
    /// build reads Type 2 in SSPI and never writes it.
    /// </summary>
    internal const string TargetInfoOutOfRange = "NTLM handshake failure (bad type-2 message). Target Info Offset Len is set incorrect by the peer";

    /// <summary>What curl writes after each challenge its NTLM refused to read, and alone for one that is not base64.</summary>
    internal const string ProblemIgnored = "NTLM authentication problem, ignoring.";

    /// <summary>
    /// Gets the line curl's SSPI build writes when SSPI makes no Type 3 message, just before the
    /// request it would have sent: the SSPI status in hexadecimal, and a line ending of its own,
    /// so an empty line follows (measured <c>SEC_E_INVALID_TOKEN</c>, BL-848 Notes). The other
    /// statuses are the SSPI codes <see cref="NegotiateFailureLines" /> names for each failure.
    /// </summary>
    /// <param name="status">What the step came to: a failure.</param>
    /// <returns>The line, without the <c>* </c> prefix.</returns>
    internal static string Type3Failure(SecurityContextStatus status) =>
        string.Create(CultureInfo.InvariantCulture, $"NTLM handshake failure (type-3 message): Status=0x{SspiCodeOf(status):x8}\n");

    /// <summary>Gets the SSPI status code a step's failure maps to.</summary>
    private static uint SspiCodeOf(SecurityContextStatus status) => status switch
    {
        SecurityContextStatus.NoCredentials => 0x8009030e,
        SecurityContextStatus.NoMechanism => 0x80090305,
        SecurityContextStatus.Refused => 0x8009030c,
        _ => 0x80090308,
    };
}
