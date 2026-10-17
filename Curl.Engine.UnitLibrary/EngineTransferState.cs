using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// What <see cref="TransferPerformer" /> keeps about one transfer while it runs - its watchdogs,
/// its <c>%{xfer_id}</c> and <c>%{conn_id}</c> numbering, its credentials, its SSH options and its
/// alt-svc cache - as libcurl keeps them on the transfer's easy handle (ADR-0506, BL-2291). The
/// curl tool's state of the same transfer derives from it.
/// </summary>
internal class EngineTransferState
{
    /// <summary>
    /// Gets or sets the <c>-Y</c>/<c>-y</c> watchdog started for the attempt whose context was created
    /// last, until the attempt takes it; <see langword="null" /> when the speed is not watched.
    /// </summary>
    internal LowSpeedWatchdog? AttemptLowSpeedWatchdog { get; set; }

    /// <summary>
    /// Gets or sets the <c>-m</c> watchdog started for the attempt whose context was created last,
    /// until the attempt takes it; <see langword="null" /> without a positive <c>-m</c>.
    /// </summary>
    internal MaxTimeWatchdog? AttemptMaxTimeWatchdog { get; set; }

    /// <summary>
    /// Gets or sets the <c>%{xfer_id}</c> of the latest attempt <c>--retry</c> ran, which curl
    /// 8.21.0 makes a transfer of its own; <see langword="null" /> until the first retry (task BL-799).
    /// </summary>
    internal long? RetryTransferId { get; set; }

    /// <summary>
    /// Gets or sets the <c>%{conn_id}</c> of the attempt <c>--retry</c> last ran again, which the next
    /// attempt keeps when it reuses that connection; <see langword="null" /> until the first retry (task BL-799).
    /// </summary>
    internal long? RetriedConnectionId { get; set; }

    /// <summary>
    /// Gets or sets the <c>%{conn_id}</c> the transfer took for its <c>--trace-ids</c> marker, which its
    /// <c>-w</c> output then prints; <see langword="null" /> until its first event (task BL-648).
    /// </summary>
    internal long? ConnectionId { get; set; }

    /// <summary>
    /// Gets or sets the <c>%{xfer_id}</c> of the transfer's first attempt, which the curl tool's runner numbered and a
    /// tool-hooked handle answers <c>CURLINFO_XFER_ID</c> with until <c>--retry</c> runs it again (BL-2495).
    /// </summary>
    internal long TransferId { get; set; }

    /// <summary>
    /// Gets or sets the URL the curl tool's runner transfers, with any <c>-G</c> / <c>--url-query</c> query and
    /// normalized, which a tool-hooked handle answers <c>CURLINFO_EFFECTIVE_URL</c> with when the report names none and
    /// whose scheme it answers <c>CURLINFO_SCHEME</c> with; <see langword="null" /> until the runner sets it (BL-2495).
    /// </summary>
    internal string? RequestUrl { get; set; }

    /// <summary>
    /// Gets or sets the OpenSSL verify code of the origin's last certificate check, which
    /// <c>%{ssl_verify_result}</c> prints; <c>0</c> until one is reported (task BL-661).
    /// </summary>
    internal long SslVerifyResult { get; set; }

    /// <summary>
    /// Gets or sets the OpenSSL verify code of the HTTPS proxy's last certificate check, which
    /// <c>%{proxy_ssl_verify_result}</c> prints; <c>0</c> until one is reported (task BL-661).
    /// </summary>
    internal long ProxySslVerifyResult { get; set; }

    /// <summary>
    /// Gets or sets the TLS 1.3 early data bytes the origin connection sent, negative when the
    /// server rejected them, which <c>%{tls_earlydata}</c> prints; <c>0</c> until one is reported
    /// (task BL-1150).
    /// </summary>
    internal long TlsEarlyDataSent { get; set; }

    /// <summary>
    /// Gets or sets the credentials <see cref="TransferCredentialLookup" /> chose for the transfer, sent in
    /// place of the <c>-u</c> ones; <see langword="null" /> when neither the netrc file nor the URL had anything to say
    /// (tasks BL-505, BL-791).
    /// </summary>
    internal System.Net.NetworkCredential? LookedUpCredentials { get; set; }

    /// <summary>
    /// Gets or sets the SSH options of an <c>scp</c> or <c>sftp</c> transfer, with the known-hosts file the
    /// performer resolved (task BL-576); <see langword="null" /> for any other scheme.
    /// </summary>
    internal SshOptions? Ssh { get; set; }

    /// <summary>
    /// Gets or sets the transfer's alt-svc cache, read before it connects and written when it is reported;
    /// <see langword="null" /> without <c>--alt-svc</c> and for a URL that is not <c>http</c> or <c>https</c> (task BL-623).
    /// </summary>
    internal AltSvcTransferCache? AltSvc { get; set; }
}
