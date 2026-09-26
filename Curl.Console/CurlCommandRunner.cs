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
/// URLs are transferred in command-line order; a failure does not stop the rest, and the
/// exit code is the last transfer's, as curl's is. The first <c>-o</c> receives the first
/// URL, the second the second, and so on. A failure's line is printed unless <c>-s</c> was
/// given without <c>-S</c>, and only when the failure carries a message. An <c>-o</c> file
/// that cannot be created prints curl's <c>Warning: Failed to open the file</c> line first,
/// unless <c>-s</c> was given, with or without <c>-S</c>. A transfer whose write to standard
/// output fails while the body still fits curl's 4096-byte stdio buffer exits 23 and prints
/// curl's <c>curl: Failed writing body</c>, with no <c>(23)</c>, under the same <c>-s</c> /
/// <c>-S</c> rule as any failure; a larger body reports the handler's own write failure. A refused command
/// line prints the refusal's lines and transfers nothing.
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

        if (!parsed.IsAccepted)
        {
            foreach (string line in parsed.Refusal.StandardErrorLines)
            {
                await WriteErrorLineAsync(line).ConfigureAwait(false);
            }

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
    /// <returns>The context.</returns>
    private TransferContext CreateContext(CommandLineOptions options, Uri url, Stream output) =>
        new()
        {
            Url = url,
            Output = output,
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
    /// <returns>The last transfer's exit code, or <see cref="CurlExitCode.Ok" /> when there was none.</returns>
    private async Task<CurlExitCode> TransferAllAsync(CommandLineOptions options)
    {
        CurlExitCode exitCode = CurlExitCode.Ok;
        ProtocolDispatcher dispatcher = createDispatcher(options);
        bool showsErrors = !options.Silent || options.ShowError;

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
        }

        return exitCode;
    }

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
    /// <returns>The transfer's result.</returns>
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

        if (outputFile is null)
        {
            return await TransferToStandardOutputAsync(dispatcher, CreateContext(options, uri, deferringStandardOutput))
                .ConfigureAwait(false);
        }

        DeferredOutputFileStream output = new(outputFileSystem, outputFile);
        await using (output.ConfigureAwait(false))
        {
            TransferResult fileResult = await dispatcher.DispatchAsync(CreateContext(options, uri, output))
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
