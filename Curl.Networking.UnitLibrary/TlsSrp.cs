using Curl.Protocol.Abstractions;
using Curl.Tls;

namespace Curl.Networking;

/// <summary>
/// What <c>--tlsuser</c> and <c>--tlspassword</c> turn on in the hand-built client, as curl's
/// OpenSSL build does it (<c>vtls/openssl.c</c>, ADR-0328): the TLS-SRP login, the verbose lines
/// that announce it, and OpenSSL's <c>SRP</c> cipher list in place of the TLS 1.2 suites unless
/// <c>--ciphers</c> names its own.
/// </summary>
public static class TlsSrp
{
    /// <summary>
    /// OpenSSL 3.5's <c>SRP</c> cipher list at curl's default security level, in its order
    /// (<c>openssl ciphers -stdname SRP</c>, measured, BL-712): the AES-256 suites, then the
    /// AES-128 ones, each DSS, RSA, then plain SRP. 3DES falls below the security level.
    /// </summary>
    public static IReadOnlyList<ushort> SrpCipherList { get; } = [0xc022, 0xc021, 0xc020, 0xc01f, 0xc01e, 0xc01d];

    /// <summary>The message for exit 43 when <c>--tlsuser</c> comes without <c>--tlspassword</c> (measured, curl 8.18.0's OpenSSL build).</summary>
    public const string PasswordMissing = "failed setting SRP password";

    /// <summary>The TLS-SRP login <paramref name="options" /> asks for, or <see langword="null" /> without <c>--tlsuser</c> or <c>--tlspassword</c>.</summary>
    /// <param name="options">The connection's TLS options.</param>
    /// <returns>The user name and password, or <see langword="null" />.</returns>
    public static TlsSrpCredentials? CredentialsOf(TlsClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.TlsUser is null || options.TlsPassword is null ? null : new(options.TlsUser, options.TlsPassword);
    }

    /// <summary>Reports the line curl's OpenSSL build prints first in an SRP handshake: the user name.</summary>
    /// <param name="events">Where the line goes.</param>
    /// <param name="options">The connection's TLS options, with <c>--tlsuser</c> given.</param>
    public static void ReportUser(ITransferEvents events, TlsClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(options);
        events.ReportInfo($"Using TLS-SRP username: {options.TlsUser}");
    }

    /// <summary>
    /// Reports the line curl's OpenSSL build prints once the password is set, when <c>--ciphers</c>
    /// is not given: the <c>SRP</c> cipher list taking the place of the default one.
    /// </summary>
    /// <param name="events">Where the line goes.</param>
    /// <param name="options">The connection's TLS options.</param>
    public static void ReportCipherList(ITransferEvents events, TlsClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(options);
        if (options.Ciphers is null)
        {
            events.ReportInfo("Setting cipher list SRP");
        }
    }

    /// <summary>
    /// The suites an SRP ClientHello offers: <paramref name="suites" /> as they are when
    /// <c>--ciphers</c> is given, otherwise their TLS 1.3 suites followed by <see cref="SrpCipherList" />,
    /// since OpenSSL's cipher list governs only TLS 1.2 and below.
    /// </summary>
    /// <param name="options">The connection's TLS options.</param>
    /// <param name="suites">The suites the hello would otherwise offer.</param>
    /// <returns>The suites to offer.</returns>
    public static IReadOnlyList<ushort> OfferedSuites(TlsClientOptions options, IReadOnlyList<ushort> suites)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(suites);
        return options.Ciphers is not null ? suites : [.. suites.Where(Tls13RecordProtection.CanProtect), .. SrpCipherList];
    }
}
