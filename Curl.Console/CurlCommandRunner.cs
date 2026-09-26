using System.Globalization;
using System.Text;
using Curl.Cli;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Runs one command line: parses it with <see cref="CommandLineParser" />, turns each URL
/// into a <see cref="TransferContext" />, performs it through the
/// <see cref="ProtocolDispatcher" />, and prints curl 8.21.0's
/// <c>curl: (N) &lt;message&gt;</c> line for each failure.
/// </summary>
/// <param name="dispatcher">Performs each transfer with the handler for its scheme.</param>
/// <param name="outputFileSystem">Opens the <c>-o</c> / <c>--output</c> files.</param>
/// <param name="standardOutput">
/// The raw standard output stream; a URL with no matching <c>-o</c> writes its bytes here,
/// unencoded.
/// </param>
/// <param name="standardError">
/// The raw standard error stream; each line is written as UTF-8 followed by
/// <see cref="Environment.NewLine" />.
/// </param>
/// <remarks>
/// URLs are transferred in command-line order; a failure does not stop the rest, and the
/// exit code is the last transfer's, as curl's is. The first <c>-o</c> receives the first
/// URL, the second the second, and so on. A failure's line is printed unless <c>-s</c> was
/// given without <c>-S</c>, and only when the failure carries a message. A refused command
/// line prints the refusal's lines and transfers nothing.
/// </remarks>
internal sealed class CurlCommandRunner(
    ProtocolDispatcher dispatcher,
    IFileSystem outputFileSystem,
    Stream standardOutput,
    Stream standardError)
{
    /// <summary>
    /// curl 8.21.0's message for a URL that cannot be parsed at all, measured on
    /// <c>dict://exa mple.com/d:x</c>.
    /// </summary>
    internal const string MalformedUrlMessage = "URL rejected: Malformed input to a URL function";

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
    private static TransferContext CreateContext(CommandLineOptions options, Uri url, Stream output) =>
        new()
        {
            Url = url,
            Output = output,
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
        bool showsErrors = !options.Silent || options.ShowError;

        for (int index = 0; index < options.Urls.Count; index++)
        {
            string? outputFile = index < options.OutputFiles.Count ? options.OutputFiles[index] : null;
            TransferResult result = await TransferAsync(options, options.Urls[index], outputFile)
                .ConfigureAwait(false);
            exitCode = result.ExitCode;

            if (showsErrors && result.ErrorMessage is not null)
            {
                string code = ((int)result.ExitCode).ToString(CultureInfo.InvariantCulture);
                await WriteErrorLineAsync($"curl: ({code}) {result.ErrorMessage}").ConfigureAwait(false);
            }
        }

        return exitCode;
    }

    /// <summary>
    /// Performs one transfer, to <paramref name="outputFile" /> when one is given and to
    /// standard output otherwise.
    /// </summary>
    /// <param name="options">The accepted command line.</param>
    /// <param name="url">The URL as typed.</param>
    /// <param name="outputFile">The matching <c>-o</c> value, or <see langword="null" />.</param>
    /// <returns>The transfer's result.</returns>
    private async Task<TransferResult> TransferAsync(CommandLineOptions options, string url, string? outputFile)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        {
            return TransferResult.Failure(CurlExitCode.UrlMalformat, MalformedUrlMessage);
        }

        if (outputFile is null)
        {
            TransferResult result = await dispatcher.DispatchAsync(CreateContext(options, uri, standardOutput))
                .ConfigureAwait(false);
            await standardOutput.FlushAsync().ConfigureAwait(false);

            return result;
        }

        DeferredOutputFileStream output = new(outputFileSystem, outputFile);
        await using (output.ConfigureAwait(false))
        {
            TransferResult fileResult = await dispatcher.DispatchAsync(CreateContext(options, uri, output))
                .ConfigureAwait(false);

            return await output.CompleteAsync(fileResult).ConfigureAwait(false);
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
