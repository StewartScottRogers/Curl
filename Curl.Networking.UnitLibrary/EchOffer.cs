using System.Globalization;

using Curl.Protocol.Abstractions;
using Curl.Tls;

namespace Curl.Networking;

/// <summary>
/// What the hand-built ClientHello offers for <c>--ech</c> (ADR-0327, ADR-0359), as curl 8.21.0's
/// OpenSSL build sets it up in <c>ossl_init_ech</c>: GREASE under <c>grease</c>; otherwise the
/// <c>ecl:</c> list, or, without one, the host's list from DNS; a list that does not decode or
/// names no configuration the client can seal for is no ECH, and under <c>hard</c> exit 35. A
/// usable list on a range that does not reach TLS 1.3 is exit 35 too, as OpenSSL cannot raise
/// the minimum above the maximum.
/// </summary>
/// <param name="Configs">The configurations to offer, with <c>pn:</c>'s public name; <see langword="null" /> for none.</param>
/// <param name="SendGrease">Whether the hello carries a GREASE <c>encrypted_client_hello</c>.</param>
/// <param name="Failure">Why the transfer fails before the handshake, or <see langword="null" />.</param>
/// <param name="InfoLines">The <c>-v</c> <c>ECH:</c> lines curl writes while it sets the offer up, in order.</param>
internal sealed record EchOffer(EchConfigList? Configs, bool SendGrease, ConnectResult? Failure, IReadOnlyList<string> InfoLines)
{
    /// <summary>The message <c>hard</c> fails with: curl calls no <c>failf</c>, so the tool prints exit 35's text.</summary>
    internal const string NoUsableConfigMessage = "SSL connect error";

    /// <summary>
    /// The message a usable list fails with on a range below TLS 1.3: OpenSSL's error string for
    /// a handshake with no version left (measured 2026-10-02, curl 8.21.0 and OpenSSL 4.0.0).
    /// </summary>
    internal const string NoProtocolsAvailableMessage = "TLS connect error: error:0A0000BF:SSL routines::no protocols available";

    /// <summary>The line <c>grease</c> writes.</summary>
    internal const string GreaseLine = "ECH: will GREASE ClientHello";

    /// <summary>The line an <c>ecl:</c> list writes, usable or not, unless <c>hard</c> stops at a failed one.</summary>
    internal const string CommandLineListLine = "ECH: ECHConfig from command line";

    /// <summary>The line an <c>ecl:</c> list OpenSSL cannot load writes first.</summary>
    internal const string CommandLineListFailedLine = "ECH: SSL_ECH_set1_ech_config_list failed";

    /// <summary>The line a list from the host's HTTPS record writes (curl 8.21.0's source; not measured).</summary>
    internal const string DnsListLine = "ECH: ECHConfig from HTTPS RR";

    /// <summary>The line a list from the host's HTTPS record that OpenSSL cannot load writes (curl 8.21.0's source; not measured).</summary>
    internal const string DnsListFailedLine = "ECH: SSL_set1_ech_config_list failed";

    /// <summary>The line written when neither <c>ecl:</c> nor DNS gives a list.</summary>
    internal const string NoListLine = "ECH: requested but no ECHConfig available";

    /// <summary>An offer of no ECH at all.</summary>
    internal static EchOffer None { get; } = new(null, false, null, []);

