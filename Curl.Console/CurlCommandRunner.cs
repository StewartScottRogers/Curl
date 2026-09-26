using System.Globalization;
using System.Text;
using Curl.Cli;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Runs one command line: parses it with <see cref="CommandLineParser" />, turns each URL
/// into a <see cref="TransferContext" />, performs it through the
/// <see cref="ProtocolDispatcher" /> built for that command line, and prints curl 8.21.0's
/// <c>curl: (N) &lt;message&gt;</c> line for each failure.
/// </summary>
/// <param name="createDispatcher">
/// Builds, from the accepted command line, the dispatcher that performs each transfer with
/// the handler for its scheme; called once per run, and not at all for a refused command
/// line. It takes the options because the network handlers' TLS settings come from them.
/// </param>
/// <param name="outputFileSystem">Opens the <c>-o</c> / <c>--output</c> files.</param>
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
/// stdin" (ADR-0006). Every other scheme's upload is <see langword="null" />.
/// </param>
/// <remarks>
/// <para>
/// URLs are transferred in command-line order; a failure does not stop the rest, and the
/// exit code is the last transfer's, as curl's is. The one exception is a resumed transfer
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
/// The parser's warning lines come first on standard error, whether or not the command line
/// is accepted; a refused command line then prints the refusal's lines and transfers nothing.
/// </para>
/// <para>
/// Each transfer carries the parsed <c>-r</c> range, the <c>-C</c> offset and the
/// <c>--max-filesize</c> limit. Range text that names no range ends the transfer with exit 33
/// before it is dispatched. <c>-C -</c> resumes from the size of the URL's <c>-o</c> file,
/// and a transfer that resumes past byte zero appends to that file, opening it first.
/// </para>
/// </remarks>
internal sealed class CurlCommandRunner(
    Func<CommandLineOptions, ProtocolDispatcher> createDispatcher,
    IFileSystem outputFileSystem,
    Stream standardOutput,
    Stream standardError,
    Stream standardInput)
{
    /// <summary>
    /// curl 8.21.0's message for a URL that cannot be parsed at all, measured on
    /// <c>dict://exa mple.com/d:x</c>.
    /// </summary>
    internal const string MalformedUrlMessage = "URL rejected: Malformed input to a URL function";

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
    internal const string CannotOpenForResumeMessage = "Failed writing received data to disk/application";

    /// <summary>The one scheme whose transfer uploads standard input.</summary>
    private const string TelnetScheme = "telnet";

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
        TransferResult.Failure(CurlExitCode.WriteError, CannotOpenForResumeMessage);

    /// <summary>
    /// Standard output, deferring a write failure as curl's stdio buffer does and recording
    /// it for the current transfer.
    /// </summary>
    private readonly StandardOutputFailureDeferringStream deferringStandardOutput = new(standardOutput);

    /// <summary>
    /// Runs <paramref name="arguments" /> to completion.
    /// </summary>
    /// <param name="arguments">The command-line arguments, without the program name.</param>
    /// <returns>The process exit code.</returns>
    internal async Task<int> RunAsync(IReadOnlyList<string> arguments)
    {
        CommandLineParseResult parsed = CommandLineParser.Parse(arguments);
        await WriteErrorLinesAsync(parsed.WarningLines).ConfigureAwait(false);

        if (!parsed.IsAccepted)
        {
            await WriteErrorLinesAsync(parsed.Refusal.StandardErrorLines).ConfigureAwait(false);

            return (int)parsed.Refusal.ExitCode;
        }

        return (int)await TransferAllAsync(parsed.Options).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates the context for one transfer, carrying every option a handler reads.
    /// </summary>
    /// <param name="options">The parsed command line.</param>
    /// <param name="url">The URL to transfer.</param>
    /// <param name="output">Where the transfer's bytes go.</param>
    /// <param name="range">The parsed <c>-r</c> range, or <see langword="null" /> for the whole resource.</param>
    /// <param name="resumeFrom">The <c>-C</c> offset, already resolved for <c>-C -</c>.</param>
    /// <returns>The context.</returns>
    private TransferContext CreateContext(
        CommandLineOptions options,
        Uri url,
        Stream output,
        ByteRange? range,
        long? resumeFrom) =>
        new()
        {
            Url = url,
            Output = output,
            Range = range,
            ResumeFrom = resumeFrom,
            MaxFileSize = options.MaxFileSize,
            Upload = string.Equals(url.Scheme, TelnetScheme, StringComparison.Ordinal) ? standardInput : null,
            PostData = options.PostData,
            Credentials = options.Credentials,
            TelnetOptions = options.TelnetOptions,
            TftpBlockSize = options.TftpBlockSize,
            TftpNoOptions = options.TftpNoOptions,
            CreateFileMode = options.CreateFileMode ?? TransferContext.DefaultCreateFileMode,
        };

    /// <summary>
    /// Transfers every URL in order and reports each failure.
    /// </summary>
    /// <param name="options">The accepted command line.</param>
    /// <returns>
    /// The last transfer's exit code, or <see cref="CurlExitCode.Ok" /> when there was none.
    /// A resumed transfer whose <c>-o</c> file cannot be opened is the last: the URLs after
    /// it are not transferred.
    /// </returns>
    private async Task<CurlExitCode> TransferAllAsync(CommandLineOptions options)
    {
        CurlExitCode exitCode = CurlExitCode.Ok;
        ProtocolDispatcher dispatcher = createDispatcher(options);
        bool showsErrors = ShowsErrors(options);

        for (int index = 0; index < options.Urls.Count; index++)
        {
            string? outputFile = index < options.OutputFiles.Count ? options.OutputFiles[index] : null;
            TransferResult result = await TransferAsync(dispatcher, options, options.Urls[index], outputFile)
                .ConfigureAwait(false);
            exitCode = result.ExitCode;

            if (showsErrors && result.ErrorMessage is not null)
            {
                await WriteErrorLineAsync(FormatErrorLine(result)).ConfigureAwait(false);
            }

            if (ReferenceEquals(result, CannotOpenForResumeFailure))
            {
                break;
            }
        }

        return exitCode;
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
    /// Performs one transfer, to <paramref name="outputFile" /> when one is given and to
    /// standard output otherwise.
    /// </summary>
    /// <param name="dispatcher">Performs the transfer with the handler for its scheme.</param>
    /// <param name="options">The accepted command line.</param>
    /// <param name="url">The URL as typed.</param>
    /// <param name="outputFile">The matching <c>-o</c> value, or <see langword="null" />.</param>
    /// <returns>
    /// The transfer's result; <see cref="ByteRangeParser.NotDeliveredFailure" />, with nothing
    /// transferred, when the <c>-r</c> text names no range, as curl 8.21.0 reports it.
    /// </returns>
    private async Task<TransferResult> TransferAsync(
        ProtocolDispatcher dispatcher,
        CommandLineOptions options,
        string url,
        string? outputFile)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        {
            return TransferResult.Failure(CurlExitCode.UrlMalformat, MalformedUrlMessage);
        }

        if (!TryParseRange(options.Range, out ByteRange? range))
        {
            return ByteRangeParser.NotDeliveredFailure;
        }

        if (outputFile is null)
        {
            TransferContext context = CreateContext(options, uri, deferringStandardOutput, range, options.ResumeFrom);

            return await TransferToStandardOutputAsync(dispatcher, context).ConfigureAwait(false);
        }

        long? resumeFrom = await ResolveResumeFromAsync(options, outputFile).ConfigureAwait(false);

        return await TransferToOutputFileAsync(dispatcher, options, uri, outputFile, range, resumeFrom)
            .ConfigureAwait(false);
    }

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
    /// <param name="outputFile">The matching <c>-o</c> value.</param>
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

        FileOpenResult existing = await outputFileSystem
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
    /// Performs one transfer whose output is <paramref name="outputFile" />.
    /// </summary>
    /// <param name="dispatcher">Performs the transfer with the handler for its scheme.</param>
    /// <param name="options">The accepted command line.</param>
    /// <param name="uri">The URL.</param>
    /// <param name="outputFile">The matching <c>-o</c> value.</param>
    /// <param name="range">The parsed <c>-r</c> range, or <see langword="null" />.</param>
    /// <param name="resumeFrom">The resolved <c>-C</c> offset, or <see langword="null" />.</param>
    /// <returns>
    /// The transfer's result. A transfer that resumes past byte zero opens the file for
    /// appending before it starts, as curl 8.21.0 does; when that open fails the result is
    /// <see cref="ReportCannotOpenForResumeAsync" />'s, and nothing is transferred.
    /// </returns>
    private async Task<TransferResult> TransferToOutputFileAsync(
        ProtocolDispatcher dispatcher,
        CommandLineOptions options,
        Uri uri,
        string outputFile,
        ByteRange? range,
        long? resumeFrom)
    {
        bool resumes = resumeFrom is > 0;
        DeferredOutputFileStream output = new(
            outputFileSystem, outputFile, resumes ? FileWriteMode.Append : FileWriteMode.Truncate);
        await using (output.ConfigureAwait(false))
        {
            if (resumes && !await output.TryOpenNowAsync().ConfigureAwait(false))
            {
                return await ReportCannotOpenForResumeAsync(options, outputFile).ConfigureAwait(false);
            }

            TransferContext context = CreateContext(options, uri, output, range, resumeFrom);
            TransferResult fileResult = await dispatcher.DispatchAsync(context).ConfigureAwait(false);
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
    /// <param name="outputFile">The <c>-o</c> value, as typed.</param>
    /// <returns><see cref="CannotOpenForResumeFailure" />: exit 23 with <see cref="CannotOpenForResumeMessage" />.</returns>
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
    /// <param name="dispatcher">Performs the transfer with the handler for its scheme.</param>
    /// <param name="context">The transfer, whose output is standard output.</param>
    /// <returns>
    /// <see cref="StandardOutputWriteFailure" /> when the handler succeeded but standard
    /// output failed, which is curl's failed flush at the end of the transfer; otherwise the
    /// handler's result, including its own write failure when curl's stdio buffer would have
    /// filled (see <see cref="StandardOutputFailureDeferringStream" />).
    /// </returns>
    private async Task<TransferResult> TransferToStandardOutputAsync(
        ProtocolDispatcher dispatcher,
        TransferContext context)
    {
        deferringStandardOutput.ClearWriteFailure();
        TransferResult result = await dispatcher.DispatchAsync(context).ConfigureAwait(false);
        await deferringStandardOutput.FlushAsync().ConfigureAwait(false);

        return result.IsSuccess && deferringStandardOutput.HasWriteFailed ? StandardOutputWriteFailure : result;
    }

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
    /// Writes <paramref name="line" /> and <see cref="Environment.NewLine" /> to standard
    /// error as UTF-8.
    /// </summary>
    /// <param name="line">The line, without a terminator.</param>
    /// <returns>A task that completes when the line is flushed.</returns>
    private async Task WriteErrorLineAsync(string line)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(line + Environment.NewLine);
        await standardError.WriteAsync(bytes).ConfigureAwait(false);
        await standardError.FlushAsync().ConfigureAwait(false);
    }
}
