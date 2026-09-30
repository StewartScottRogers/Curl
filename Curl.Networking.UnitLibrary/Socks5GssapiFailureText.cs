using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// The exit 97 messages of SOCKS5 GSS-API authentication (RFC 1961) in the words of the
/// platform's curl: its SSPI build on Windows, its GSS-API build with MIT Kerberos elsewhere
/// (BL-615, ADR-0276). The context-failure texts for a missing credential were measured; the
/// rest follow curl's <c>socks_sspi.c</c> and <c>socks_gssapi.c</c>.
/// </summary>
/// <param name="usesSspi">Whether to word them as the SSPI build does.</param>
/// <param name="credentialCacheName">The cache MIT names when it has no credential.</param>
internal sealed class Socks5GssapiFailureText(bool usesSspi, string credentialCacheName)
{
    /// <summary>The SSPI build's text when there is no ticket for the proxy (measured).</summary>
    public const string SspiTargetUnknown =
        "SSPI error: InitializeSecurityContext failed: SEC_E_TARGET_UNKNOWN (0x80090303) - The specified target is unknown or unreachable";

    private readonly string _api = usesSspi ? "SSPI" : "GSS-API";

    /// <summary>Gets the text when the proxy's answer to a token cannot be read in full.</summary>
    public string AuthenticationResponseLost => $"Failed to receive {_api} authentication response.";

    /// <summary>Gets the text when the proxy's token cannot be read in full.</summary>
    public string AuthenticationTokenLost => $"Failed to receive {_api} authentication token.";

    /// <summary>Gets the text when the answer to the protection level cannot be read in full.</summary>
    public string EncryptionResponseLost => $"Failed to receive {_api} encryption response.";

    /// <summary>Gets the text when the proxy's protection level cannot be read in full.</summary>
    public string EncryptionTypeLost => $"Failed to receive {_api} encryption type.";

    /// <summary>Gets the text when the wrapped protection level cannot be made.</summary>
    public string WrapFailed => usesSspi
        ? "SSPI error: EncryptMessage failed: SEC_E_INTERNAL_ERROR (0x80090304) - The Local Security Authority cannot be contacted"
        : "GSS-API error: gss_wrap failed: Unspecified GSS failure.  Minor code may provide more information.";

    /// <summary>Gets the text when the proxy's wrapped protection level does not verify.</summary>
    public string UnwrapFailed => usesSspi
        ? "SSPI error: DecryptMessage failed: SEC_E_MESSAGE_ALTERED (0x8009030f) - The message or signature supplied for verification has been altered"
        : "GSS-API error: gss_unwrap failed: A token had an invalid Message Integrity Check (MIC).";

    /// <summary>The text when the proxy grants a protection level curl does not apply.</summary>
    public static string ProtectionNotImplemented => "SOCKS5 GSS-API protection not yet implemented.";

    /// <summary>The text when the proxy answers with its rejection message type.</summary>
    /// <param name="reply">The answer's first two bytes.</param>
    /// <returns>The text.</returns>
    public static string Rejected(byte[] reply) => $"User was rejected by the SOCKS5 server ({reply[0]} {reply[1]}).";

    /// <summary>Gets the text when the answer to a token is not an authentication message.</summary>
    /// <param name="reply">The answer's first two bytes.</param>
    /// <returns>The text.</returns>
    public string InvalidAuthenticationResponse(byte[] reply) => $"Invalid {_api} authentication response type ({reply[0]} {reply[1]}).";

    /// <summary>Gets the text when the answer to the protection level is not a protection message.</summary>
    /// <param name="reply">The answer's first two bytes.</param>
    /// <returns>The text.</returns>
    public string InvalidEncryptionResponse(byte[] reply) => $"Invalid {_api} encryption response type ({reply[0]} {reply[1]}).";

    /// <summary>Gets the text when the proxy's protection level is not one byte.</summary>
    /// <param name="length">Its length.</param>
    /// <returns>The text.</returns>
    public string InvalidEncryptionLength(int length) => $"Invalid {_api} encryption response length ({length}).";

    /// <summary>Gets the text when a context step fails with <paramref name="status" />.</summary>
    /// <param name="status">The failed step's status.</param>
    /// <returns>The text.</returns>
    public string ContextFailed(SecurityContextStatus status) => usesSspi
        ? SspiContextFailurePrefix + SspiStatusTexts.GetValueOrDefault(status, SspiTargetUnknown[SspiContextFailurePrefix.Length..])
        : "GSS-API error: gss_init_sec_context failed: " + GssStatusTexts.GetValueOrDefault(status, GssUnspecifiedFailure)
            .Replace(CredentialCachePlaceholder, credentialCacheName, StringComparison.Ordinal);

    private const string SspiContextFailurePrefix = "SSPI error: InitializeSecurityContext failed: ";

    private const string GssUnspecifiedFailure = "Unspecified GSS failure.  Minor code may provide more information.";

    private const string CredentialCachePlaceholder = "{cache}";

    // SSPI's Kerberos gives SEC_E_TARGET_UNKNOWN for a proxy it holds no ticket for (measured),
    // which the BCL reports as no credential or as a refusal: the default.
    private static readonly Dictionary<SecurityContextStatus, string> SspiStatusTexts = new()
    {
        [SecurityContextStatus.NoMechanism] = "SEC_E_SECPKG_NOT_FOUND (0x80090305) - The requested security package does not exist",
        [SecurityContextStatus.MalformedToken] = "SEC_E_INVALID_TOKEN (0x80090308) - The token supplied to the function is invalid",
    };

    // MIT's major status text, and for a missing credential its minor text after a line feed (measured).
    private static readonly Dictionary<SecurityContextStatus, string> GssStatusTexts = new()
    {
        [SecurityContextStatus.NoCredentials] =
            "No credentials were supplied, or the credentials were unavailable or inaccessible.\nNo Kerberos credentials available (default cache: " + CredentialCachePlaceholder + ")",
        [SecurityContextStatus.NoMechanism] = "An unsupported mechanism was requested.",
        [SecurityContextStatus.MalformedToken] = "Invalid token was supplied.",
    };
}