    /// <summary>Decides the offer for a handshake with <paramref name="host" />.</summary>
    /// <param name="options">The connection's TLS options.</param>
    /// <param name="offersTls13">Whether the handshake offers TLS 1.3, the only version ECH runs on.</param>
    /// <param name="lookup">Finds the host's list in DNS, or <see langword="null" /> when no DoH server is used.</param>
    /// <param name="host">The host the transfer connects to.</param>
    /// <param name="port">The port it connects to.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The offer.</returns>
    internal static async ValueTask<EchOffer> DecideAsync(
        TlsClientOptions options,
        bool offersTls13,
        IEchConfigListLookup? lookup,
        string host,
        int port,
        CancellationToken cancellationToken)
    {
        var mode = EchModes.Of(options);
        if (mode is EchMode.Off or EchMode.Grease)
        {
            return GreaseOrNone(mode, offersTls13);
        }

        var (encoded, fromCommandLine) = await FindListAsync(options, lookup, host, port, cancellationToken).ConfigureAwait(false);
        var configs = UsableConfigs(encoded, options.EchPublicName);
        var lines = SourceLines(encoded, fromCommandLine, configs is not null, mode);
        if (configs is not null && options.EchPublicName is { } publicName)
        {
            lines.Add(string.Create(CultureInfo.InvariantCulture, $"ECH: inner: '{host}', outer: '{publicName}'"));
        }

        return Settle(mode, configs, offersTls13) with { InfoLines = lines };
    }

    private static EchOffer GreaseOrNone(EchMode mode, bool offersTls13) =>
        mode == EchMode.Grease ? new(null, offersTls13, null, [GreaseLine]) : None;

    // ossl_init_ech's lines for where the list came from: a failed ecl: list still writes the
    // command-line line unless hard stops at the failure; a list from DNS writes its length.
    private static List<string> SourceLines(byte[]? encoded, bool fromCommandLine, bool usable, EchMode mode) =>
        fromCommandLine ? CommandLineLines(usable, mode)
            : encoded is not { Length: > 0 } ? [NoListLine]
            : usable ? [DnsListLine, string.Create(CultureInfo.InvariantCulture, $"ECH: imported ECHConfigList of length {encoded.Length}")]
            : [DnsListLine, DnsListFailedLine];

    private static List<string> CommandLineLines(bool usable, EchMode mode) =>
        usable ? [CommandLineListLine]
            : mode == EchMode.Mandatory ? [CommandLineListFailedLine]
            : [CommandLineListFailedLine, CommandLineListLine];

    // A usable list needs TLS 1.3, which OpenSSL cannot force on a lower range; without one hard fails and true goes without.
    private static EchOffer Settle(EchMode mode, EchConfigList? configs, bool offersTls13) =>
        configs is not null && offersTls13 ? new(configs, false, null, [])
            : configs is not null ? new(null, false, ConnectResult.Failed(CurlExitCode.SslConnectError, NoProtocolsAvailableMessage), [])
            : mode == EchMode.Mandatory ? new(null, false, ConnectResult.Failed(CurlExitCode.SslConnectError, NoUsableConfigMessage), [])
            : None;

    // The ecl: list when given, else the host's list from DNS when a DoH server is used.
    private static async ValueTask<(byte[]? Encoded, bool FromCommandLine)> FindListAsync(TlsClientOptions options, IEchConfigListLookup? lookup, string host, int port, CancellationToken cancellationToken) =>
        options.EchConfigList is { } base64 ? (FromBase64(base64), true)
            : lookup is null ? (null, false)
            : (await lookup.FindEchConfigListAsync(host, port, cancellationToken).ConfigureAwait(false), false);

    private static byte[]? FromBase64(string base64)
    {
        var buffer = new byte[base64.Length];
        return Convert.TryFromBase64String(base64, buffer, out var written) ? buffer[..written] : null;
    }

    // The list when it decodes and names a configuration the client can seal for, each
    // configuration's public name replaced by pn:'s when given (OpenSSL's outer name).
    private static EchConfigList? UsableConfigs(byte[]? encoded, string? publicName) =>
        Decoded(encoded) is { SupportedConfig: not null } list ? WithPublicName(list, publicName) : null;

    private static EchConfigList? Decoded(byte[]? encoded) =>
        encoded is { Length: > 0 } && EchConfigList.Decode(encoded) is { Succeeded: true } result ? result.Value : null;

    private static EchConfigList WithPublicName(EchConfigList list, string? publicName) =>
        publicName is null ? list : list with { Configs = [.. list.Configs.Select(config => config with { PublicName = publicName })] };
}
