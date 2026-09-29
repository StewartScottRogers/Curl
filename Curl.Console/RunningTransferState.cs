using Curl.Cli;
using Curl.Core;

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
internal sealed class RunningTransferState(
    long firstTransferIdOfGroup,
    IReadOnlyList<CommandLineOptions> laterGroups,
    CancellationToken abortToken)
{
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
    /// Gets or sets a value indicating whether the transfer has written the progress meter's header
    /// lines, which curl 8.21.0 writes once however many times <c>--retry</c> runs the transfer.
    /// </summary>
    internal bool ProgressMeterHeaderWritten { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the transfer is a <c>-T</c> upload under <c>-C -</c>,
    /// whose meter curl 8.21.0 heads with <c>** Resuming transfer from byte position -1</c> whatever
    /// the <c>-o</c> file holds (task BL-416).
    /// </summary>
    internal bool UploadResumesFromUnknownOffset { get; set; }

    /// <summary>
    /// Gets or sets the credentials <see cref="NetrcCredentialLookup" /> chose for the transfer, sent in
    /// place of the <c>-u</c> ones; <see langword="null" /> when the netrc file had nothing to say
    /// (task BL-505).
    /// </summary>
    internal System.Net.NetworkCredential? NetrcCredentials { get; set; }
}
