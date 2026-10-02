using System.Globalization;

using Curl.Protocol.Abstractions;
using Curl.Tls;

namespace Curl.Networking;

/// <summary>
/// The text after <c>ECH: result: </c> that curl 8.21.0's OpenSSL ECH build writes after
/// <c>SSL connection using</c> for a completed handshake under <c>--ech</c>
/// (<c>lib/vtls/openssl.c</c>; measured with
/// OpenSSL 4.0.0, ADR-0359, BL-1170).
/// </summary>
internal static class EchResultText
{
    /// <summary>The text <c>grease</c> writes, at TLS 1.2 as well as TLS 1.3.</summary>
    internal const string Grease = "status is sent GREASE, inner is NULL, outer is NULL";

    /// <summary>The text <c>true</c> writes when it had no usable list to offer.</summary>
    internal const string NotConfigured = "status is not configured, inner is NULL, outer is NULL";

    /// <summary>
    /// Returns the text for a completed handshake. A rejected offer never completes (the client
    /// aborts with <c>ech_required</c>), so an offer that completed was accepted: under <c>-k</c>
    /// OpenSSL reports the outer name unchecked, which curl tolerates; verified, it is a success
    /// (that wording is curl's source, not measured).
    /// </summary>
    /// <param name="options">The connection's TLS options.</param>
    /// <param name="offeredConfigs">The configurations the hello sealed its inner hello for, or <see langword="null" />.</param>
    /// <param name="host">The host the inner hello names.</param>
    /// <returns>The text, or <see langword="null" /> when <c>--ech</c> is off.</returns>
    internal static string? Of(TlsClientOptions options, EchConfigList? offeredConfigs, string host) =>
        EchModes.Of(options) switch
        {
            EchMode.Off => null,
            EchMode.Grease => Grease,
            _ => offeredConfigs?.SupportedConfig is { } config ? Accepted(options.Insecure, host, config.PublicName) : NotConfigured,
        };

    private static string Accepted(bool insecure, string host, string publicName) =>
        string.Create(CultureInfo.InvariantCulture, $"status is {(insecure ? "bad name (tolerated without peer verification)" : "success")}, inner is {host}, outer is {publicName}");
}
