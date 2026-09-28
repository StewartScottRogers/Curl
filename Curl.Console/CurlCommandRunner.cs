using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using Curl.Authentication;
using Curl.Cli;
using Curl.Core;
using Curl.Core.FileSystem;
using Curl.Core.Globbing;
using Curl.Core.Multipart;
using Curl.Output;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Runs one command line: parses it with <see cref="CommandLineParser" />, turns each URL
/// into a <see cref="TransferContext" />, performs it through a <see cref="RedirectFollower" />
/// over the <see cref="ProtocolDispatcher" /> built for that command line, and prints curl 8.21.0's
/// <c>curl: (N) &lt;message&gt;</c> line for each failure.
/// </summary>
/// <param name="createTransferDispatch">
/// Builds, from the accepted command line, the dispatcher that performs each transfer with
/// the handler for its scheme and the warning lines printed before each transfer; called
/// once per run, and not at all for a refused command line. It takes the options because
/// the network handlers' TLS settings, and the warnings for the ones the build ignores,
/// come from them.
/// </param>
/// <param name="fileSystem">
/// Opens the <c>-o</c> / <c>--output</c> files, the <c>-b</c> cookie files and the <c>-c</c> jar.
/// </param>
/// <param name="outputFileTimeSetter">
/// Stamps an <c>-o</c> file with the source's modification time under <c>-R</c> /
/// <c>--remote-time</c>.
/// </param>
/// <param name="standardOutput">
/// The raw standard output stream; a URL with no matching <c>-o</c> writes its bytes here,
/// unencoded.
/// </param>
/// <param name="standardError">
/// The raw standard error stream; each line is written as UTF-8 followed by
/// <see cref="Environment.NewLine" />.
/// </param>
/// <param name="standardInput">
/// The raw standard input stream, given to a <c>telnet</c> transfer as its
/// <see cref="ITransferContext.Upload" />, because curl's telnet "sends what it reads on
/// stdin" (ADR-0006), to a <c>-T -</c> upload, and to the default
/// <see cref="MultipartFormBodyBuilder" /> for <c>-F name=@-</c> and <c>-F name=&lt;-</c> parts
/// (ADR-0062). Every other scheme's upload is <see langword="null" />.
/// </param>
/// <param name="runsOnWindows">
/// Whether the process runs on Windows, where each <c>-o</c> name is rewritten by
/// <see cref="WindowsOutputFileNameSanitizer" /> before it is used, as curl 8.21.0 does.
/// </param>
/// <param name="terminalColumns">
/// The terminal width, from <see cref="TerminalColumns" />, at which each <c>Warning: </c>
/// line is wrapped by <see cref="WarningLineWrapper" /> before it is written; curl's default
/// of 79 when not given.
/// </param>
/// <param name="writesProgressMeter">
/// Whether this runner writes curl's progress meter at all; the composition passes
/// <see langword="true" />. See <see cref="ShowsProgressMeter" /> for when it is shown.
/// </param>
/// <param name="standardOutputIsTerminal">
/// Whether standard output is a terminal, where curl hides the meter of a transfer with no
/// <c>-o</c>.
/// </param>
/// <param name="formBodyBuilder">
/// Builds each transfer's <c>-F</c> / <c>--form-string</c> body; when not given, one reading the
/// real disk, encoding text in the platform's encoding (ADR-0027: the one
/// <see cref="CredentialEncoding.ForPlatform" /> gives) and drawing random boundaries with
/// <see cref="MultipartBoundary.CreateRandom" />.
/// </param>
/// <param name="writeOutFileOpener">
/// Opens the <c>%output{file}</c> targets of a <c>-w</c> template; when not given, a
/// <see cref="RefusingWriteOutFileOpener" />.
/// </param>
/// <param name="timeProvider">
/// The provider that took each transfer's <see cref="TransferTimings" />, which the <c>-w</c>
/// <c>time_*</c> variables are measured with, and the clock <c>%time{format}</c> reads;
/// <see cref="TimeProvider.System" /> when not given.
/// </param>
/// <param name="outputPaths">
/// Creates the <c>--create-dirs</c> directories; a <see cref="PhysicalOutputPaths" /> when
/// not given.
/// </param>
/// <param name="configFileReader">
/// Reads the default config file (<c>.curlrc</c>), each <c>-K</c> / <c>--config</c> file, and every
/// file an option names, such as a <c>-d @file</c>; <see cref="DiskDataFileReader.ForProcess" /> when
/// not given.
/// </param>
/// <param name="defaultConfigFileSearch">
/// Lists where to look for the default config file, read before the command line unless the first
/// argument starts with <c>-q</c> or is <c>--disable</c>; the composition passes
/// <see cref="DefaultConfigFileSearch.ForProcess" />. When not given, no default config file is read.
/// </param>
/// <param name="readEnvironmentVariable">
/// Returns the value of the named environment variable, or <see langword="null" /> when it is not
/// set; the <see cref="IpfsGatewayRewriter" /> reads <c>IPFS_GATEWAY</c>, <c>IPFS_PATH</c> and
/// <c>HOME</c> through it, and the gateway file through the config-file reader. The composition
/// passes <see cref="Environment.GetEnvironmentVariable(string)" />; when not given, no variable is
/// set, which keeps tests off the real environment.
/// </param>
/// <remarks>
/// <para>
/// The command line is parsed after the default config file, so its options apply first and the
/// command line's after them; a <c>-K</c> file's options apply where the <c>-K</c> stands, as in
/// curl 8.21.0 (measured 2026-09-27, BL-243). When a default config file was read and <c>-v</c> or a
/// <c>--trace</c> option is on, the accepted command line's warning lines are followed by curl's
/// <c>Note: Read config file from '&lt;path&gt;'</c>, wrapped as a warning is but with its own
/// prefix, whether or not <c>-s</c> was given, before a <c>-V</c> version or the first transfer.
/// </para>
/// <para>
/// URLs are transferred in command-line order; a failure does not stop the rest, and the
/// exit code is the last transfer's, as curl's is. The exceptions are <c>--fail-early</c>,
/// under which the first failure stops the run with its own exit code, and a resumed transfer
/// whose <c>-o</c> file cannot be opened: curl stops the run there with exit 23. The first <c>-o</c> receives the first
/// URL, the second the second, and so on. A failure's line is printed unless <c>-s</c> was
/// given without <c>-S</c>, and only when the failure carries a message. An <c>-o</c> file
/// that cannot be created prints curl's <c>Warning: Failed to open the file</c> line first,
/// unless <c>-s</c> was given, with or without <c>-S</c>. A transfer whose write to standard
/// output fails while the body still fits curl's 4096-byte stdio buffer exits 23 and prints
/// curl's <c>curl: Failed writing body</c>, with no <c>(23)</c>, under the same <c>-s</c> /
/// <c>-S</c> rule as any failure; a larger body reports the handler's own write failure.
/// </para>
/// <para>
/// Each transfer starts with the <see cref="TransferDispatch.WarningLinesBeforeEachTransfer" />,
/// unless <c>-s</c> was given anywhere on the command line, with or without <c>-S</c>: once per
/// URL, before its malformed-URL, range, <c>-o</c> or transfer lines, but after a <c>-D</c> file
/// that cannot be opened, which ends the run without them. curl 8.21.0 (Schannel) prints its
/// <c>--capath</c> lines this way, measured on 2026-09-26.
/// </para>
/// <para>
/// The parser's warning lines come first on standard error, whether or not the command line
/// is accepted; a refused command line then prints the refusal's lines and transfers nothing.
/// An accepted command line's warning lines for after the transfers, such as curl's
/// <c>Warning: Got more output options than URLs</c>, come last, after every transfer's
/// lines; they do not change the exit code.
/// </para>
/// <para>
/// Each transfer carries the parsed <c>-r</c> range, the <c>-C</c> offset and the
/// <c>--max-filesize</c> limit. Range text that names no range ends the transfer with exit 33
/// before it is dispatched. <c>-C -</c> resumes from the size of the URL's <c>-o</c> file,
/// and a transfer that resumes past byte zero appends to that file, opening it first.
/// </para>
/// <para>
/// Each transfer's header lines go where <c>-D</c> says: standard output for <c>-D -</c>,
/// otherwise the named file, opened before the transfer. Under <c>-i</c> or <c>-I</c> they
/// also go to the body output (<see cref="TransferContextFactory" />). A <c>-D</c> file that cannot be
/// opened prints curl's <c>curl: Failed to open &lt;file&gt;</c>, under the same <c>-s</c> /
/// <c>-S</c> rule as any failure, and stops the run with exit 23.
/// </para>
/// <para>
/// A successful transfer, or one <c>-f</c> failed with exit 22, is followed on standard error by curl's progress
/// meter (<see cref="ProgressMeterLines.HeaderLines" />, then the status lines
/// <see cref="TransferProgressRecorder" /> drew from the handler's byte reports), preceded by curl's
/// <c>** Resuming transfer from byte position N</c> line when it resumed past byte zero.
/// The meter is on standard error and the body is not, so writing it after the transfer
/// leaves the bytes of each stream as curl's. Under <c>-#</c> the bar
/// <see cref="ProgressBarRecorder" /> drew is written in the meter's place, with no resuming line,
/// and its newline follows the transfer's failure lines (ADR-0082).
/// </para>
/// <para>
/// Every transfer goes through <see cref="RedirectFollower" /> with the
/// <see cref="RedirectPolicy" /> <see cref="RedirectPolicyMapping" /> maps from the command
/// line, so under <c>-L</c> each redirect is followed into the same body and header outputs:
/// <c>-L -i</c> writes every response's head and the last response's body, and following one
/// redirect more than <c>--max-redirs</c> allows fails with exit 47,
/// <c>Maximum (N) redirects followed</c>, as curl 8.21.0 does. Without <c>-L</c> the
/// follower dispatches once.
/// </para>
/// <para>
/// Under <c>-R</c> a successful transfer to an <c>-o</c> file whose result carries
/// <see cref="TransferResult.SourceLastWriteTimeUtc" /> sets the file's last-write time to it,
/// after the file is closed. As in curl 8.21.0 the time is applied even when the transfer
/// wrote no body, as for an unmet <c>-z</c>. A transfer to standard output has no file to stamp.
/// </para>
/// <para>
/// With <c>-F</c> / <c>--form-string</c> each URL gets its own multipart body, built after the
/// URL and range are checked and before anything is sent, and closed when the transfer ends. A
/// form file that cannot be opened ends that transfer with the builder's exit 26 and no
/// connection, as curl 8.21.0 does.
/// </para>
/// <para>
/// With <c>-w</c> each transfer's template is rendered by <see cref="WriteOutTemplateRenderer" />
/// after its failure lines, whether it succeeded or failed, and also after a <c>-D</c> or resumed
/// <c>-o</c> file that could not be opened, as curl 8.21.0 does (measured 2026-09-26, BL-235).
/// On Windows curl writes standard error, and standard output until a body is sent there, in
/// text mode, so those line feeds are written as CR LF (<see cref="LineFeedToCrLfStream" />).
/// Because curl's standard output is buffered, its mode when the buffer is written out decides:
/// the template's standard output is binary once any transfer of the run sends its body to
/// standard output, including a later one (see <see cref="IsStandardOutputBinaryForWriteOut" />).
/// </para>
/// <para>
/// With <c>-b</c> or <c>-c</c>, the <see cref="TransferDispatch.Cookies" /> load their <c>-b</c>
/// files, <c>-b -</c> from standard input, before the first <c>http</c> or <c>https</c> transfer
/// (<see cref="LoadCookieFilesAsync" />), and the <c>-c</c> jar is written after each <c>http</c> or
/// <c>https</c> transfer's <c>-w</c> output (<see cref="WriteCookieJarAsync" />), as curl 8.21.0
/// does (measured 2026-09-26, BL-237).
/// </para>
/// </remarks>
internal sealed class CurlCommandRunner(
    Func<CommandLineOptions, TransferDispatch> createTransferDispatch,
    IFileSystem fileSystem,
    IFileTimeSetter outputFileTimeSetter,
    Stream standardOutput,
    Stream standardError,
    Stream standardInput,
    bool runsOnWindows,
    int terminalColumns = TerminalColumns.Default,
    bool writesProgressMeter = false,
    bool standardOutputIsTerminal = false,
    MultipartFormBodyBuilder? formBodyBuilder = null,
    IWriteOutFileOpener? writeOutFileOpener = null,
    TimeProvider? timeProvider = null,
    IOutputPaths? outputPaths = null,
    IDataFileReader? configFileReader = null,
    DefaultConfigFileSearch? defaultConfigFileSearch = null,
    Func<string, string?>? readEnvironmentVariable = null)
{
    /// <summary>
    /// What curl 8.21.0 prints before its URL parser's reason when it rejects a transfer
    /// URL, as in <c>URL rejected: Bad file:// URL</c>.
    /// </summary>
    internal const string UrlRejectedPrefix = "URL rejected: ";

    /// <summary>
    /// The longest percent-decoded host, in bytes, curl 8.21.0 connects to; a longer one
    /// fails with <see cref="TooLongHostnameMessage" /> before any connection.
    /// </summary>
    internal const int MaximumHostLength = 65535;

    /// <summary>curl 8.21.0's message for a host longer than <see cref="MaximumHostLength" />.</summary>
    internal const string TooLongHostnameMessage = "Too long hostname (maximum is 65535)";

    /// <summary>
    /// The line curl 8.21.0's own write callback prints, with no <c>(23)</c>, when standard
    /// output is closed or its reader has gone and the body fits its stdio buffer; measured on
    /// <c>telnet</c> and <c>file</c> transfers.
    /// </summary>
    internal const string FailedWritingBodyLine = "curl: Failed writing body";

    /// <summary>
    /// curl 8.21.0's message for a resumed transfer whose <c>-o</c> file cannot be opened
    /// for appending, measured on <c>-C 3 -o</c> naming a directory.
    /// </summary>
    internal const string WriteReceivedDataFailedMessage = "Failed writing received data to disk/application";

    /// <summary>The <see cref="DefaultConfigFileSearch" /> used when the runner is given none: it lists no path.</summary>
    private static readonly DefaultConfigFileSearch NoDefaultConfigFile = new(_ => null, false, null, null);

    /// <summary>
    /// Gets what reads the config files, every file an option names, and the IPFS gateway file:
    /// the reader the runner was given, else the disk.
    /// </summary>
    private IDataFileReader DataFileReader => configFileReader ?? DiskDataFileReader.ForProcess;

    /// <summary>The <c>-D</c> value that sends the header lines to standard output.</summary>
    private const string StandardOutputHeaderFile = "-";

    /// <summary>Builds each transfer's context on the runner's clock; gives a <c>telnet</c> transfer standard input.</summary>
    private readonly TransferContextFactory transferContextFactory = new(standardInput, timeProvider);

    /// <summary>Builds each transfer's <c>-F</c> body; reads <c>@-</c> and <c>&lt;-</c> parts from standard input.</summary>
    private readonly MultipartFormBodyBuilder formBodyBuilder = formBodyBuilder
        ?? new MultipartFormBodyBuilder(
            new PhysicalFileSystem(),
            CredentialEncoding.ForPlatform(runsOnWindows),
            MultipartBoundary.CreateRandom,
            standardInput);

    /// <summary>The <see cref="IOutputPaths" /> used when the runner is given none.</summary>
    private static readonly PhysicalOutputPaths DiskOutputPaths = new();

    /// <summary>The clock the <c>-w</c> variables and <c>%time{format}</c> read.</summary>
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;

    /// <summary>
    /// Renders each transfer's <c>-w</c> template. It writes line feeds as they are; the
    /// runner decides which targets are in text mode.
    /// </summary>
    private readonly WriteOutTemplateRenderer writeOutRenderer = new(
        writeOutFileOpener ?? new RefusingWriteOutFileOpener(),
        writesLineFeedAsCrLf: false,
        timeProvider ?? TimeProvider.System);

    /// <summary>
    /// The result of a transfer whose write to standard output failed. It is compared by
    /// reference, so that <see cref="FormatErrorLine" /> prints
    /// <see cref="FailedWritingBodyLine" /> for it rather than a <c>curl: (23)</c> line.
    /// </summary>
    private static readonly TransferResult StandardOutputWriteFailure =
        new(CurlExitCode.WriteError, 0, "Failed writing body");

    /// <summary>
    /// The result of a resumed transfer whose <c>-o</c> file could not be opened for
    /// appending. It is compared by reference, so that <see cref="TransferAllAsync" /> stops
    /// before the remaining URLs, as curl 8.21.0 does.
    /// </summary>
    private static readonly TransferResult CannotOpenForResumeFailure =
        TransferResult.Failure(CurlExitCode.WriteError, WriteReceivedDataFailedMessage);

    /// <summary>
    /// The result of a transfer whose <c>-D</c> file could not be opened, measured on
    /// curl 8.21.0 with a read-only <c>-D</c> file: exit 23 with
    /// <see cref="WriteReceivedDataFailedMessage" />. It is compared by reference, so that
    /// <see cref="TransferAllAsync" /> stops before the remaining URLs, as curl does.
    /// </summary>
    private static readonly TransferResult CannotOpenHeaderFileFailure =
        TransferResult.Failure(CurlExitCode.WriteError, WriteReceivedDataFailedMessage);

    /// <summary>
    /// The result of a transfer whose <c>--create-dirs</c> directory could not be created,
    /// measured on curl 8.21.0 with a file in the way: exit 23 with
    /// <see cref="WriteReceivedDataFailedMessage" />, nothing sent. It is compared by reference, so that
    /// <see cref="TransferAllAsync" /> stops before the remaining URLs, as curl does.
    /// </summary>
    private static readonly TransferResult CannotCreateDirectoryFailure =
        TransferResult.Failure(CurlExitCode.WriteError, WriteReceivedDataFailedMessage);

    /// <summary>
    /// The result of a <c>-T</c> transfer whose URL <see cref="UploadTransferUrl" /> cannot parse:
    /// exit 3 with <see cref="UploadTransferUrl.MalformedUrlMessage" />, before anything else of the
    /// transfer, as curl 8.21.0 does (measured 2026-09-27, BL-030).
    /// </summary>
    private static readonly TransferResult UploadUrlMalformedFailure =
        TransferResult.Failure(CurlExitCode.UrlMalformat, UploadTransferUrl.MalformedUrlMessage);

    /// <summary>
    /// The result of an <c>ipfs://</c> or <c>ipns://</c> URL for which no gateway is configured:
    /// exit 37, whose <c>%{errormsg}</c> curl 8.21.0 prints as its generic text for the code
    /// (measured 2026-09-27, BL-240 Notes). It is compared by reference, so that
    /// <see cref="TransferAllAsync" /> stops before the remaining URLs, as curl does.
    /// </summary>
    private static readonly TransferResult IpfsGatewayDetectionFailure =
        TransferResult.Failure(IpfsGatewayFailure.GatewayDetectionFailed.ExitCode, "Could not read a file:// file");

    /// <summary>
    /// The line curl 8.21.0 prints, after <c>curl: </c>, when <c>-O</c> or <c>--remote-name-all</c>
    /// asks for the remote name of an <c>ipfs://</c> or <c>ipns://</c> URL (measured 2026-09-27,
    /// BL-372 Notes).
    /// </summary>
    private const string IpfsRemoteNameMessage = "Failed to extract a filename from the URL to use for storage";

    /// <summary>
    /// The result of <c>-O</c> or <c>--remote-name-all</c> on an <c>ipfs://</c> or <c>ipns://</c>
    /// URL: exit 1, <c>Unsupported protocol</c>, before any gateway is looked up, with no transfer
    /// number and no connection, as curl 8.21.0 does whether or not a gateway is given (measured
    /// 2026-09-27, BL-372 Notes). It is compared by reference, so that
    /// <see cref="TransferAllAsync" /> stops before the remaining URLs, as curl does.
    /// </summary>
    private static readonly TransferResult IpfsRemoteNameFailure =
        TransferResult.Failure(CurlExitCode.UnsupportedProtocol, "Unsupported protocol");

    /// <summary>
    /// The result of an <c>ipfs://</c> or <c>ipns://</c> URL whose gateway, or rewritten URL, curl
    /// rejects: exit 3, whose <c>%{errormsg}</c> curl 8.21.0 prints as its generic text for the
    /// code (measured 2026-09-27, BL-240 Notes). It is compared by reference, so that
    /// <see cref="TransferAllAsync" /> stops before the remaining URLs, as curl does.
    /// </summary>
    private static readonly TransferResult IpfsMalformedTargetUrlFailure =
        TransferResult.Failure(IpfsGatewayFailure.MalformedTargetUrl.ExitCode, "URL using bad/illegal format or missing URL");

    /// <summary>
    /// The result of a transfer whose <c>-T</c> file could not be opened, measured on curl 8.21.0
    /// with <c>-T nosuchfile</c>: exit 26 with <see cref="MultipartFormBodyBuilder.OpenFailedMessage" />.
    /// It is compared by reference, so that <see cref="TransferAllAsync" /> stops before the
    /// remaining URLs, as curl does.
    /// </summary>
    private static readonly TransferResult CannotOpenUploadFileFailure =
        TransferResult.Failure(CurlExitCode.ReadError, MultipartFormBodyBuilder.OpenFailedMessage);

    /// <summary>
    /// Standard output, deferring a write failure as curl's stdio buffer does and recording
    /// it for the current transfer.
    /// </summary>
    private readonly StandardOutputFailureDeferringStream deferringStandardOutput = new(standardOutput);

    /// <summary>
    /// The file the current transfer saved its body to, under the name <c>-J</c> gave it if it
    /// gave one, printed by <c>%{filename_effective}</c>; <see langword="null" /> while the
    /// body goes to standard output. Cleared before each transfer.
    /// </summary>
    private string? transferOutputFileName;

    /// <summary>
    /// Records whether the current transfer's handler reported it past connect or open, and
    /// the status lines its byte reports draw, for <see cref="WriteProgressAsync" />; a new
    /// one, on the runner's clock, for each transfer. The first is only a placeholder.
    /// </summary>
    private TransferProgressRecorder transferProgress = new(TimeProvider.System);

    /// <summary>
    /// The current transfer's <c>-#</c> bar, which <see cref="transferProgress" /> passes every
    /// report on to; <see langword="null" /> when the bar is not shown. Cleared before each transfer.
    /// </summary>
    private ProgressBarRecorder? progressBar;

    /// <summary>
    /// The <c>%{conn_id}</c> the next transfer that connects takes, counted per run from zero.
    /// </summary>
    private long nextConnectionId;

    /// <summary>
    /// The <c>%{xfer_id}</c> the next transfer takes, counted per run from zero, one for every URL
    /// a glob expands to.
    /// </summary>
    private long nextTransferId;

    /// <summary>
    /// Whether a transfer of this run has sent, or was set up to send, its body to standard output.
    /// </summary>
    private bool bodyWrittenToStandardOutput;

    /// <summary>
    /// Whether the current transfer has written the progress meter's header lines, which curl
    /// 8.21.0 writes once however many times <c>--retry</c> runs the transfer.
    /// </summary>
    private bool progressMeterHeaderWritten;

    /// <summary>
    /// Whether the current transfer is a <c>-T</c> upload under <c>-C -</c>, whose meter curl
    /// 8.21.0 heads with <c>** Resuming transfer from byte position -1</c> whatever the
    /// <c>-o</c> file holds (task BL-416).
    /// </summary>
    private bool uploadResumesFromUnknownOffset;

    /// <summary>
    /// Where this run's <c>-v</c>, <c>--trace</c> and <c>--trace-ascii</c> output goes, opened once the
    /// first command-line URL has parsed as a glob and closed after the last transfer.
    /// </summary>
    private TransferEventOutput transferEventOutput = TransferEventOutput.None;

    /// <summary>
    /// Runs <paramref name="arguments" /> to completion.
    /// </summary>
    /// <param name="arguments">The command-line arguments, without the program name.</param>
    /// <returns>The process exit code.</returns>
    internal async Task<int> RunAsync(IReadOnlyList<string> arguments)
    {
        CommandLineParseResult parsed = ParseCommandLine(arguments);
        await WriteErrorLinesAsync(parsed.WarningLines).ConfigureAwait(false);

        if (!parsed.IsAccepted)
        {
            await WriteRefusalAsync(parsed.Refusal, parsed.NotedDefaultConfigFile).ConfigureAwait(false);

            return (int)parsed.Refusal.ExitCode;
        }

        await WriteDefaultConfigFileNoteAsync(parsed.NotedDefaultConfigFile).ConfigureAwait(false);

        if (parsed.Options.VersionRequested)
        {
            await WriteVersionLinesAsync().ConfigureAwait(false);

            return (int)CurlExitCode.Ok;
        }

        if (RequestMethodConflictLines(parsed.Options) is { } conflictLines)
        {
            if (!parsed.Options.Silent)
            {
                await WriteErrorLinesAsync(conflictLines).ConfigureAwait(false);
            }

            return (int)CurlExitCode.FailedInit;
        }

        CurlExitCode exitCode = await TransferAllAsync(parsed.Options).ConfigureAwait(false);
        await WriteErrorLinesAsync(parsed.WarningLinesAfterTransfers).ConfigureAwait(false);

        return (int)exitCode;
    }

    /// <summary>
    /// The warning lines curl 8.21.0 prints at transfer setup, not while reading the command line,
    /// before it refuses with <see cref="CurlExitCode.FailedInit" /> a <c>-d</c> / <c>--json</c> body to
    /// be posted (no <c>-G</c>) when <c>-I</c> selected <c>HEAD</c> or <c>--no-head</c> selected
    /// <c>GET</c>, whatever order the options came in. <c>-s</c> drops the lines, and <c>-S</c> does not
    /// bring them back.
    /// </summary>
    /// <param name="options">The accepted command line.</param>
    /// <returns>
    /// <see cref="CommandLineWarning.PostRequestedWithHead" /> or
    /// <see cref="CommandLineWarning.PostRequestedWithGet" />, or <see langword="null" /> when the
    /// request methods do not conflict.
    /// </returns>
    private static IReadOnlyList<string>? RequestMethodConflictLines(CommandLineOptions options)
    {
        if (options.PostData is null || options.DataInQuery)
        {
            return null;
        }

        return options.HttpMethodSelected switch
        {
            SelectedHttpMethod.Head => CommandLineWarning.PostRequestedWithHead,
            SelectedHttpMethod.Get => CommandLineWarning.PostRequestedWithGet,
            _ => null,
        };
    }

    /// <summary>
    /// Parses <paramref name="arguments" /> after the default config file, reading the config files
    /// with the runner's reader, checking paths on disk and asking the console for a missing
    /// <c>-u</c> password.
    /// </summary>
    /// <param name="arguments">The command-line arguments, without the program name.</param>
    /// <returns>The parse result.</returns>
    private CommandLineParseResult ParseCommandLine(IReadOnlyList<string> arguments) =>
        CommandLineParser.Parse(
            arguments,
            Path.Exists,
            ConsolePasswordPrompt.ForProcessConsole,
            DataFileReader,
            defaultConfigFileSearch ?? NoDefaultConfigFile);

    /// <summary>
    /// Transfers every URL in order, each command-line URL once for every URL its glob expands to,
    /// and reports each failure, then disposes the run's <see cref="TransferDispatch" />, closing
    /// its connection pool, whatever the outcome.
    /// </summary>
    /// <param name="options">The accepted command line.</param>
    /// <returns>
    /// The last transfer's exit code, or <see cref="CurlExitCode.Ok" /> when there was none.
    /// A transfer <see cref="EndsTheRun" /> names is the last: the URLs after it are not
    /// transferred. A URL that is not a well-formed glob also ends the run, with exit 3.
    /// </returns>
    private async Task<CurlExitCode> TransferAllAsync(CommandLineOptions options)
    {
        TransferDispatch dispatch = createTransferDispatch(options);
        try
        {
            return await TransferEachUrlAsync(dispatch, options).ConfigureAwait(false);
        }
        finally
        {
            await dispatch.DisposeAsync().ConfigureAwait(false);
            await transferEventOutput.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Transfers every URL in order, for <see cref="TransferAllAsync" />, opening the run's
    /// <see cref="transferEventOutput" /> once the first URL has parsed as a glob: curl 8.21.0
    /// opens its trace file at the first event, and a URL that is not a well-formed glob makes
    /// none (measured 2026-09-27, BL-242 Notes).
    /// </summary>
    /// <param name="dispatch">What the run transfers through.</param>
    /// <param name="options">The accepted command line.</param>
    /// <returns>The exit code <see cref="TransferAllAsync" /> returns.</returns>
    private async Task<CurlExitCode> TransferEachUrlAsync(TransferDispatch dispatch, CommandLineOptions options)
    {
        CurlExitCode exitCode = CurlExitCode.Ok;
        for (int index = 0; index < options.Urls.Count; index++)
        {
            if (!TryParseUploadFiles(options, index, out IReadOnlyList<string?>? uploadFiles, out TransferResult? globFailure)
                || !TryParseGlob(options, index, out UrlGlob? glob, out globFailure))
            {
                await WriteGlobFailureLinesAsync(options, globFailure).ConfigureAwait(false);
                return globFailure.ExitCode;
            }

            if (index == 0)
            {
                transferEventOutput = await TransferEventOutput
                    .OpenAsync(options, fileSystem, deferringStandardOutput, standardError, runsOnWindows, standardOutputIsTerminal, timeProvider)
                    .ConfigureAwait(false);
            }

            (exitCode, bool runEnded) = await TransferEachMatchAsync(dispatch, options, index, uploadFiles, glob, exitCode)
                .ConfigureAwait(false);
            if (runEnded)
            {
                return exitCode;
            }
        }

        return exitCode;
    }

    /// <summary>
    /// Transfers the command-line URL at <paramref name="index" /> once for every pair of an upload
    /// file and a URL its glob expands to, the upload files the outer loop, as curl 8.21.0 does:
    /// <c>-T '{local.txt,sub/in.txt}' 'http://h/{x,y}/'</c> sent x/local.txt, y/local.txt, x/in.txt
    /// and then y/in.txt (measured 2026-09-27, BL-031 Notes).
    /// </summary>
    /// <param name="dispatch">What the run transfers through.</param>
    /// <param name="options">The accepted command line.</param>
    /// <param name="index">The URL's position on the command line.</param>
    /// <param name="uploadFiles">The upload files, or one <see langword="null" /> for no upload.</param>
    /// <param name="glob">The URL's glob.</param>
    /// <param name="exitCode">The exit code so far, returned when nothing is transferred.</param>
    /// <returns>The last transfer's exit code, and whether a transfer <see cref="EndsTheRun" /> names ended the run.</returns>
    private async Task<(CurlExitCode ExitCode, bool RunEnded)> TransferEachMatchAsync(
        TransferDispatch dispatch,
        CommandLineOptions options,
        int index,
        IReadOnlyList<string?> uploadFiles,
        UrlGlob glob,
        CurlExitCode exitCode)
    {
        foreach (string? uploadFile in uploadFiles)
        {
            foreach (UrlGlobMatch match in glob.Expand())
            {
                UrlTransfer transfer = new(options, index, nextTransferId++, match, uploadFile, runsOnWindows);
                TransferResult result = await TransferAndReportAsync(dispatch, options, transfer).ConfigureAwait(false);
                exitCode = result.ExitCode;
                if (EndsTheRun(options, result))
                {
                    return (exitCode, true);
                }
            }
        }

        return (exitCode, false);
    }

    /// <summary>
    /// Expands the <c>-T</c> value paired with the URL at <paramref name="index" /> with
    /// <see cref="UploadFileGlob" />: one upload file per match, or under <c>-g</c> / <c>--globoff</c>
    /// the value as written. curl 8.21.0 reads it before the URL, and a malformed one is exit 3 with
    /// the glob's message about the <c>-T</c> text (measured 2026-09-27, BL-031 Notes).
    /// </summary>
    /// <param name="options">The accepted command line.</param>
    /// <param name="index">The URL's position on the command line.</param>
    /// <param name="uploadFiles">
    /// The upload files in curl's order, or one <see langword="null" /> when the URL has no upload;
    /// <see langword="null" /> when the <c>-T</c> value is not a well-formed glob.
    /// </param>
    /// <param name="failure">The exit 3 failure with curl's message; <see langword="null" /> on success.</param>
    /// <returns><see langword="true" /> when <paramref name="uploadFiles" /> was produced.</returns>
    private static bool TryParseUploadFiles(
        CommandLineOptions options,
        int index,
        [NotNullWhen(true)] out IReadOnlyList<string?>? uploadFiles,
        [NotNullWhen(false)] out TransferResult? failure)
    {
        if (UploadFileOf(options, index) is not { } uploadFile)
        {
            uploadFiles = [null];
            failure = null;
            return true;
        }

        if (!UploadFileGlob.TryParse(uploadFile, options.GlobOff, out UploadFileGlob? glob, out failure))
        {
            uploadFiles = null;
            return false;
        }

        uploadFiles = [.. glob.ExpandUploadFiles()];
        return true;
    }

    /// <summary>
    /// Reads the URL at <paramref name="index" /> as a glob, or under <c>-g</c> / <c>--globoff</c>
    /// as the one URL it is.
    /// </summary>
    /// <param name="options">The accepted command line.</param>
    /// <param name="index">The URL's position on the command line.</param>
    /// <param name="glob">The glob; <see langword="null" /> when the URL is not a well-formed one.</param>
    /// <param name="failure">The exit 3 failure with curl's message; <see langword="null" /> on success.</param>
    /// <returns><see langword="true" /> when <paramref name="glob" /> was produced.</returns>
    private static bool TryParseGlob(
        CommandLineOptions options,
        int index,
        [NotNullWhen(true)] out UrlGlob? glob,
        [NotNullWhen(false)] out TransferResult? failure)
    {
        if (options.GlobOff)
        {
            glob = UrlGlob.Unglobbed(options.Urls[index]);
            failure = null;
            return true;
        }

        return UrlGlob.TryParse(options.Urls[index], out glob, out failure);
    }

    /// <summary>
    /// Writes a bad glob's lines, <c>curl: (3) </c> and curl's message, one line per line of the
    /// message, unless <c>-s</c> was given without <c>-S</c>, as curl 8.21.0 does (measured
    /// 2026-09-27, BL-240 Notes).
    /// </summary>
    /// <param name="options">The accepted command line.</param>
    /// <param name="failure">The glob's failure.</param>
    /// <returns>A task that completes when the lines are flushed.</returns>
    private async Task WriteGlobFailureLinesAsync(CommandLineOptions options, TransferResult failure)
    {
        if (ShowsErrors(options))
        {
            await WriteErrorLinesAsync(FormatErrorLine(failure).Split('\n')).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Performs one transfer, then writes its failure lines, its <c>-w</c> output and the
    /// <c>-c</c> jar.
    /// </summary>
    /// <param name="dispatch">Performs the transfer with the handler for its scheme, after its warning lines.</param>
    /// <param name="options">The accepted command line.</param>
    /// <param name="transfer">The transfer.</param>
    /// <returns>The transfer's result.</returns>
    private async Task<TransferResult> TransferAndReportAsync(
        TransferDispatch dispatch,
        CommandLineOptions options,
        UrlTransfer transfer)
    {
        transferOutputFileName = null;
        progressBar = null;
        progressMeterHeaderWritten = false;
        (TransferResult result, string givenUrl, string transferUrl) = await TransferUrlAsync(dispatch, options, transfer)
            .ConfigureAwait(false);

        if (ShowsErrors(options) && result.ErrorMessage is not null && !IsIpfsGatewayFailure(result))
        {
            await WriteFailureLinesAsync(result).ConfigureAwait(false);
        }

        if (progressBar is { HasBeenCalled: true })
        {
            await WriteErrorLineAsync(string.Empty).ConfigureAwait(false);
        }

        bool endsTheRun = EndsTheRun(options, result);
        bodyWrittenToStandardOutput |= SendsBodyToStandardOutput(transfer, result);
        bool standardOutputIsBinary = IsStandardOutputBinaryForWriteOut(
            options, transfer.UrlIndex, bodyWrittenToStandardOutput, endsTheRun);
        await WriteOutAsync(options, transfer, givenUrl, transferUrl, result, standardOutputIsBinary).ConfigureAwait(false);
        await WriteCookieJarAsync(dispatch, options, transferUrl, standardOutputIsBinary).ConfigureAwait(false);

        return result;
    }

    /// <summary>
    /// Performs one transfer: rewrites an <c>ipfs://</c> or <c>ipns://</c> URL to its gateway URL,
    /// gives a URL typed without a scheme the one <see cref="UrlSchemeGuesser" /> guesses, and
    /// resolves the URL of a <c>-T</c> upload with <see cref="UploadTransferUrl" />.
    /// </summary>
    /// <param name="dispatch">Performs the transfer with the handler for its scheme, after its warning lines.</param>
    /// <param name="options">The accepted command line.</param>
    /// <param name="transfer">The transfer.</param>
    /// <returns>
    /// The transfer's result; the URL <c>%{url}</c> prints, the gateway URL for a rewritten IPFS
    /// URL and otherwise the URL as the glob expanded it; and the URL transferred, with its
    /// guessed scheme and, for a <c>-T</c> upload, as resolved. An IPFS URL that cannot be
    /// rewritten, and a <c>-T</c> URL that cannot be parsed, return their failure and an empty
    /// URL transferred, with nothing else done.
    /// </returns>
    private async Task<(TransferResult Result, string GivenUrl, string TransferUrl)> TransferUrlAsync(
        TransferDispatch dispatch,
        CommandLineOptions options,
        UrlTransfer transfer)
    {
        if (TakesRemoteNameFromIpfsUrl(transfer))
        {
            return (await RefuseIpfsRemoteNameAsync(options).ConfigureAwait(false), transfer.Url, string.Empty);
        }

        if (!TryRewriteIpfsUrl(options, transfer.Url, out string givenUrl, out IpfsGatewayFailure? ipfsFailure))
        {
            return (await ReportIpfsGatewayFailureAsync(options, transfer, ipfsFailure).ConfigureAwait(false), givenUrl, string.Empty);
        }

        string? uploadFile = transfer.UploadFile;
        string transferUrl = UrlSchemeGuesser.AddGuessedScheme(givenUrl);
        if (uploadFile is not null && !UploadTransferUrl.TryResolve(transferUrl, uploadFile, out transferUrl))
        {
            return (UploadUrlMalformedFailure, givenUrl, transferUrl);
        }

        await LoadCookieFilesAsync(dispatch, options, transferUrl).ConfigureAwait(false);
        TransferResult result = await TransferWithHeaderOutputAsync(dispatch, options, transfer, transferUrl, uploadFile)
            .ConfigureAwait(false);
        return (result, givenUrl, transferUrl);
    }

    /// <summary>
    /// Tells whether <paramref name="transfer" /> saves its body under the remote name of an
    /// <c>ipfs://</c> or <c>ipns://</c> URL, which curl 8.21.0 takes from the URL before the IPFS
    /// rewrite and cannot (measured 2026-09-27, BL-372 Notes).
    /// </summary>
    /// <param name="transfer">The transfer.</param>
    /// <returns><see langword="true" /> for <c>-O</c> or <c>--remote-name-all</c> on an IPFS URL with no <c>-o</c> name.</returns>
    private static bool TakesRemoteNameFromIpfsUrl(UrlTransfer transfer) =>
        transfer is { UsesRemoteName: true, OutputFileName: null }
        && CurlUrl.TryParse(transfer.Url, pathAsIs: false, out CurlUrl? parsed)
        && IpfsGatewayRewriter.IsIpfsUrl(parsed);

    /// <summary>
    /// Prints curl's <see cref="IpfsRemoteNameMessage" /> line unless <c>-s</c> was given without
    /// <c>-S</c>; the <c>curl: (1)</c> line follows as any failure's does.
    /// </summary>
    /// <param name="options">The accepted command line.</param>
    /// <returns><see cref="IpfsRemoteNameFailure" />.</returns>
    private async Task<TransferResult> RefuseIpfsRemoteNameAsync(CommandLineOptions options)
    {
        if (ShowsErrors(options))
        {
            await WriteErrorLineAsync("curl: " + IpfsRemoteNameMessage).ConfigureAwait(false);
        }

        return IpfsRemoteNameFailure;
    }

    /// <summary>
    /// Rewrites <paramref name="url" /> to its IPFS gateway URL when it is an <c>ipfs://</c> or
    /// <c>ipns://</c> URL, with <see cref="IpfsGatewayRewriter" />.
    /// </summary>
    /// <param name="options">The accepted command line, whose <c>--ipfs-gateway</c> comes first.</param>
    /// <param name="url">The URL as the glob expanded it.</param>
    /// <param name="rewrittenUrl">The gateway URL, or <paramref name="url" /> when it is not an IPFS URL or cannot be rewritten.</param>
    /// <param name="failure">Why an IPFS URL could not be rewritten; <see langword="null" /> otherwise.</param>
    /// <returns><see langword="false" /> only for an IPFS URL that could not be rewritten.</returns>
    private bool TryRewriteIpfsUrl(
        CommandLineOptions options,
        string url,
        out string rewrittenUrl,
        [NotNullWhen(false)] out IpfsGatewayFailure? failure)
    {
        rewrittenUrl = url;
        failure = null;
        if (!CurlUrl.TryParse(url, pathAsIs: false, out CurlUrl? parsed) || !IpfsGatewayRewriter.IsIpfsUrl(parsed))
        {
            return true;
        }

        IpfsGatewayRewriter rewriter = new(readEnvironmentVariable ?? ReadNoEnvironmentVariable, ReadGatewayFileText);
        if (!rewriter.TryRewrite(parsed, options.IpfsGateway, out string? gatewayUrl, out failure))
        {
            return false;
        }

        rewrittenUrl = gatewayUrl;
        return true;
    }

    /// <summary>
    /// Reads an IPFS gateway file as UTF-8 through <see cref="DataFileReader" />.
    /// </summary>
    /// <param name="path">The file's path.</param>
    /// <returns>The file's text, or <see langword="null" /> when it cannot be read.</returns>
    private string? ReadGatewayFileText(string path) =>
        DataFileReader.TryReadFile(path, out byte[] contents) ? Encoding.UTF8.GetString(contents) : null;

    /// <summary>
    /// Reads the environment when the runner was given no function for it: no variable is set,
    /// which keeps tests off the real environment.
    /// </summary>
    /// <param name="name">The variable's name.</param>
    /// <returns>Always <see langword="null" />.</returns>
    private static string? ReadNoEnvironmentVariable(string name) => null;

    /// <summary>
    /// Prints curl's lines for an IPFS URL that could not be rewritten, <c>curl: </c> and the
    /// message, then the try-help line, whether or not <c>-s</c> was given, as curl 8.21.0 does
    /// (measured 2026-09-27, BL-240 Notes). curl has named the <c>-o</c> file by then, so
    /// <c>%{filename_effective}</c> prints it, under <c>--output-dir</c>, though nothing is opened.
    /// </summary>
    /// <param name="options">The accepted command line.</param>
    /// <param name="transfer">The transfer, which names its output.</param>
    /// <param name="failure">Why the URL could not be rewritten.</param>
    /// <returns>
    /// <see cref="IpfsGatewayDetectionFailure" /> or <see cref="IpfsMalformedTargetUrlFailure" />.
    /// </returns>
    private async Task<TransferResult> ReportIpfsGatewayFailureAsync(
        CommandLineOptions options,
        UrlTransfer transfer,
        IpfsGatewayFailure failure)
    {
        transferOutputFileName = transfer.OutputFileName is { } fileName ? InOutputDirectory(options, fileName) : null;
        await WriteErrorLinesAsync(["curl: " + failure.Message, CommandLineRefusal.TryHelpLine]).ConfigureAwait(false);

        return failure == IpfsGatewayFailure.GatewayDetectionFailed
            ? IpfsGatewayDetectionFailure
            : IpfsMalformedTargetUrlFailure;
    }

    /// <summary>
    /// Tells whether <paramref name="result" /> is an IPFS URL's failure to be rewritten, which
    /// prints its own lines, uses no connection and has no transfer number.
    /// </summary>
    /// <param name="result">The transfer's result.</param>
    /// <returns><see langword="true" /> for <see cref="IpfsGatewayDetectionFailure" /> and <see cref="IpfsMalformedTargetUrlFailure" />.</returns>
    private static bool IsIpfsGatewayFailure(TransferResult result) =>
        ReferenceEquals(result, IpfsGatewayDetectionFailure) || ReferenceEquals(result, IpfsMalformedTargetUrlFailure);

    /// <summary>
    /// Tells whether <paramref name="result" /> ended an IPFS URL before curl numbered its
    /// transfer, so that <c>%{xfer_id}</c> prints <c>-1</c>: a gateway failure, or a remote name
    /// asked of the URL.
    /// </summary>
    /// <param name="result">The transfer's result.</param>
    /// <returns><see langword="true" /> when the transfer has no number.</returns>
    private static bool HasNoTransferNumber(TransferResult result) =>
        IsIpfsGatewayFailure(result) || ReferenceEquals(result, IpfsRemoteNameFailure);

    /// <summary>
    /// The <c>-T</c> file uploaded to the URL at <paramref name="index" />: the Nth <c>-T</c> value
    /// goes to the Nth URL, as curl 8.21.0 pairs them.
    /// </summary>
    /// <param name="options">The accepted command line.</param>
    /// <param name="index">The URL's position on the command line.</param>
    /// <returns>
    /// The <c>-T</c> value, or <see langword="null" /> when the URL has none or an empty one, which
    /// curl 8.21.0 takes as no upload (measured 2026-09-27, BL-030).
    /// </returns>
    private static string? UploadFileOf(CommandLineOptions options, int index) =>
        index < options.UploadFiles.Count && options.UploadFiles[index].Length > 0
            ? options.UploadFiles[index]
            : null;

    /// <summary>
    /// Writes a failed transfer's <c>curl: (N) &lt;message&gt;</c> line, followed by curl's
    /// certificate help block for exit 60.
    /// </summary>
    /// <param name="result">The failed transfer's result, which carries a message.</param>
    /// <returns>A task that completes when the lines are flushed.</returns>
    private async Task WriteFailureLinesAsync(TransferResult result)
    {
        await WriteErrorLineAsync(FormatErrorLine(result)).ConfigureAwait(false);
        if (result.ExitCode == CurlExitCode.PeerFailedVerification)
        {
            await WriteCertificateHelpBlockAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Tells whether <paramref name="transfer" /> was set up to send its body to standard output,
    /// which is when curl switches standard output to binary mode: it has no <c>-o</c> or remote
    /// name, and its <c>-D</c> file, if any, could be opened. An IPFS URL that could not be
    /// rewritten counts: curl 8.21.0 wrote its <c>-w</c> line feed as LF without <c>-o</c> and as
    /// CR LF with one (measured 2026-09-27, BL-240 Notes).
    /// </summary>
    /// <param name="transfer">The transfer.</param>
    /// <param name="result">The transfer's result.</param>
    /// <returns><see langword="true" /> when the body went, or was to go, to standard output.</returns>
    private static bool SendsBodyToStandardOutput(UrlTransfer transfer, TransferResult result) =>
        !transfer.WritesToFile && !ReferenceEquals(result, CannotOpenHeaderFileFailure);

    /// <summary>
    /// Gives the output entry of the URL at <paramref name="index" />.
    /// </summary>
    /// <param name="options">The accepted command line.</param>
    /// <param name="index">The URL's position on the command line.</param>
    /// <returns>The entry, or <see langword="null" /> when no output option was paired with the URL.</returns>
    private static UrlOutput? UrlOutputOf(CommandLineOptions options, int index) =>
        index < options.UrlOutputs.Count ? options.UrlOutputs[index] : null;

    /// <summary>
    /// Tells whether the URL at <paramref name="index" /> saves its body to a file: an <c>-o</c>
    /// name other than <c>-</c>, or the remote name.
    /// </summary>
    /// <param name="options">The accepted command line.</param>
    /// <param name="index">The URL's position on the command line.</param>
    /// <returns><see langword="true" /> when the body goes to a file.</returns>
    private static bool WritesToFile(CommandLineOptions options, int index) =>
        UrlOutputOf(options, index) is { FileName: not null and not UrlTransfer.StandardOutputFileName } or { UsesRemoteName: true };

    /// <summary>
    /// Tells whether any URL after the one at <paramref name="index" /> sends its body to
    /// standard output.
    /// </summary>
    /// <param name="options">The accepted command line.</param>
    /// <param name="index">The URL's position on the command line.</param>
    /// <returns><see langword="true" /> when a later URL writes to standard output.</returns>
    private static bool LaterUrlWritesToStandardOutput(CommandLineOptions options, int index)
    {
        for (int later = index + 1; later < options.Urls.Count; later++)
        {
            if (!WritesToFile(options, later))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Tells whether the <c>-w</c> output of the URL at <paramref name="index" /> reaches
    /// standard output in binary mode, its line feeds unchanged.
    /// </summary>
    /// <param name="options">The accepted command line.</param>
    /// <param name="index">The URL's position on the command line.</param>
    /// <param name="bodyWrittenToStandardOutput">Whether this or an earlier transfer sent its body to standard output.</param>
    /// <param name="endsTheRun">Whether this transfer is the run's last.</param>
    /// <returns>
    /// Always <see langword="true" /> off Windows. On Windows, <see langword="true" /> when this or
    /// an earlier transfer sent its body to standard output, or when the run goes on and a later
    /// URL has no <c>-o</c> or remote name: curl 8.21.0 holds the <c>-w</c> text in its stdio buffer, and the
    /// later transfer switches standard output to binary before the buffer is written out.
    /// Measured on 2026-09-26: <c>-o NUL -o NUL -w "%{exitcode}\n" f f g</c>, with <c>g</c> to
    /// standard output, writes <c>0\n0\n</c> before <c>g</c>'s body. A later transfer whose
    /// resumed <c>-o</c> file cannot be opened stops the run before that URL, which curl reports as
    /// CR LF and this reports as LF; that case is not modelled.
    /// </returns>
    private bool IsStandardOutputBinaryForWriteOut(
        CommandLineOptions options,
        int index,
        bool bodyWrittenToStandardOutput,
        bool endsTheRun) =>
        !runsOnWindows
        || bodyWrittenToStandardOutput
        || (!endsTheRun && LaterUrlWritesToStandardOutput(options, index));

    /// <summary>
    /// Renders the <c>-w</c> template, when one was given, for the finished
    /// <paramref name="transfer" />, after taking its connection number: its standard output goes where the body does, or nowhere once
    /// standard output has failed, while the rest of the template still renders, as curl 8.21.0
    /// prints <c>-w "A%{exitcode}%{stderr}B%{exitcode}\n"</c> to a closed standard output as
    /// <c>B23</c> on standard error (measured 2026-09-26); and on Windows
    /// the line feeds written to standard error, and to standard output while it is in text mode,
    /// become CR LF.
    /// </summary>
    /// <param name="options">The accepted command line.</param>
    /// <param name="transfer">
    /// The transfer, whose command-line position <c>%{urlnum}</c> prints and whose number
    /// <c>%{xfer_id}</c> prints.
    /// </param>
    /// <param name="givenUrl">
    /// The URL <c>%{url}</c> prints: as the glob expanded it, or the gateway URL of a rewritten IPFS URL.
    /// </param>
    /// <param name="transferUrl">
    /// The URL transferred: with its guessed scheme, or for a <c>-T</c> upload as
    /// <see cref="UploadTransferUrl" /> resolved it, empty when it could not.
    /// </param>
    /// <param name="result">The transfer's result.</param>
    /// <param name="standardOutputIsBinary">Whether standard output keeps its line feeds.</param>
    /// <returns>A task that completes when the template is rendered.</returns>
    /// <remarks>
    /// Every URL a glob expands to is a transfer of its own, so <c>%{xfer_id}</c> counts them while
    /// <c>%{urlnum}</c> counts command-line URLs, as curl 8.21.0 printed xfer_id <c>0 1 2</c> and
    /// urlnum <c>0 0 1</c> for <c>{a,b}</c> then a plain URL (measured 2026-09-27, BL-240 Notes).
    /// An IPFS URL that could not be rewritten never became a transfer, so its <c>%{xfer_id}</c> and
    /// <c>%{conn_id}</c> are <c>-1</c>.
    /// </remarks>
    private async Task WriteOutAsync(
        CommandLineOptions options,
        UrlTransfer transfer,
        string givenUrl,
        string transferUrl,
        TransferResult result,
        bool standardOutputIsBinary)
    {
        long connectionId = TakeConnectionId(result);
        if (options.WriteOut is not { } template)
        {
            return;
        }

        string requestUrl = UrlRootPath.AddToEmptyPath(QueryUrl.Append(transferUrl, options));
        TransferWriteOutVariables variables = new(
            result, givenUrl, transfer.UrlIndex, requestUrl, WriteOutScheme(requestUrl, result), timeProvider)
        {
            Referer = options.Referer,
            OutputFileName = transferOutputFileName,
            ConnectionId = connectionId,
            TransferId = HasNoTransferNumber(result) ? NoTransferId : transfer.TransferId,
        };

        Stream liveStandardOutput = deferringStandardOutput.HasWriteFailed ? Stream.Null : deferringStandardOutput;
        Stream writeOutStandardOutput = standardOutputIsBinary
            ? liveStandardOutput
            : new LineFeedToCrLfStream(liveStandardOutput);
        Stream writeOutStandardError = runsOnWindows ? new LineFeedToCrLfStream(standardError) : standardError;
        await writeOutRenderer
            .RenderAsync(template, variables, writeOutStandardOutput, writeOutStandardError)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Writes the run's <c>-c</c> jar after the transfer of <paramref name="transferUrl" />,
    /// when there is a jar and the URL is <c>http</c> or <c>https</c>, whatever the transfer's
    /// outcome: curl 8.21.0 writes it after every HTTP transfer, after its <c>-w</c> output, and
    /// not after a <c>file</c> or <c>dict</c> one (measured 2026-09-26, BL-237 Notes). The jar
    /// file is replaced each time; <c>-c -</c> prints the jar once per HTTP transfer, in the mode
    /// standard output is in, and not at all once standard output has failed.
    /// </summary>
    /// <param name="dispatch">What the run transfers through, with its cookies.</param>
    /// <param name="options">The accepted command line.</param>
    /// <param name="transferUrl">The URL transferred, as typed or as a <c>-T</c> upload resolved it.</param>
    /// <param name="standardOutputIsBinary">Whether standard output keeps its line feeds.</param>
    /// <returns>A task that completes when the jar is written.</returns>
    private async Task WriteCookieJarAsync(TransferDispatch dispatch, CommandLineOptions options, string transferUrl, bool standardOutputIsBinary)
    {
        if (dispatch.Cookies is not { } cookies || !IsHttpUrl(QueryUrl.Append(transferUrl, options)))
        {
            return;
        }

        Stream liveStandardOutput = deferringStandardOutput.HasWriteFailed ? Stream.Null : deferringStandardOutput;
        Stream jarStandardOutput = standardOutputIsBinary ? liveStandardOutput : new LineFeedToCrLfStream(liveStandardOutput);
        await cookies.WriteCookieJarAsync(fileSystem, jarStandardOutput, timeProvider.GetUtcNow()).ConfigureAwait(false);
    }

    /// <summary>
    /// Loads the run's <c>-b</c> files before its first <c>http</c> or <c>https</c> transfer and
    /// not before any other: curl 8.21.0 reads <c>-b -</c> from standard input only then, so a
    /// <c>telnet</c> transfer or <c>file</c> upload before it gets standard input and the
    /// <c>-b -</c> after it finds it empty, and a <c>-T -</c> upload in the same transfer finds it
    /// empty (measured 2026-09-27, BL-316 Notes).
    /// </summary>
    /// <param name="dispatch">What the run transfers through, with its cookies.</param>
    /// <param name="options">The accepted command line.</param>
    /// <param name="transferUrl">The URL about to be transferred.</param>
    /// <returns>A task that completes when the files are loaded.</returns>
    private async Task LoadCookieFilesAsync(TransferDispatch dispatch, CommandLineOptions options, string transferUrl)
    {
        if (dispatch.Cookies is { } cookies && IsHttpUrl(QueryUrl.Append(transferUrl, options)))
        {
            await cookies.LoadCookieFilesAsync(fileSystem, standardInput, timeProvider.GetUtcNow()).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Tells whether <paramref name="requestUrl" /> is an absolute <c>http</c> or <c>https</c> URL.
    /// </summary>
    /// <param name="requestUrl">The URL, with any <c>-G</c> / <c>--url-query</c> query.</param>
    /// <returns><see langword="true" /> for an HTTP URL.</returns>
    private static bool IsHttpUrl(string requestUrl) =>
        CurlUrl.TryParse(requestUrl, pathAsIs: false, out CurlUrl? url)
        && url.Scheme is "http" or "https";

    /// <summary>
    /// The <c>%{conn_id}</c> of a transfer that used no connection, as curl 8.21.0 prints it.
    /// </summary>
    private const long NoConnectionId = -1;

    /// <summary>The <c>%{xfer_id}</c> of what never became a transfer.</summary>
    private const long NoTransferId = -1;

    /// <summary>
    /// Tells whether a transfer got as far as a connection of its own: every transfer does
    /// but one whose URL curl rejects before connecting, with exit 1 or 3. curl 8.21.0 printed
    /// <c>conn_id</c> <c>0</c> for a refused connection (exit 7) and <c>-1</c> for a rejected
    /// URL (exit 3); no connection is reused, so each transfer that connects takes the next number
    /// (measured 2026-09-26, BL-284 Notes).
    /// </summary>
    /// <param name="result">The transfer's result.</param>
    /// <returns><see langword="true" /> when the transfer numbers a connection.</returns>
    private static bool UsedAConnection(TransferResult result) =>
        result.ExitCode is not (CurlExitCode.UnsupportedProtocol or CurlExitCode.UrlMalformat)
        && !IsIpfsGatewayFailure(result);

    /// <summary>
    /// Gives a finished transfer its <c>%{conn_id}</c>: the next connection number when it
    /// <see cref="UsedAConnection" />, else <see cref="NoConnectionId" />.
    /// </summary>
    /// <param name="result">The transfer's result.</param>
    /// <returns>The number <c>%{conn_id}</c> prints.</returns>
    private long TakeConnectionId(TransferResult result) =>
        UsedAConnection(result) ? nextConnectionId++ : NoConnectionId;

    /// <summary>
    /// The scheme <c>%{scheme}</c> prints: the URL's, in lower case, or <see langword="null" />
    /// (printed as nothing) when the URL cannot be parsed or no handler serves its scheme, as
    /// curl 8.21.0 prints it for <c>dict://exa mple.com/</c> and <c>xyz://a/b</c>.
    /// </summary>
    /// <param name="requestUrl">The URL, with any <c>-G</c> / <c>--url-query</c> query.</param>
    /// <param name="result">The transfer's result.</param>
    /// <returns>The scheme, or <see langword="null" />.</returns>
    private static string? WriteOutScheme(string requestUrl, TransferResult result) =>
        result.ExitCode != CurlExitCode.UnsupportedProtocol
        && CurlUrl.TryParse(requestUrl, pathAsIs: false, out CurlUrl? url)
            ? url.Scheme
            : null;

    /// <summary>
    /// Tells whether a transfer's result stops the URLs after it: a resumed transfer whose
    /// <c>-o</c> file cannot be opened, a transfer whose <c>-D</c> or <c>-T</c> file cannot be
    /// opened or whose <c>--create-dirs</c> directory cannot be created, an IPFS URL that cannot
    /// be rewritten or asked for its remote name, and, under <c>--fail-early</c>, any failed
    /// transfer, as curl 8.21.0 does.
    /// </summary>
    /// <param name="options">The accepted command line.</param>
    /// <param name="result">The transfer's result.</param>
    /// <returns><see langword="true" /> when no further URL is transferred.</returns>
    private static bool EndsTheRun(CommandLineOptions options, TransferResult result) =>
        RunEndingFailures.Contains(result) || (options.FailEarly && !result.IsSuccess);

    /// <summary>
    /// The results, compared by reference, after which no further URL is transferred whatever
    /// the options: a resumed <c>-o</c> file, a <c>-T</c> or <c>-D</c> file that cannot be opened,
    /// a <c>--create-dirs</c> directory that cannot be created, and an IPFS URL that cannot be
    /// rewritten or asked for its remote name.
    /// </summary>
    private static readonly HashSet<TransferResult> RunEndingFailures = new(ReferenceEqualityComparer.Instance)
    {
        CannotOpenForResumeFailure,
        CannotOpenUploadFileFailure,
        CannotOpenHeaderFileFailure,
        CannotCreateDirectoryFailure,
        IpfsGatewayDetectionFailure,
        IpfsMalformedTargetUrlFailure,
        IpfsRemoteNameFailure,
    };

    /// <summary>
    /// Writes the five lines curl 8.21.0's command-line tool prints on standard error after
    /// the <c>curl: (60)</c> line (its <c>CURL_CA_CERT_ERRORMSG</c>), identical in the Schannel
    /// and OpenSSL builds (ADR-0009), each followed by <see cref="Environment.NewLine" />.
    /// Printed only where the error line is, so <c>-s</c> without <c>-S</c> prints neither.
    /// </summary>
    /// <returns>A task that completes when the lines are flushed.</returns>
    private async Task WriteCertificateHelpBlockAsync()
    {
        string newLine = Environment.NewLine;
        string text =
            "More details here: https://curl.se/docs/sslcerts.html" + newLine
            + newLine
            + "curl failed to verify the legitimacy of the server and therefore could not" + newLine
            + "establish a secure connection to it. To learn more about this situation and" + newLine
            + "how to fix it, please visit the webpage mentioned above." + newLine;

        byte[] bytes = Encoding.UTF8.GetBytes(text);
        await standardError.WriteAsync(bytes).ConfigureAwait(false);
        await standardError.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Tells whether failure lines are printed: always, unless <c>-s</c> was given without <c>-S</c>.
    /// </summary>
    /// <param name="options">The accepted command line.</param>
    /// <returns><see langword="true" /> when failure lines are printed.</returns>
    private static bool ShowsErrors(CommandLineOptions options) => !options.Silent || options.ShowError;

    /// <summary>
    /// Formats the line printed for a failed transfer that carries a message.
    /// </summary>
    /// <param name="result">The failed transfer's result.</param>
    /// <returns>
    /// <see cref="FailedWritingBodyLine" /> when standard output could not be written;
    /// otherwise <c>curl: (N) &lt;message&gt;</c>.
    /// </returns>
    private static string FormatErrorLine(TransferResult result)
    {
        if (ReferenceEquals(result, StandardOutputWriteFailure))
        {
            return FailedWritingBodyLine;
        }

        string code = ((int)result.ExitCode).ToString(CultureInfo.InvariantCulture);

        return $"curl: ({code}) {result.ErrorMessage}";
    }

    /// <summary>
    /// Performs <paramref name="transfer" /> with its <c>-o</c> file,
    /// sending its header lines where <c>-D</c> says: nowhere without <c>-D</c>, standard
    /// output for <c>-D -</c>, otherwise the named file.
    /// </summary>
    /// <param name="dispatch">Performs the transfer with the handler for its scheme, after its warning lines.</param>
    /// <param name="options">The accepted command line.</param>
    /// <param name="transfer">The transfer, which names its output.</param>
    /// <param name="url">The URL to transfer: with its guessed scheme, or for a <c>-T</c> upload as <see cref="UploadTransferUrl" /> resolved it.</param>
    /// <param name="uploadFile">The URL's <c>-T</c> file, or <see langword="null" /> when it uploads nothing.</param>
    /// <returns>
    /// The transfer's result; <see cref="CannotOpenHeaderFileFailure" />, with nothing
    /// transferred, when the <c>-D</c> file cannot be opened.
    /// </returns>
    /// <remarks>
    /// The <c>-D</c> file is opened before the transfer starts and closed after it, as curl
    /// 8.21.0 does: truncated for the first transfer and appended to for each one after it, even
    /// one of the same glob (measured 2026-09-27, BL-240 Notes), and
    /// never renamed by <see cref="WindowsOutputFileNameSanitizer" />, which curl applies to
    /// <c>-o</c> names only.
    /// </remarks>
    private async Task<TransferResult> TransferWithHeaderOutputAsync(
        TransferDispatch dispatch,
        CommandLineOptions options,
        UrlTransfer transfer,
        string url,
        string? uploadFile)
    {
        string? headerFile = options.DumpHeaderFile;

        if (headerFile is null)
        {
            return await TransferAsync(dispatch, options, url, uploadFile, transfer, null).ConfigureAwait(false);
        }

        if (headerFile == StandardOutputHeaderFile)
        {
            return await TransferReportingHeaderWriteFailureAsync(dispatch, options, url, uploadFile, transfer, headerFile, standardOutput)
                .ConfigureAwait(false);
        }

        return await TransferWithHeaderFileAsync(dispatch, options, url, uploadFile, transfer, headerFile)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Opens the <c>-D</c> file (truncated for the first transfer, appended to after it), performs
    /// the transfer with its header lines going there, and closes the file; reports the file
    /// when it cannot be opened.
    /// </summary>
    /// <param name="dispatch">Performs the transfer with the handler for its scheme, after its warning lines.</param>
    /// <param name="options">The accepted command line.</param>
    /// <param name="url">The URL to transfer.</param>
    /// <param name="uploadFile">The URL's <c>-T</c> file, or <see langword="null" /> when it uploads nothing.</param>
    /// <param name="transfer">The transfer, which names its output.</param>
    /// <param name="headerFile">The <c>-D</c> file name, used as given.</param>
    /// <returns>
    /// The transfer's result; <see cref="CannotOpenHeaderFileFailure" />, with nothing
    /// transferred, when the <c>-D</c> file cannot be opened.
    /// </returns>
    private async Task<TransferResult> TransferWithHeaderFileAsync(
        TransferDispatch dispatch,
        CommandLineOptions options,
        string url,
        string? uploadFile,
        UrlTransfer transfer,
        string headerFile)
    {
        FileOpenResult opened = await fileSystem
            .OpenForWriteAsync(
                headerFile,
                transfer.TransferId == 0 ? FileWriteMode.Truncate : FileWriteMode.Append,
                DeferredOutputFileStream.CreateMode,
                CancellationToken.None)
            .ConfigureAwait(false);

        if (opened.Content is not { } headerStream)
        {
            return await ReportCannotOpenHeaderFileAsync(options, headerFile).ConfigureAwait(false);
        }

        await using (headerStream.ConfigureAwait(false))
        {
            return await TransferReportingHeaderWriteFailureAsync(dispatch, options, url, uploadFile, transfer, headerFile, headerStream)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Performs the transfer with its header lines going to <paramref name="destination" />
    /// through a <see cref="DumpHeaderOutputStream" />, and when a header write failed prints
    /// curl's <c>curl: Failed writing headers to &lt;file&gt;</c> line, naming the <c>-D</c>
    /// value as given, unless <c>-s</c> was given without <c>-S</c>; curl 8.21.0 prints it
    /// before the transfer's <c>curl: (23)</c> line (measured 2026-09-26, BL-111 Notes).
    /// </summary>
    /// <param name="dispatch">Performs the transfer with the handler for its scheme, after its warning lines.</param>
    /// <param name="options">The accepted command line.</param>
    /// <param name="url">The URL to transfer.</param>
    /// <param name="uploadFile">The URL's <c>-T</c> file, or <see langword="null" /> when it uploads nothing.</param>
    /// <param name="transfer">The transfer, which names its output.</param>
    /// <param name="headerFile">The <c>-D</c> value, as given.</param>
    /// <param name="destination">Standard output for <c>-D -</c>, otherwise the opened <c>-D</c> file.</param>
    /// <returns>The transfer's result.</returns>
    private async Task<TransferResult> TransferReportingHeaderWriteFailureAsync(
        TransferDispatch dispatch,
        CommandLineOptions options,
        string url,
        string? uploadFile,
        UrlTransfer transfer,
        string headerFile,
        Stream destination)
    {
        DumpHeaderOutputStream headerOutput = new(destination);
        TransferResult result = await TransferAsync(dispatch, options, url, uploadFile, transfer, headerOutput).ConfigureAwait(false);

        if (headerOutput.HasWriteFailed && ShowsErrors(options))
        {
            await WriteErrorLineAsync($"curl: Failed writing headers to {headerFile}").ConfigureAwait(false);
        }

        return result;
    }

    /// <summary>
    /// Prints curl's <c>curl: Failed to open &lt;file&gt;</c> line for a <c>-D</c> file that
    /// could not be opened, unless <c>-s</c> was given without <c>-S</c>.
    /// </summary>
    /// <param name="options">The accepted command line.</param>
    /// <param name="headerFile">The <c>-D</c> value.</param>
    /// <returns><see cref="CannotOpenHeaderFileFailure" />: exit 23 with <see cref="WriteReceivedDataFailedMessage" />.</returns>
    private async Task<TransferResult> ReportCannotOpenHeaderFileAsync(CommandLineOptions options, string headerFile)
    {
        if (ShowsErrors(options))
        {
            await WriteErrorLineAsync($"curl: Failed to open {headerFile}").ConfigureAwait(false);
        }

        return CannotOpenHeaderFileFailure;
    }

    /// <summary>
    /// Performs one transfer, to the file <paramref name="transfer" /> names when it names one and to
    /// standard output otherwise.
    /// </summary>
    /// <param name="dispatch">Performs the transfer with the handler for its scheme, after its warning lines.</param>
    /// <param name="options">The accepted command line.</param>
    /// <param name="url">
    /// The URL to transfer; the <c>-G</c> / <c>--url-query</c> query is appended by
    /// <see cref="QueryUrl" /> before it is parsed.
    /// </param>
    /// <param name="uploadFile">
    /// The URL's <c>-T</c> file, or <see langword="null" /> when it uploads nothing. <c>-</c> and
    /// <c>.</c> upload standard input; any other is opened after the warning lines, and closed when
    /// the transfer ends.
    /// </param>
    /// <param name="transfer">
    /// The transfer, which names its output; the file is resolved by
    /// <see cref="ResolveOutputFileAsync" />.
    /// </param>
    /// <param name="headerOutput">Where the <c>-D</c> header lines go, or <see langword="null" /> without <c>-D</c>.</param>
    /// <returns>
    /// The transfer's result; <see cref="ByteRangeParser.NotDeliveredFailure" />, with nothing
    /// transferred, when the <c>-r</c> text names no range, as curl 8.21.0 reports it; the
    /// <c>-F</c> body's build failure, with nothing transferred, when a form file cannot be opened;
    /// <see cref="CannotOpenUploadFileFailure" />, with nothing transferred, when the <c>-T</c> file
    /// cannot be opened, after curl's <c>curl: cannot open</c> and try-help lines, which curl 8.21.0
    /// prints even under <c>-s</c> (measured 2026-09-27, BL-030).
    /// </returns>
    private async Task<TransferResult> TransferAsync(
        TransferDispatch dispatch,
        CommandLineOptions options,
        string url,
        string? uploadFile,
        UrlTransfer transfer,
        Stream? headerOutput)
    {
        if (!options.Silent)
        {
            await WriteErrorLinesAsync(dispatch.WarningLinesBeforeEachTransfer).ConfigureAwait(false);
        }

        if (uploadFile is null || UploadUrl.IsStandardInput(uploadFile))
        {
            Stream? standardInputUpload = uploadFile is null ? null : standardInput;
            return await TransferUploadingAsync(dispatch, options, url, standardInputUpload, transfer, headerOutput)
                .ConfigureAwait(false);
        }

        FileOpenResult opened = await fileSystem.OpenForReadAsync(uploadFile, CancellationToken.None).ConfigureAwait(false);
        if (opened.Content is not { } upload)
        {
            await WriteErrorLinesAsync([$"curl: cannot open '{uploadFile}'", CommandLineRefusal.TryHelpLine])
                .ConfigureAwait(false);
            return CannotOpenUploadFileFailure;
        }

        await using (upload.ConfigureAwait(false))
        {
            return await TransferUploadingAsync(dispatch, options, url, upload, transfer, headerOutput)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Performs one transfer that sends <paramref name="upload" /> when given, to
    /// the file <paramref name="transfer" /> names when it names one and to standard output otherwise.
    /// </summary>
    /// <param name="dispatch">Performs the transfer with the handler for its scheme.</param>
    /// <param name="options">The accepted command line.</param>
    /// <param name="url">
    /// The URL to transfer; the <c>-G</c> / <c>--url-query</c> query is appended by
    /// <see cref="QueryUrl" /> before it is parsed.
    /// </param>
    /// <param name="upload">
    /// The <c>-T</c> source, or <see langword="null" /> without <c>-T</c>, when a <c>telnet</c>
    /// transfer still uploads standard input.
    /// </param>
    /// <param name="transfer">The transfer, which names its output.</param>
    /// <param name="headerOutput">Where the <c>-D</c> header lines go, or <see langword="null" /> without <c>-D</c>.</param>
    /// <returns>The transfer's result, as <see cref="TransferAsync" /> describes it.</returns>
    private async Task<TransferResult> TransferUploadingAsync(
        TransferDispatch dispatch,
        CommandLineOptions options,
        string url,
        Stream? upload,
        UrlTransfer transfer,
        Stream? headerOutput)
    {
        RedirectFollower follower = new(
            dispatch.Dispatcher,
            (CurlUrl hopUrl, out ProxyEndpoint? hopProxy, [NotNullWhen(false)] out TransferResult? hopFailure) =>
                TransferProxySelection.TrySelect(dispatch.ProxySelector, options, hopUrl, out hopProxy, out hopFailure));

        if (!CurlUrl.TryParse(
            QueryUrl.Append(url, options),
            options.PathAsIs,
            out CurlUrl? transferUrl,
            out CurlUrlRejection rejection))
        {
            return TransferResult.Failure(CurlExitCode.UrlMalformat, UrlRejectedPrefix + rejection.ToCurlMessage());
        }

        if (Encoding.UTF8.GetByteCount(transferUrl.Host) > MaximumHostLength)
        {
            return TransferResult.Failure(CurlExitCode.UrlMalformat, TooLongHostnameMessage);
        }

        if (!TryParseRange(options.Range, out ByteRange? range))
        {
            return ByteRangeParser.NotDeliveredFailure;
        }

        if (options.FormParts.Count == 0)
        {
            return await TransferWithBodyAsync(
                    follower, dispatch.ProxySelector, options, transferUrl, transfer, range, headerOutput, null, upload)
                .ConfigureAwait(false);
        }

        MultipartFormBuildResult form = await formBodyBuilder
            .BuildAsync(MultipartFormPartMapping.FromCommandLine(options.FormParts), CancellationToken.None)
            .ConfigureAwait(false);
        if (!form.IsBuilt)
        {
            return form.Failure;
        }

        await using (form.Body.Content.ConfigureAwait(false))
        {
            return await TransferWithBodyAsync(
                    follower, dispatch.ProxySelector, options, transferUrl, transfer, range, headerOutput, form.Body, upload)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Performs one checked transfer, sending <paramref name="formBody" /> when given, to the file
    /// <see cref="ResolveOutputFileAsync" /> names when it names one and to standard output
    /// otherwise, and writes the progress meter after it. The transfer goes through the proxy
    /// <see cref="TransferProxySelection" /> chooses; when it refuses one, the transfer ends with
    /// its failure and nothing is sent.
    /// </summary>
    /// <param name="follower">Performs the transfer with the handler for its scheme, following redirects under <c>-L</c>.</param>
    /// <param name="proxySelector">Chooses the transfer's proxy from <c>-x</c>, <c>--noproxy</c> and the proxy environment variables.</param>
    /// <param name="options">The accepted command line.</param>
    /// <param name="url">The URL, with any <c>-G</c> / <c>--url-query</c> query.</param>
    /// <param name="transfer">The transfer, which names its output.</param>
    /// <param name="range">The parsed <c>-r</c> range, or <see langword="null" />.</param>
    /// <param name="headerOutput">Where the <c>-D</c> header lines go, or <see langword="null" /> without <c>-D</c>.</param>
    /// <param name="formBody">The <c>-F</c> body, or <see langword="null" /> without <c>-F</c>.</param>
    /// <param name="upload">The <c>-T</c> source, or <see langword="null" /> without <c>-T</c>.</param>
    /// <returns>
    /// The transfer's result; <see cref="CannotCreateDirectoryFailure" />, with nothing
    /// transferred, when a <c>--create-dirs</c> directory cannot be created.
    /// </returns>
    private async Task<TransferResult> TransferWithBodyAsync(
        RedirectFollower follower,
        ProxySelector proxySelector,
        CommandLineOptions options,
        CurlUrl url,
        UrlTransfer transfer,
        ByteRange? range,
        Stream? headerOutput,
        HttpRequestBody? formBody,
        Stream? upload)
    {
        uploadResumesFromUnknownOffset = options.ResumeFromOutputSize && upload is not null;
        if (!TransferProxySelection.TrySelect(proxySelector, options, url, out ProxyEndpoint? proxy, out TransferResult? proxyFailure))
        {
            return proxyFailure;
        }

        string? outputFile = await ResolveOutputFileAsync(options, transfer, url).ConfigureAwait(false);
        if (outputFile is null)
        {
            StartTransferProgress(options, options.ResumeFrom, toStandardOutput: true);
            TransferResult standardOutputResult = await TransferToStandardOutputAsync(
                    follower,
                    options,
                    () => transferContextFactory.Create(
                        options,
                        url,
                        RateLimited(options, deferringStandardOutput),
                        range,
                        options.ResumeFrom,
                        headerOutput,
                        formBody,
                        upload,
                        proxy,
                        progress: transferProgress,
                        events: transferEventOutput.Events))
                .ConfigureAwait(false);

            return await WriteProgressAsync(options, standardOutputResult, options.ResumeFrom, toStandardOutput: true)
                .ConfigureAwait(false);
        }

        if (options.CreateDirectories
            && OutputFileDirectories.CreateLeadingDirectories(OutputPaths, outputFile, runsOnWindows) is { } directory)
        {
            return await ReportCannotCreateDirectoryAsync(options, directory).ConfigureAwait(false);
        }

        long? resumeFrom = await ResolveResumeFromAsync(options, outputFile).ConfigureAwait(false);
        StartTransferProgress(options, resumeFrom, toStandardOutput: false);
        OutputFileTarget target = new(outputFile, TakesContentDispositionName(options, transfer));
        TransferResult fileResult = await TransferToOutputFileAsync(
                follower, options, url, target, range, resumeFrom, headerOutput, formBody, upload, proxy)
            .ConfigureAwait(false);

        return await WriteProgressAsync(options, fileResult, resumeFrom, toStandardOutput: false)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets what checks <c>-J</c> names and creates <c>--create-dirs</c> directories: the one the
    /// runner was given, else <see cref="DiskOutputPaths" />.
    /// </summary>
    private IOutputPaths OutputPaths => outputPaths ?? DiskOutputPaths;

    /// <summary>
    /// Works out the file a transfer saves its body to: the <c>-o</c> name, or the remote name
    /// <see cref="RemoteFileName" /> takes from the URL's path (printing
    /// <see cref="RemoteFileName.NoRemoteNameWarning" />, unless <c>-s</c> was given, when there is
    /// none), rewritten on Windows and put under <c>--output-dir</c>.
    /// </summary>
    /// <param name="options">The accepted command line.</param>
    /// <param name="transfer">The transfer, which names its output.</param>
    /// <param name="url">The URL.</param>
    /// <returns>The file, or <see langword="null" /> when the body goes to standard output.</returns>
    /// <remarks>
    /// The <c>-o</c> name comes from <see cref="UrlTransfer.OutputFileName" />, its <c>#N</c> already
    /// substituted and, on Windows, sanitized; on Windows a remote name goes through
    /// <see cref="WindowsOutputFileNameSanitizer.SanitizeRemoteName" />;
    /// the <c>--output-dir</c> text is kept as typed and joined with <c>/</c>, as curl 8.21.0 does
    /// (<c>--output-dir "o?d" -o x</c> fails on <c>o?d/x</c>, measured 2026-09-27, BL-239 Notes).
    /// </remarks>
    private async Task<string?> ResolveOutputFileAsync(CommandLineOptions options, UrlTransfer transfer, CurlUrl url)
    {
        if (transfer.OutputFileName is { } fileName)
        {
            return InOutputDirectory(options, fileName);
        }

        if (!transfer.UsesRemoteName)
        {
            return null;
        }

        string? remoteName = RemoteFileName.FromUrlPath(url.AbsolutePath);
        if (remoteName is null && !options.Silent)
        {
            await WriteErrorLineAsync(RemoteFileName.NoRemoteNameWarning).ConfigureAwait(false);
        }

        return RemoteNamePath(options, remoteName ?? RemoteFileName.Fallback);
    }

    /// <summary>
    /// Turns a remote file name, from the URL or a <c>Content-Disposition</c> header, into the
    /// path opened: rewritten on Windows, and put under <c>--output-dir</c>.
    /// </summary>
    /// <param name="options">The accepted command line.</param>
    /// <param name="remoteName">The file name, without a directory.</param>
    /// <returns>The path.</returns>
    private string RemoteNamePath(CommandLineOptions options, string remoteName) =>
        InOutputDirectory(options, runsOnWindows ? WindowsOutputFileNameSanitizer.SanitizeRemoteName(remoteName) : remoteName);

    /// <summary>
    /// Puts <paramref name="fileName" /> under the <c>--output-dir</c> directory, when one was given.
    /// </summary>
    /// <param name="options">The accepted command line.</param>
    /// <param name="fileName">The file name.</param>
    /// <returns><c>&lt;dir&gt;/&lt;fileName&gt;</c>, or <paramref name="fileName" /> without <c>--output-dir</c>.</returns>
    private static string InOutputDirectory(CommandLineOptions options, string fileName) =>
        options.OutputDirectory is { } directory ? directory + "/" + fileName : fileName;

    /// <summary>
    /// Tells whether a <c>Content-Disposition</c> header may rename the transfer's file: under
    /// <c>-J</c>, for a URL saved under its remote name and not an <c>-o</c> one, as measured on
    /// curl 8.21.0 (<c>-J -o o.txt</c> writes <c>o.txt</c>, BL-239 Notes).
    /// </summary>
    /// <param name="options">The accepted command line.</param>
    /// <param name="transfer">The transfer, which names its output.</param>
    /// <returns><see langword="true" /> when the header's name is used.</returns>
    private static bool TakesContentDispositionName(CommandLineOptions options, UrlTransfer transfer) =>
        options.RemoteHeaderName && transfer.OutputFileName is null;

    /// <summary>
    /// Prints curl's <c>curl: Error creating directory &lt;dir&gt;</c> line for a
    /// <c>--create-dirs</c> directory that could not be created, unless <c>-s</c> was given
    /// without <c>-S</c>, as curl 8.21.0 does (measured 2026-09-27, BL-239 Notes).
    /// </summary>
    /// <param name="options">The accepted command line.</param>
    /// <param name="directory">The directory, as a leading part of the output path.</param>
    /// <returns><see cref="CannotCreateDirectoryFailure" />.</returns>
    private async Task<TransferResult> ReportCannotCreateDirectoryAsync(CommandLineOptions options, string directory)
    {
        if (ShowsErrors(options))
        {
            await WriteErrorLineAsync($"curl: Error creating directory {directory}").ConfigureAwait(false);
        }

        return CannotCreateDirectoryFailure;
    }

    /// <summary>
    /// Makes the current transfer's progress sink, on the runner's clock, with a
    /// <see cref="ProgressBarRecorder" /> for <see cref="progressBar" /> when
    /// <see cref="ShowsProgressBar" /> says the bar is shown.
    /// </summary>
    /// <param name="options">The accepted command line.</param>
    /// <param name="resumeFrom">The resolved <c>-C</c> offset, or <see langword="null" />.</param>
    /// <param name="toStandardOutput">Whether the transfer writes its body to standard output.</param>
    private void StartTransferProgress(CommandLineOptions options, long? resumeFrom, bool toStandardOutput)
    {
        progressBar = ShowsProgressBar(options, toStandardOutput)
            ? new ProgressBarRecorder(timeProvider, resumeFrom ?? 0, terminalColumns)
            : null;
        transferProgress = new TransferProgressRecorder(timeProvider, progressBar);
    }

    /// <summary>
    /// Writes curl's progress for a finished transfer. Under <c>-#</c>, when
    /// <see cref="progressBar" /> is set, that is everything the bar drew, the last call of a
    /// successful transfer included, and no newline: curl writes that after the transfer's
    /// failure lines (task BL-132). Otherwise it is the progress meter, when
    /// <see cref="ShowsProgressMeter" /> says it is shown: its header lines, then the status
    /// lines <see cref="transferProgress" /> drew, each starting with a carriage return, then
    /// one newline, as curl 8.21.0 does (task BL-131); under <c>-L</c>, one such line per hop
    /// (task BL-277). Either is written after the
    /// transfer, so its bytes are curl's but a terminal does not see it move.
    /// </summary>
    /// <param name="options">The accepted command line.</param>
    /// <param name="result">
    /// The transfer's result; the meter follows a success, a <c>-f</c> failure
    /// (<see cref="CurlExitCode.HttpReturnedError" />), and any failure after the handler
    /// reported the transfer past connect or open (<see cref="transferProgress" />), which
    /// curl 8.21.0 reports after the meter (task BL-130).
    /// </param>
    /// <param name="resumeFrom">The resolved <c>-C</c> offset, or <see langword="null" />.</param>
    /// <param name="toStandardOutput">Whether the transfer wrote its body to standard output.</param>
    /// <returns><paramref name="result" />, unchanged.</returns>
    private async Task<TransferResult> WriteProgressAsync(
        CommandLineOptions options,
        TransferResult result,
        long? resumeFrom,
        bool toStandardOutput)
    {
        if (progressBar is not null)
        {
            progressBar.Finish(result.IsSuccess, result.BytesTransferred);
            await WriteErrorTextAsync(progressBar.Drawn).ConfigureAwait(false);

            return result;
        }

        if ((result.IsSuccess || result.ExitCode == CurlExitCode.HttpReturnedError || transferProgress.HasTransferStarted)
            && ShowsProgressMeter(options, toStandardOutput))
        {
            FinishTransferProgress(result);
            await WriteErrorLinesAsync([.. TakeProgressMeterHeaderLines(resumeFrom), transferProgress.StatusLines])
                .ConfigureAwait(false);
        }

        return result;
    }

    /// <summary>
    /// Makes the draws curl 8.21.0 makes as <see cref="transferProgress" />'s transfer ends: a
    /// redirect hop's when <c>--max-redirs</c> refused to follow its redirect (exit 47), which
    /// curl draws like a followed hop (task BL-277), and the finished transfer's otherwise.
    /// </summary>
    /// <param name="result">The transfer's result.</param>
    private void FinishTransferProgress(TransferResult result)
    {
        if (result.ExitCode == CurlExitCode.TooManyRedirects)
        {
            transferProgress.FinishRedirectHop();
        }
        else
        {
            transferProgress.Finish(result.IsSuccess);
        }
    }

    /// <summary>
    /// Gets the progress meter's header lines the first time the current transfer writes its
    /// meter, and none after: curl 8.21.0 writes them once however many times <c>--retry</c>
    /// runs the transfer (measured 2026-09-27, BL-241 Notes).
    /// </summary>
    /// <param name="resumeFrom">
    /// The resolved <c>-C</c> offset, or <see langword="null" />; a <c>-T</c> upload under
    /// <c>-C -</c> (<see cref="uploadResumesFromUnknownOffset" />) names
    /// <see cref="ProgressMeterLines.UnknownUploadOffset" /> instead.
    /// </param>
    /// <returns>The header lines, or none when they were already written.</returns>
    private IReadOnlyList<string> TakeProgressMeterHeaderLines(long? resumeFrom)
    {
        long? meterResumeFrom = uploadResumesFromUnknownOffset ? ProgressMeterLines.UnknownUploadOffset : resumeFrom;
        IReadOnlyList<string> headerLines = progressMeterHeaderWritten ? [] : ProgressMeterLines.HeaderLines(meterResumeFrom);
        progressMeterHeaderWritten = true;

        return headerLines;
    }

    /// <summary>
    /// Tells whether a transfer's progress meter is shown: when <see cref="ShowsProgress" />
    /// says progress is shown and <c>-#</c> did not ask for the bar instead.
    /// </summary>
    /// <param name="options">The accepted command line.</param>
    /// <param name="toStandardOutput">Whether the transfer wrote its body to standard output.</param>
    /// <returns><see langword="true" /> when the meter is shown.</returns>
    private bool ShowsProgressMeter(CommandLineOptions options, bool toStandardOutput) =>
        ShowsProgress(options, toStandardOutput) && !options.ProgressBar;

    /// <summary>
    /// Tells whether a transfer's <c>-#</c> bar is shown: when <see cref="ShowsProgress" />
    /// says progress is shown and <c>-#</c> asked for the bar.
    /// </summary>
    /// <param name="options">The accepted command line.</param>
    /// <param name="toStandardOutput">Whether the transfer writes its body to standard output.</param>
    /// <returns><see langword="true" /> when the bar is shown.</returns>
    private bool ShowsProgressBar(CommandLineOptions options, bool toStandardOutput) =>
        ShowsProgress(options, toStandardOutput) && options.ProgressBar;

    /// <summary>
    /// Tells whether a transfer shows progress, the meter or the <c>-#</c> bar: only when this
    /// runner writes it, never under <c>-s</c> or <c>--no-progress-meter</c>, and not for a body
    /// written to standard output when that is a terminal, as curl 8.21.0 does.
    /// </summary>
    /// <param name="options">The accepted command line.</param>
    /// <param name="toStandardOutput">Whether the transfer writes its body to standard output.</param>
    /// <returns><see langword="true" /> when progress is shown.</returns>
    private bool ShowsProgress(CommandLineOptions options, bool toStandardOutput) =>
        writesProgressMeter
        && !options.Silent
        && !options.ProgressMeterOff
        && !(toStandardOutput && standardOutputIsTerminal);

    /// <summary>
    /// Parses the <c>-r</c> / <c>--range</c> text, when there is any.
    /// </summary>
    /// <param name="rangeText">The text, or <see langword="null" /> when <c>-r</c> was not given.</param>
    /// <param name="range">The range; <see langword="null" /> for the whole resource.</param>
    /// <returns>
    /// <see langword="false" /> when the text names no range, so the transfer must end with
    /// <see cref="ByteRangeParser.NotDeliveredFailure" />.
    /// </returns>
    private static bool TryParseRange(string? rangeText, out ByteRange? range)
    {
        range = null;

        return rangeText is null || ByteRangeParser.TryParse(rangeText, out range);
    }

    /// <summary>
    /// Works out the offset a transfer to <paramref name="outputFile" /> resumes from.
    /// </summary>
    /// <param name="options">The accepted command line.</param>
    /// <param name="outputFile">The file: the <c>-o</c> value, after <see cref="WindowsOutputFileNameSanitizer" /> on Windows.</param>
    /// <returns>
    /// For <c>-C -</c>, the size of <paramref name="outputFile" />, or <see langword="null" />
    /// when it cannot be opened, since curl 8.21.0 then transfers the whole resource; otherwise
    /// the <c>-C</c> offset, or <see langword="null" /> when <c>-C</c> was not given.
    /// </returns>
    private async Task<long?> ResolveResumeFromAsync(CommandLineOptions options, string outputFile)
    {
        if (!options.ResumeFromOutputSize)
        {
            return options.ResumeFrom;
        }

        FileOpenResult existing = await fileSystem
            .OpenForReadAsync(outputFile, CancellationToken.None)
            .ConfigureAwait(false);

        if (existing.Content is not { } content)
        {
            return null;
        }

        await content.DisposeAsync().ConfigureAwait(false);

        return existing.Length;
    }

    /// <summary>
    /// Performs one transfer whose output is the <paramref name="target" /> file.
    /// </summary>
    /// <param name="follower">Performs the transfer with the handler for its scheme, following redirects under <c>-L</c>.</param>
    /// <param name="options">The accepted command line.</param>
    /// <param name="url">The URL.</param>
    /// <param name="target">The file, as <see cref="ResolveOutputFileAsync" /> resolved it.</param>
    /// <param name="range">The parsed <c>-r</c> range, or <see langword="null" />.</param>
    /// <param name="resumeFrom">The resolved <c>-C</c> offset, or <see langword="null" />.</param>
    /// <param name="headerOutput">Where the <c>-D</c> header lines go, or <see langword="null" /> without <c>-D</c>.</param>
    /// <param name="formBody">The <c>-F</c> body, or <see langword="null" /> without <c>-F</c>.</param>
    /// <param name="upload">The <c>-T</c> source, or <see langword="null" /> without <c>-T</c>.</param>
    /// <param name="proxy">The proxy chosen for the transfer, or <see langword="null" /> to connect directly.</param>
    /// <returns>
    /// The transfer's result. A transfer that resumes past byte zero opens the file for
    /// appending before it starts, as curl 8.21.0 does; when that open fails the result is
    /// <see cref="ReportCannotOpenForResumeAsync" />'s, and nothing is transferred. Under
    /// <c>-R</c> a success then stamps the closed file, under the name <c>-J</c> gave it if it
    /// gave one, with the source's time.
    /// </returns>
    private async Task<TransferResult> TransferToOutputFileAsync(
        RedirectFollower follower,
        CommandLineOptions options,
        CurlUrl url,
        OutputFileTarget target,
        ByteRange? range,
        long? resumeFrom,
        Stream? headerOutput,
        HttpRequestBody? formBody,
        Stream? upload,
        ProxyEndpoint? proxy)
    {
        DeferredOutputFileStream output = CreateOutputFileStream(target.Path, resumeFrom);
        TransferResult completed = await TransferIntoOutputFileAsync(
                follower,
                options,
                url,
                output,
                HeaderOutputWatcher(options, target, output),
                range,
                resumeFrom,
                headerOutput,
                formBody,
                upload,
                proxy)
            .ConfigureAwait(false);

        transferOutputFileName = output.Path;
        if (options.RemoteTime && completed.IsSuccess && completed.SourceLastWriteTimeUtc is { } sourceLastWriteTimeUtc)
        {
            await StampOutputFileTimeAsync(options, output.Path, sourceLastWriteTimeUtc).ConfigureAwait(false);
        }

        return completed;
    }

    /// <summary>
    /// Creates the stream that opens <paramref name="path" /> on its first write: for appending
    /// when the transfer resumes past byte zero, as curl opens the file <c>"ab"</c> then, and
    /// truncated otherwise.
    /// </summary>
    /// <param name="path">The output file.</param>
    /// <param name="resumeFrom">The resolved <c>-C</c> offset, or <see langword="null" />.</param>
    /// <returns>The stream, not yet opened.</returns>
    private DeferredOutputFileStream CreateOutputFileStream(string path, long? resumeFrom) =>
        new(fileSystem, path, resumeFrom is > 0 ? FileWriteMode.Append : FileWriteMode.Truncate);

    /// <summary>
    /// Gives the wrapper that puts a <see cref="RemoteHeaderNameStream" /> in front of the
    /// transfer's header output, when <paramref name="target" /> takes its name from
    /// <c>Content-Disposition</c>.
    /// </summary>
    /// <param name="options">The accepted command line.</param>
    /// <param name="target">The output file as resolved before the transfer.</param>
    /// <param name="output">The output file's stream, which the header's name opens.</param>
    /// <returns>The wrapper, or <see langword="null" /> when the file keeps its name.</returns>
    private Func<Stream?, Stream>? HeaderOutputWatcher(
        CommandLineOptions options,
        OutputFileTarget target,
        DeferredOutputFileStream output) =>
        target.TakesContentDispositionName
            ? chosen => new RemoteHeaderNameStream(output, chosen, name => RemoteNamePath(options, name))
            : null;

    /// <summary>
    /// Sets <paramref name="outputFile" />'s last-write time to the source's for <c>-R</c>, and
    /// when that fails prints <see cref="RemoteTimeFailureWarning" />'s line unless <c>-s</c>
    /// was given; the transfer's exit code is unchanged either way.
    /// </summary>
    /// <param name="options">The accepted command line.</param>
    /// <param name="outputFile">The closed <c>-o</c> file.</param>
    /// <param name="sourceLastWriteTimeUtc">The source's last-write time.</param>
    /// <returns>A task that completes when the time is set or the warning written.</returns>
    /// <remarks>
    /// curl 8.21.0 (Windows, measured 2026-09-26) mutes the warning under <c>-s</c> and under
    /// <c>-s -S</c> alike: <c>-S</c> brings back error messages, not warnings. The line is the
    /// Windows form on every platform, for the reason <see cref="RemoteTimeFailureWarning" />
    /// gives.
    /// </remarks>
    private async Task StampOutputFileTimeAsync(
        CommandLineOptions options,
        string outputFile,
        DateTimeOffset sourceLastWriteTimeUtc)
    {
        if (!outputFileTimeSetter.TrySetLastWriteTimeUtc(outputFile, sourceLastWriteTimeUtc, out int errorCode)
            && !options.Silent)
        {
            await WriteErrorLineAsync(RemoteTimeFailureWarning.For(sourceLastWriteTimeUtc, errorCode))
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Performs one transfer into <paramref name="output" /> and closes the file.
    /// </summary>
    /// <param name="follower">Performs the transfer with the handler for its scheme, following redirects under <c>-L</c>.</param>
    /// <param name="options">The accepted command line.</param>
    /// <param name="url">The URL.</param>
    /// <param name="output">The output file, not yet opened; closed here.</param>
    /// <param name="watchHeaderOutput">
    /// Wraps the transfer's header output in a <see cref="RemoteHeaderNameStream" /> under
    /// <c>-J</c>, or <see langword="null" />.
    /// </param>
    /// <param name="range">The parsed <c>-r</c> range, or <see langword="null" />.</param>
    /// <param name="resumeFrom">The resolved <c>-C</c> offset, or <see langword="null" />.</param>
    /// <param name="headerOutput">Where the <c>-D</c> header lines go, or <see langword="null" /> without <c>-D</c>.</param>
    /// <param name="formBody">The <c>-F</c> body, or <see langword="null" /> without <c>-F</c>.</param>
    /// <param name="upload">The <c>-T</c> source, or <see langword="null" /> without <c>-T</c>.</param>
    /// <param name="proxy">The proxy chosen for the transfer, or <see langword="null" /> to connect directly.</param>
    /// <returns>The transfer's result, as <see cref="TransferToOutputFileAsync" /> describes it.</returns>
    private async Task<TransferResult> TransferIntoOutputFileAsync(
        RedirectFollower follower,
        CommandLineOptions options,
        CurlUrl url,
        DeferredOutputFileStream output,
        Func<Stream?, Stream>? watchHeaderOutput,
        ByteRange? range,
        long? resumeFrom,
        Stream? headerOutput,
        HttpRequestBody? formBody,
        Stream? upload,
        ProxyEndpoint? proxy)
    {
        await using (output.ConfigureAwait(false))
        {
            if (resumeFrom is > 0 && !await output.TryOpenNowAsync().ConfigureAwait(false))
            {
                return await ReportCannotOpenForResumeAsync(options, output.Path).ConfigureAwait(false);
            }

            TransferResult fileResult = await FollowRetryingAsync(
                    follower,
                    options,
                    () => transferContextFactory.Create(
                        options,
                        url,
                        RateLimited(options, output),
                        range,
                        resumeFrom,
                        headerOutput,
                        formBody,
                        upload,
                        proxy,
                        watchHeaderOutput,
                        transferProgress,
                        transferEventOutput.Events),
                    resumeFrom,
                    output)
                .ConfigureAwait(false);
            TransferResult completed = await output.CompleteAsync(fileResult).ConfigureAwait(false);

            if (!options.Silent && output.OpenFailureWarning is { } warning)
            {
                await WriteErrorLineAsync(warning).ConfigureAwait(false);
            }

            return completed;
        }
    }

    /// <summary>
    /// Prints curl's <c>curl: cannot open '&lt;path&gt;'</c> line for an <c>-o</c> file a
    /// resumed transfer could not open for appending, unless <c>-s</c> was given without
    /// <c>-S</c>.
    /// </summary>
    /// <param name="options">The accepted command line.</param>
    /// <param name="outputFile">The file: the <c>-o</c> value, after <see cref="WindowsOutputFileNameSanitizer" /> on Windows.</param>
    /// <returns><see cref="CannotOpenForResumeFailure" />: exit 23 with <see cref="WriteReceivedDataFailedMessage" />.</returns>
    private async Task<TransferResult> ReportCannotOpenForResumeAsync(CommandLineOptions options, string outputFile)
    {
        if (ShowsErrors(options))
        {
            await WriteErrorLineAsync($"curl: cannot open '{outputFile}'").ConfigureAwait(false);
        }

        return CannotOpenForResumeFailure;
    }

    /// <summary>
    /// Performs one transfer whose output is standard output, and flushes it.
    /// </summary>
    /// <param name="follower">Performs the transfer with the handler for its scheme, following redirects under <c>-L</c>.</param>
    /// <param name="options">The accepted command line, whose redirect options the follower applies.</param>
    /// <param name="createAttemptContext">
    /// Creates the context of each attempt, whose output is standard output, on the current
    /// <see cref="transferProgress" />.
    /// </param>
    /// <returns>
    /// <see cref="StandardOutputWriteFailure" /> when the handler succeeded but standard
    /// output failed, which is curl's failed flush at the end of the transfer; otherwise the
    /// handler's result, including its own write failure when curl's stdio buffer would have
    /// filled (see <see cref="StandardOutputFailureDeferringStream" />).
    /// </returns>
    private async Task<TransferResult> TransferToStandardOutputAsync(
        RedirectFollower follower,
        CommandLineOptions options,
        Func<TransferContext> createAttemptContext)
    {
        deferringStandardOutput.ClearWriteFailure();
        TransferResult result = await FollowRetryingAsync(follower, options, createAttemptContext, options.ResumeFrom, null)
            .ConfigureAwait(false);
        await deferringStandardOutput.FlushAsync().ConfigureAwait(false);

        return result.IsSuccess && deferringStandardOutput.HasWriteFailed ? StandardOutputWriteFailure : result;
    }

    /// <summary>
    /// Performs one transfer through <paramref name="follower" />, run again by
    /// <see cref="TransferRetrier" /> under <c>--retry</c> (<see cref="RetryPolicyMapping" />),
    /// each attempt on a context of its own.
    /// </summary>
    /// <param name="follower">Performs each attempt, following redirects under <c>-L</c>.</param>
    /// <param name="options">The accepted command line.</param>
    /// <param name="createAttemptContext">
    /// Creates an attempt's context, on the current <see cref="transferProgress" />; called
    /// once before the first attempt and again after each retried one's lines are written.
    /// </param>
    /// <param name="resumeFrom">The resolved <c>-C</c> offset, or <see langword="null" />.</param>
    /// <param name="outputFile">
    /// The <c>-o</c> file, cut back before each retry; <see langword="null" /> when the body goes
    /// to standard output, where every attempt's body stays, as curl 8.21.0 leaves it.
    /// </param>
    /// <returns>The result of the attempt not retried.</returns>
    private async Task<TransferResult> FollowRetryingAsync(
        RedirectFollower follower,
        CommandLineOptions options,
        Func<TransferContext> createAttemptContext,
        long? resumeFrom,
        DeferredOutputFileStream? outputFile)
    {
        RedirectPolicy redirectPolicy = RedirectPolicyMapping.FromCommandLine(options);
        TransferContext? firstContext = createAttemptContext();
        Task retryLinesWritten = Task.CompletedTask;
        TransferRetrier retrier = new(async _ =>
        {
            await retryLinesWritten.ConfigureAwait(false);
            TransferContext context = firstContext ?? createAttemptContext();
            firstContext = null;
            return await follower.FollowAsync(context, redirectPolicy).ConfigureAwait(false);
        });

        TransferResult result = await retrier
            .RunAsync(
                firstContext,
                RetryPolicyMapping.FromCommandLine(options),
                (attempt, warning) => retryLinesWritten = WriteRetryLinesAsync(options, attempt, warning, resumeFrom, outputFile),
                (_, warning) => retryLinesWritten = WriteRetryWarningAsync(options, warning))
            .ConfigureAwait(false);
        await retryLinesWritten.ConfigureAwait(false);

        return result;
    }

    /// <summary>
    /// Writes what curl 8.21.0 prints for an attempt <c>--retry</c> runs again, and readies the
    /// next attempt: the attempt's progress, its failure lines unless <c>-s</c> was given
    /// without <c>-S</c>, then the retry warning unless <c>-s</c> was given, whether or not with
    /// <c>-S</c> (measured 2026-09-27, BL-241 Notes). The <c>-o</c> file is then cut back and the
    /// next attempt gets fresh progress.
    /// </summary>
    /// <param name="options">The accepted command line.</param>
    /// <param name="attempt">The attempt's result.</param>
    /// <param name="warning">The retry warning, unwrapped.</param>
    /// <param name="resumeFrom">The resolved <c>-C</c> offset, or <see langword="null" />.</param>
    /// <param name="outputFile">The <c>-o</c> file, or <see langword="null" /> for standard output.</param>
    /// <returns>A task that completes when the lines are written.</returns>
    private async Task WriteRetryLinesAsync(
        CommandLineOptions options,
        TransferResult attempt,
        string warning,
        long? resumeFrom,
        DeferredOutputFileStream? outputFile)
    {
        bool toStandardOutput = outputFile is null;
        await WriteProgressAsync(options, attempt, resumeFrom, toStandardOutput).ConfigureAwait(false);
        if (ShowsErrors(options) && attempt.ErrorMessage is not null)
        {
            await WriteFailureLinesAsync(attempt).ConfigureAwait(false);
        }

        await WriteRetryWarningAsync(options, warning).ConfigureAwait(false);
        outputFile?.TruncateForRetry();
        StartTransferProgress(options, resumeFrom, toStandardOutput);
    }

    /// <summary>
    /// Writes a <see cref="TransferRetrier" /> warning line, wrapped as every <c>Warning: </c>
    /// line is, unless <c>-s</c> was given: curl 8.21.0 prints none under <c>-s</c> or
    /// <c>-sS</c>.
    /// </summary>
    /// <param name="options">The accepted command line.</param>
    /// <param name="warning">The warning, unwrapped.</param>
    /// <returns>A task that completes when the line is written, or at once under <c>-s</c>.</returns>
    private Task WriteRetryWarningAsync(CommandLineOptions options, string warning) =>
        options.Silent ? Task.CompletedTask : WriteErrorLineAsync(warning);

    /// <summary>
    /// Holds <paramref name="output" /> at the <c>--limit-rate</c> with a
    /// <see cref="RateLimitedStream" /> on the runner's clock; a rate of zero, like no rate,
    /// sets no limit, as it does to curl.
    /// </summary>
    /// <param name="options">The accepted command line.</param>
    /// <param name="output">The attempt's body output.</param>
    /// <returns>The limited stream, or <paramref name="output" /> when there is no limit.</returns>
    private Stream RateLimited(CommandLineOptions options, Stream output) =>
        options.LimitRate is > 0 and long bytesPerSecond
            ? new RateLimitedStream(output, bytesPerSecond, timeProvider)
            : output;

    /// <summary>
    /// Writes each of <paramref name="lines" /> to standard error, in order.
    /// </summary>
    /// <param name="lines">The lines, without terminators.</param>
    /// <returns>A task that completes when every line is flushed.</returns>
    private async Task WriteErrorLinesAsync(IReadOnlyList<string> lines)
    {
        foreach (string line in lines)
        {
            await WriteErrorLineAsync(line).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Writes <see cref="CurlVersionText" />'s lines for the running system to standard output as
    /// UTF-8, each followed by <see cref="Environment.NewLine" />, for <c>-V</c> / <c>--version</c>.
    /// </summary>
    /// <returns>A task that completes when the lines are flushed.</returns>
    private async Task WriteVersionLinesAsync()
    {
        StringBuilder text = new();
        foreach (string line in CurlVersionText.Lines(runsOnWindows, OperatingSystem.IsMacOS()))
        {
            text.Append(line).Append(Environment.NewLine);
        }

        byte[] bytes = Encoding.UTF8.GetBytes(text.ToString());
        await standardOutput.WriteAsync(bytes).ConfigureAwait(false);
        await standardOutput.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Writes a refused command line's lines and, where curl 8.21.0 prints it, the default config
    /// file's note: before the lines for a refusal curl meets at transfer setup
    /// (<see cref="CommandLineRefusal.FoundAtTransferSetup" />), after them for one it meets while
    /// reading the command line (measured 2026-09-27, BL-352).
    /// </summary>
    /// <param name="refusal">The refusal.</param>
    /// <param name="notedDefaultConfigFile">The default config file to announce, or <see langword="null" /> for none.</param>
    /// <returns>A task that completes when everything is flushed.</returns>
    private async Task WriteRefusalAsync(CommandLineRefusal refusal, string? notedDefaultConfigFile)
    {
        if (refusal.FoundAtTransferSetup)
        {
            await WriteDefaultConfigFileNoteAsync(notedDefaultConfigFile).ConfigureAwait(false);
            await WriteErrorLinesAsync(refusal.StandardErrorLines).ConfigureAwait(false);
        }
        else
        {
            await WriteErrorLinesAsync(refusal.StandardErrorLines).ConfigureAwait(false);
            await WriteDefaultConfigFileNoteAsync(notedDefaultConfigFile).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Writes curl 8.21.0's <c>Note: Read config file from '&lt;path&gt;'</c> for the default config
    /// file, wrapped at <c>terminalColumns</c>, when there is one to announce
    /// (<see cref="CommandLineParseResult.NotedDefaultConfigFile" />: one was read and <c>-v</c> or a
    /// <c>--trace</c> option is on); <c>-s</c> does not hide it (measured 2026-09-27, BL-243).
    /// </summary>
    /// <param name="path">The default config file to announce, or <see langword="null" /> for none.</param>
    /// <returns>A task that completes when the note, if any, is flushed.</returns>
    private async Task WriteDefaultConfigFileNoteAsync(string? path)
    {
        if (path is not null)
        {
            await WriteErrorPiecesAsync(WarningLineWrapper.WrapNoteText($"Read config file from '{path}'", terminalColumns))
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Writes <paramref name="line" /> to standard error as UTF-8, wrapped at
    /// <c>terminalColumns</c> by <see cref="WarningLineWrapper" /> when it is a
    /// <c>Warning: </c> line, each piece followed by <see cref="Environment.NewLine" />.
    /// </summary>
    /// <param name="line">The line, without a terminator.</param>
    /// <returns>A task that completes when the line is flushed.</returns>
    private Task WriteErrorLineAsync(string line) =>
        WriteErrorPiecesAsync(WarningLineWrapper.WrapLine(line, terminalColumns));

    /// <summary>
    /// Writes <paramref name="text" /> to standard error as UTF-8, with no terminator added.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>A task that completes when the text is flushed.</returns>
    private async Task WriteErrorTextAsync(string text)
    {
        await standardError.WriteAsync(Encoding.UTF8.GetBytes(text)).ConfigureAwait(false);
        await standardError.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Writes <paramref name="pieces" /> to standard error as UTF-8, each followed by
    /// <see cref="Environment.NewLine" />, in one write.
    /// </summary>
    /// <param name="pieces">The lines, already wrapped, without terminators.</param>
    /// <returns>A task that completes when the lines are flushed.</returns>
    private async Task WriteErrorPiecesAsync(IReadOnlyList<string> pieces)
    {
        StringBuilder text = new();
        foreach (string piece in pieces)
        {
            text.Append(piece).Append(Environment.NewLine);
        }

        byte[] bytes = Encoding.UTF8.GetBytes(text.ToString());
        await standardError.WriteAsync(bytes).ConfigureAwait(false);
        await standardError.FlushAsync().ConfigureAwait(false);
    }
}
