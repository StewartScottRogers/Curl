namespace Curl.Protocol.Ssh.KeyExchange;

/// <summary>
/// The prime sizes, in bits, <c>diffie-hellman-group-exchange-*</c> asks for in its
/// <c>SSH_MSG_KEX_DH_GEX_REQUEST</c> and accepts in the server's group (RFC 4419): the
/// client refuses a prime below <paramref name="MinimumBits" /> or above
/// <paramref name="MaximumBits" />.
/// </summary>
/// <param name="MinimumBits">The smallest prime asked for and accepted.</param>
/// <param name="PreferredBits">The size asked for.</param>
/// <param name="MaximumBits">The largest prime asked for and accepted.</param>
internal sealed record SshGroupExchangeSizes(uint MinimumBits, uint PreferredBits, uint MaximumBits)
{
    /// <summary>
    /// Gets what curl 8.21.0's Windows build (libssh2 1.11.1 on WinCNG) asks for:
    /// (2048, 4096, 4096), measured 2026-09-29 (BL-564, ADR-0206).
    /// </summary>
    internal static SshGroupExchangeSizes WindowsReference { get; } = new(2048, 4096, 4096);

    /// <summary>
    /// Gets what curl 8.21.0's OpenSSL build (libssh2 1.11.1 on OpenSSL) asks for:
    /// (2048, 4096, 8192), measured 2026-09-29 (BL-888).
    /// </summary>
    internal static SshGroupExchangeSizes OpenSslReference { get; } = new(2048, 4096, 8192);
}
