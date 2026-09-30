using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <summary>
/// The <c>-v</c> line curl 8.21.0 writes when a Negotiate context makes no token (ADR-0231):
/// its SSPI build's <c>InitializeSecurityContext failed: ...</c> on Windows, its GSS-API
/// build's <c>gss_init_sec_context() failed: ...</c> elsewhere, each naming what the step
/// came to.
/// </summary>
internal static class NegotiateFailureLines
{
    /// <summary>What curl's SSPI build writes before the SSPI status's name.</summary>
    internal const string SspiPrefix = "InitializeSecurityContext failed: ";

    /// <summary>What curl's GSS-API build writes before the GSS-API status's messages.</summary>
    internal const string GssApiPrefix = "gss_init_sec_context() failed: ";

    /// <summary>Gets the line for a step that came to <paramref name="status" />.</summary>
    /// <param name="status">What the step came to: a failure, not <see cref="SecurityContextStatus.ContinueNeeded" /> or <see cref="SecurityContextStatus.Completed" />.</param>
    /// <param name="wordsAsSspi"><see langword="true" /> for the SSPI build's wording, <see langword="false" /> for the GSS-API build's.</param>
    /// <returns>The line, without the <c>* </c> prefix.</returns>
    internal static string For(SecurityContextStatus status, bool wordsAsSspi) =>
        wordsAsSspi ? SspiPrefix + SspiStatusOf(status) : GssApiPrefix + GssApiMessagesOf(status);

    /// <summary>
    /// Gets the SSPI status as curl's <c>Curl_sspi_strerror</c> words it: its name, its code
    /// and Windows' message for it without the final full stop. <c>SEC_E_NO_CREDENTIALS</c> is
    /// measured (ADR-0176); the others are Windows' messages for the status each failure maps to.
    /// </summary>
    private static string SspiStatusOf(SecurityContextStatus status) => status switch
    {
        SecurityContextStatus.NoCredentials => "SEC_E_NO_CREDENTIALS (0x8009030e) - No credentials are available in the security package",
        SecurityContextStatus.NoMechanism => "SEC_E_SECPKG_NOT_FOUND (0x80090305) - The requested security package does not exist",
        SecurityContextStatus.Refused => "SEC_E_LOGON_DENIED (0x8009030c) - The logon attempt failed",
        _ => "SEC_E_INVALID_TOKEN (0x80090308) - The token supplied to the function is invalid",
    };

    /// <summary>
    /// Gets the GSS-API major and minor messages as curl's <c>Curl_gss_log_error</c> words them,
    /// each followed by <c>. </c>. The no-credentials pair is measured from MIT Kerberos with
    /// curl 8.18.0 (ADR-0176); the others are MIT's major messages for the status each
    /// failure maps to.
    /// </summary>
    private static string GssApiMessagesOf(SecurityContextStatus status) => status switch
    {
        SecurityContextStatus.NoCredentials => "No credentials were supplied, or the credentials were unavailable or inaccessible. SPNEGO cannot find mechanisms to negotiate. ",
        SecurityContextStatus.NoMechanism => "An unsupported mechanism was requested. ",
        SecurityContextStatus.Refused => "Unspecified GSS failure.  Minor code may provide more information. ",
        _ => "Invalid token was supplied. ",
    };
}
