using Curl.Cli;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// What <see cref="CurlCommandRunner" /> keeps about one transfer while it runs and until its
/// <c>-w</c> output is written: one per transfer, so that transfers running at once under <c>-Z</c>
/// never see each other's (ADR-0127, decision 5).
/// </summary>
/// <param name="firstTransferIdOfGroup">
/// The <c>%{xfer_id}</c> of its option group's first transfer, which truncates the group's <c>-D</c> file.
/// </param>
/// <param name="laterGroups">The option groups after its own, in order.</param>
/// <param name="abortToken">
/// Cancelled when <c>--fail-early</c> aborts the transfer because another failed;
/// <see cref="CancellationToken.None" /> outside <c>-Z</c>.
/// </param>
/// <param name="standardOutput">
/// Standard output, deferring a write failure as curl's stdio buffer does: the run's one without
/// <c>-Z</c>, and one of the transfer's own under it, so that one transfer's failure is never
/// another's (task BL-773).
/// </param>
/// <param name="gatedStandardOutput"><paramref name="standardOutput" /> through the run's <see cref="WriteGate" />.</param>
internal sealed class RunningTransferState(
    long firstTransferIdOfGroup,
    IReadOnlyList<CommandLineOptions> laterGroups,
    CancellationToken abortToken,
    StandardOutputFailureDeferringStream standardOutput,
    Stream gatedStandardOutput)
{
    /// <summary>
    /// Gets standard output as the transfer writes it, which records the transfer's write failure:
    /// the run's without <c>-Z</c>, the transfer's own under it.
    /// </summary>
    internal StandardOutputFailureDeferringStream StandardOutput { get; } = standardOutput;

    /// <summary>Gets <see cref="StandardOutput" /> through the run's <see cref="WriteGate" />.</summary>
    internal Stream GatedStandardOutput { get; } = gatedStandardOutput;

    /// <summary>Gets the <c>%{xfer_id}</c> of the transfer's option group's first transfer.</summary>
    internal long FirstTransferIdOfGroup { get; } = firstTransferIdOfGroup;

    /// <summary>Gets the option groups that run after the transfer's own, in order.</summary>
    internal IReadOnlyList<CommandLineOptions> LaterGroups { get; } = laterGroups;

    /// <summary>Gets the token <c>--fail-early</c> cancels to abort the transfer under <c>-Z</c>.</summary>
    internal CancellationToken AbortToken { get; } = abortToken;

    /// <summary>
    /// Gets or sets the file the transfer saved its body to, under the name <c>-J</c> gave it if it
    /// gave one, printed by <c>%{filename_effective}</c>; <see langword="null" /> while the body goes
    /// to standard output.
    /// </summary>
    internal string? OutputFileName { get; set; }

    /// <summary>
    /// Gets or sets the output file the transfer opened, which <c>--remove-on-error</c> deletes when
    /// it fails; <see langword="null" /> while no file was opened.
    /// </summary>
    internal string? OpenedOutputFile { get; set; }

    /// <summary>
    /// Gets or sets what records whether the transfer's handler reported it past connect or open, and
    /// the status lines its byte reports draw; a new one for each attempt. The first is only a placeholder.
    /// </summary>
    internal TransferProgressRecorder Progress { get; set; } = new(TimeProvider.System);

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
    /// Gets or sets the transfer's <c>-#</c> bar, which <see cref="Progress" /> passes every report on
    /// to; <see langword="null" /> when the bar is not shown.
    /// </summary>
    internal ProgressBarRecorder? ProgressBar { get; set; }

    /// <summary>
    /// Gets or sets the transfer's share of a <c>-Z</c> run's combined progress meter, which its
    /// <see cref="Progress" /> passes every byte report on to; <see langword="null" /> outside one or
    /// when the run shows no meter (BL-521).
    /// </summary>
    internal ParallelTransferProgress? ParallelProgress { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the transfer has written the progress meter's header
    /// lines, which curl 8.21.0 writes once however many times <c>--retry</c> runs the transfer.
    /// </summary>
    internal bool ProgressMeterHeaderWritten { get; set; }

    /// <summary>
    /// Gets or sets how many times <c>--retry</c> has run the transfer again, printed by
    /// <c>%{num_retries}</c> (task BL-513).
    /// </summary>
    internal int RetryCount { get; set; }

    /// <summary>
    /// Gets or sets the warning <c>--retry</c> gave up its retries with, such as
    /// <see cref="TransferRetryWarning.RetryAfterExceedsMaxTime" />, which curl 8.21.0 prints
    /// after the transfer's progress meter (upstream test366, BL-2008); <see langword="null" />
    /// when it gave up none.
    /// </summary>
    internal string? AbandonedRetryWarning { get; set; }

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
    /// Gets or sets the <see cref="TimeProvider.GetTimestamp" /> the transfer started at, which the
    /// diagnostic log's end line measures its elapsed milliseconds from (ADR-0222).
    /// </summary>
    internal long StartTimestamp { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the transfer is a <c>-T</c> upload under <c>-C -</c>,
    /// whose meter curl 8.21.0 heads with <c>** Resuming transfer from byte position -1</c> whatever
    /// the <c>-o</c> file holds (task BL-416).
    /// </summary>
    internal bool UploadResumesFromUnknownOffset { get; set; }

    /// <summary>
    /// Gets or sets the credentials <see cref="TransferCredentialLookup" /> chose for the transfer, sent in
    /// place of the <c>-u</c> ones; <see langword="null" /> when neither the netrc file nor the URL had anything to say
    /// (tasks BL-505, BL-791).
    /// </summary>
    internal System.Net.NetworkCredential? LookedUpCredentials { get; set; }

    /// <summary>
    /// Gets or sets the SSH options of an <c>scp</c> or <c>sftp</c> transfer, with the known-hosts file the
    /// runner resolved (task BL-576); <see langword="null" /> for any other scheme.
    /// </summary>
    internal SshOptions? Ssh { get; set; }

    /// <summary>
    /// Gets or sets where the transfer's <c>-v</c> and trace events go once it is set up to connect:
    /// under <c>--trace-ids</c> marked with its IDs (task BL-648); <see cref="NoTransferEvents.Instance" /> until then.
    /// </summary>
    internal ITransferEvents Events { get; set; } = NoTransferEvents.Instance;

    /// <summary>
    /// Gets or sets the <c>%{conn_id}</c> the transfer took for its <c>--trace-ids</c> marker, which its
    /// <c>-w</c> output then prints; <see langword="null" /> until its first event (task BL-648).
    /// </summary>
    internal long? ConnectionId { get; set; }

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
    /// Gets or sets the <c>If-None-Match</c> lines <c>--etag-compare</c> has added to the transfer's option
    /// group, this transfer's last; <see langword="null" /> without <c>--etag-compare</c> (task BL-619).
    /// </summary>
    internal IReadOnlyList<string>? IfNoneMatchHeaders { get; set; }

    /// <summary>
    /// Gets or sets where the transfer's entry is in the <c>--libcurl</c> file's list of transfers;
    /// <see langword="null" /> without <c>--libcurl</c> (task BL-1177).
    /// </summary>
    internal int? LibcurlTransferIndex { get; set; }

    /// <summary>
    /// Gets or sets the transfer's alt-svc cache, read before it connects and written when it is reported;
    /// <see langword="null" /> without <c>--alt-svc</c> and for a URL that is not <c>http</c> or <c>https</c> (task BL-623).
    /// </summary>
    internal AltSvcTransferCache? AltSvc { get; set; }

    /// <summary>
    /// Gets or sets what saves an <c>ETag</c> line for <c>--etag-save</c>, which the transfer's
    /// <see cref="EtagSaveStream" /> calls; <see langword="null" /> without <c>--etag-save</c> (task BL-619).
    /// </summary>
    internal Func<byte[], CancellationToken, ValueTask>? SaveEtag { get; set; }
}
