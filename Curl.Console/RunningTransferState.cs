using Curl.Cli;
using Curl.Core;
using Curl.Engine;
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
    Stream gatedStandardOutput) : EngineTransferState
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
    /// Gets or sets, under <c>-w</c>, the code of the last response status line the transfer received
    /// (<see cref="ResponseCodeRecordingTransferEvents" />), which a <c>-Z</c> transfer <c>--fail-early</c>
    /// aborted prints as <c>%{http_code}</c>; <see langword="null" /> while none was received.
    /// </summary>
    internal int? ReceivedResponseCode { get; set; }

    /// <summary>
    /// Gets or sets what records whether the transfer's handler reported it past connect or open, and
    /// the status lines its byte reports draw; a new one for each attempt. The first is only a placeholder.
    /// </summary>
    internal TransferProgressRecorder Progress { get; set; } = new(TimeProvider.System);

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
    /// Gets or sets what the transfer's easy handle answered to <c>curl_easy_getinfo</c> after its last perform,
    /// which its <c>-w</c> output prints (ADR-0625, BL-2452); <see langword="null" /> without <c>-w</c>, under
    /// <c>-Z</c>, and for a transfer that never reached its handle.
    /// </summary>
    internal EasyHandleWriteOutInfo? EasyWriteOutInfo { get; set; }

    /// <summary>
    /// Gets or sets the warning <c>--retry</c> gave up its retries with, such as
    /// <see cref="TransferRetryWarning.RetryAfterExceedsMaxTime" />, which curl 8.21.0 prints
    /// after the transfer's progress meter (upstream test366, BL-2008); <see langword="null" />
    /// when it gave up none.
    /// </summary>
    internal string? AbandonedRetryWarning { get; set; }

    /// <summary>
    /// Gets or sets curl's <c>Warning: Failed to open the file</c> line for an <c>-o</c> file the
    /// transfer could not open, held back so it is printed after the progress meter's closing
    /// newline as curl 8.21.0 does (AF-0145, BL-1964); <see langword="null" /> when there is none
    /// or it was already printed.
    /// </summary>
    internal string? OutputFileOpenWarning { get; set; }

    /// <summary>
    /// Gets or sets the transfer's <c>-o</c> file, which <see cref="OutputFileOpenFailureTransferEvents" /> asks
    /// whether it could be opened; <see langword="null" /> when the body goes elsewhere.
    /// </summary>
    internal DeferredOutputFileStream? OutputFile { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the handler's transfer succeeded and only creating
    /// the empty <c>-o</c> file afterwards failed it with exit 23, so the progress meter finishes
    /// with curl 8.21.0's done rows, as after a success (BL-2039).
    /// </summary>
    internal bool SucceededBeforeOutputFileCreationFailed { get; set; }

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
    /// Gets or sets a value indicating whether the transfer is a <c>-T .</c> upload, which curl
    /// 8.22.0 reads from standard input without blocking under its own progress callback
    /// (<c>tool_readbusy_cb</c>), so <c>Curl_pgrsDone</c> ends its meter with no newline
    /// (upstream test1498, BL-2170).
    /// </summary>
    internal bool UploadsFromStandardInputWithoutBlocking { get; set; }

    /// <summary>
    /// Gets or sets where the transfer's <c>-v</c> and trace events go once it is set up to connect:
    /// under <c>--trace-ids</c> marked with its IDs (task BL-648); <see cref="NoTransferEvents.Instance" /> until then.
    /// </summary>
    internal ITransferEvents Events { get; set; } = NoTransferEvents.Instance;

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
    /// Gets or sets what saves an <c>ETag</c> line for <c>--etag-save</c>, which the transfer's
    /// <see cref="EtagSaveStream" /> calls; <see langword="null" /> without <c>--etag-save</c> (task BL-619).
    /// </summary>
    internal Func<byte[], CancellationToken, ValueTask>? SaveEtag { get; set; }
}
