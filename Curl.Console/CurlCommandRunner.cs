using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Text;
using Curl.Authentication;
using Curl.Cli;
using Curl.Core;
using Curl.Core.FileSystem;
using Curl.Core.Globbing;
using Curl.Core.Hsts;
using Curl.Core.Multipart;
using Curl.Networking;
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
/// Builds, from one option group of the accepted command line, the dispatcher that performs each
/// transfer with the handler for its scheme and the warning lines printed before each transfer;
/// called once per <c>-:</c> / <c>--next</c> option group that runs, and not at all for a refused
/// command line. It takes the options because
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
/// <param name="writeOutTimeDialect">
/// The C runtime whose <c>strftime</c> a <c>-w</c> <c>%time{format}</c> follows; the composition
/// passes the platform's (<see cref="CurlComposition.WriteOutTimeDialectFor" />), and
/// <see cref="WriteOutTimeDialect.WindowsCRuntime" /> is used when not given.
/// </param>
/// <param name="outputPaths">
/// Creates the <c>--create-dirs</c> directories and checks for a <c>--skip-existing</c> file; a
/// <see cref="PhysicalOutputPaths" /> when not given.
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
/// <param name="terminalRendersStyles">
/// Whether the terminal on standard output renders bold, curl's <c>tool_term_has_bold</c>: always off
/// Windows, and on Windows once virtual-terminal processing is on (<see cref="StandardOutputVirtualTerminal" />).
/// With <paramref name="standardOutputIsTerminal" /> and <c>--styled-output</c>, the <c>-i</c> and <c>-I</c>
/// header lines written to standard output are styled (<see cref="HeaderStylesFor" />, ADR-0246); off when not given.
/// </param>
/// <param name="accountHomeDirectory">
/// The account's home directory from the user database, where an <c>scp</c> or <c>sftp</c> transfer looks
/// for <c>.ssh/known_hosts</c> last off Windows (<see cref="SshKnownHostsFileSearch" />); the composition
/// passes .NET's user profile folder. When not given, only the environment is searched.
/// </param>
/// <param name="runConnectionCache">
/// The connection cache every option group's dispatch shares, closed once the run's transfers end
/// (ADR-0285, BL-754); <see langword="null" /> when each group's dispatch keeps its own.
/// </param>
/// <param name="tlsSessions">
/// The run's TLS session cache, which the <c>--ssl-sessions</c> file is loaded into before the
/// transfers and saved from after them (ADR-0319, <see cref="TlsSessionFileLines" />);
/// <see langword="null" /> to leave the file alone.
/// </param>
/// <param name="extendedAttributeWriter">
/// Stores <c>--xattr</c>'s attributes on an <c>-o</c> / <c>-O</c> file after a successful transfer
/// (<see cref="OutputFileExtendedAttributes" />, ADR-0320); <see langword="null" /> where curl
/// writes none, as on Windows, and in tests that give none.
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
/// whose <c>-o</c> file cannot be opened: curl stops the run there with exit 23. Under <c>-Z</c>
/// up to <c>--parallel-max</c> transfers run at once and report in completion order, and the exit
/// code is the first failure's (ADR-0127, <see cref="ParallelRun" />). The first <c>-o</c> receives the first
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
/// is accepted, each <c>--stderr</c> moving the lines after it to its file as curl's does while
/// it parses; a refused command line then prints the refusal's lines and transfers nothing.
/// An accepted command line's warning lines for after the transfers, such as curl's
/// <c>Warning: Got more output options than URLs</c>, come last, after every transfer's
/// lines; they do not change the exit code.
/// </para>
/// <para>
/// Each transfer carries the <c>-r</c> text and its parsed range, the <c>-C</c> offset and the
/// <c>--max-filesize</c> limit. Range text that names no range ends the transfer with exit 33
/// before it is dispatched, except on an <c>http</c> or <c>https</c> URL, where curl sends the
/// text verbatim. <c>-C -</c> resumes from the size of the URL's <c>-o</c> file,
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
/// From the handler's first report that the transfer started, the meter is written as it is
/// drawn, so a terminal sees the status line rewritten in place; what is drawn after the
/// transfer, and all of it for a transfer never reported started, is written when it ends
/// (ADR-0099). Under <c>-#</c> the bar
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
/// <param name="lateBoundDiagnosticLog">The log the authenticators were composed with, bound to the run's diagnostic log once it is open (BL-923); <see langword="null" /> when none was composed.</param>
/// <param name="parsesAsWindowsBuild">Whether the command line is read as curl's Windows Schannel build reads it (<see cref="CommandLineOptions.ActsAsWindowsSchannelBuild" />); <see langword="null" />, the default, reads it as this process's platform's build does. Tests pass <see langword="false" /> to reach the HTTP/2, HTTP/3 and TLS-SRP options on any platform.</param>
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
    WriteOutTimeDialect writeOutTimeDialect = WriteOutTimeDialect.WindowsCRuntime,
    IOutputPaths? outputPaths = null,
    IDataFileReader? configFileReader = null,
    DefaultConfigFileSearch? defaultConfigFileSearch = null,
    Func<string, string?>? readEnvironmentVariable = null,
    bool terminalRendersStyles = false,
    string? accountHomeDirectory = null,
    IAsyncDisposable? runConnectionCache = null,
    TlsSessionCache? tlsSessions = null,
    IExtendedAttributeWriter? extendedAttributeWriter = null,
    LateBoundDiagnosticLog? lateBoundDiagnosticLog = null,
    bool? parsesAsWindowsBuild = null)
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

    /// <summary>
    /// Gets what chooses each transfer's credentials from the URL and the netrc file, reading it with
    /// <see cref="DataFileReader" /> and the home directory from the runner's environment (BL-505, BL-791).
    /// </summary>
    private TransferCredentialLookup CredentialLookup =>
        new(DataFileReader, EnvironmentVariables, runsOnWindows, diagnosticLog);

    /// <summary>
    /// Gets where an <c>scp</c> or <c>sftp</c> transfer looks for its known-hosts file, from the runner's
    /// environment and <c>accountHomeDirectory</c> (BL-576).
    /// </summary>
    private SshKnownHostsFileSearch KnownHostsSearch => new(EnvironmentVariables, runsOnWindows, accountHomeDirectory);

    /// <summary>The line curl 8.21.0 prints before exit 2 when an SSH transfer has no known-hosts file.</summary>
    internal const string KnownHostsFileMissingLine = "curl: Could not find a known_hosts file";

    /// <summary>The warning curl 8.21.0 prints instead when a host key fingerprint was given.</summary>
    internal const string KnownHostsFileMissingWarning = "Warning: Could not find a known_hosts file";

    /// <summary>
    /// Reads no environment variable, created once with <see langword="new" /> so no use of it carries
    /// the compiler's method-group cache.
    /// </summary>
    private static readonly Func<string, string?> NoEnvironmentVariables = new(ReadNoEnvironmentVariable);

    /// <summary>
    /// Gets what reads an environment variable: the function the runner was given, else
    /// <see cref="NoEnvironmentVariables" />.
    /// </summary>
    private Func<string, string?> EnvironmentVariables => readEnvironmentVariable ?? NoEnvironmentVariables;

    /// <summary>The <c>-D</c> value that sends the header lines to standard output.</summary>
    private const string StandardOutputHeaderFile = "-";

    /// <summary>The <c>--stderr</c> value that sends standard error to standard output.</summary>
    private const string StandardOutputStandardErrorFile = "-";

    /// <summary>
    /// What curl 8.21.0 prints, before the file name, when the <c>--stderr</c> file cannot be opened:
    /// its <c>warnf</c> prefix in front of a message that carries the prefix already (measured
    /// 2026-09-27, BL-410 Notes).
    /// </summary>
    internal const string StandardErrorFileOpenFailedPrefix = "Warning: Warning: Failed to open ";

    /// <summary>
    /// Where every standard error line of the run goes: the stream the runner was given, until
    /// <c>--stderr</c> replaces it with its file or standard output (<see cref="RedirectStandardErrorAsync" />).
    /// </summary>
    private Stream standardError = standardError;

    /// <summary>
    /// The file the last successful <c>--stderr</c> opened, which the runner closes when a later one
    /// replaces it or the run ends; <see langword="null" /> when none is open.
    /// </summary>
    private Stream? standardErrorFile;

    /// <summary>Builds each transfer's context on the runner's clock; gives a <c>telnet</c> transfer standard input and sends command-line header text in the platform's encoding (ADR-0067).</summary>
    private readonly TransferContextFactory transferContextFactory = new(standardInput, timeProvider, CredentialEncoding.ForPlatform(runsOnWindows));

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
        writeOutTimeDialect,
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
    /// The result of a transfer skipped because its <c>--etag-save</c> file could not be created. It is
    /// compared by reference: the transfer is not reported, and a run whose every transfer was skipped
    /// ends with <see cref="NoTransferPerformedLine" /> and exit 26, as curl 8.21.0 does (measured
    /// 2026-09-29, BL-619 Notes).
    /// </summary>
    private static readonly TransferResult EtagSaveFileSkippedTransfer =
        TransferResult.Failure(CurlExitCode.ReadError, "no transfer performed");

    /// <summary>What curl 8.21.0 prints when every transfer of the run was skipped, unless <c>-s</c> was given without <c>-S</c>.</summary>
    private const string NoTransferPerformedLine = "curl: no transfer performed";

    /// <summary>The <c>If-None-Match</c> line <c>--etag-compare</c> sends for a file that is missing or empty.</summary>
    private const string EmptyIfNoneMatchHeader = "If-None-Match: \"\"";

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
    /// The result of an <c>ipfs://</c> or <c>ipns://</c> URL whose <c>--ipfs-gateway</c> is not a
    /// URL curl can parse: exit 43, whose <c>%{errormsg}</c> curl 8.21.0 prints as its generic
    /// text for the code (measured 2026-09-27, BL-403). It is compared by reference, so that
    /// <see cref="TransferAllAsync" /> stops before the remaining URLs, as curl does.
    /// </summary>
    private static readonly TransferResult IpfsMalformedGatewayOptionFailure =
        TransferResult.Failure(IpfsGatewayFailure.MalformedGatewayOption.ExitCode, "A libcurl function was given a bad argument");

    /// <summary>
    /// The result of a transfer whose <c>-T</c> file could not be opened, measured on curl 8.21.0
    /// with <c>-T nosuchfile</c>: exit 26 with <see cref="MultipartFormBodyBuilder.OpenFailedMessage" />.
    /// It is compared by reference, so that <see cref="TransferAllAsync" /> stops before the
    /// remaining URLs, as curl does.
    /// </summary>
    private static readonly TransferResult CannotOpenUploadFileFailure =
        TransferResult.Failure(CurlExitCode.ReadError, MultipartFormBodyBuilder.OpenFailedMessage);

    /// <summary>
    /// The result of an <c>scp</c> or <c>sftp</c> transfer with no <c>-k</c>, no <c>--knownhosts</c>, no
    /// host key fingerprint and no known-hosts file to be found: exit 2 with curl's text for it, before
    /// any connection, measured on curl 8.21.0 (BL-576 Notes). It is compared by reference, so that
    /// <see cref="TransferAllAsync" /> stops before the remaining URLs, as curl does.
    /// </summary>
    private static readonly TransferResult KnownHostsFileMissingFailure =
        TransferResult.Failure(CurlExitCode.FailedInit, CurlEasyErrorText.Of(CurlExitCode.FailedInit));

    /// <summary>
    /// The result of a transfer whose <c>--interface</c> value libcurl refuses when curl sets it
    /// (<c>CURLOPT_INTERFACE</c>, option 10062, 0x274e): exit 43 with curl 8.21.0's message, before
    /// any connection (measured 2026-09-29, BL-600 Notes). It is compared by reference, so that
    /// <see cref="TransferAllAsync" /> stops before the remaining URLs, as curl does.
    /// </summary>
    private static readonly TransferResult InterfaceSetoptFailure =
        TransferResult.Failure(CurlExitCode.BadFunctionArgument, InterfaceSetoptMessage);

    private const string InterfaceSetoptMessage = "setopt 0x274e got bad argument";

    /// <summary>
    /// The result of a transfer whose <c>--ech</c> mode libcurl refuses when curl sets it
    /// (<c>CURLOPT_ECH</c>, option 10325, 0x2855): exit 43 with curl 8.21.0's message, before any
    /// connection (measured with curl's ECH build 2026-10-02, BL-1107; ADR-0378). It is compared by
    /// reference, so that <see cref="TransferAllAsync" /> stops before the remaining URLs, as curl does.
    /// </summary>
    private static readonly TransferResult EchSetoptFailure =
        TransferResult.Failure(CurlExitCode.BadFunctionArgument, EchSetoptMessage);

    private const string EchSetoptMessage = "setopt 0x2855 got bad argument";

    /// <summary>
    /// Standard output, deferring a write failure as curl's stdio buffer does and recording
    /// it for the current transfer: every transfer's without <c>-Z</c>, and a <c>--trace -</c>
    /// dump's; under <c>-Z</c> each transfer has one of its own (task BL-773).
    /// </summary>
    private readonly StandardOutputFailureDeferringStream deferringStandardOutput = new(standardOutput);

    /// <summary>Each transfer's option group, URL and file facts, in the order they started, for the <c>--libcurl</c> file.</summary>
    private readonly List<LibcurlTransfer> libcurlTransfers = [];

    /// <summary>
    /// The state of the transfer the current asynchronous flow is running, set as each transfer
    /// starts, so that transfers running at once under <c>-Z</c> each see their own (ADR-0127).
    /// </summary>
    private readonly AsyncLocal<RunningTransferState?> runningTransfer = new();

    /// <summary>Gets the state of the transfer the current asynchronous flow is running.</summary>
    private RunningTransferState Running => runningTransfer.Value!;

    /// <summary>
    /// The gate every write to standard output and standard error goes through once the transfers
    /// start, so that transfers running at once under <c>-Z</c> never split each other's writes.
    /// </summary>
    private readonly WriteGate writeGate = new();

    /// <summary>Standard output through <see cref="writeGate" />, deferring a write failure as <see cref="deferringStandardOutput" /> does.</summary>
    private Stream? gatedDeferringStandardOutput;

    /// <summary>Gets standard output through <see cref="writeGate" />, deferring a write failure as <see cref="deferringStandardOutput" /> does.</summary>
    private Stream GatedDeferringStandardOutput => gatedDeferringStandardOutput ??= writeGate.Guard(deferringStandardOutput);

    /// <summary>The raw standard output through <see cref="writeGate" />, where <c>-D -</c> writes.</summary>
    private Stream? gatedStandardOutput;

    /// <summary>Gets the raw standard output through <see cref="writeGate" />, where <c>-D -</c> writes.</summary>
    private Stream GatedStandardOutput => gatedStandardOutput ??= writeGate.Guard(standardOutput);

    /// <summary>
    /// The state of the running <c>-Z</c> run; <see langword="null" /> when the transfers run one at a
    /// time, as they do without <c>-Z</c>.
    /// </summary>
    private ParallelRun? parallelRun;

    /// <summary>
    /// The result of the run's last transfer, which a later <c>-T</c> file that cannot be opened
    /// reports when it failed (<see cref="CannotOpenUploadFileResult" />); <see langword="null" />
    /// before the first.
    /// </summary>
    private TransferResult? previousTransferResult;

    /// <summary>
    /// When the run's last serial transfer started - its last <c>--retry</c> attempt's start, set by
    /// <see cref="RecordSerialAttemptStart" /> - as a <see cref="TimeProvider.GetTimestamp" />, which
    /// <see cref="WaitForTransferStartRateAsync" /> measures <c>--rate</c> from; <see langword="null" />
    /// before the first.
    /// </summary>
    private long? previousSerialTransferStart;

    /// <summary>
    /// The run's HSTS cache, shared by every transfer of every option group and on with or without
    /// <c>--hsts</c>, as curl 8.21.0's tool shares one through its share handle: an <c>https</c>
    /// response's <c>Strict-Transport-Security</c> switches a later <c>http</c> URL or redirect to the
    /// same host to <c>https</c> (measured 2026-09-29, BL-621 Notes; ADR-0218). Made on first use, on
    /// the run's clock, writing to the run's diagnostic log (BL-921).
    /// </summary>
    private HstsTransferPolicy Hsts => LazyInitializer.EnsureInitialized(ref hsts, () => new HstsTransferPolicy(timeProvider, diagnosticLog));

    /// <summary>The run's HSTS cache once <see cref="Hsts" /> has made it.</summary>
    private HstsTransferPolicy? hsts;

    /// <summary>
    /// The <c>If-None-Match</c> lines <c>--etag-compare</c> has added to each option group, one per
    /// transfer started, as curl 8.21.0 adds them to the group's header list (BL-619 Notes). Locked
    /// while read or changed, as <c>-Z</c> transfers start at once.
    /// </summary>
    private readonly Dictionary<CommandLineOptions, List<string>> ifNoneMatchHeadersByGroup = [];

    /// <summary>
    /// Whether any transfer of the run has been reported (<see cref="ReportAsync" />); a run with none
    /// whose transfers were skipped ends with <see cref="NoTransferPerformedLine" />.
    /// </summary>
    private bool anyTransferReported;

    /// <summary>
    /// The option group of the run's last transfer skipped for its <c>--etag-save</c> file
    /// (<see cref="EtagSaveFileSkippedTransfer" />); <see langword="null" /> while none was.
    /// </summary>
    private CommandLineOptions? lastSkippedTransferGroup;

    /// <summary>
    /// The failure <see cref="CannotOpenUploadFileResult" /> made from a failed earlier transfer,
    /// compared by reference, so that <see cref="EndsTheRun" /> stops the run after it as after
    /// <see cref="CannotOpenUploadFileFailure" />; <see langword="null" /> until it makes one.
    /// </summary>
    private TransferResult? cannotOpenUploadFileAfterFailure;

    /// <summary>
    /// The <c>%{conn_id}</c> the next transfer that connects takes, counted per run from zero.
    /// </summary>
    private long nextConnectionId;

    /// <summary>
    /// The <c>%{conn_id}</c> of each connection a transfer of the run opened, by the pool's number
    /// for it, which a later transfer that reuses the connection takes (<see cref="ConnectionIdRecordingTransferEvents" />, BL-1052).
    /// </summary>
    private readonly ConcurrentDictionary<long, long> connectionIdsByPoolNumber = new();

    /// <summary>
    /// The <c>%{xfer_id}</c> the next transfer takes, counted per run from zero, one for every URL
    /// a glob expands to.
    /// </summary>
    private long nextTransferId;

    /// <summary>
    /// The <c>%{xfer_id}</c> of the running <c>-:</c> / <c>--next</c> option group's first transfer,
    /// which truncates the group's <c>-D</c> file: curl 8.21.0 opens each group's <c>-D</c> file anew, so
    /// <c>-D h A --next -D h B</c> left only B's head in <c>h</c> (measured 2026-09-28, BL-509 Notes).
    /// </summary>
    private long firstTransferIdOfGroup;

    /// <summary>
    /// The <c>%{urlnum}</c> of the running option group's first command-line URL: the command-line
    /// URLs of the groups before it, counted.
    /// </summary>
    private int firstUrlNumberOfGroup;

    /// <summary>The option groups that run after the running one, in order; empty for the last.</summary>
    private IReadOnlyList<CommandLineOptions> laterGroups = [];

    /// <summary>
    /// Whether a transfer of this run has switched standard output to binary mode, as one set up to
    /// send its body to standard output, or to discard it under <c>--out-null</c>, does.
    /// </summary>
    private bool standardOutputSwitchedToBinary;

    /// <summary>
    /// Where this run's <c>-v</c>, <c>--trace</c> and <c>--trace-ascii</c> output goes, opened once the
    /// first command-line URL has parsed as a glob and closed after the last transfer.
    /// </summary>
    private TransferEventOutput transferEventOutput = TransferEventOutput.None;

    /// <summary>
    /// The standard error <see cref="transferEventOutput" /> writes through, which each transfer's
    /// <see cref="RunningTransferState.Progress" /> holds once the handler reports the transfer done and
    /// <see cref="WriteProgressAsync" /> releases after the meter's end (task BL-411);
    /// <see langword="null" /> until it is opened.
    /// </summary>
    private HoldableStream? eventStandardError;

    /// <summary>
    /// The run's diagnostic log (ADR-0222), which owns the <c>--log-file</c>; <see langword="null" />
    /// until an accepted command line opens it.
    /// </summary>
    private RunDiagnosticLog? runDiagnosticLog;

    /// <summary>
    /// Where the runner writes its own <c>cli</c> and <c>runner</c> diagnostic lines: the run's log once
    /// it is open, <see cref="NoDiagnosticLog.Instance" /> until then.
    /// </summary>
    private IDiagnosticLog diagnosticLog = NoDiagnosticLog.Instance;

    /// <summary>
    /// Runs <paramref name="arguments" /> to completion.
    /// </summary>
    /// <param name="arguments">The command-line arguments, without the program name.</param>
    /// <returns>The process exit code.</returns>
    internal async Task<int> RunAsync(IReadOnlyList<string> arguments)
    {
        RecordingDataFileReader parseFileReader = new(DataFileReader);
        CommandLineParseResult parsed = ParseCommandLine(arguments, parseFileReader);

        return await ThenCleanUpAsync(RunWarnedAsync(parsed, parseFileReader.FilesTried), CloseRunOutputsAsync).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes the parser's warning lines, following its <c>--stderr</c> redirects, then runs the parse result.
    /// </summary>
    /// <param name="parsed">The parse result.</param>
    /// <param name="filesTriedWhileParsing">The files the parse tried to read, in order, and whether each was read.</param>
    /// <returns>The process exit code.</returns>
    private async Task<int> RunWarnedAsync(CommandLineParseResult parsed, IReadOnlyList<(string Path, bool WasRead)> filesTriedWhileParsing)
    {
        await WriteWarningLinesRedirectingStandardErrorAsync(parsed).ConfigureAwait(false);

        return await RunParsedAsync(parsed, filesTriedWhileParsing).ConfigureAwait(false);
    }

    /// <summary>
    /// Closes the run's diagnostic log and then its <c>--stderr</c> file.
    /// </summary>
    /// <returns>A task that completes when both are closed.</returns>
    private async Task CloseRunOutputsAsync()
    {
        await CloseDiagnosticLogAsync().ConfigureAwait(false);
        await CloseStandardErrorFileAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Waits for <paramref name="work" /> to end, however it ends, then runs <paramref name="cleanUp" />,
    /// then gives the work's result or throws its exception: a <c>finally</c> block, without the
    /// branches the compiler writes to rethrow after an awaited one, which no test can take (BL-1356).
    /// As with a <c>finally</c> block, an exception from <paramref name="cleanUp" /> replaces the work's.
    /// </summary>
    /// <typeparam name="T">The work's result.</typeparam>
    /// <param name="work">The work, already started.</param>
    /// <param name="cleanUp">What runs once the work has ended.</param>
    /// <returns>The work's result.</returns>
    private static async Task<T> ThenCleanUpAsync<T>(Task<T> work, Func<Task> cleanUp)
    {
        await Task.WhenAny(work).ConfigureAwait(false);
        await cleanUp().ConfigureAwait(false);

        return await work.ConfigureAwait(false);
    }

    /// <summary>
    /// Opens the run's diagnostic log from the accepted command line's <c>--log-level</c> and
    /// <c>--log-file</c> once standard error is where <c>--stderr</c> sends it, hands it to every
    /// transfer's context, and logs the command line: a <c>--log-file</c> that cannot be opened writes
    /// <see cref="RunDiagnosticLog.LogFileOpenFailedPrefix" />'s warning, even under <c>-s</c>, and the run
    /// carries on without a log (ADR-0222, decision 4).
    /// </summary>
    /// <param name="parsed">The accepted parse result.</param>
    /// <param name="filesTriedWhileParsing">The files the parse tried to read, in order, and whether each was read.</param>
    /// <returns>A task that completes when the log is open and the command line logged.</returns>
    private async Task OpenDiagnosticLogAsync(CommandLineParseResult parsed, IReadOnlyList<(string Path, bool WasRead)> filesTriedWhileParsing)
    {
        CommandLineOptions options = parsed.Options!;
        runDiagnosticLog = await RunDiagnosticLog
            .OpenAsync(options.DiagnosticLogLevel, options.DiagnosticLogFile, fileSystem, () => standardError, timeProvider, Environment.NewLine, writeGate)
            .ConfigureAwait(false);
        if (runDiagnosticLog.LogFileOpenFailed)
        {
            await WriteErrorLineAsync(RunDiagnosticLog.LogFileOpenFailedPrefix + options.DiagnosticLogFile).ConfigureAwait(false);
        }

        diagnosticLog = runDiagnosticLog.Log;
        transferContextFactory.DiagnosticLog = diagnosticLog;
        lateBoundDiagnosticLog?.Bind(diagnosticLog);
        LogCommandLine(parsed, filesTriedWhileParsing);
    }

    /// <summary>
    /// Logs the accepted command line: <c>info</c> its option groups and URLs, <c>verbose</c> each file
    /// the parse tried to read (<c>.curlrc</c> candidates, <c>-K</c> and <c>@file</c> values) by path, saying
    /// whether it was read, and <c>warning</c> each
    /// of the parser's <c>Warning: </c> lines, already printed.
    /// </summary>
    /// <param name="parsed">The accepted parse result.</param>
    /// <param name="filesTriedWhileParsing">The files the parse tried to read, in order, and whether each was read.</param>
    private void LogCommandLine(CommandLineParseResult parsed, IReadOnlyList<(string Path, bool WasRead)> filesTriedWhileParsing)
    {
        foreach (string warning in parsed.WarningLines.Where(IsWarningLine))
        {
            diagnosticLog.Write(DiagnosticLogLevel.Warning, DiagnosticLogComponents.Cli, warning);
        }

        if (diagnosticLog.IsEnabled(DiagnosticLogLevel.Info))
        {
            diagnosticLog.Write(
                DiagnosticLogLevel.Info,
                DiagnosticLogComponents.Cli,
                string.Create(CultureInfo.InvariantCulture, $"command line accepted: {parsed.Groups.Count} option groups, {parsed.Groups.Sum(group => group.Urls.Count)} URLs"));
        }

        LogFilesTriedWhileParsing(filesTriedWhileParsing);
    }

    /// <summary>
    /// Logs, at <c>verbose</c>, each file the parse tried to read, by path, and whether it was read.
    /// </summary>
    /// <param name="filesTriedWhileParsing">The files the parse tried to read, in order, and whether each was read.</param>
    private void LogFilesTriedWhileParsing(IReadOnlyList<(string Path, bool WasRead)> filesTriedWhileParsing)
    {
        if (!diagnosticLog.IsEnabled(DiagnosticLogLevel.Verbose))
        {
            return;
        }

        foreach ((string path, bool wasRead) in filesTriedWhileParsing)
        {
            diagnosticLog.Write(DiagnosticLogLevel.Verbose, DiagnosticLogComponents.Cli, $"{(wasRead ? "read" : "could not read")} file '{path}' while parsing the command line");
        }
    }

    /// <summary>
    /// Tells whether a standard-error line is one of curl's <c>Warning: </c> lines.
    /// </summary>
    /// <param name="line">The line.</param>
    /// <returns><see langword="true" /> when it starts with <c>Warning: </c>.</returns>
    private static bool IsWarningLine(string line) => line.StartsWith("Warning: ", StringComparison.Ordinal);

    /// <summary>
    /// Closes the run's diagnostic log, and with it the <c>--log-file</c>, if one was opened.
    /// </summary>
    /// <returns>A task that completes when the log is closed.</returns>
    private async Task CloseDiagnosticLogAsync()
    {
        if (runDiagnosticLog is not null)
        {
            await runDiagnosticLog.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Runs a parsed command line, for <see cref="RunAsync" />, once its warning lines are written
    /// and standard error is where its <c>--stderr</c> options send it: a refused one prints the
    /// refusal's lines, an accepted one the config-file note and then the run.
    /// </summary>
    /// <param name="parsed">The parse result.</param>
    /// <param name="filesTriedWhileParsing">The files the parse tried to read, in order, and whether each was read, for the diagnostic log.</param>
    /// <returns>The process exit code.</returns>
    private async Task<int> RunParsedAsync(CommandLineParseResult parsed, IReadOnlyList<(string Path, bool WasRead)> filesTriedWhileParsing)
    {
        if (!parsed.IsAccepted)
        {
            await WriteRefusalAsync(parsed.Refusal, parsed.NotedDefaultConfigFile).ConfigureAwait(false);

            return (int)parsed.Refusal.ExitCode;
        }

        await WriteDefaultConfigFileNoteAsync(parsed.NotedDefaultConfigFile).ConfigureAwait(false);
        await OpenDiagnosticLogAsync(parsed, filesTriedWhileParsing).ConfigureAwait(false);

        int exitCode = await RunAcceptedAsync(parsed.Options, parsed.Groups, parsed.WarningLinesAfterTransfers).ConfigureAwait(false);
        if (parsed.RefusalAfterGroups is not { } refusalAfterGroups)
        {
            return exitCode;
        }

        // curl 8.21.0 refuses a later -:/--next group only once the groups before it have run
        // (test686: "htdhdhdtp://localhost --next" exits 2).
        await WriteErrorLinesAsync(refusalAfterGroups.StandardErrorLines).ConfigureAwait(false);
        return (int)refusalAfterGroups.ExitCode;
    }

    /// <summary>
    /// Writes the parser's warning lines, carrying out each <c>--stderr</c> at its place among them,
    /// as curl 8.21.0 opens the file while it parses the option: the lines before it go where
    /// standard error went until then, the lines after it where it sends them
    /// (<see cref="RedirectStandardErrorAsync" />).
    /// </summary>
    /// <param name="parsed">The parse result, accepted or refused.</param>
    /// <returns>A task that completes when every warning line is flushed.</returns>
    /// <remarks>
    /// Measured 2026-09-27 (BL-410, BL-476): <c>--stderr se -H nocolon</c> writes the <c>-H</c>
    /// warning to <c>se</c>, <c>-H nocolon --stderr se</c> to standard error, and
    /// <c>--stderr a -H x --stderr b</c> to <c>a</c>, with the rest in <c>b</c>.
    /// </remarks>
    private async Task WriteWarningLinesRedirectingStandardErrorAsync(CommandLineParseResult parsed)
    {
        int written = 0;
        foreach (StandardErrorRedirect redirect in parsed.StandardErrorRedirects)
        {
            await WriteErrorLinesAsync([.. parsed.WarningLines.Take(redirect.WarningLinesBefore).Skip(written)]).ConfigureAwait(false);
            written = redirect.WarningLinesBefore;
            await RedirectStandardErrorAsync(redirect).ConfigureAwait(false);
        }

        await WriteErrorLinesAsync([.. parsed.WarningLines.Skip(written)]).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends everything the runner writes to standard error from here on where one <c>--stderr</c>
    /// says: <c>-</c> to standard output, any other name to that file, truncated, closing the file an
    /// earlier <c>--stderr</c> opened. A file that cannot be opened prints curl 8.21.0's doubled
    /// <see cref="StandardErrorFileOpenFailedPrefix" /> warning, unless <c>-s</c> came before the
    /// option, and leaves standard error where it was.
    /// </summary>
    /// <param name="redirect">The <c>--stderr</c> option.</param>
    /// <returns>A task that completes when standard error has moved or the warning is flushed.</returns>
    /// <remarks>
    /// Measured 2026-09-27 (BL-410 Notes): <c>--stderr adir -s</c> prints the warning,
    /// <c>-s --stderr adir</c> prints nothing, and <c>--stderr a --stderr adir</c> writes it to <c>a</c>.
    /// </remarks>
    private async Task RedirectStandardErrorAsync(StandardErrorRedirect redirect)
    {
        if (redirect.File == StandardOutputStandardErrorFile)
        {
            await CloseStandardErrorFileAsync().ConfigureAwait(false);
            standardError = standardOutput;
            return;
        }

        FileOpenResult opened = await fileSystem
            .OpenForWriteAsync(redirect.File, FileWriteMode.Truncate, DeferredOutputFileStream.CreateMode, CancellationToken.None)
            .ConfigureAwait(false);
        if (opened.Content is not { } file)
        {
            if (!redirect.Silent)
            {
                await WriteErrorLineAsync(StandardErrorFileOpenFailedPrefix + redirect.File).ConfigureAwait(false);
            }

            return;
        }

        await CloseStandardErrorFileAsync().ConfigureAwait(false);
        standardError = file;
        standardErrorFile = file;
    }

    /// <summary>
    /// Closes the file the last successful <c>--stderr</c> opened, if it is still open.
    /// </summary>
    /// <returns>A task that completes when the file is closed.</returns>
    private async Task CloseStandardErrorFileAsync()
    {
        if (standardErrorFile is not null)
        {
            await standardErrorFile.DisposeAsync().ConfigureAwait(false);
            standardErrorFile = null;
        }
    }

    /// <summary>
    /// Runs an accepted command line, for <see cref="RunAsync" />, once standard error is where
    /// <c>--stderr</c> sends it.
    /// </summary>
    /// <param name="options">The accepted command line's first option group.</param>
    /// <param name="groups">The option groups to run, <paramref name="options" /> first.</param>
    /// <param name="warningLinesAfterTransfers">The parser's warning lines printed after the transfers.</param>
    /// <returns>The process exit code.</returns>
    private async Task<int> RunAcceptedAsync(
        CommandLineOptions options,
        IReadOnlyList<CommandLineOptions> groups,
        IReadOnlyList<string> warningLinesAfterTransfers)
    {
        if (options.AiHelpRequested)
        {
            await WriteAiHelpAsync(options.AiHelpSubject).ConfigureAwait(false);

            return (int)CurlExitCode.Ok;
        }

        if (InformationLines(options) is { } informationLines)
        {
            await WriteStandardOutputLinesAsync(informationLines).ConfigureAwait(false);

            return (int)CurlExitCode.Ok;
        }

        if (CurlHelpText.IsOptionSubject(options.HelpSubject))
        {
            await WriteOptionManualSectionAsync(options.HelpSubject!).ConfigureAwait(false);

            return (int)CurlExitCode.Ok;
        }

        CurlExitCode? transfersExitCode = await TransferAllGroupsAsync(groups).ConfigureAwait(false);
        await WriteLibcurlSourceAsync(options).ConfigureAwait(false);
        if (transfersExitCode is not { } exitCode)
        {
            return (int)CurlExitCode.FailedInit;
        }

        await WriteErrorLinesAsync(warningLinesAfterTransfers).ConfigureAwait(false);

        return (int)exitCode;
    }

    /// <summary>
    /// Notes a transfer for the <c>--libcurl</c> source file, in the order the transfers start; nothing
    /// without <c>--libcurl</c>.
    /// </summary>
    /// <param name="options">The transfer's option group.</param>
    /// <param name="givenUrl">The URL as the glob expanded it, before any scheme is guessed.</param>
    /// <param name="uploadFile">The URL's <c>-T</c> file, or <see langword="null" /> when it uploads nothing.</param>
    private void RecordLibcurlTransfer(CommandLineOptions options, string givenUrl, string? uploadFile)
    {
        if (options.LibcurlFile is null)
        {
            return;
        }

        lock (libcurlTransfers)
        {
            Running.LibcurlTransferIndex = libcurlTransfers.Count;
            libcurlTransfers.Add(new LibcurlTransfer(options, givenUrl) { UploadFile = uploadFile });
        }
    }

    /// <summary>
    /// Notes what the transfer learnt about its files in its <c>--libcurl</c> entry; nothing without
    /// <c>--libcurl</c>.
    /// </summary>
    /// <param name="change">Makes the entry with the new facts from the old one.</param>
    private void UpdateLibcurlTransfer(Func<LibcurlTransfer, LibcurlTransfer> change)
    {
        if (Running.LibcurlTransferIndex is not { } index)
        {
            return;
        }

        lock (libcurlTransfers)
        {
            libcurlTransfers[index] = change(libcurlTransfers[index]);
        }
    }

    /// <summary>
    /// Writes the <c>--libcurl</c> source file once the transfers are done, as curl 8.21.0 does whether
    /// they succeeded or not (<see cref="LibcurlSourceCode" />): <c>-</c> to standard output with
    /// <c>\n</c> line ends, and a file in text mode, so with <c>\r\n</c> on Windows. A file that cannot
    /// be opened gets curl's warning, unless <c>-s</c> was given (measured 2026-10-01, BL-652 Notes).
    /// </summary>
    /// <param name="options">The first option group; <c>--libcurl</c> is global.</param>
    /// <returns>A task that completes when the file is written.</returns>
    private async Task WriteLibcurlSourceAsync(CommandLineOptions options)
    {
        if (options.LibcurlFile is not { } file)
        {
            return;
        }

        string source = LibcurlSourceCode.Generate(libcurlTransfers);
        if (file == "-")
        {
            await GatedStandardOutput.WriteAsync(Encoding.UTF8.GetBytes(source)).ConfigureAwait(false);
            await GatedStandardOutput.FlushAsync().ConfigureAwait(false);
            return;
        }

        FileOpenResult opened = await fileSystem
            .OpenForWriteAsync(file, FileWriteMode.Truncate, DeferredOutputFileStream.CreateMode, CancellationToken.None)
            .ConfigureAwait(false);
        if (opened.Content is not { } content)
        {
            await (options.Silent ? Task.CompletedTask : WriteErrorLineAsync($"Warning: Failed to open {file} to write libcurl code")).ConfigureAwait(false);
            return;
        }

        await using (content.ConfigureAwait(false))
        {
            await content.WriteAsync(Encoding.UTF8.GetBytes(runsOnWindows ? source.Replace("\n", "\r\n", StringComparison.Ordinal) : source)).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Runs the <c>-:</c> / <c>--next</c> option groups in order, for <see cref="RunAcceptedAsync" />,
    /// each command-line URL with its own group's options, then closes the run's
    /// <see cref="transferEventOutput" />. <c>%{xfer_id}</c>, <c>%{conn_id}</c> and <c>%{urlnum}</c> count
    /// on across the groups, and the exit code is the last transfer's, as curl 8.21.0 does: a failed
    /// first group followed by a good one exits 0, a good one followed by a failed one exits with the
    /// failure's code (measured 2026-09-28, BL-509 Notes).
    /// </summary>
    /// <param name="groups">The option groups to run.</param>
    /// <returns>
    /// The last transfer's exit code, under <c>-Z</c> the first failure's in completion order
    /// (<see cref="EndParallelRunAsync" />), or <see cref="CurlExitCode.Ok" /> when there was none; or
    /// <see langword="null" /> when a group's request methods conflict
    /// (<see cref="RequestMethodConflictLines" />), whose lines are written and whose group and those
    /// after it are not run, as curl 8.21.0 refuses it once the groups before it have run. A run whose
    /// every transfer was skipped for its <c>--etag-save</c> file ends with
    /// <see cref="CurlExitCode.ReadError" /> instead (<see cref="ReportNoTransferPerformedAsync" />).
    /// </returns>
    /// <remarks>
    /// No group runs after one that ends the run (<see cref="EndsTheRun" />, <c>--fail-early</c>
    /// included) or one with an output option left over, whose run curl 8.21.0 ends there
    /// (<c>-o nul -o nul2 A --next B</c> requested only A, BL-508 Notes).
    /// </remarks>
    private async Task<CurlExitCode?> TransferAllGroupsAsync(IReadOnlyList<CommandLineOptions> groups)
    {
        standardError = writeGate.Guard(standardError);
        await WriteTlsSessionLinesAsync(groups[0], TlsSessionFileLines.Load).ConfigureAwait(false);
        parallelRun = groups[0].Parallel ? NewParallelRun(groups[0]) : null;

        return await ThenCleanUpAsync(TransferGroupsAndEndRunAsync(groups), () => CloseRunAsync(groups[0])).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs the option groups in order, then ends a <c>-Z</c> run and reports a run whose every
    /// transfer was skipped.
    /// </summary>
    /// <param name="groups">The option groups to run.</param>
    /// <returns>As <see cref="TransferAllGroupsAsync" />.</returns>
    private async Task<CurlExitCode?> TransferGroupsAndEndRunAsync(IReadOnlyList<CommandLineOptions> groups)
    {
        CurlExitCode? exitCode = await TransferGroupsInOrderAsync(groups).ConfigureAwait(false);
        exitCode = parallelRun is { } run ? await EndParallelRunAsync(run, exitCode).ConfigureAwait(false) : exitCode;

        return await ReportNoTransferPerformedAsync(exitCode).ConfigureAwait(false);
    }

    /// <summary>
    /// Closes what the run kept open across its groups, whatever its outcome: the connection cache,
    /// then the <c>--ssl-sessions</c> file, saved, then the <c>-v</c> and trace output.
    /// </summary>
    /// <param name="firstGroup">The first option group, which holds the global <c>--ssl-sessions</c>.</param>
    /// <returns>A task that completes when all three are closed.</returns>
    private async Task CloseRunAsync(CommandLineOptions firstGroup)
    {
        await (runConnectionCache?.DisposeAsync() ?? ValueTask.CompletedTask).ConfigureAwait(false);
        await WriteTlsSessionLinesAsync(firstGroup, (sessions, options) => TlsSessionFileLines.Save(sessions, options, runsOnWindows)).ConfigureAwait(false);
        await transferEventOutput.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Loads or saves the run's <c>--ssl-sessions</c> file (ADR-0319) when the runner keeps a
    /// session cache, and writes the lines that gives to standard error.
    /// </summary>
    /// <param name="options">The first option group, which holds the global <c>--ssl-sessions</c>.</param>
    /// <param name="step">The load or the save.</param>
    /// <returns>A task that completes when the lines are written.</returns>
    private async Task WriteTlsSessionLinesAsync(CommandLineOptions options, Func<TlsSessionCache, CommandLineOptions, IReadOnlyList<string>> step)
    {
        foreach (string line in tlsSessions is null ? [] : step(tlsSessions, options))
        {
            await WriteErrorLineAsync(line).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Ends a run whose every transfer was skipped for its <c>--etag-save</c> file as curl 8.21.0 does:
    /// <see cref="NoTransferPerformedLine" />, unless <c>-s</c> was given without <c>-S</c> in the last
    /// skipped transfer's group, and exit 26 (measured 2026-09-29, BL-619 Notes).
    /// </summary>
    /// <param name="exitCode">The run's exit code so far.</param>
    /// <returns><see cref="CurlExitCode.ReadError" /> when no transfer ran and one was skipped; otherwise <paramref name="exitCode" />.</returns>
    private async Task<CurlExitCode?> ReportNoTransferPerformedAsync(CurlExitCode? exitCode)
    {
        if (anyTransferReported || lastSkippedTransferGroup is not { } skippedGroup)
        {
            return exitCode;
        }

        if (ShowsErrors(skippedGroup))
        {
            await WriteErrorLineAsync(NoTransferPerformedLine).ConfigureAwait(false);
        }

        return CurlExitCode.ReadError;
    }

    /// <summary>
    /// Makes the state of a <c>-Z</c> run from its first option group, with the combined progress meter
    /// curl 8.21.0 draws in place of each transfer's own (<see cref="ParallelProgressMeter" />, BL-521)
    /// unless this runner writes no meter or <c>-s</c> or <c>--no-progress-meter</c> hides it; <c>-#</c>
    /// changes nothing, as curl ignores it under <c>-Z</c>.
    /// </summary>
    /// <param name="options">The first option group.</param>
    /// <returns>The run.</returns>
    private ParallelRun NewParallelRun(CommandLineOptions options) =>
        new(options.ParallelMax, options.ParallelMaxHost, options.ParallelImmediate)
        {
            ProgressMeter = writesProgressMeter && !options.Silent && !options.ProgressMeterOff
                ? new ParallelProgressMeter(timeProvider, writeGate, WriteParallelProgressMeter)
                : null,
        };

    /// <summary>
    /// Writes text of a <c>-Z</c> run's combined progress meter to standard error and flushes it, holding
    /// the run's <see cref="writeGate" />.
    /// </summary>
    /// <param name="text">The text.</param>
    private void WriteParallelProgressMeter(string text)
    {
        standardError.Write(Encoding.UTF8.GetBytes(text));
        standardError.Flush();
    }

    /// <summary>
    /// Runs the option groups in order, for <see cref="TransferAllGroupsAsync" />: under <c>-Z</c> this
    /// only starts their transfers, in command-line order, as slots free.
    /// </summary>
    /// <param name="groups">The option groups to run.</param>
    /// <returns>The exit code <see cref="TransferAllGroupsAsync" /> describes, for a run without <c>-Z</c>.</returns>
    private async Task<CurlExitCode?> TransferGroupsInOrderAsync(IReadOnlyList<CommandLineOptions> groups)
    {
        IReadOnlyList<CommandLineOptions> groupsThatRun = GroupsThatRun(groups);
        CurlExitCode exitCode = CurlExitCode.Ok;
        for (int group = 0; group < groupsThatRun.Count; group++)
        {
            if (!await AcceptsRequestMethodsAsync(groupsThatRun[group]).ConfigureAwait(false))
            {
                return null;
            }

            laterGroups = [.. groupsThatRun.Skip(group + 1)];
            (exitCode, bool runEnded) = await TransferAllAsync(groupsThatRun[group], exitCode).ConfigureAwait(false);
            if (runEnded)
            {
                break;
            }
        }

        return exitCode;
    }

    /// <summary>
    /// Ends a <c>-Z</c> run once every transfer has been started: waits for the running ones, writes
    /// what <c>--fail-early</c> left to report, in command-line order, and closes the dispatches.
    /// </summary>
    /// <param name="run">The run.</param>
    /// <param name="exitCode">
    /// What <see cref="TransferGroupsInOrderAsync" /> returned: <see langword="null" /> for a group whose
    /// request methods conflict.
    /// </param>
    /// <returns>
    /// <see langword="null" /> when <paramref name="exitCode" /> is; otherwise the code of the first
    /// transfer to fail, in completion order, or <see cref="CurlExitCode.Ok" /> (ADR-0127, decision 3).
    /// </returns>
    private static async Task<CurlExitCode?> EndParallelRunAsync(ParallelRun run, CurlExitCode? exitCode)
    {
        await run.EndAsync().ConfigureAwait(false);

        return exitCode is null ? null : run.ExitCode;
    }

    /// <summary>
    /// Tells whether an option group's request methods agree, writing
    /// <see cref="RequestMethodConflictLines" />' lines, unless <c>-s</c>, when they do not.
    /// </summary>
    /// <param name="options">The option group.</param>
    /// <returns><see langword="true" /> when the group can run.</returns>
    private async Task<bool> AcceptsRequestMethodsAsync(CommandLineOptions options)
    {
        if (RequestMethodConflictLines(options) is not { } conflictLines)
        {
            return true;
        }

        if (!options.Silent)
        {
            await WriteErrorLinesAsync(conflictLines).ConfigureAwait(false);
        }

        return false;
    }

    /// <summary>
    /// The option groups that run: every one up to and including the first with an <c>-o</c>,
    /// <c>-O</c> or kept <c>--no-remote-name</c> left over with no URL to pair with. An output entry is
    /// only ever left without a URL by an output option, as a URL takes the first entry without one.
    /// </summary>
    /// <param name="groups">The option groups.</param>
    /// <returns>The groups that run, in order.</returns>
    private static IReadOnlyList<CommandLineOptions> GroupsThatRun(IReadOnlyList<CommandLineOptions> groups)
    {
        int firstWithOutputLeftOver = groups.ToList().FindIndex(group => group.UrlOutputs.Any(output => output.Url is null));
        return firstWithOutputLeftOver < 0 ? groups : [.. groups.Take(firstWithOutputLeftOver + 1)];
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
    /// <param name="fileReader">Reads the config files and <c>@file</c> values, recording each path.</param>
    /// <returns>The parse result.</returns>
    private CommandLineParseResult ParseCommandLine(IReadOnlyList<string> arguments, RecordingDataFileReader fileReader) =>
        CommandLineParser.Parse(
            arguments,
            Path.Exists,
            ConsolePasswordPrompt.ForProcessConsole,
            fileReader,
            defaultConfigFileSearch ?? NoDefaultConfigFile,
            parsesAsWindowsBuild ?? OperatingSystem.IsWindows());

    /// <summary>
    /// Transfers every URL of one option group in order, each command-line URL once for every URL
    /// its glob expands to, and reports each failure, then disposes the group's
    /// <see cref="TransferDispatch" />, closing its connection pool, whatever the outcome.
    /// </summary>
    /// <param name="options">The option group.</param>
    /// <param name="exitCode">The exit code so far, returned when nothing is transferred.</param>
    /// <returns>
    /// The last transfer's exit code, and whether the run ended: a transfer <see cref="EndsTheRun" />
    /// names is the last, and the URLs after it are not transferred. A URL that is not a well-formed
    /// glob also ends the run, with exit 3.
    /// </returns>
    private async Task<(CurlExitCode ExitCode, bool RunEnded)> TransferAllAsync(CommandLineOptions options, CurlExitCode exitCode)
    {
        firstTransferIdOfGroup = nextTransferId;
        TransferDispatch dispatch = createTransferDispatch(options);

        return await ThenCleanUpAsync(TransferEachUrlAsync(dispatch, options, exitCode), () => EndGroupAsync(dispatch, options)).ConfigureAwait(false);
    }

    /// <summary>
    /// Ends an option group whatever its outcome: closes its dispatch and counts its URLs, so the next
    /// group's <c>%{urlnum}</c> counts on from them.
    /// </summary>
    /// <param name="dispatch">The group's dispatch.</param>
    /// <param name="options">The option group.</param>
    /// <returns>A task that completes when the group is ended.</returns>
    private async Task EndGroupAsync(TransferDispatch dispatch, CommandLineOptions options)
    {
        await CloseDispatchAsync(dispatch).ConfigureAwait(false);
        firstUrlNumberOfGroup += options.Urls.Count;
    }

    /// <summary>
    /// Closes an option group's dispatch once its transfers are over: at once without <c>-Z</c>, and
    /// when the <c>-Z</c> run ends otherwise, since its transfers may still be running.
    /// </summary>
    /// <param name="dispatch">The group's dispatch.</param>
    /// <returns>A task that completes when the dispatch is closed or left to the run.</returns>
    private async Task CloseDispatchAsync(TransferDispatch dispatch)
    {
        if (parallelRun is { } run)
        {
            run.CloseAtEnd(dispatch);
            return;
        }

        await dispatch.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Transfers every URL of one option group in order, for <see cref="TransferAllAsync" />, opening
    /// the run's <see cref="transferEventOutput" /> once the run's first URL has parsed as a glob: curl
    /// 8.21.0 opens its trace file at the first event, and a URL that is not a well-formed glob makes
    /// none (measured 2026-09-27, BL-242 Notes).
    /// </summary>
    /// <param name="dispatch">What the group transfers through.</param>
    /// <param name="options">The option group.</param>
    /// <param name="exitCode">The exit code so far, returned when nothing is transferred.</param>
    /// <returns>The exit code and run end <see cref="TransferAllAsync" /> returns.</returns>
    private async Task<(CurlExitCode ExitCode, bool RunEnded)> TransferEachUrlAsync(
        TransferDispatch dispatch,
        CommandLineOptions options,
        CurlExitCode exitCode)
    {
        for (int index = 0; index < options.Urls.Count; index++)
        {
            if (!TryParseUploadFiles(options, index, out IReadOnlyList<string?>? uploadFiles, out TransferResult? globFailure)
                || !TryParseGlob(options, index, out UrlGlob? glob, out globFailure))
            {
                await WriteGlobFailureLinesAsync(options, globFailure).ConfigureAwait(false);
                await RecordParallelRunEndAsync(globFailure).ConfigureAwait(false);
                return (globFailure.ExitCode, true);
            }

            if (eventStandardError is null)
            {
                eventStandardError = new HoldableStream(standardError);
                transferEventOutput = await TransferEventOutput
                    .OpenAsync(options, fileSystem, GatedDeferringStandardOutput, eventStandardError, runsOnWindows, standardOutputIsTerminal, timeProvider, () => standardOutputSwitchedToBinary)
                    .ConfigureAwait(false);
            }

            (exitCode, bool runEnded) = await TransferEachMatchAsync(dispatch, options, index, uploadFiles, glob, exitCode)
                .ConfigureAwait(false);
            if (runEnded)
            {
                return (exitCode, true);
            }
        }

        return (exitCode, false);
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
                UrlTransfer transfer = new(options, index, firstUrlNumberOfGroup + index, nextTransferId++, match, uploadFile, runsOnWindows);
                (exitCode, bool runEnded) = await RunTransferAsync(dispatch, options, transfer, exitCode).ConfigureAwait(false);
                if (runEnded)
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
            await WriteErrorLinesAsync(FormatErrorLine(failure, failure.ErrorMessage).Split('\n')).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Runs one transfer: to its end without <c>-Z</c>, and under it as <see cref="StartInParallelAsync" /> does.
    /// </summary>
    /// <param name="dispatch">Performs the transfer with the handler for its scheme, after its warning lines.</param>
    /// <param name="options">The transfer's option group.</param>
    /// <param name="transfer">The transfer.</param>
    /// <param name="exitCode">The exit code so far, kept under <c>-Z</c>, whose run decides its own.</param>
    /// <returns>The exit code so far, and whether the run ended.</returns>
    private async Task<(CurlExitCode ExitCode, bool RunEnded)> RunTransferAsync(
        TransferDispatch dispatch,
        CommandLineOptions options,
        UrlTransfer transfer,
        CurlExitCode exitCode)
    {
        if (parallelRun is { } run)
        {
            return (exitCode, await StartInParallelAsync(run, dispatch, options, transfer).ConfigureAwait(false));
        }

        await WaitForTransferStartRateAsync(options).ConfigureAwait(false);
        previousSerialTransferStart = timeProvider.GetTimestamp();
        TransferResult result = await TransferAndReportAsync(dispatch, options, transfer).ConfigureAwait(false);

        return ReferenceEquals(result, EtagSaveFileSkippedTransfer)
            ? (exitCode, false)
            : (result.ExitCode, EndsTheRun(options, result));
    }

    /// <summary>
    /// Waits, before a serial transfer starts, until <see cref="CommandLineOptions.MillisecondsBetweenTransferStarts" />
    /// have passed since the previous one started, as curl 8.21.0 does for <c>--rate</c>: the wait follows a
    /// failed transfer too, whole milliseconds are counted, and under <c>-v</c> or a <c>--trace</c> option,
    /// <c>-s</c> or not, standard error first gets <c>Note: Transfer took &lt;n&gt; ms, waits &lt;m&gt;ms as set by --rate</c>
    /// (measured 2026-09-29, BL-650 Notes). Nothing waits before the first transfer or once the interval has passed.
    /// </summary>
    /// <param name="options">The transfer's option group.</param>
    /// <returns>A task that completes when the transfer may start.</returns>
    private async Task WaitForTransferStartRateAsync(CommandLineOptions options)
    {
        long wait = MillisecondsUntilTransferMayStart(options.MillisecondsBetweenTransferStarts, out long took);
        if (wait == 0)
        {
            return;
        }

        if (options.Trace != TraceKind.None)
        {
            await WriteErrorPiecesAsync(WarningLineWrapper.WrapNoteText($"Transfer took {took} ms, waits {wait}ms as set by --rate", terminalColumns))
                .ConfigureAwait(false);
        }

        await DelayMillisecondsAsync(wait).ConfigureAwait(false);
    }

    /// <summary>
    /// How long a serial transfer must still wait for <c>--rate</c>, in whole milliseconds, as
    /// <see cref="WaitForTransferStartRateAsync" /> describes.
    /// </summary>
    /// <param name="interval">The <c>--rate</c> interval, or <see langword="null" /> when none was given.</param>
    /// <param name="took">The whole milliseconds since the previous transfer started; zero before the first.</param>
    /// <returns>The milliseconds to wait; zero when there is no need.</returns>
    private long MillisecondsUntilTransferMayStart(long? interval, out long took)
    {
        took = 0;
        if (interval is not { } least || previousSerialTransferStart is not { } previousStart)
        {
            return 0;
        }

        took = (long)timeProvider.GetElapsedTime(previousStart).TotalMilliseconds;
        return Math.Max(least - took, 0);
    }

    /// <summary>
    /// Waits <paramref name="milliseconds" /> on the injected clock, in pieces no longer than
    /// <see cref="int.MaxValue" /> milliseconds, since a <c>--rate</c> interval may be longer than one delay allows.
    /// </summary>
    /// <param name="milliseconds">How long to wait.</param>
    /// <returns>A task that completes when the time has passed.</returns>
    private async Task DelayMillisecondsAsync(long milliseconds)
    {
        for (long remaining = milliseconds; remaining > 0; remaining -= int.MaxValue)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(Math.Min(remaining, int.MaxValue)), timeProvider).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Starts one transfer of a <c>-Z</c> run once fewer than <c>--parallel-max</c> are running, without
    /// waiting for it to end (ADR-0127, decision 1). Once <c>--fail-early</c> has aborted the run the
    /// transfer is not started, and its report is left for the end of the run.
    /// </summary>
    /// <param name="run">The run.</param>
    /// <param name="dispatch">Performs the transfer with the handler for its scheme, after its warning lines.</param>
    /// <param name="options">The transfer's option group.</param>
    /// <param name="transfer">The transfer.</param>
    /// <returns>
    /// <see langword="true" /> when a result that ends the run without <c>--fail-early</c> has stopped
    /// every further start.
    /// </returns>
    private async Task<bool> StartInParallelAsync(
        ParallelRun run,
        TransferDispatch dispatch,
        CommandLineOptions options,
        UrlTransfer transfer)
    {
        await run.WaitForFreeSlotAsync().ConfigureAwait(false);
        if (run.HasEnded)
        {
            return true;
        }

        StandardOutputFailureDeferringStream transferStandardOutput = new(standardOutput);
        RunningTransferState state = NewRunningTransferState(run.AbortToken, transferStandardOutput, writeGate.Guard(transferStandardOutput));
        if (run.IsAborted)
        {
            run.DeferReport(transfer.TransferId, () => ReportSkippedAsync(dispatch, options, transfer, state, run.FirstFailure!));
            return false;
        }

        state.ParallelProgress = run.ProgressMeter?.AddTransfer();

        LogParallelScheduler("started", transfer);
        run.Queue.Add(TransferInParallelAsync(run, dispatch, options, transfer, state));
        return false;
    }

    /// <summary>
    /// Performs one transfer of a <c>-Z</c> run, then, holding the run's <see cref="writeGate" />,
    /// records how it ended and writes its report (<see cref="EndParallelTransferAsync" />). Where <c>-w</c> or <c>--trace-ids</c> prints
    /// it, a transfer
    /// <c>--fail-early</c> cancels ends as <see cref="ParallelRun.AbortedResult" />.
    /// </summary>
    /// <param name="run">The run.</param>
    /// <param name="dispatch">Performs the transfer with the handler for its scheme, after its warning lines.</param>
    /// <param name="options">The transfer's option group.</param>
    /// <param name="transfer">The transfer.</param>
    /// <param name="state">The transfer's state.</param>
    /// <returns>A task that completes when the transfer and its report, if not deferred, are done.</returns>
    private async Task TransferInParallelAsync(
        ParallelRun run,
        TransferDispatch dispatch,
        CommandLineOptions options,
        UrlTransfer transfer,
        RunningTransferState state)
    {
        runningTransfer.Value = state;
        (TransferResult Result, string GivenUrl, string TransferUrl) ended;
        try
        {
            ended = await TransferOnceHostIsFreeAsync(run, dispatch, options, transfer, state.AbortToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (state.AbortToken.IsCancellationRequested)
        {
            ended = (ParallelRun.AbortedResult, transfer.Url, UrlSchemeGuesser.AddScheme(transfer.Url, options.DefaultProtocol));
        }

        LogParallelScheduler("finished", transfer);

        await writeGate.RunExclusiveAsync(() => EndParallelTransferAsync(run, dispatch, options, transfer, state, ended))
            .ConfigureAwait(false);
        state.ParallelProgress?.End();
    }

    /// <summary>
    /// Logs, at <c>verbose</c>, the <c>-Z</c> scheduler starting or finishing a transfer.
    /// </summary>
    /// <param name="happened">What the scheduler did: <c>started</c> or <c>finished</c>.</param>
    /// <param name="transfer">The transfer.</param>
    private void LogParallelScheduler(string happened, UrlTransfer transfer)
    {
        if (diagnosticLog.IsEnabled(DiagnosticLogLevel.Verbose))
        {
            diagnosticLog.Write(
                DiagnosticLogLevel.Verbose,
                DiagnosticLogComponents.Runner,
                string.Create(CultureInfo.InvariantCulture, $"parallel scheduler {happened} transfer {transfer.TransferId}"));
        }
    }

    /// <summary>
    /// Performs one transfer of a <c>-Z</c> run once its host is free (<see cref="ParallelHostQueue" />:
    /// <c>--parallel-max-host</c> and <c>--parallel-immediate</c>), counting it at the host until it ends.
    /// </summary>
    /// <param name="run">The run.</param>
    /// <param name="dispatch">Performs the transfer with the handler for its scheme, after its warning lines.</param>
    /// <param name="options">The transfer's option group.</param>
    /// <param name="transfer">The transfer.</param>
    /// <param name="abortToken">Ends the wait for the host when <c>--fail-early</c> aborts the run.</param>
    /// <returns>The transfer's result and URLs, as <see cref="TransferUrlAsync" /> gives them.</returns>
    private async Task<(TransferResult Result, string GivenUrl, string TransferUrl)> TransferOnceHostIsFreeAsync(
        ParallelRun run,
        TransferDispatch dispatch,
        CommandLineOptions options,
        UrlTransfer transfer,
        CancellationToken abortToken)
    {
        string url = UrlSchemeGuesser.AddScheme(transfer.Url, options.DefaultProtocol);
        await run.Hosts.WaitForHostAsync(url, options.HttpVersion == RequestedHttpVersion.Http2PriorKnowledge, abortToken).ConfigureAwait(false);
        Running.ParallelProgress?.MarkLive();
        try
        {
            return await TransferUrlAsync(dispatch, options, transfer).ConfigureAwait(false);
        }
        finally
        {
            run.Hosts.Leave(url);
        }
    }

    /// <summary>
    /// Ends one transfer of a <c>-Z</c> run, holding the run's <see cref="writeGate" />: once
    /// <c>--fail-early</c> has aborted the run, the transfer counts as aborted and its report is left for
    /// the end of the run, in command-line order; otherwise its end is recorded
    /// (<see cref="ParallelRun.RecordEnd" />) and its report written at once, in completion order
    /// (ADR-0127, decisions 2 to 4).
    /// </summary>
    /// <param name="run">The run.</param>
    /// <param name="dispatch">What the transfer went through, with its cookies.</param>
    /// <param name="options">The transfer's option group.</param>
    /// <param name="transfer">The transfer.</param>
    /// <param name="state">The transfer's state.</param>
    /// <param name="ended">The transfer's result and URLs, as <see cref="TransferUrlAsync" /> gives them.</param>
    /// <returns>A task that completes when the report is written or left.</returns>
    private Task EndParallelTransferAsync(
        ParallelRun run,
        TransferDispatch dispatch,
        CommandLineOptions options,
        UrlTransfer transfer,
        RunningTransferState state,
        (TransferResult Result, string GivenUrl, string TransferUrl) ended)
    {
        if (NotesSkippedTransfer(options, ended.Result))
        {
            return Task.CompletedTask;
        }

        if (run.IsAborted)
        {
            run.DeferReport(
                transfer.TransferId,
                () => ReportAsync(dispatch, options, transfer, state, ParallelRun.AbortedResult, ended.GivenUrl, ended.TransferUrl));
            return Task.CompletedTask;
        }

        run.RecordEnd(ended.Result, EndsTheRun(options, ended.Result), options.FailEarly);

        return ReportAsync(dispatch, options, transfer, state, ended.Result, ended.GivenUrl, ended.TransferUrl);
    }

    /// <summary>
    /// Writes the report of a transfer <c>--fail-early</c> kept from starting: it ends with the first
    /// failure's code and curl's generic text for it (<see cref="CurlEasyErrorText" />), as curl 8.21.0
    /// reports it (ADR-0127, rows 12 and 13).
    /// </summary>
    /// <param name="dispatch">What the transfer would have gone through, with its cookies.</param>
    /// <param name="options">The transfer's option group.</param>
    /// <param name="transfer">The transfer.</param>
    /// <param name="state">The transfer's state.</param>
    /// <param name="firstFailure">The run's first failure.</param>
    /// <returns>A task that completes when the report is written.</returns>
    private Task ReportSkippedAsync(
        TransferDispatch dispatch,
        CommandLineOptions options,
        UrlTransfer transfer,
        RunningTransferState state,
        TransferResult firstFailure)
    {
        TransferResult skipped = TransferResult.Failure(firstFailure.ExitCode, CurlEasyErrorText.Of(firstFailure.ExitCode));

        return ReportAsync(dispatch, options, transfer, state, skipped, transfer.Url, UrlSchemeGuesser.AddScheme(transfer.Url, options.DefaultProtocol));
    }

    /// <summary>
    /// Records, under <c>-Z</c> and holding the run's <see cref="writeGate" />, a failure that ends the
    /// run before its transfer starts, such as a URL that is not a well-formed glob.
    /// </summary>
    /// <param name="failure">The failure.</param>
    /// <returns>A task that completes when it is recorded, at once without <c>-Z</c>.</returns>
    private Task RecordParallelRunEndAsync(TransferResult failure) =>
        parallelRun is { } run
            ? writeGate.RunExclusiveAsync(() =>
            {
                run.RecordEnd(failure, endsTheRun: true, failEarly: false);
                return Task.CompletedTask;
            })
            : Task.CompletedTask;

    /// <summary>
    /// Makes the state of a transfer starting now, in the running option group.
    /// </summary>
    /// <param name="abortToken">The token <c>--fail-early</c> aborts the transfer through, under <c>-Z</c>.</param>
    /// <param name="transferStandardOutput">Standard output as the transfer writes it, recording its write failure.</param>
    /// <param name="gatedTransferStandardOutput"><paramref name="transferStandardOutput" /> through <see cref="writeGate" />.</param>
    /// <returns>The state.</returns>
    private RunningTransferState NewRunningTransferState(
        CancellationToken abortToken,
        StandardOutputFailureDeferringStream transferStandardOutput,
        Stream gatedTransferStandardOutput) =>
        new(firstTransferIdOfGroup, laterGroups, abortToken, transferStandardOutput, gatedTransferStandardOutput);

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
        RunningTransferState state = NewRunningTransferState(CancellationToken.None, deferringStandardOutput, GatedDeferringStandardOutput);
        runningTransfer.Value = state;
        (TransferResult result, string givenUrl, string transferUrl) = await TransferUrlAsync(dispatch, options, transfer)
            .ConfigureAwait(false);
        if (NotesSkippedTransfer(options, result))
        {
            return result;
        }

        await ReportAsync(dispatch, options, transfer, state, result, givenUrl, transferUrl).ConfigureAwait(false);

        return result;
    }

    /// <summary>
    /// Tells whether <paramref name="result" /> is a transfer skipped for its <c>--etag-save</c> file, which is
    /// not reported, and remembers its group for <see cref="ReportNoTransferPerformedAsync" /> when it is.
    /// </summary>
    /// <param name="options">The transfer's option group.</param>
    /// <param name="result">The transfer's result.</param>
    /// <returns><see langword="true" /> for <see cref="EtagSaveFileSkippedTransfer" />.</returns>
    private bool NotesSkippedTransfer(CommandLineOptions options, TransferResult result)
    {
        if (!ReferenceEquals(result, EtagSaveFileSkippedTransfer))
        {
            return false;
        }

        lastSkippedTransferGroup = options;
        return true;
    }

    /// <summary>
    /// Writes a finished transfer's report: its failure lines, the <c>-#</c> bar's newline, the
    /// <c>--remove-on-error</c> removal, its <c>-w</c> output and the <c>-c</c> jar.
    /// </summary>
    /// <param name="dispatch">What the transfer went through, with its cookies.</param>
    /// <param name="options">The transfer's option group.</param>
    /// <param name="transfer">The transfer.</param>
    /// <param name="state">The transfer's state.</param>
    /// <param name="result">The transfer's result.</param>
    /// <param name="givenUrl">The URL <c>%{url}</c> prints.</param>
    /// <param name="transferUrl">The URL transferred.</param>
    /// <returns>A task that completes when the report is written.</returns>
    private async Task ReportAsync(
        TransferDispatch dispatch,
        CommandLineOptions options,
        UrlTransfer transfer,
        RunningTransferState state,
        TransferResult result,
        string givenUrl,
        string transferUrl)
    {
        runningTransfer.Value = state;
        anyTransferReported = true;
        LogTransferEnded(transfer, state, result);
        if (ShowsErrors(options) && result.ErrorMessage is not null && !IsIpfsGatewayFailure(result))
        {
            await WriteFailureLinesAsync(result).ConfigureAwait(false);
        }

        if (state.ProgressBar is { HasBeenCalled: true })
        {
            await WriteErrorLineAsync(string.Empty).ConfigureAwait(false);
        }

        await RemoveOutputFileOfFailedTransferAsync(options, result).ConfigureAwait(false);
        bool endsTheRun = EndsTheRun(options, result);
        standardOutputSwitchedToBinary |= SwitchesStandardOutputToBinary(options, transfer, result);
        bool standardOutputIsBinary = IsStandardOutputBinaryForWriteOut(
            options, transfer.UrlIndex, standardOutputSwitchedToBinary, endsTheRun);
        await WriteOutAsync(options, transfer, givenUrl, transferUrl, result, standardOutputIsBinary).ConfigureAwait(false);
        await WriteCookieJarAltSvcAndHstsFilesAsync(dispatch, options, transferUrl, standardOutputIsBinary, state).ConfigureAwait(false);

        previousTransferResult = result;
    }

    /// <summary>
    /// Logs a transfer about to start: <c>info</c> <see cref="TransferDiagnosticLines.Started" />'s
    /// summary of its options, and marks the start its end line measures from.
    /// </summary>
    /// <param name="options">The transfer's option group.</param>
    /// <param name="transfer">The transfer.</param>
    private void LogTransferStarted(CommandLineOptions options, UrlTransfer transfer)
    {
        Running.StartTimestamp = timeProvider.GetTimestamp();
        if (diagnosticLog.IsEnabled(DiagnosticLogLevel.Info))
        {
            string url = UrlSchemeGuesser.AddScheme(transfer.Url, options.DefaultProtocol);
            diagnosticLog.Write(
                DiagnosticLogLevel.Info,
                DiagnosticLogComponents.Runner,
                TransferDiagnosticLines.Started(transfer.TransferId, options, url, WritesToFile(options, transfer.UrlIndex)));
        }
    }

    /// <summary>
    /// Logs a transfer that has ended: <c>error</c> its failing exit code, then <c>info</c> its exit
    /// code, bytes and elapsed milliseconds.
    /// </summary>
    /// <param name="transfer">The transfer.</param>
    /// <param name="state">The transfer's state.</param>
    /// <param name="result">The transfer's result.</param>
    private void LogTransferEnded(UrlTransfer transfer, RunningTransferState state, TransferResult result)
    {
        if (!result.IsSuccess && diagnosticLog.IsEnabled(DiagnosticLogLevel.Error))
        {
            diagnosticLog.Write(DiagnosticLogLevel.Error, DiagnosticLogComponents.Runner, TransferDiagnosticLines.Failed(transfer.TransferId, result.ExitCode));
        }

        if (diagnosticLog.IsEnabled(DiagnosticLogLevel.Info))
        {
            diagnosticLog.Write(
                DiagnosticLogLevel.Info,
                DiagnosticLogComponents.Runner,
                TransferDiagnosticLines.Ended(transfer.TransferId, result, timeProvider.GetElapsedTime(state.StartTimestamp)));
        }
    }

    /// <summary>
    /// Under <c>--remove-on-error</c>, deletes the output file a failed transfer opened, as curl
    /// 8.21.0 does after the transfer's failure lines and before its <c>-w</c> output: under
    /// <c>-v</c> or a <c>--trace</c> option, <c>-s</c> or not, standard error then gets
    /// <c>Note: Removed output file: &lt;file&gt;</c>, wrapped as a note is, and a file that
    /// cannot be deleted gets <c>Warning: Failed removing: &lt;file&gt;</c> unless <c>-s</c> was
    /// given, and off Windows one that is not a regular file, such as <c>/dev/null</c>, is left
    /// alone with <c>Warning: Skipping removal; not a regular file: &lt;file&gt;</c> unless
    /// <c>-s</c> was given (measured 2026-10-01, BL-752 Notes). A file the transfer never opened - a <c>-f</c> failure with no body written, a
    /// failed connect - is left as it is, even one there before the transfer (measured
    /// 2026-09-28, BL-494 Notes). The transfer's exit code and message are unchanged.
    /// </summary>
    /// <param name="options">The accepted command line.</param>
    /// <param name="result">The transfer's result.</param>
    /// <returns>A task that completes when the file is dealt with.</returns>
    private async Task RemoveOutputFileOfFailedTransferAsync(CommandLineOptions options, TransferResult result)
    {
        if (result.IsSuccess || !options.RemoveOnError || Running.OpenedOutputFile is not { } openedFile)
        {
            return;
        }

        OutputFileRemoval removal = OutputPaths.RemoveFile(openedFile);
        if (removal != OutputFileRemoval.Removed)
        {
            await WriteWarningUnlessSilentAsync(options, RemovalWarningLine(removal, openedFile)).ConfigureAwait(false);
        }
        else if (options.Trace != TraceKind.None)
        {
            await WriteErrorPiecesAsync(WarningLineWrapper.WrapNoteText($"Removed output file: {openedFile}", terminalColumns))
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Gives curl 8.21.0's warning for a <c>--remove-on-error</c> output file that was not removed.
    /// </summary>
    /// <param name="removal">What became of the file: not a regular file, or a failed delete.</param>
    /// <param name="file">The file.</param>
    /// <returns>The warning line.</returns>
    private static string RemovalWarningLine(OutputFileRemoval removal, string file) =>
        removal == OutputFileRemoval.NotRegularFile
            ? $"Warning: Skipping removal; not a regular file: {file}"
            : $"Warning: Failed removing: {file}";

    /// <summary>
    /// Performs one transfer: rewrites an <c>ipfs://</c> or <c>ipns://</c> URL to its gateway URL,
    /// gives a URL typed without a scheme the <c>--proto-default</c> scheme or the one <see cref="UrlSchemeGuesser" /> guesses, and
    /// resolves the URL of a <c>-T</c> upload with <see cref="UploadTransferUrl" />, then loads
    /// the <c>--resolve</c> entries and the <c>-b</c> cookie files, in curl 8.21.0's order, before
    /// the transfer itself.
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
        LogTransferStarted(options, transfer);
        if (TakesRemoteNameFromIpfsUrl(transfer))
        {
            return (await RefuseIpfsRemoteNameAsync(options).ConfigureAwait(false), transfer.Url, string.Empty);
        }

        if (!TryRewriteIpfsUrl(options, transfer.Url, out string givenUrl, out IpfsGatewayFailure? ipfsFailure))
        {
            return (await ReportIpfsGatewayFailureAsync(options, transfer, ipfsFailure).ConfigureAwait(false), givenUrl, string.Empty);
        }

        string? uploadFile = transfer.UploadFile;
        RecordLibcurlTransfer(options, givenUrl, uploadFile);
        if (!TryResolveTransferUrl(options, givenUrl, uploadFile, out string transferUrl))
        {
            return (UploadUrlMalformedFailure, givenUrl, transferUrl);
        }

        if (await PrepareEtagFilesAsync(options).ConfigureAwait(false) is { } etagFailure)
        {
            return (etagFailure, givenUrl, transferUrl);
        }

        ITransferEvents eventsBeforeConnecting = SetUpTransferEvents(options, transfer);
        if (RefuseMalformedSetopt(options, eventsBeforeConnecting) is { } setoptFailure)
        {
            return (setoptFailure, givenUrl, transferUrl);
        }

        dispatch.LoadResolveEntries(eventsBeforeConnecting);
        TraceTransferStart(options, eventsBeforeConnecting);
        await LoadCookieFilesAsync(dispatch, options, transferUrl, eventsBeforeConnecting).ConfigureAwait(false);
        await OpenAltSvcCacheAsync(options, transferUrl).ConfigureAwait(false);
        transferUrl = await SwitchToHttpsForHstsAsync(options, transferUrl, eventsBeforeConnecting).ConfigureAwait(false);
        TransferResult result = await TransferWithHeaderOutputAsync(dispatch, options, transfer, transferUrl, uploadFile)
            .ConfigureAwait(false);
        return (result, givenUrl, transferUrl);
    }

    /// <summary>
    /// Gives a URL typed without a scheme the <c>--proto-default</c> scheme or the one
    /// <see cref="UrlSchemeGuesser" /> guesses and, for a <c>-T</c> upload, resolves it with
    /// <see cref="UploadTransferUrl" />.
    /// </summary>
    /// <param name="options">The accepted command line.</param>
    /// <param name="givenUrl">The URL as the glob expanded it, after any IPFS rewrite.</param>
    /// <param name="uploadFile">The transfer's <c>-T</c> file, or <see langword="null" /> when it uploads nothing.</param>
    /// <param name="transferUrl">The URL to transfer; for a <c>-T</c> URL that cannot be parsed, the empty string.</param>
    /// <returns><see langword="false" /> when a <c>-T</c> URL cannot be parsed.</returns>
    private static bool TryResolveTransferUrl(CommandLineOptions options, string givenUrl, string? uploadFile, out string transferUrl)
    {
        transferUrl = UrlSchemeGuesser.AddScheme(givenUrl, options.DefaultProtocol);
        return uploadFile is null || UploadTransferUrl.TryResolve(transferUrl, uploadFile, out transferUrl);
    }

    /// <summary>
    /// Refuses an <c>--interface</c> and then an <c>--ech</c> value curl's setopt refuses, before the transfer connects.
    /// </summary>
    /// <param name="options">The accepted command line.</param>
    /// <param name="events">The transfer's events before connecting.</param>
    /// <returns>The first refusal, or <see langword="null" /> when both are accepted.</returns>
    private static TransferResult? RefuseMalformedSetopt(CommandLineOptions options, ITransferEvents events) =>
        RefuseMalformedInterface(options, events) ?? RefuseMalformedEchMode(options, events);

    /// <summary>
    /// Sets the transfer up for <c>--etag-compare</c> and <c>--etag-save</c>, in that order, as curl 8.21.0
    /// does before it connects: the compare file's <c>If-None-Match</c> line joins the group's
    /// (<see cref="AddIfNoneMatchHeader" />), and the save file is created, or kept as it is, and chosen as
    /// where the transfer's ETag goes (measured 2026-09-29, BL-619 Notes).
    /// </summary>
    /// <param name="options">The transfer's option group.</param>
    /// <returns>
    /// <see langword="null" /> when the transfer goes ahead; <see cref="CannotCreateDirectoryFailure" /> when a
    /// <c>--create-dirs</c> directory of the save file cannot be made, or <see cref="EtagSaveFileSkippedTransfer" />
    /// when the save file cannot be created.
    /// </returns>
    private async Task<TransferResult?> PrepareEtagFilesAsync(CommandLineOptions options)
    {
        if (options.EtagCompareFile is { } compareFile)
        {
            string header = await ReadIfNoneMatchHeaderAsync(options, compareFile).ConfigureAwait(false);
            IReadOnlyList<string> headers = AddIfNoneMatchHeader(options, header);
            Running.IfNoneMatchHeaders = headers;
            UpdateLibcurlTransfer(entry => entry with { IfNoneMatchHeaders = headers });
        }

        return options.EtagSaveFile is { } saveFile
            ? await PrepareEtagSaveFileAsync(options, saveFile).ConfigureAwait(false)
            : null;
    }

    /// <summary>
    /// Reads the <c>--etag-compare</c> file into the <c>If-None-Match</c> line curl 8.21.0 sends: its bytes with
    /// every carriage return and line feed dropped, so several lines run together, or <c>""</c> when the file is
    /// empty or cannot be opened, after <c>Warning: Failed to open &lt;file&gt;: &lt;reason&gt;</c> unless <c>-s</c>
    /// was given (measured 2026-09-29, BL-619 Notes). The bytes are read as the platform's command-line text,
    /// which is what the header is sent in.
    /// </summary>
    /// <param name="options">The transfer's option group.</param>
    /// <param name="compareFile">The <c>--etag-compare</c> value.</param>
    /// <returns>The header line.</returns>
    private async Task<string> ReadIfNoneMatchHeaderAsync(CommandLineOptions options, string compareFile)
    {
        FileOpenResult opened = await fileSystem.OpenForReadAsync(compareFile, CancellationToken.None).ConfigureAwait(false);
        if (opened.Content is not { } content)
        {
            await WriteWarningUnlessSilentAsync(options, $"Warning: Failed to open {compareFile}: {OutputFileOpenWarning.ReasonFor(opened.Status)}")
                .ConfigureAwait(false);
            return EmptyIfNoneMatchHeader;
        }

        using MemoryStream read = new();
        await using (content.ConfigureAwait(false))
        {
            await content.CopyToAsync(read).ConfigureAwait(false);
        }

        byte[] etag = [.. read.ToArray().Where(value => value is not ((byte)'\r' or (byte)'\n'))];
        return etag.Length == 0
            ? EmptyIfNoneMatchHeader
            : "If-None-Match: " + CredentialEncoding.ForPlatform(runsOnWindows).GetString(etag);
    }

    /// <summary>
    /// Adds <paramref name="header" /> to the <c>If-None-Match</c> lines of <paramref name="options" />'s group.
    /// </summary>
    /// <param name="options">The transfer's option group.</param>
    /// <param name="header">The line this transfer adds.</param>
    /// <returns>Every line the group has added so far, this one last.</returns>
    private IReadOnlyList<string> AddIfNoneMatchHeader(CommandLineOptions options, string header)
    {
        lock (ifNoneMatchHeadersByGroup)
        {
            if (!ifNoneMatchHeadersByGroup.TryGetValue(options, out List<string>? headers))
            {
                headers = [];
                ifNoneMatchHeadersByGroup[options] = headers;
            }

            headers.Add(header);
            return [.. headers];
        }
    }

    /// <summary>
    /// Chooses where the transfer's ETag goes: standard output for <c>-</c>; otherwise the file, which is
    /// created now if it is missing, its <c>--create-dirs</c> directories first, and otherwise kept as it
    /// is until an ETag arrives, as curl 8.21.0 opens it for appending before it connects. A file that cannot
    /// be created skips the transfer, after
    /// <c>Warning: Failed creating file for saving etags: "&lt;file&gt;". Skip this transfer</c> unless
    /// <c>-s</c> was given (measured 2026-09-29, BL-619 Notes).
    /// </summary>
    /// <param name="options">The transfer's option group.</param>
    /// <param name="saveFile">The <c>--etag-save</c> value.</param>
    /// <returns>
    /// <see langword="null" /> when the transfer goes ahead; otherwise <see cref="CannotCreateDirectoryFailure" />
    /// or <see cref="EtagSaveFileSkippedTransfer" />.
    /// </returns>
    private async Task<TransferResult?> PrepareEtagSaveFileAsync(CommandLineOptions options, string saveFile)
    {
        if (saveFile == StandardOutputHeaderFile)
        {
            Running.SaveEtag = SaveEtagToStandardOutputAsync;
            return null;
        }

        if (options.CreateDirectories
            && OutputFileDirectories.CreateLeadingDirectories(OutputPaths, saveFile, runsOnWindows) is { } directory)
        {
            return await ReportCannotCreateDirectoryAsync(options, directory).ConfigureAwait(false);
        }

        FileOpenResult opened = await fileSystem
            .OpenForWriteAsync(saveFile, FileWriteMode.Append, DeferredOutputFileStream.CreateMode, CancellationToken.None)
            .ConfigureAwait(false);
        if (opened.Content is not { } created)
        {
            await WriteWarningUnlessSilentAsync(options, $"Warning: Failed creating file for saving etags: \"{saveFile}\". Skip this transfer")
                .ConfigureAwait(false);
            return EtagSaveFileSkippedTransfer;
        }

        await created.DisposeAsync().ConfigureAwait(false);
        Running.SaveEtag = (etagLine, cancellationToken) => SaveEtagToFileAsync(saveFile, etagLine, cancellationToken);
        return null;
    }

    /// <summary>Writes an <c>--etag-save -</c> ETag line to standard output, among the header and body bytes.</summary>
    /// <param name="etagLine">The ETag and its line feed.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the line is flushed.</returns>
    private async ValueTask SaveEtagToStandardOutputAsync(byte[] etagLine, CancellationToken cancellationToken)
    {
        await GatedStandardOutput.WriteAsync(etagLine, cancellationToken).ConfigureAwait(false);
        await GatedStandardOutput.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Replaces what the <c>--etag-save</c> file holds with an ETag line, as curl 8.21.0 truncates the file
    /// before each one it writes.
    /// </summary>
    /// <param name="saveFile">The <c>--etag-save</c> file.</param>
    /// <param name="etagLine">The ETag and its line feed.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the file is closed.</returns>
    /// <exception cref="IOException">The file could not be opened again, which fails the transfer's header write.</exception>
    private async ValueTask SaveEtagToFileAsync(string saveFile, byte[] etagLine, CancellationToken cancellationToken)
    {
        FileOpenResult opened = await fileSystem
            .OpenForWriteAsync(saveFile, FileWriteMode.Truncate, DeferredOutputFileStream.CreateMode, cancellationToken)
            .ConfigureAwait(false);
        if (opened.Content is not { } saved)
        {
            throw new IOException($"Could not write the ETag to {saveFile}.");
        }

        await using (saved.ConfigureAwait(false))
        {
            await saved.WriteAsync(etagLine, cancellationToken).ConfigureAwait(false);
        }
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

        IpfsGatewayRewriter rewriter = new(EnvironmentVariables, ReadGatewayFileText);
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
    /// <see cref="IpfsGatewayDetectionFailure" />, <see cref="IpfsMalformedGatewayOptionFailure" /> or
    /// <see cref="IpfsMalformedTargetUrlFailure" />.
    /// </returns>
    private async Task<TransferResult> ReportIpfsGatewayFailureAsync(
        CommandLineOptions options,
        UrlTransfer transfer,
        IpfsGatewayFailure failure)
    {
        Running.OutputFileName = transfer.OutputFileName is { } fileName ? InOutputDirectory(options, fileName) : null;
        await WriteErrorLinesAsync(["curl: " + failure.Message, CommandLineRefusal.TryHelpLine]).ConfigureAwait(false);

        if (failure == IpfsGatewayFailure.GatewayDetectionFailed)
        {
            return IpfsGatewayDetectionFailure;
        }

        return failure == IpfsGatewayFailure.MalformedGatewayOption
            ? IpfsMalformedGatewayOptionFailure
            : IpfsMalformedTargetUrlFailure;
    }

    /// <summary>
    /// Tells whether <paramref name="result" /> is an IPFS URL's failure to be rewritten, which
    /// prints its own lines, uses no connection and has no transfer number.
    /// </summary>
    /// <param name="result">The transfer's result.</param>
    /// <returns>
    /// <see langword="true" /> for <see cref="IpfsGatewayDetectionFailure" />,
    /// <see cref="IpfsMalformedGatewayOptionFailure" /> and <see cref="IpfsMalformedTargetUrlFailure" />.
    /// </returns>
    private static bool IsIpfsGatewayFailure(TransferResult result) =>
        ReferenceEquals(result, IpfsGatewayDetectionFailure)
        || ReferenceEquals(result, IpfsMalformedGatewayOptionFailure)
        || ReferenceEquals(result, IpfsMalformedTargetUrlFailure);

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
        await WriteErrorLineAsync(FormatTransferErrorLine(result)).ConfigureAwait(false);
        if (result.ExitCode == CurlExitCode.PeerFailedVerification)
        {
            await WriteCertificateHelpBlockAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Tells whether <paramref name="transfer" /> switches standard output to binary mode, as curl
    /// does for a transfer with no <c>-o</c> or remote name whose <c>-D</c> file, if any, could be
    /// opened: one sending its body to standard output, or discarding it under <c>--out-null</c>
    /// (<c>--out-null u -w "%{http_code}\n"</c> wrote LF, measured 2026-09-28, BL-495 Notes). An
    /// IPFS URL that could not be rewritten counts: curl 8.21.0 wrote its <c>-w</c> line feed as LF
    /// without <c>-o</c> and as CR LF with one (measured 2026-09-27, BL-240 Notes). Where <c>-w</c> or <c>--trace-ids</c> prints
    /// it, a transfer
    /// under <c>-B</c> / <c>--use-ascii</c> never does: curl leaves standard output in text mode
    /// for it (measured 2026-10-01, BL-961 Notes).
    /// </summary>
    /// <param name="options">The transfer's option group.</param>
    /// <param name="transfer">The transfer.</param>
    /// <param name="result">The transfer's result.</param>
    /// <returns><see langword="true" /> when standard output is now in binary mode.</returns>
    private static bool SwitchesStandardOutputToBinary(CommandLineOptions options, UrlTransfer transfer, TransferResult result) =>
        !options.UseAscii && !transfer.WritesToFile && !ReferenceEquals(result, CannotOpenHeaderFileFailure);

    /// <summary>
    /// Gives the output entry of the URL at <paramref name="index" />: the parser gives every URL
    /// one, at the URL's own position, whether or not an output option was paired with it.
    /// </summary>
    /// <param name="options">The accepted command line.</param>
    /// <param name="index">The URL's position on the command line.</param>
    /// <returns>The entry.</returns>
    private static UrlOutput UrlOutputOf(CommandLineOptions options, int index) => options.UrlOutputs[index];

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
    /// Tells whether any URL after the one at <paramref name="index" /> switches standard output
    /// to binary mode: one that saves no file, sending its body to standard output or discarding
    /// it under <c>--out-null</c>.
    /// </summary>
    /// <param name="options">The running option group.</param>
    /// <param name="index">The URL's position in its option group.</param>
    /// <returns>
    /// <see langword="true" /> when a later URL of the group, or any URL of a later group, saves no
    /// file: curl 8.21.0 wrote <c>-o NUL -w "%{exitcode}\n" A --next B</c>'s line feed as LF, and as
    /// CR LF with <c>-o NUL</c> in the second group too (measured 2026-09-28, BL-509 Notes).
    /// </returns>
    private bool LaterUrlSwitchesStandardOutputToBinary(CommandLineOptions options, int index) =>
        UrlFromSwitchesStandardOutputToBinary(options, index + 1)
        || Running.LaterGroups.Any(group => UrlFromSwitchesStandardOutputToBinary(group, 0));

    /// <summary>
    /// Tells whether any URL of <paramref name="options" /> from <paramref name="first" /> on saves no file.
    /// </summary>
    /// <param name="options">The option group.</param>
    /// <param name="first">The position of the first URL looked at.</param>
    /// <returns>
    /// <see langword="true" /> when one of those URLs saves no file; never under <c>-B</c>, which
    /// leaves standard output in text mode.
    /// </returns>
    private static bool UrlFromSwitchesStandardOutputToBinary(CommandLineOptions options, int first)
    {
        if (options.UseAscii)
        {
            return false;
        }

        for (int later = first; later < options.Urls.Count; later++)
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
    /// <param name="standardOutputSwitchedToBinary">Whether this or an earlier transfer switched standard output to binary mode.</param>
    /// <param name="endsTheRun">Whether this transfer is the run's last.</param>
    /// <returns>
    /// Always <see langword="true" /> off Windows. On Windows, <see langword="true" /> when this or
    /// an earlier transfer switched standard output to binary mode, or when the run goes on and a later
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
        bool standardOutputSwitchedToBinary,
        bool endsTheRun) =>
        !runsOnWindows
        || standardOutputSwitchedToBinary
        || (!endsTheRun && LaterUrlSwitchesStandardOutputToBinary(options, index));

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

        TransferWriteOutVariables variables = WriteOutVariables(options, transfer, givenUrl, transferUrl, result, connectionId);
        Stream liveStandardOutput = Running.StandardOutput.HasWriteFailed ? Stream.Null : Running.StandardOutput;
        Stream writeOutStandardOutput = standardOutputIsBinary
            ? liveStandardOutput
            : new LineFeedToCrLfStream(liveStandardOutput);
        Stream writeOutStandardError = runsOnWindows ? new LineFeedToCrLfStream(standardError) : standardError;
        await writeOutRenderer
            .RenderAsync(template, variables, writeOutStandardOutput, writeOutStandardError)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// The values <c>-w</c> prints for one transfer. <c>%{referer}</c> is the <c>Referer</c> the
    /// last request was sent with - under <c>-e "...;auto" -L</c> the URL the last redirect came
    /// from, as the redirect follower reports it - else the <c>-e</c> text (BL-361 Notes).
    /// </summary>
    private TransferWriteOutVariables WriteOutVariables(
        CommandLineOptions options,
        UrlTransfer transfer,
        string givenUrl,
        string transferUrl,
        TransferResult result,
        long connectionId)
    {
        string requestUrl = UrlEffective.Normalize(QueryUrl.Append(transferUrl, options), options.PathAsIs);
        return new(result, givenUrl, transfer.UrlNumber, requestUrl, WriteOutScheme(requestUrl, result), timeProvider)
        {
            Referer = result.Report?.Referer ?? options.Referer,
            OutputFileName = Running.OutputFileName,
            ConnectionId = connectionId,
            TransferId = HasNoTransferNumber(result) ? NoTransferId : Running.RetryTransferId ?? transfer.TransferId,
            RetryCount = Running.RetryCount,
            SslVerifyResult = Running.SslVerifyResult,
            ProxySslVerifyResult = Running.ProxySslVerifyResult,
            TlsEarlyDataSent = Running.TlsEarlyDataSent,
        };
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

        Stream liveStandardOutput = Running.StandardOutput.HasWriteFailed ? Stream.Null : Running.StandardOutput;
        Stream jarStandardOutput = standardOutputIsBinary ? liveStandardOutput : new LineFeedToCrLfStream(liveStandardOutput);
        await cookies.WriteCookieJarAsync(fileSystem, jarStandardOutput, timeProvider.GetUtcNow()).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes the <c>-c</c> jar (<see cref="WriteCookieJarAsync" />), then the transfer's <c>--alt-svc</c>
    /// file, when it has one (<see cref="RunningTransferState.AltSvc" />), then the group's <c>--hsts</c>
    /// file, when it has one (<see cref="HstsCacheFile.WriteAsync" />).
    /// </summary>
    /// <param name="dispatch">What the run transfers through, with its cookies.</param>
    /// <param name="options">The transfer's option group.</param>
    /// <param name="transferUrl">The URL transferred.</param>
    /// <param name="standardOutputIsBinary">Whether standard output is in binary mode for <c>-c -</c>.</param>
    /// <param name="state">The transfer's state, with its alt-svc cache.</param>
    /// <returns>A task that completes when all are written.</returns>
    private async Task WriteCookieJarAltSvcAndHstsFilesAsync(
        TransferDispatch dispatch,
        CommandLineOptions options,
        string transferUrl,
        bool standardOutputIsBinary,
        RunningTransferState state)
    {
        await WriteCookieJarAsync(dispatch, options, transferUrl, standardOutputIsBinary).ConfigureAwait(false);
        if (state.AltSvc is { } altSvc)
        {
            await altSvc.WriteAsync(fileSystem).ConfigureAwait(false);
        }

        if (options.HstsFile is { } hstsFile)
        {
            await HstsCacheFile.WriteAsync(hstsFile, Hsts, fileSystem, runsOnWindows).ConfigureAwait(false);
        }
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
    /// <param name="events">Where the files' <c>-v</c> lines go.</param>
    /// <returns>A task that completes when the files are loaded.</returns>
    private async Task LoadCookieFilesAsync(TransferDispatch dispatch, CommandLineOptions options, string transferUrl, ITransferEvents events)
    {
        if (dispatch.Cookies is { } cookies && IsHttpUrl(QueryUrl.Append(transferUrl, options)))
        {
            await cookies.LoadCookieFilesAsync(fileSystem, standardInput, timeProvider.GetUtcNow(), events).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Reads the group's <c>--hsts</c> file into the run's HSTS cache, then switches an <c>http</c> URL whose
    /// host the cache knows to <c>https</c>, printing curl 8.21.0's <c>-v</c> line with the URL
    /// <c>%{url_effective}</c> then shows, as curl does just before it connects (BL-621 Notes).
    /// </summary>
    /// <param name="options">The transfer's option group.</param>
    /// <param name="transferUrl">The URL about to be transferred.</param>
    /// <param name="events">Where the <c>-v</c> line goes.</param>
    /// <returns>The URL to transfer: switched, or as it was.</returns>
    private async Task<string> SwitchToHttpsForHstsAsync(CommandLineOptions options, string transferUrl, ITransferEvents events)
    {
        if (options.HstsFile is { } file)
        {
            await HstsCacheFile.ReadAsync(file, Hsts, fileSystem).ConfigureAwait(false);
        }

        if (!CurlUrl.TryParse(QueryUrl.Append(transferUrl, options), options.PathAsIs, out CurlUrl? url)
            || !Hsts.TrySwitchToHttps(transferUrl, url, out string? httpsUrl))
        {
            return transferUrl;
        }

        events.ReportInfo(HstsTransferPolicy.SwitchedMessagePrefix + UrlEffective.Normalize(QueryUrl.Append(httpsUrl, options), options.PathAsIs));
        return httpsUrl;
    }

    /// <summary>
    /// Gives an <c>http</c> or <c>https</c> transfer of a group with <c>--alt-svc</c> its own alt-svc cache,
    /// its file read now, as curl 8.21.0 gives each transfer's handle one; any other transfer gets none, and
    /// its file is neither read nor written (measured 2026-09-29, BL-623 Notes).
    /// </summary>
    /// <param name="options">The transfer's option group.</param>
    /// <param name="transferUrl">The URL transferred.</param>
    /// <returns>A task that completes when the file is read.</returns>
    private async Task OpenAltSvcCacheAsync(CommandLineOptions options, string transferUrl)
    {
        if (options.AltSvcFile is { } file && IsHttpUrl(QueryUrl.Append(transferUrl, options)))
        {
            Running.AltSvc = await AltSvcTransferCache
                .OpenAsync(file, options.HttpVersion, options.ConnectToEntries, fileSystem, timeProvider, diagnosticLog)
                .ConfigureAwait(false);
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
        UsedAConnection(result) ? Running.ConnectionId ?? nextConnectionId++ : NoConnectionId;

    /// <summary>
    /// Sets the running transfer's <see cref="RunningTransferState.Events" />, whose lines under
    /// <c>--trace-ids</c> carry <c>[&lt;xfer&gt;-&lt;conn&gt;] </c>, the transfer taking its
    /// <c>%{conn_id}</c> at its first event; and returns the sink for what comes before it
    /// connects, whose lines carry <c>[&lt;xfer&gt;-x] </c>, as curl 8.21.0 marked
    /// <c>Added a.test:1:127.0.0.1 to DNS cache</c> <c>[0-x]</c> and the lines after it <c>[0-0]</c>
    /// (measured 2026-09-29, BL-648 Notes). With <c>-w</c> the events also record the certificate
    /// verify codes <c>%{ssl_verify_result}</c> and <c>%{proxy_ssl_verify_result}</c> print and the
    /// early data bytes <c>%{tls_earlydata}</c> prints (<see cref="TlsResultRecordingTransferEvents" />,
    /// BL-661, BL-1150); nothing else reads them.
    /// Where <c>-w</c> or <c>--trace-ids</c> prints it, a transfer that reuses a connection takes that connection's <c>%{conn_id}</c> (<see cref="ConnectionIdRecordingTransferEvents" />, BL-1052).
    /// Under <c>--trace-config dns</c> a failed resolve's <c>[DNS] [1] destroy async</c> follows its
    /// <c>closing connection #N</c> (<see cref="AsyncResolveTeardownTraceEvents" />, BL-1157).
    /// Under <c>--trace-config read</c> a finished transfer's <c>[READ] client_reset, clear readers</c>
    /// comes before its connection's last line (<see cref="ClientReaderResetTraceEvents" />, BL-1159).
    /// </summary>
    /// <param name="options">The transfer's option group.</param>
    /// <param name="transfer">The transfer.</param>
    /// <returns>The sink for the <c>--resolve</c> entries and <c>-b</c> files the transfer loads.</returns>
    private ITransferEvents SetUpTransferEvents(CommandLineOptions options, UrlTransfer transfer)
    {
        RunningTransferState state = Running;
        Func<long> takeConnectionId = () => state.ConnectionId ??= nextConnectionId++;
        ITransferEvents output = transferEventOutput.EventsFor(transfer.TransferId, () => takeConnectionId());
        ITransferEvents events = WithTraceLineEvents(options, output, takeConnectionId);
        ITransferEvents recorded = options.WriteOut is null ? events : new TlsResultRecordingTransferEvents(events, state);
        state.Events = options.WriteOut is null && !options.TraceIds
            ? recorded
            : new ConnectionIdRecordingTransferEvents(recorded, state, connectionIdsByPoolNumber, takeConnectionId);
        return EventsBeforeConnecting(transfer);
    }

    /// <summary>
    /// The sink for what <paramref name="transfer" /> reports before it connects, whose lines under
    /// <c>--trace-ids</c> carry <c>[&lt;xfer&gt;-x] </c>.
    /// </summary>
    /// <param name="transfer">The transfer.</param>
    /// <returns>The transfer's events before connecting.</returns>
    private ITransferEvents EventsBeforeConnecting(UrlTransfer transfer) =>
        transferEventOutput.EventsFor(transfer.TransferId, () => null);

    /// <summary>
    /// Wraps <paramref name="output" /> in the events that add the trace lines the transfer writes
    /// after its connect has returned: <see cref="AsyncResolveTeardownTraceEvents" /> under
    /// <see cref="CurlComposition.TracesDns" />, <see cref="MultiStateTraceEvents" /> under
    /// <see cref="CurlComposition.TracesMulti" />, <see cref="ClientReaderResetTraceEvents" /> under
    /// <see cref="CurlComposition.TracesRead" /> and, outermost so its <c>[WRITE] [OUT] done</c> comes before
    /// the <c>[READ]</c> line, <see cref="ClientWriterTraceEvents" /> under <see cref="CurlComposition.TracesWrite" />.
    /// The <c>[MULTI]</c> lines sit inside the other two, which is what lets them find their place among those lines.
    /// Under <see cref="CurlComposition.TracesTimer" /> the response wait's <c>[TIMER]</c> lines
    /// (<see cref="TransferTimers.WaitLines" />) go among the <c>[MULTI]</c> lines, or, with the multi not
    /// traced, through <see cref="ResponseWaitTimerTraceEvents" /> in their place (BL-1258).
    /// </summary>
    /// <param name="options">The transfer's option group.</param>
    /// <param name="output">The transfer's own events.</param>
    /// <param name="takeConnectionId">Gives the number of the connection the transfer opens.</param>
    /// <returns>The events the transfer reports to.</returns>
    private ITransferEvents WithTraceLineEvents(CommandLineOptions options, ITransferEvents output, Func<long> takeConnectionId)
    {
        ITransferEvents teardown = CurlComposition.TracesDns(options) ? new AsyncResolveTeardownTraceEvents(output) : output;
        ITransferEvents multi = WithMultiOrTimerEvents(options, teardown, takeConnectionId);
        ITransferEvents readers = CurlComposition.TracesRead(options) ? new ClientReaderResetTraceEvents(multi) : multi;
        return CurlComposition.TracesWrite(options) ? new ClientWriterTraceEvents(readers) : readers;
    }

    private ITransferEvents WithMultiOrTimerEvents(CommandLineOptions options, ITransferEvents inner, Func<long> takeConnectionId)
    {
        TransferTimers timers = TransferTimers.Of(options);
        bool tracesMulti = CurlComposition.TracesMulti(options);
        IReadOnlyList<string> waitLines = CurlComposition.TracesTimer(options) ? timers.WaitLines(tracesMulti) : [];
        if (tracesMulti)
        {
            return new MultiStateTraceEvents(inner, timeProvider, takeConnectionId, timers, waitLines);
        }

        return waitLines.Count > 0 ? new ResponseWaitTimerTraceEvents(inner, waitLines) : inner;
    }

    /// <summary>
    /// Writes the trace lines curl 8.21.0 writes as the transfer starts, after the <c>--resolve</c> entries'
    /// lines and before anything it resolves or dials, with <c>[&lt;xfer&gt;-x]</c> under <c>--trace-ids</c>:
    /// under <c>--trace-config multi</c> (or <c>network</c>, <c>all</c>, <c>-vvvv</c>)
    /// <see cref="MultiStateTraceEvents.StartLines" /> (BL-1188 Notes), then under <c>--trace-config read</c>
    /// (or <c>-vvv</c>, <c>all</c>) <see cref="ClientReaderResetTraceEvents.ResetLine" /> (BL-1159 Notes).
    /// </summary>
    /// <param name="options">The transfer's option group.</param>
    /// <param name="eventsBeforeConnecting">The transfer's events before it has a connection.</param>
    private static void TraceTransferStart(CommandLineOptions options, ITransferEvents eventsBeforeConnecting)
    {
        if (CurlComposition.TracesMulti(options))
        {
            foreach (string line in MultiStateTraceEvents.StartLines)
            {
                eventsBeforeConnecting.ReportInfo(line);
            }
        }

        if (CurlComposition.TracesRead(options))
        {
            eventsBeforeConnecting.ReportInfo(ClientReaderResetTraceEvents.ResetLine);
        }
    }

    /// <summary>
    /// Refuses a <c>--interface</c> value libcurl refuses when curl sets it
    /// (<see cref="InterfaceBinding.IsMalformed" />): curl 8.21.0 reports <c>setopt 0x274e got bad
    /// argument</c> as a <c>-v</c> line, fails the transfer with exit 43 and that message, still writes
    /// its <c>-w</c> output, and transfers nothing after it (measured 2026-09-29, BL-600 Notes).
    /// </summary>
    /// <param name="options">The transfer's option group.</param>
    /// <param name="events">Where the <c>-v</c> line goes.</param>
    /// <returns><see cref="InterfaceSetoptFailure" />, or <see langword="null" /> when the value was accepted.</returns>
    private static TransferResult? RefuseMalformedInterface(CommandLineOptions options, ITransferEvents events)
    {
        if (options.Interface is not { IsMalformed: true })
        {
            return null;
        }

        events.ReportInfo(InterfaceSetoptMessage);
        return InterfaceSetoptFailure;
    }

    /// <summary>
    /// Refuses an <c>--ech</c> mode libcurl refuses when curl sets it
    /// (<see cref="CommandLineOptions.EchModeIsMalformed" />): curl 8.21.0's ECH build reports
    /// <c>setopt 0x2855 got bad argument</c> and fails the transfer with exit 43 (BL-1107), as it
    /// fails a malformed <c>--interface</c>, so the <c>-v</c> line and the <c>-w</c> output follow that
    /// measurement (ADR-0378).
    /// </summary>
    /// <param name="options">The transfer's option group.</param>
    /// <param name="events">Where the <c>-v</c> line goes.</param>
    /// <returns><see cref="EchSetoptFailure" />, or <see langword="null" /> when the mode was accepted.</returns>
    private static TransferResult? RefuseMalformedEchMode(CommandLineOptions options, ITransferEvents events)
    {
        if (!options.EchModeIsMalformed)
        {
            return null;
        }

        events.ReportInfo(EchSetoptMessage);
        return EchSetoptFailure;
    }

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
    private bool EndsTheRun(CommandLineOptions options, TransferResult result) =>
        RunEndingFailures.Contains(result)
        || ReferenceEquals(result, cannotOpenUploadFileAfterFailure)
        || (options.FailEarly && !result.IsSuccess);

    /// <summary>
    /// The result of a transfer whose <c>-T</c> file cannot be opened. curl 8.21.0 does not set
    /// one of its own: it reports the run's previous transfer's code, with the text
    /// <see cref="CurlEasyErrorText" /> gives it, when that transfer failed, and exit 26 when it
    /// succeeded or there was none. <c>-T '{local.txt,nosuch}' http://127.0.0.1:1/g/</c> printed
    /// <c>curl: (7) Could not connect to server</c> for <c>nosuch</c> and exited 7 (measured
    /// 2026-09-27, BL-440 Notes). Either way the run ends.
    /// </summary>
    /// <returns>
    /// <see cref="CannotOpenUploadFileFailure" />, or the previous transfer's failure code with
    /// curl's text for it, kept in <see cref="cannotOpenUploadFileAfterFailure" />.
    /// </returns>
    private TransferResult CannotOpenUploadFileResult()
    {
        if (previousTransferResult is not { IsSuccess: false } previous)
        {
            return CannotOpenUploadFileFailure;
        }

        cannotOpenUploadFileAfterFailure = TransferResult.Failure(previous.ExitCode, CurlEasyErrorText.Of(previous.ExitCode));
        return cannotOpenUploadFileAfterFailure;
    }

    /// <summary>
    /// The results, compared by reference, after which no further URL is transferred whatever
    /// the options: a resumed <c>-o</c> file, a <c>-T</c> or <c>-D</c> file that cannot be opened,
    /// a <c>--create-dirs</c> directory that cannot be created, an IPFS URL that cannot be
    /// rewritten or asked for its remote name, an SSH transfer with no known-hosts file, and an
    /// <c>--interface</c> value or <c>--ech</c> mode libcurl refuses when curl sets it.
    /// </summary>
    private static readonly HashSet<TransferResult> RunEndingFailures = new(ReferenceEqualityComparer.Instance)
    {
        CannotOpenForResumeFailure,
        CannotOpenUploadFileFailure,
        CannotOpenHeaderFileFailure,
        CannotCreateDirectoryFailure,
        IpfsGatewayDetectionFailure,
        IpfsMalformedGatewayOptionFailure,
        IpfsMalformedTargetUrlFailure,
        IpfsRemoteNameFailure,
        KnownHostsFileMissingFailure,
        InterfaceSetoptFailure,
        EchSetoptFailure,
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
    /// Formats the line printed for a failed transfer that carries a message, with the message
    /// cut to curl's error buffer (<see cref="CurlErrorBuffer" />). A bad glob's lines are not
    /// cut: curl's tool formats them itself (measured 2026-09-27, BL-380 Notes).
    /// </summary>
    /// <param name="result">The failed transfer's result.</param>
    /// <returns>The line <see cref="FormatErrorLine" /> gives for the cut message.</returns>
    private static string FormatTransferErrorLine(TransferResult result) =>
        FormatErrorLine(result, CurlErrorBuffer.Truncate(result.ErrorMessage));

    /// <summary>
    /// Formats the line printed for a failure that carries a message.
    /// </summary>
    /// <param name="result">The failure's result.</param>
    /// <param name="message">The message to print.</param>
    /// <returns>
    /// <see cref="FailedWritingBodyLine" /> when standard output could not be written;
    /// otherwise <c>curl: (N) &lt;message&gt;</c>.
    /// </returns>
    private static string FormatErrorLine(TransferResult result, string? message)
    {
        if (ReferenceEquals(result, StandardOutputWriteFailure))
        {
            return FailedWritingBodyLine;
        }

        string code = ((int)result.ExitCode).ToString(CultureInfo.InvariantCulture);

        return $"curl: ({code}) {message}";
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
            return await TransferReportingHeaderWriteFailureAsync(dispatch, options, url, uploadFile, transfer, headerFile, GatedStandardOutput)
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
                transfer.TransferId == Running.FirstTransferIdOfGroup ? FileWriteMode.Truncate : FileWriteMode.Append,
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
    /// through a <see cref="DumpHeaderOutputStream" />, which prints curl's
    /// <c>curl: Failed writing headers to &lt;file&gt;</c> line to standard error the moment a
    /// header write fails, naming the <c>-D</c> value as given, unless <c>-s</c> was given
    /// without <c>-S</c>; curl 8.21.0 prints it before the handler's <c>-v</c> line and the
    /// transfer's <c>curl: (23)</c> line (measured 2026-09-26, BL-111 Notes; BL-388).
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
        DumpHeaderOutputStream headerOutput = new(destination, headerFile, ShowsErrors(options) ? standardError : null);

        return await TransferAsync(dispatch, options, url, uploadFile, transfer, headerOutput).ConfigureAwait(false);
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
    /// The transfer's result; <see cref="NoCryptoEngines.LoadFailure" />'s failure, before any upload
    /// file is opened or the URL parsed, for an <c>--engine</c> off Windows; <see cref="ByteRangeParser.NotDeliveredFailure" />, with nothing
    /// transferred, when the <c>-r</c> text names no range on an <c>sftp</c> or <c>scp</c> URL, as curl 8.21.0 reports it; the
    /// <c>-F</c> body's build failure, with nothing transferred, when a form file cannot be opened;
    /// <see cref="CannotOpenUploadFileResult" />, with nothing transferred, when the <c>-T</c> file
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
        // curl switches standard output to binary mode as the transfer starts, so the transfer's
        // own --trace - lines already end in a bare line feed (measured, BL-546 Notes); under -B
        // it leaves standard output in text mode (BL-961 Notes).
        standardOutputSwitchedToBinary |= !options.UseAscii && !transfer.WritesToFile;
        if (!options.Silent)
        {
            await WriteErrorLinesAsync(dispatch.WarningLinesBeforeEachTransfer).ConfigureAwait(false);
        }

        Stream? etagWatchedHeaderOutput = Running.SaveEtag is { } saveEtag ? new EtagSaveStream(saveEtag, headerOutput) : headerOutput;
        return NoCryptoEngines.LoadFailure(options.Engine, runsOnWindows)
            ?? await TransferOpeningUploadFileAsync(dispatch, options, url, uploadFile, transfer, etagWatchedHeaderOutput).ConfigureAwait(false);
    }

    /// <summary>
    /// Performs one transfer for <see cref="TransferAsync" /> once its warning lines are written,
    /// opening the <c>-T</c> file first when it names one other than standard input.
    /// </summary>
    /// <param name="dispatch">Performs the transfer with the handler for its scheme.</param>
    /// <param name="options">The accepted command line.</param>
    /// <param name="url">The URL to transfer.</param>
    /// <param name="uploadFile">The URL's <c>-T</c> file, or <see langword="null" /> when it uploads nothing.</param>
    /// <param name="transfer">The transfer, which names its output.</param>
    /// <param name="headerOutput">Where the <c>-D</c> header lines go, or <see langword="null" /> without <c>-D</c>.</param>
    /// <returns>The transfer's result, as <see cref="TransferAsync" /> describes it.</returns>
    private async Task<TransferResult> TransferOpeningUploadFileAsync(
        TransferDispatch dispatch,
        CommandLineOptions options,
        string url,
        string? uploadFile,
        UrlTransfer transfer,
        Stream? headerOutput)
    {
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
            return CannotOpenUploadFileResult();
        }

        UpdateLibcurlTransfer(entry => entry with { UploadFileSize = opened.Length });
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
                TransferProxySelection.TrySelect(dispatch.ProxySelector, options, hopUrl, out hopProxy, out hopFailure, diagnosticLog),
            hsts: Hsts,
            selectHopAltSvc: (hopUrl, hopHttp) => Running.AltSvc is { } altSvc ? altSvc.ApplyTo(hopUrl, hopHttp) : hopHttp,
            selectHopCredentials: CredentialLookup.ForRedirectHops(options));

        if (!TryParseTransferUrl(QueryUrl.Append(url, options), options, out CurlUrl? transferUrl, out CurlUrlRejection rejection))
        {
            return ReportUrlRejected(rejection, EventsBeforeConnecting(transfer));
        }

        if (ParsedUrlRefusal(transferUrl) is { } refusal)
        {
            return refusal;
        }

        if (!TryParseRange(options.Range, transferUrl, out ByteRange? range))
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
            .BuildAsync(
                MultipartFormPartMapping.FromCommandLine(options.FormParts),
                MultipartFormPartMapping.NameEscapingOf(options),
                CancellationToken.None)
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
    /// Parses the transfer's URL; under <c>--disallow-username-in-url</c> a URL with user information,
    /// even an empty user, is rejected with <see cref="CurlUrlRejection.UserNotAllowed" /> before its
    /// host and port are checked, so it fails with exit 67 even when its host or port is bad (curl
    /// 8.21.0, measured, BL-626 and BL-910 Notes).
    /// </summary>
    private static bool TryParseTransferUrl(
        string text,
        CommandLineOptions options,
        [NotNullWhen(true)] out CurlUrl? url,
        out CurlUrlRejection rejection) =>
        options.DisallowUsernameInUrl
            ? CurlUrl.TryParseDisallowingUser(text, options.PathAsIs, out url, out rejection)
            : CurlUrl.TryParse(text, options.PathAsIs, out url, out rejection);

    /// <summary>
    /// The failure for a transfer URL curl's parser rejects: exit 67 for
    /// <see cref="CurlUrlRejection.UserNotAllowed" />, exit 3 for every other rejection, each with
    /// <c>URL rejected: </c> and curl's message.
    /// </summary>
    private static TransferResult UrlRejectedFailure(CurlUrlRejection rejection) =>
        TransferResult.Failure(
            rejection == CurlUrlRejection.UserNotAllowed ? CurlExitCode.LoginDenied : CurlExitCode.UrlMalformat,
            UrlRejectedPrefix + rejection.ToCurlMessage());

    /// <summary>
    /// Reports a transfer URL curl's parser rejects as the <c>-v</c> info line
    /// <c>URL rejected: &lt;reason&gt;</c>, as curl 8.21.0's <c>failf</c> in <c>lib/url.c</c> does
    /// (measured 2026-10-03, BL-1332), and returns <see cref="UrlRejectedFailure" />.
    /// </summary>
    /// <param name="rejection">Why the URL was rejected.</param>
    /// <param name="events">The transfer's events before connecting.</param>
    /// <returns>The failure carrying the same text.</returns>
    private static TransferResult ReportUrlRejected(CurlUrlRejection rejection, ITransferEvents events)
    {
        events.ReportInfo(UrlRejectedPrefix + rejection.ToCurlMessage());
        return UrlRejectedFailure(rejection);
    }

    /// <summary>
    /// Why the parsed URL is refused before any connection, or <see langword="null" /> when it is not:
    /// a host longer than <see cref="MaximumHostLength" /> fails with exit 3 and
    /// <see cref="TooLongHostnameMessage" />.
    /// </summary>
    private static TransferResult? ParsedUrlRefusal(CurlUrl url)
    {
        return Encoding.UTF8.GetByteCount(url.Host) > MaximumHostLength
            ? TransferResult.Failure(CurlExitCode.UrlMalformat, TooLongHostnameMessage)
            : null;
    }

    /// <summary>
    /// Chooses the transfer's proxy with <see cref="TransferProxySelection" />, then its credentials
    /// with <see cref="TransferCredentialLookup" />, which it keeps as the running transfer's
    /// <see cref="RunningTransferState.LookedUpCredentials" /> for every attempt's context.
    /// </summary>
    /// <param name="proxySelector">Chooses the transfer's proxy.</param>
    /// <param name="options">The accepted command line.</param>
    /// <param name="url">The transfer's URL.</param>
    /// <param name="proxy">The proxy chosen, or <see langword="null" /> to connect directly.</param>
    /// <param name="failure">The proxy's or the netrc file's failure, when either refuses the transfer.</param>
    /// <returns><see langword="false" /> when the transfer ends before it starts.</returns>
    private bool TrySelectProxyAndCredentials(
        ProxySelector proxySelector,
        CommandLineOptions options,
        CurlUrl url,
        out ProxyEndpoint? proxy,
        [NotNullWhen(false)] out TransferResult? failure)
    {
        if (!TransferProxySelection.TrySelect(proxySelector, options, url, out proxy, out failure, diagnosticLog))
        {
            return false;
        }

        bool looked = CredentialLookup.TryLookUp(options, url, out NetworkCredential? lookedUpCredentials, out failure);
        Running.LookedUpCredentials = lookedUpCredentials;
        return looked;
    }

    /// <summary>
    /// Sets <see cref="RunningTransferState.Ssh" /> for an <c>scp</c> or <c>sftp</c> transfer, resolving its
    /// known-hosts file as curl 8.21.0's tool does (ADR-0122): none under <c>-k</c>, whatever
    /// <c>--knownhosts</c> says; else the <c>--knownhosts</c> file; else <see cref="KnownHostsSearch" />'s.
    /// When none is found, a <c>--hostpubmd5</c> or <c>--hostpubsha256</c> fingerprint lets the transfer
    /// go on after <c>Warning: Could not find a known_hosts file</c> (hidden by <c>-s</c>); without one,
    /// <c>curl: Could not find a known_hosts file</c> (hidden by <c>-s</c> unless <c>-S</c>) and
    /// <see cref="KnownHostsFileMissingFailure" /> end the run before any connection (measured 2026-09-29,
    /// BL-576 Notes). Any other scheme is left without SSH options.
    /// </summary>
    /// <param name="options">The transfer's option group.</param>
    /// <param name="url">The URL to transfer.</param>
    /// <returns><see cref="KnownHostsFileMissingFailure" />, or <see langword="null" /> to go on.</returns>
    private async Task<TransferResult?> ResolveSshOptionsAsync(CommandLineOptions options, CurlUrl url)
    {
        if (!SshOptionsMapping.IsSshScheme(url.Scheme))
        {
            return null;
        }

        string? knownHosts = options.Insecure ? null : options.SshKnownHostsFile ?? KnownHostsSearch.Find(DataFileReader);
        UpdateLibcurlTransfer(entry => entry with { SshKnownHostsFile = knownHosts, SshKnownHostsFileMissing = knownHosts is null && !options.Insecure });
        if (knownHosts is null && !options.Insecure
            && await ReportKnownHostsFileMissingAsync(options).ConfigureAwait(false) is { } failure)
        {
            return failure;
        }

        Running.Ssh = SshOptionsMapping.FromCommandLine(options, knownHosts);
        return null;
    }

    /// <summary>
    /// Reports that an SSH transfer checked by known hosts has no known-hosts file, for
    /// <see cref="ResolveSshOptionsAsync" />: a warning when a host key fingerprint was given, and
    /// otherwise curl's line and <see cref="KnownHostsFileMissingFailure" />.
    /// </summary>
    /// <param name="options">The transfer's option group.</param>
    /// <returns><see cref="KnownHostsFileMissingFailure" />, or <see langword="null" /> when the transfer goes on.</returns>
    private async Task<TransferResult?> ReportKnownHostsFileMissingAsync(CommandLineOptions options)
    {
        if (options.SshHostPublicKeyMd5 is not null || options.SshHostPublicKeySha256 is not null)
        {
            await WriteWarningUnlessSilentAsync(options, KnownHostsFileMissingWarning).ConfigureAwait(false);
            return null;
        }

        if (ShowsErrors(options))
        {
            await WriteErrorLineAsync(KnownHostsFileMissingLine).ConfigureAwait(false);
        }

        return KnownHostsFileMissingFailure;
    }

    /// <summary>
    /// Performs one checked transfer, sending <paramref name="formBody" /> when given, to the file
    /// <see cref="ResolveOutputFileAsync" /> names when it names one, nowhere under <c>--out-null</c>
    /// (<see cref="Stream.Null" />, with the progress meter drawn as for a file) and to standard
    /// output otherwise, and writes the progress meter after it. The transfer goes through the proxy
    /// <see cref="TransferProxySelection" /> chooses, with the credentials
    /// <see cref="TransferCredentialLookup" /> chooses; when either fails, the transfer ends with its
    /// failure and nothing is sent (<see cref="TrySelectProxyAndCredentials" />).
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
    /// transferred, when a <c>--create-dirs</c> directory cannot be created; a success with
    /// nothing transferred when <c>--skip-existing</c> finds the output file already there
    /// (<see cref="SkipExistingOutputFileAsync" />). Both are decided before the proxy is chosen.
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
        Running.UploadResumesFromUnknownOffset = options.ResumeFromOutputSize && upload is not null;
        string? outputFile = await ResolveOutputFileAsync(options, transfer, url).ConfigureAwait(false);
        if (outputFile is not null
            && await CreateOutputDirectoriesOrSkipAsync(options, outputFile).ConfigureAwait(false) is { } unstarted)
        {
            return unstarted;
        }

        if (!TrySelectProxyAndCredentials(proxySelector, options, url, out ProxyEndpoint? proxy, out TransferResult? proxyFailure))
        {
            return proxyFailure;
        }

        if (await ResolveSshOptionsAsync(options, url).ConfigureAwait(false) is { } sshFailure)
        {
            return sshFailure;
        }

        if (outputFile is null)
        {
            bool toStandardOutput = !transfer.DiscardsBody;
            StartTransferProgress(options, options.ResumeFrom, toStandardOutput);
            Func<TransferContext> createAttemptContext = () => transferContextFactory.Create(
                options,
                url,
                RateLimited(options, toStandardOutput ? FlushedEachWriteUnderNoBuffer(options, InTextModeUnderUseAscii(options, Running.GatedStandardOutput)) : Stream.Null),
                range,
                options.ResumeFrom,
                headerOutput,
                formBody,
                upload,
                proxy,
                progress: Running.Progress,
                events: Running.Events,
                lowSpeedWatchdog: StartLowSpeedWatchdog(options),
                abortToken: Running.AbortToken,
                maxTimeWatchdog: StartMaxTimeWatchdog(options),
                lookedUpCredentials: Running.LookedUpCredentials,
                ifNoneMatchHeaders: Running.IfNoneMatchHeaders,
                altSvc: Running.AltSvc,
                bodyHeaderStyles: toStandardOutput ? HeaderStylesFor(options, url) : null,
                ssh: Running.Ssh);
            TransferResult result = toStandardOutput
                ? await TransferToStandardOutputAsync(follower, options, createAttemptContext).ConfigureAwait(false)
                : await FollowRetryingAsync(follower, options, createAttemptContext, options.ResumeFrom, null).ConfigureAwait(false);

            return await WriteProgressAsync(options, result, options.ResumeFrom, toStandardOutput)
                .ConfigureAwait(false);
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
    /// Gets what creates <c>--create-dirs</c> directories and checks for a <c>--skip-existing</c>
    /// file: the one the runner was given, else <see cref="DiskOutputPaths" />.
    /// </summary>
    private IOutputPaths OutputPaths => outputPaths ?? DiskOutputPaths;

    /// <summary>
    /// Makes the <c>--create-dirs</c> directories for <paramref name="outputFile" />, then, under
    /// <c>--skip-existing</c>, skips the transfer when the file is already there, in curl
    /// 8.21.0's order.
    /// </summary>
    /// <param name="options">The accepted command line.</param>
    /// <param name="outputFile">The transfer's output file.</param>
    /// <returns>
    /// <see cref="CannotCreateDirectoryFailure" /> when a directory cannot be created, the
    /// skipped transfer's success when the file exists, or <see langword="null" /> when the
    /// transfer goes ahead.
    /// </returns>
    private async Task<TransferResult?> CreateOutputDirectoriesOrSkipAsync(CommandLineOptions options, string outputFile)
    {
        if (options.CreateDirectories
            && OutputFileDirectories.CreateLeadingDirectories(OutputPaths, outputFile, runsOnWindows) is { } directory)
        {
            return await ReportCannotCreateDirectoryAsync(options, directory).ConfigureAwait(false);
        }

        return options.SkipExisting && OutputPaths.Exists(outputFile)
            ? await SkipExistingOutputFileAsync(options, outputFile).ConfigureAwait(false)
            : null;
    }

    /// <summary>
    /// Skips a transfer under <c>--skip-existing</c> because its output file is already there, as
    /// curl 8.21.0 does: nothing is connected, opened or written, no progress meter is drawn, and
    /// the transfer succeeds with <c>%{filename_effective}</c> naming the file. Under <c>-v</c> or a
    /// <c>--trace</c> option, <c>-s</c> or not, standard error gets
    /// <c>Note: skips transfer, "&lt;file&gt;" exists locally</c>, wrapped as a note is
    /// (measured 2026-09-28, BL-493 Notes).
    /// </summary>
    /// <param name="options">The accepted command line.</param>
    /// <param name="outputFile">The output file that exists.</param>
    /// <returns>A successful result with no bytes moved.</returns>
    private async Task<TransferResult> SkipExistingOutputFileAsync(CommandLineOptions options, string outputFile)
    {
        Running.OutputFileName = outputFile;
        if (options.Trace != TraceKind.None)
        {
            await WriteErrorPiecesAsync(WarningLineWrapper.WrapNoteText($"skips transfer, \"{outputFile}\" exists locally", terminalColumns))
                .ConfigureAwait(false);
        }

        return TransferResult.Success(0);
    }

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
    /// <see cref="ProgressBarRecorder" /> for its <see cref="RunningTransferState.ProgressBar" /> when
    /// <see cref="ShowsProgressBar" /> says the bar is shown, and
    /// <see cref="WriteProgressMeterLive" /> as its live writer when
    /// <see cref="ShowsProgressMeter" /> says the meter is.
    /// </summary>
    /// <param name="options">The accepted command line.</param>
    /// <param name="resumeFrom">The resolved <c>-C</c> offset, or <see langword="null" />.</param>
    /// <param name="toStandardOutput">Whether the transfer writes its body to standard output.</param>
    private void StartTransferProgress(CommandLineOptions options, long? resumeFrom, bool toStandardOutput)
    {
        RunningTransferState state = Running;
        state.ProgressBar = ShowsProgressBar(options, toStandardOutput)
            ? new ProgressBarRecorder(timeProvider, resumeFrom ?? 0, terminalColumns)
            : null;
        state.Progress = new TransferProgressRecorder(
            timeProvider,
            state.ProgressBar,
            ShowsProgressMeter(options, toStandardOutput) ? statusText => WriteProgressMeterLive(state, resumeFrom, statusText) : null,
            eventStandardError,
            state.ParallelProgress);
    }

    /// <summary>
    /// Writes status-line text the handler's reports drew to standard error while the transfer
    /// runs, after the meter's header lines when they are not yet written, and flushes it. The
    /// write is synchronous because <see cref="ITransferProgress" /> reports are.
    /// </summary>
    /// <param name="state">The transfer's state.</param>
    /// <param name="resumeFrom">The resolved <c>-C</c> offset, or <see langword="null" />.</param>
    /// <param name="statusText">The status lines, each starting with a carriage return.</param>
    private void WriteProgressMeterLive(RunningTransferState state, long? resumeFrom, string statusText)
    {
        StringBuilder text = new();
        foreach (string headerLine in TakeProgressMeterHeaderLines(state, resumeFrom))
        {
            text.Append(headerLine).Append(Environment.NewLine);
        }

        standardError.Write(Encoding.UTF8.GetBytes(text.Append(statusText).ToString()));
        standardError.Flush();
    }

    /// <summary>
    /// Writes curl's progress for a finished transfer. Under <c>-#</c>, when the transfer's
    /// <see cref="RunningTransferState.ProgressBar" /> is set, that is everything the bar drew, the last call of a
    /// successful transfer included, and no newline: curl writes that after the transfer's
    /// failure lines (task BL-132). Otherwise it is the progress meter, when
    /// <see cref="ShowsProgressMeter" /> says it is shown: its header lines, then the status
    /// lines its <see cref="RunningTransferState.Progress" /> drew, each starting with a carriage return, then
    /// one newline, as curl 8.21.0 does (task BL-131); under <c>-L</c>, one such line per hop
    /// (task BL-277). The header lines and status lines <see cref="WriteProgressMeterLive" />
    /// already wrote while the transfer ran are not written again (task BL-383).
    /// </summary>
    /// <param name="options">The accepted command line.</param>
    /// <param name="result">
    /// The transfer's result; the meter follows a success, a <c>-f</c> failure
    /// (<see cref="CurlExitCode.HttpReturnedError" />), and any failure after the handler
    /// reported the transfer past connect or open (<see cref="RunningTransferState.Progress" />), which
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
        RunningTransferState state = Running;
        if (state.ProgressBar is { } progressBar)
        {
            progressBar.Finish(result.IsSuccess, result.BytesTransferred);
            await WriteErrorTextAsync(progressBar.Drawn).ConfigureAwait(false);

            return result;
        }

        if (EndsWithProgressMeter(options, result, state, toStandardOutput))
        {
            FinishTransferProgress(state.Progress, result);
            await WriteErrorLinesAsync([.. TakeProgressMeterHeaderLines(state, resumeFrom), state.Progress.TakeUnwrittenStatusLines()])
                .ConfigureAwait(false);
        }

        // Every transfer runs after TransferEachUrlAsync has opened the event output.
        eventStandardError!.Release();

        return result;
    }

    /// <summary>
    /// Tells whether a finished transfer writes its progress meter's end: after a success, after a
    /// <c>-f</c> failure (exit 22) and after a failure once the handler reported the transfer
    /// started, when <see cref="ShowsProgressMeter" /> says the meter is shown.
    /// </summary>
    /// <param name="options">The accepted command line.</param>
    /// <param name="result">The transfer's result.</param>
    /// <param name="state">The transfer's state.</param>
    /// <param name="toStandardOutput">Whether the transfer wrote its body to standard output.</param>
    /// <returns><see langword="true" /> when the meter's end is written.</returns>
    private bool EndsWithProgressMeter(CommandLineOptions options, TransferResult result, RunningTransferState state, bool toStandardOutput) =>
        (result.IsSuccess || result.ExitCode == CurlExitCode.HttpReturnedError || state.Progress.HasTransferStarted)
        && ShowsProgressMeter(options, toStandardOutput);

    /// <summary>
    /// Makes the draws curl 8.21.0 makes as <paramref name="progress" />'s transfer ends: a
    /// redirect hop's when <c>--max-redirs</c> refused to follow its redirect (exit 47), which
    /// curl draws like a followed hop (task BL-277), and the finished transfer's otherwise.
    /// </summary>
    /// <param name="progress">The transfer's progress sink.</param>
    /// <param name="result">The transfer's result.</param>
    private static void FinishTransferProgress(TransferProgressRecorder progress, TransferResult result)
    {
        if (result.ExitCode == CurlExitCode.TooManyRedirects)
        {
            progress.FinishRedirectHop();
        }
        else
        {
            progress.Finish(result.IsSuccess);
        }
    }

    /// <summary>
    /// Gets the progress meter's header lines the first time the transfer writes its
    /// meter, and none after: curl 8.21.0 writes them once however many times <c>--retry</c>
    /// runs the transfer (measured 2026-09-27, BL-241 Notes).
    /// </summary>
    /// <param name="state">The transfer's state.</param>
    /// <param name="resumeFrom">
    /// The resolved <c>-C</c> offset, or <see langword="null" />; a <c>-T</c> upload under
    /// <c>-C -</c> (<see cref="RunningTransferState.UploadResumesFromUnknownOffset" />) names
    /// <see cref="ProgressMeterLines.UnknownUploadOffset" /> instead.
    /// </param>
    /// <returns>The header lines, or none when they were already written.</returns>
    private static IReadOnlyList<string> TakeProgressMeterHeaderLines(RunningTransferState state, long? resumeFrom)
    {
        long? meterResumeFrom = state.UploadResumesFromUnknownOffset ? ProgressMeterLines.UnknownUploadOffset : resumeFrom;
        IReadOnlyList<string> headerLines = state.ProgressMeterHeaderWritten ? [] : ProgressMeterLines.HeaderLines(meterResumeFrom);
        state.ProgressMeterHeaderWritten = true;

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
    /// runner writes it, never under <c>-s</c> or <c>--no-progress-meter</c>, not for a body
    /// written to standard output when that is a terminal, and never under <c>-Z</c>, whose run draws
    /// one combined meter instead (<see cref="ParallelProgressMeter" />), as curl 8.21.0 does.
    /// </summary>
    /// <param name="options">The accepted command line.</param>
    /// <param name="toStandardOutput">Whether the transfer writes its body to standard output.</param>
    /// <returns><see langword="true" /> when progress is shown.</returns>
    private bool ShowsProgress(CommandLineOptions options, bool toStandardOutput) =>
        writesProgressMeter
        && parallelRun is null
        && !options.Silent
        && !options.ProgressMeterOff
        && !(toStandardOutput && standardOutputIsTerminal);

    /// <summary>
    /// Chooses how the <c>-i</c> and <c>-I</c> header lines of a transfer writing to standard
    /// output are styled, as curl 8.21.0's <c>tool_header_cb</c> chooses: only on a terminal
    /// that renders bold, under <c>--styled-output</c> (the default), and for an <c>http</c>,
    /// <c>https</c>, <c>rtsp</c> or <c>file</c> URL (ADR-0246, BL-736).
    /// </summary>
    /// <param name="options">The accepted command line.</param>
    /// <param name="url">The transfer's URL.</param>
    /// <returns>The styles for this platform, or <see langword="null" /> to write the lines as they come.</returns>
    private StyledHeaderLines? HeaderStylesFor(CommandLineOptions options, CurlUrl url) =>
        StylesTerminalOutput(options) && IsStyledHeaderScheme(url.Scheme)
            ? StyledHeaderLines.ForPlatform(runsOnWindows, StyledHeaderBaseUrl(url), EnvironmentVariables("VTE_VERSION"))
            : null;

    /// <summary>
    /// Tells whether standard output is a terminal that renders bold and <c>--styled-output</c> is on.
    /// </summary>
    /// <param name="options">The accepted command line.</param>
    /// <returns><see langword="true" /> when header lines written there are styled, scheme permitting.</returns>
    private bool StylesTerminalOutput(CommandLineOptions options) =>
        standardOutputIsTerminal && terminalRendersStyles && options.StyledOutput;

    /// <summary>
    /// Tells whether curl 8.21.0 styles the header lines of a transfer with <paramref name="scheme" />.
    /// </summary>
    /// <param name="scheme">The transfer URL's scheme, in lower case.</param>
    /// <returns><see langword="true" /> for <c>http</c>, <c>https</c>, <c>rtsp</c> and <c>file</c>.</returns>
    private static bool IsStyledHeaderScheme(string scheme) => scheme is "http" or "https" or "rtsp" or "file";

    /// <summary>
    /// Writes <paramref name="url" /> absolute, without its fragment, as the URL a relative
    /// <c>Location</c> resolves against: curl's effective URL, its default port left out.
    /// </summary>
    /// <param name="url">The transfer's URL.</param>
    /// <returns>The URL text.</returns>
    private static string StyledHeaderBaseUrl(CurlUrl url)
    {
        string userInformation = url.User is null ? string.Empty : url.User + (url.Password is null ? string.Empty : ":" + url.Password) + "@";
        string port = url.IsDefaultPort ? string.Empty : ":" + url.Port.ToString(CultureInfo.InvariantCulture);
        string query = url.Query is null ? string.Empty : "?" + url.Query;
        return $"{url.Scheme}://{userInformation}{url.Host}{port}{url.AbsolutePath}{query}";
    }

    /// <summary>
    /// Parses the <c>-r</c> / <c>--range</c> text, when there is any.
    /// </summary>
    /// <param name="rangeText">The text, or <see langword="null" /> when <c>-r</c> was not given.</param>
    /// <param name="url">The transfer's URL.</param>
    /// <param name="range">
    /// The range; <see langword="null" /> for the whole resource, and for text that names no
    /// range on any URL but an <c>sftp</c> or <c>scp</c> one.
    /// </param>
    /// <returns>
    /// <see langword="false" /> when the text names no range and the URL is SSH's, so the
    /// transfer must end with <see cref="ByteRangeParser.NotDeliveredFailure" />. Every other
    /// handler gets the text as <see cref="ITransferContext.RangeText" />, as curl 8.21.0 hands
    /// it on: HTTP, RTSP and WebSocket send it verbatim, FTP and file parse it themselves
    /// (<c>Curl_range</c>), and the rest ignore it (BL-386, BL-1322 Notes).
    /// </returns>
    private static bool TryParseRange(string? rangeText, CurlUrl url, out ByteRange? range)
    {
        range = null;

        return rangeText is null
            || ByteRangeParser.TryParse(rangeText, out range)
            || url.Scheme is not ("sftp" or "scp");
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
        UpdateLibcurlTransfer(entry => entry with { OutputFileSize = existing.Length });

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
        DeferredOutputFileStream output = CreateOutputFileStream(options, target.Path, resumeFrom);
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

        Running.OutputFileName = output.Path;
        Running.OpenedOutputFile = output.IsOpen ? output.Path : null;
        await FinishOutputFileAsync(options, output, url, completed).ConfigureAwait(false);

        return completed;
    }

    /// <summary>
    /// Applies the after-transfer file options in curl 8.21.0's order: <c>--xattr</c>'s attributes
    /// (<see cref="WriteRequestedExtendedAttributesAsync" />), then, under <c>-R</c>, the source's
    /// time on a successful transfer whose result carries one.
    /// </summary>
    /// <param name="options">The accepted command line.</param>
    /// <param name="output">The output file's stream, closed.</param>
    /// <param name="url">The transfer's URL.</param>
    /// <param name="completed">The transfer's result.</param>
    /// <returns>A task that completes when both are done.</returns>
    private async Task FinishOutputFileAsync(
        CommandLineOptions options,
        DeferredOutputFileStream output,
        CurlUrl url,
        TransferResult completed)
    {
        await WriteRequestedExtendedAttributesAsync(options, output, url, completed).ConfigureAwait(false);
        if (options.RemoteTime && completed.IsSuccess && completed.SourceLastWriteTimeUtc is { } sourceLastWriteTimeUtc)
        {
            await StampOutputFileTimeAsync(options, output.Path, sourceLastWriteTimeUtc).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Stores <c>--xattr</c>'s attributes when it was given, the transfer succeeded, it opened the
    /// file itself rather than creating it empty afterwards, and the platform has a writer
    /// (<see cref="WriteExtendedAttributesAsync" />); otherwise does nothing.
    /// </summary>
    /// <param name="options">The accepted command line.</param>
    /// <param name="output">The output file's stream, closed.</param>
    /// <param name="url">The transfer's URL.</param>
    /// <param name="completed">The transfer's result.</param>
    /// <returns>A task that completes when the attributes are set or the warning written.</returns>
    private Task WriteRequestedExtendedAttributesAsync(
        CommandLineOptions options,
        DeferredOutputFileStream output,
        CurlUrl url,
        TransferResult completed) =>
        options.ExtendedAttributes && completed.IsSuccess && output.OpenedForTheTransfer && extendedAttributeWriter is { } writer
            ? WriteExtendedAttributesAsync(options, writer, output.Path, url, completed)
            : Task.CompletedTask;

    /// <summary>
    /// Creates the stream that opens <paramref name="path" /> on its first write: for appending
    /// when the transfer resumes past byte zero, as curl opens the file <c>"ab"</c> then, and
    /// truncated otherwise, or kept and numbered under <c>--no-clobber</c>.
    /// </summary>
    /// <param name="options">The accepted command line, whose <c>--clobber</c> choice the stream takes.</param>
    /// <param name="path">The output file.</param>
    /// <param name="resumeFrom">The resolved <c>-C</c> offset, or <see langword="null" />.</param>
    /// <returns>The stream, not yet opened.</returns>
    private DeferredOutputFileStream CreateOutputFileStream(CommandLineOptions options, string path, long? resumeFrom) =>
        new(fileSystem, path, resumeFrom is > 0 ? FileWriteMode.Append : FileWriteMode.Truncate, options.Clobber);

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
    /// Stores <c>--xattr</c>'s attributes on <paramref name="outputFile" /> after a successful
    /// transfer that opened it, as curl 8.21.0's <c>fwrite_xattr</c> does: the URL as typed,
    /// normalised and without credentials, the <c>Referer</c> sent and the reply's content type
    /// (<see cref="OutputFileExtendedAttributes" />). A failure prints curl's warning unless
    /// <c>-s</c> was given; the transfer's exit code is unchanged either way.
    /// </summary>
    /// <param name="options">The accepted command line.</param>
    /// <param name="writer">Sets each attribute.</param>
    /// <param name="outputFile">The <c>-o</c> / <c>-O</c> file.</param>
    /// <param name="url">The transfer's URL.</param>
    /// <param name="completed">The transfer's result, whose report holds the referer and content type.</param>
    /// <returns>A task that completes when the attributes are set or the warning written.</returns>
    private async Task WriteExtendedAttributesAsync(
        CommandLineOptions options,
        IExtendedAttributeWriter writer,
        string outputFile,
        CurlUrl url,
        TransferResult completed)
    {
        IReadOnlyList<KeyValuePair<string, string>> attributes = OutputFileExtendedAttributes.For(
            UrlEffective.Normalize(url.OriginalString, options.PathAsIs),
            completed.Report?.Referer ?? options.Referer,
            completed.Report?.ContentType);
        if (OutputFileExtendedAttributes.Write(writer, outputFile, attributes) is { } warning && !options.Silent)
        {
            await WriteErrorLineAsync(warning).ConfigureAwait(false);
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
                        RateLimited(options, FlushedEachWriteUnderNoBuffer(options, output)),
                        range,
                        resumeFrom,
                        headerOutput,
                        formBody,
                        upload,
                        proxy,
                        watchHeaderOutput,
                        Running.Progress,
                        Running.Events,
                        StartLowSpeedWatchdog(options),
                        Running.AbortToken,
                        StartMaxTimeWatchdog(options),
                        Running.LookedUpCredentials,
                        Running.IfNoneMatchHeaders,
                        Running.AltSvc,
                        ssh: Running.Ssh),
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
    /// Creates the context of each attempt, whose output is standard output, on the transfer's
    /// current <see cref="RunningTransferState.Progress" />.
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
        RunningTransferState state = Running;
        state.StandardOutput.ClearWriteFailure();
        TransferResult result = await FollowRetryingAsync(follower, options, createAttemptContext, options.ResumeFrom, null)
            .ConfigureAwait(false);
        await state.GatedStandardOutput.FlushAsync().ConfigureAwait(false);

        return result.IsSuccess && state.StandardOutput.HasWriteFailed ? StandardOutputWriteFailure : result;
    }

    /// <summary>
    /// Performs one transfer through <paramref name="follower" />, run again by
    /// <see cref="TransferRetrier" /> under <c>--retry</c> (<see cref="RetryPolicyMapping" />),
    /// each attempt on a context of its own.
    /// </summary>
    /// <param name="follower">Performs each attempt, following redirects under <c>-L</c>.</param>
    /// <param name="options">The accepted command line.</param>
    /// <param name="createAttemptContext">
    /// Creates an attempt's context, on the transfer's current <see cref="RunningTransferState.Progress" />; called
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
            RecordSerialAttemptStart();
            TransferContext context = firstContext ?? createAttemptContext();
            firstContext = null;
            TransferResult attemptResult = await FollowWatchingSpeedAsync(follower, context, redirectPolicy).ConfigureAwait(false);
            KeepRetriedConnectionIdWhenReused(context, attemptResult);
            return attemptResult;
        });

        TransferResult result = await retrier
            .RunAsync(
                firstContext,
                RetryPolicyMapping.FromCommandLine(options),
                (attempt, warning) =>
                {
                    Running.RetryCount++;
                    TakeNextAttemptIds();
                    retryLinesWritten = WriteRetryLinesAsync(options, attempt, warning, resumeFrom, outputFile);
                },
                (_, warning) => retryLinesWritten = WriteWarningUnlessSilentAsync(options, warning))
            .ConfigureAwait(false);
        await retryLinesWritten.ConfigureAwait(false);

        return result;
    }

    /// <summary>
    /// Moves <see cref="previousSerialTransferStart" /> to now as a serial transfer's attempt starts, after any
    /// <c>--retry</c> wait, so <c>--rate</c> measures from the last attempt's start, as curl 8.21.0's
    /// <c>serial_transfers</c> resets its start on every retry: 503 then 200 with <c>--retry-delay 1</c> and
    /// 300 ms answers printed <c>Note: Transfer took 315 ms</c> (measured 2026-10-02, BL-971 Notes). Under
    /// <c>-Z</c>, where <c>--rate</c> waits for nothing, it is left alone.
    /// </summary>
    private void RecordSerialAttemptStart()
    {
        if (parallelRun is null)
        {
            previousSerialTransferStart = timeProvider.GetTimestamp();
        }
    }

    /// <summary>
    /// Numbers the attempt <c>--retry</c> is about to run as a transfer of its own, as curl 8.21.0
    /// does: it takes the next <c>%{xfer_id}</c>, and the retried attempt keeps the <c>%{conn_id}</c>
    /// it connected with, so the next attempt takes a new one unless it reuses that connection
    /// (<see cref="KeepRetriedConnectionIdWhenReused" />). Against 503, 503, 200 on connections the
    /// server closed, curl printed <c>xfer_id</c> 2 and <c>conn_id</c> 2 (measured 2026-09-29, BL-799 Notes).
    /// </summary>
    private void TakeNextAttemptIds()
    {
        RunningTransferState state = Running;
        state.RetriedConnectionId = state.ConnectionId ?? nextConnectionId++;
        state.ConnectionId = null;
        state.RetryTransferId = nextTransferId++;
    }

    /// <summary>
    /// Gives a <c>--retry</c> attempt that reused the retried attempt's connection that attempt's
    /// <c>%{conn_id}</c>, as curl 8.21.0 printed <c>conn_id</c> 0 after three attempts over one
    /// kept-alive connection (measured 2026-09-29, BL-799 Notes). Only an <c>http</c> or
    /// <c>https</c> attempt says whether it opened a connection (<see cref="TransferReport.ConnectionCount" />);
    /// any other is taken to have opened one.
    /// </summary>
    /// <param name="context">The attempt's context.</param>
    /// <param name="attempt">The attempt's result.</param>
    private void KeepRetriedConnectionIdWhenReused(TransferContext context, TransferResult attempt)
    {
        RunningTransferState state = Running;
        if (state.RetriedConnectionId is { } retriedConnectionId && ReportsNoConnectionOpened(context, attempt))
        {
            state.ConnectionId = retriedConnectionId;
        }
    }

    /// <summary>
    /// Tells whether an attempt said it opened no connection: an <c>http</c> or <c>https</c> one
    /// whose report counts none.
    /// </summary>
    /// <param name="context">The attempt's context.</param>
    /// <param name="attempt">The attempt's result.</param>
    /// <returns><see langword="true" /> when the attempt reused a connection.</returns>
    private static bool ReportsNoConnectionOpened(TransferContext context, TransferResult attempt) =>
        IsHttpScheme(context.Url.Scheme) && attempt.Report is { ConnectionCount: 0 };

    /// <summary>Tells whether <paramref name="scheme" /> is <c>http</c> or <c>https</c>.</summary>
    /// <param name="scheme">The scheme, in lower case.</param>
    /// <returns><see langword="true" /> for <c>http</c> and <c>https</c>.</returns>
    private static bool IsHttpScheme(string scheme) => scheme is "http" or "https";

    /// <summary>
    /// Starts the <c>-Y</c>/<c>-y</c> watchdog for the attempt whose context is being created, on
    /// the runner's clock, and keeps it for <see cref="FollowWatchingSpeedAsync" />: <c>-Y</c>
    /// alone watches for 30 seconds and <c>-y</c> alone for 1 byte per second, as curl 8.21.0
    /// does (measured 2026-09-27, BL-400 Notes).
    /// </summary>
    /// <param name="options">The accepted command line.</param>
    /// <returns>The started watchdog, or <see langword="null" /> when the speed is not watched.</returns>
    private LowSpeedWatchdog? StartLowSpeedWatchdog(CommandLineOptions options) =>
        Running.AttemptLowSpeedWatchdog = LowSpeedWatchdog.StartFromCommandLine(options.SpeedLimit, options.SpeedTimeSeconds, timeProvider, diagnosticLog);

    /// <summary>
    /// Starts the <c>-m</c> watchdog for the attempt whose context is being created, on the
    /// runner's clock, and keeps it for <see cref="FollowWatchingSpeedAsync" /> (ADR-0117): each
    /// <c>--retry</c> attempt gets a fresh <c>-m</c>, as in curl 8.21.0.
    /// </summary>
    /// <param name="options">The accepted command line.</param>
    /// <returns>The started watchdog, or <see langword="null" /> without a positive <c>-m</c>.</returns>
    private MaxTimeWatchdog? StartMaxTimeWatchdog(CommandLineOptions options) =>
        Running.AttemptMaxTimeWatchdog = MaxTimeWatchdog.StartFromCommandLine(options.MaxTime, timeProvider, diagnosticLog);

    /// <summary>
    /// Performs one attempt through <paramref name="follower" /> under the <c>-Y</c>/<c>-y</c> and
    /// <c>-m</c> watchdogs its context was created with, stopping both when the attempt ends. An
    /// attempt either watchdog cancelled ends with that watchdog's exit 28 failure, which
    /// <c>--retry</c> counts as transient.
    /// </summary>
    /// <param name="follower">Performs the attempt, following redirects under <c>-L</c>.</param>
    /// <param name="context">The attempt's context.</param>
    /// <param name="redirectPolicy">The <c>-L</c> policy.</param>
    /// <returns>The attempt's result, or a watchdog's failure.</returns>
    private async Task<TransferResult> FollowWatchingSpeedAsync(RedirectFollower follower, TransferContext context, RedirectPolicy redirectPolicy)
    {
        RunningTransferState state = Running;
        using LowSpeedWatchdog? watchdog = state.AttemptLowSpeedWatchdog;
        using MaxTimeWatchdog? maxTimeWatchdog = state.AttemptMaxTimeWatchdog;
        state.AttemptLowSpeedWatchdog = null;
        state.AttemptMaxTimeWatchdog = null;
        try
        {
            return await follower.FollowAsync(context, redirectPolicy).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (watchdog is { IsTooSlow: true })
        {
            return watchdog.Failure;
        }
        catch (OperationCanceledException) when (maxTimeWatchdog is { HasTimedOut: true })
        {
            return maxTimeWatchdog.Failure;
        }
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

        await WriteWarningUnlessSilentAsync(options, warning).ConfigureAwait(false);
        outputFile?.TruncateForRetry();
        StartTransferProgress(options, resumeFrom, toStandardOutput);
    }

    /// <summary>
    /// Writes a warning line, such as a <see cref="TransferRetrier" /> one, wrapped as every <c>Warning: </c>
    /// line is, unless <c>-s</c> was given: curl 8.21.0 prints none under <c>-s</c> or
    /// <c>-sS</c>.
    /// </summary>
    /// <param name="options">The accepted command line.</param>
    /// <param name="warning">The warning, unwrapped.</param>
    /// <returns>A task that completes when the line is written, or at once under <c>-s</c>.</returns>
    private Task WriteWarningUnlessSilentAsync(CommandLineOptions options, string warning) =>
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
    /// Flushes <paramref name="output" /> after every write with a <see cref="FlushEachWriteStream" />
    /// under <c>-N</c> / <c>--no-buffer</c>, as curl 8.21.0 flushes each write then.
    /// </summary>
    /// <param name="options">The accepted command line.</param>
    /// <param name="output">The attempt's body output.</param>
    /// <returns>The flushing stream, or <paramref name="output" /> when buffering is on.</returns>
    private static Stream FlushedEachWriteUnderNoBuffer(CommandLineOptions options, Stream output) =>
        options.NoBuffer ? new FlushEachWriteStream(output) : output;

    /// <summary>
    /// Writes a body sent to standard output in text mode on Windows under <c>-B</c> /
    /// <c>--use-ascii</c>, each line feed as CR LF, until an earlier or concurrent transfer has
    /// switched standard output to binary mode (<see cref="TextModeUntilBinaryStream" />). curl
    /// 8.21.0's Schannel build wrote <c>-B</c>'s <c>l1\nl2\r\n</c> as <c>l1\r\nl2\r\r\n</c>, its
    /// <c>-I</c> and <c>-i</c> header lines ending <c>\r\r\n</c>, and a <c>-B</c> transfer after
    /// one without it unchanged (measured 2026-10-01, BL-961 Notes).
    /// </summary>
    /// <param name="options">The transfer's option group.</param>
    /// <param name="output">Standard output.</param>
    /// <returns>The text-mode stream, or <paramref name="output" /> without <c>-B</c> or off Windows.</returns>
    private Stream InTextModeUnderUseAscii(CommandLineOptions options, Stream output) =>
        runsOnWindows && options.UseAscii ? new TextModeUntilBinaryStream(output, () => standardOutputSwitchedToBinary) : output;

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
    /// The lines an option asking for information instead of a transfer prints on standard output:
    /// <see cref="CurlVersionText" />'s for the running system for <c>-V</c> / <c>--version</c>,
    /// <see cref="CurlManual" />'s for <c>-M</c> / <c>--manual</c>, and <see cref="CurlHelpText" />'s at
    /// <c>terminalColumns</c> for <c>-h</c> / <c>--help</c> with no subject, a category, <c>all</c> or
    /// <c>category</c>, and <see cref="TlsBuildInformation" />'s for <c>--engine list</c> and
    /// <c>--dump-ca-embed</c>. Parsing ends at the first of these options, so at most one is set.
    /// </summary>
    /// <param name="options">The parsed options.</param>
    /// <returns>
    /// The lines, without terminators; <see langword="null" /> when no such option was given, or
    /// <c>--help</c> names an option, whose manual section <see cref="WriteOptionManualSectionAsync" /> writes.
    /// </returns>
    private IReadOnlyList<string>? InformationLines(CommandLineOptions options) => options switch
    {
        { VersionRequested: true } => CurlVersionText.Lines(runsOnWindows, OperatingSystem.IsMacOS()),
        { ManualRequested: true } => CurlManual.Lines(),
        { HelpRequested: true, HelpSubject: var subject } when !CurlHelpText.IsOptionSubject(subject) =>
            CurlHelpText.Lines(subject, terminalColumns),
        _ => TlsBuildInformation.Lines(options),
    };

    /// <summary>
    /// Writes what <c>--help &lt;option&gt;</c> prints, as curl 8.21.0 does: the option's
    /// <see cref="CurlOptionManualSection" /> on standard output, or, for a subject naming no option,
    /// <see cref="CurlOptionManualSection.IncorrectOptionNameMessage" /> on standard error.
    /// </summary>
    /// <param name="subject">The subject, starting with <c>-</c>.</param>
    /// <returns>A task that completes when the output is flushed.</returns>
    private async Task WriteOptionManualSectionAsync(string subject)
    {
        if (CurlOptionManualSection.TryGetLines(subject, out IReadOnlyList<string> lines))
        {
            await WriteStandardOutputLinesAsync(lines).ConfigureAwait(false);
        }
        else
        {
            await WriteErrorLineAsync(CurlOptionManualSection.IncorrectOptionNameMessage).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Writes what <c>--ai-help [subject]</c> prints: <see cref="CurlAiHelpText" />'s Markdown as it is, its
    /// lines ending in <c>\n</c> on every platform, or, for a subject naming no category,
    /// <see cref="CurlAiHelpText.UnknownCategoryLines" /> as <c>--help</c> writes them.
    /// </summary>
    /// <param name="subject">The subject, <see langword="null" /> for the index.</param>
    /// <returns>A task that completes when the output is flushed.</returns>
    private async Task WriteAiHelpAsync(string? subject)
    {
        if (CurlAiHelpText.TryGetMarkdown(subject, out string markdown))
        {
            await standardOutput.WriteAsync(Encoding.UTF8.GetBytes(markdown)).ConfigureAwait(false);
            await standardOutput.FlushAsync().ConfigureAwait(false);
        }
        else
        {
            await WriteStandardOutputLinesAsync(CurlAiHelpText.UnknownCategoryLines()).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Writes <paramref name="lines" /> to standard output as UTF-8, each followed by
    /// <see cref="Environment.NewLine" />, in one write.
    /// </summary>
    /// <param name="lines">The lines, without terminators.</param>
    /// <returns>A task that completes when the lines are flushed.</returns>
    private async Task WriteStandardOutputLinesAsync(IReadOnlyList<string> lines)
    {
        StringBuilder text = new();
        foreach (string line in lines)
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
    private Task WriteErrorLineAsync(string line)
    {
        if (IsWarningLine(line))
        {
            diagnosticLog.Write(DiagnosticLogLevel.Warning, DiagnosticLogComponents.Runner, line);
        }

        return WriteErrorPiecesAsync(WarningLineWrapper.WrapLine(line, terminalColumns));
    }

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
