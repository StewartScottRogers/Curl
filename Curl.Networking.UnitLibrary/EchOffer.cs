using Curl.Protocol.Abstractions;
using Curl.Tls;

namespace Curl.Networking;

/// <summary>
/// What the hand-built ClientHello offers for <c>--ech</c> (ADR-0326), as curl 8.21.0's OpenSSL
/// build sets it up in <c>ossl_init_ech</c>: GREASE under <c>grease</c>; otherwise the
/// <c>ecl:</c> list, or, without one, the host's list from DNS; a list that does not decode or
/// names no configuration the client can seal for is no ECH, and under <c>hard</c> exit 35.
/// </summary>
/// <param name="Configs">The configurations to offer, with <c>pn:</c>'s public name; <see langword="null" /> for none.</param>
/// <param name="SendGrease">Whether the hello carries a GREASE <c>encrypted_client_hello</c>.</param>
/// <param name="Failure">Why the transfer fails before the handshake, or <see langword="null" />.</param>
internal sealed record EchOffer(EchConfigList? Configs, bool SendGrease, ConnectResult? Failure)
{
    /// <summary>The message <c>hard</c> fails with: curl calls no <c>failf</c>, so the tool prints exit 35's text.</summary>
    internal const string NoUsableConfigMessage = "SSL connect error";

    /// <summary>An offer of no ECH at all.</summary>
    internal static EchOffer None { get; } = new(null, false, null);

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

        var encoded = await FindListAsync(options, lookup, host, port, cancellationToken).ConfigureAwait(false);
        return Settle(mode, UsableConfigs(encoded, options.EchPublicName), offersTls13);
    }

    private static EchOffer GreaseOrNone(EchMode mode, bool offersTls13) =>
        mode == EchMode.Grease && offersTls13 ? new(null, true, null) : None;

    // The configurations when the hello can carry them; otherwise hard fails and true goes without.
    private static EchOffer Settle(EchMode mode, EchConfigList? configs, bool offersTls13) =>
        configs is not null && offersTls13 ? new(configs, false, null)
            : mode == EchMode.Mandatory ? new(null, false, ConnectResult.Failed(CurlExitCode.SslConnectError, NoUsableConfigMessage))
            : None;

    // The ecl: list when given, else the host's list from DNS when a DoH server is used.
    private static ValueTask<byte[]?> FindListAsync(TlsClientOptions options, IEchConfigListLookup? lookup, string host, int port, CancellationToken cancellationToken) =>
        options.EchConfigList is { } base64 ? ValueTask.FromResult(FromBase64(base64))
            : lookup is null ? ValueTask.FromResult<byte[]?>(null)
            : lookup.FindEchConfigListAsync(host, port, cancellationToken);

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
