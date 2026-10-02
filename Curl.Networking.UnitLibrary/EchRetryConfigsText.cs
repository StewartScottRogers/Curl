using System.Globalization;

using Curl.Tls;

namespace Curl.Networking;

/// <summary>
/// The lines curl 8.21.0's OpenSSL ECH build writes for a server's <c>retry_configs</c>
/// (<c>ossl_trace_ech_retry_configs</c> in <c>lib/vtls/openssl.c</c>): the list in base64,
/// then the inner and outer names, the OpenSSL reason and the ECH status (measured with
/// OpenSSL 4.0.0, BL-1171).
/// </summary>
internal static class EchRetryConfigsText
{
    /// <summary>The reason and status a rejected offer reports: <c>SSL_R_ECH_REQUIRED</c> and <c>SSL_ECH_STATUS_FAILED_ECH</c>.</summary>
    private const string RejectedReasonAndStatus = "424 -106";

    /// <summary>The reason and status GREASE answered with retry_configs reports: none, and <c>SSL_ECH_STATUS_GREASE_ECH</c>.</summary>
    private const string GreaseReasonAndStatus = "0 3";

    /// <summary>Returns the lines for an offer the server rejected with <c>ech_required</c>.</summary>
    /// <param name="retryConfigs">The server's retry_configs, or <see langword="null" /> when it sent none.</param>
    /// <param name="inner">The host the inner hello named.</param>
    /// <param name="outer">The public name the outer hello named.</param>
    /// <returns>The two retry_configs lines, or curl's <c>ECH: no retry_configs (rv = 1)</c>.</returns>
    internal static IReadOnlyList<string> Rejected(EchConfigList? retryConfigs, string inner, string outer) =>
        retryConfigs is null
            ? [TlsFailureMessages.EchNoRetryConfigsLine]
            : Lines(retryConfigs, inner, outer, RejectedReasonAndStatus);

    /// <summary>Returns the lines for GREASE a server answered: none when it sent no retry_configs.</summary>
    /// <param name="retryConfigs">The server's retry_configs, or <see langword="null" /> when it sent none.</param>
    /// <returns>The lines.</returns>
    internal static IReadOnlyList<string> Grease(EchConfigList? retryConfigs) =>
        retryConfigs is null ? [] : Lines(retryConfigs, "NULL", "NULL", GreaseReasonAndStatus);

    private static string[] Lines(EchConfigList retryConfigs, string inner, string outer, string reasonAndStatus) =>
    [
        "ECH: retry_configs " + Convert.ToBase64String(retryConfigs.Encoded),
        string.Create(CultureInfo.InvariantCulture, $"ECH: retry_configs for {inner} from {outer}, {reasonAndStatus}"),
    ];
}
