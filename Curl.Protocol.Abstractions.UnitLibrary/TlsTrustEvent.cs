namespace Curl.Protocol.Abstractions;

/// <summary>
/// The trust a TLS connection is set up with before its handshake, reported through
/// <see cref="ITransferEvents.ReportTlsTrust" /> (ADR-0085).
/// </summary>
/// <remarks>
/// curl's OpenSSL build words it as <c>SSL Trust: peer verification disabled</c>, or
/// <c>SSL Trust Anchors:</c> and one line per source of trust anchors.
/// </remarks>
public sealed record TlsTrustEvent
{
    /// <summary>
    /// Gets a value indicating whether the peer's certificate is verified; <see langword="false" />
    /// with <c>-k</c> or <c>--proxy-insecure</c>.
    /// </summary>
    public required bool VerifiesPeer { get; init; }

    /// <summary>
    /// Gets a value indicating whether trust anchors come from a certificate blob given in
    /// memory (<c>CURLOPT_CAINFO_BLOB</c>), which overrides <see cref="CaCertificateFile" />.
    /// </summary>
    public bool HasCaCertificateBlob { get; init; }

    /// <summary>
    /// Gets the file trust anchors are loaded from (<c>--cacert</c>, or the build's default
    /// bundle), or <see langword="null" /> when there is none.
    /// </summary>
    public string? CaCertificateFile { get; init; }

    /// <summary>
    /// Gets the directory trust anchors are loaded from (<c>--capath</c>), or
    /// <see langword="null" /> when there is none.
    /// </summary>
    public string? CaCertificateDirectory { get; init; }

    /// <summary>
    /// Gets a value indicating whether trust anchors come from Windows' ROOT and CA system
    /// stores, which curl's builds name as <c>Native: Windows System Stores ROOT+CA</c>.
    /// </summary>
    public bool UsesWindowsSystemStores { get; init; }

    /// <summary>
    /// Gets a value indicating whether the trust is a QUIC connect's (HTTP/3), which on
    /// Windows curl.se's LibreSSL build words rather than the Schannel build (ADR-0144).
    /// </summary>
    public bool IsQuic { get; init; }
}
