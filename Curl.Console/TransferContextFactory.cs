using Curl.Cli;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Builds the <see cref="TransferContext" /> for one URL of an accepted command line,
/// carrying every option a handler reads.
/// </summary>
/// <param name="standardInput">
/// The raw standard input stream, given to a <c>telnet</c> transfer as its
/// <see cref="ITransferContext.Upload" />, because curl's telnet "sends what it reads on
/// stdin" (ADR-0006). Every other scheme's upload is <see langword="null" />.
/// </param>
internal sealed class TransferContextFactory(Stream standardInput)
{
    /// <summary>The one scheme whose transfer uploads standard input.</summary>
    private const string TelnetScheme = "telnet";

    /// <summary>
    /// Creates the context for one transfer, carrying every option a handler reads.
    /// </summary>
    /// <param name="options">The parsed command line.</param>
    /// <param name="url">The URL to transfer.</param>
    /// <param name="output">Where the transfer's bytes go.</param>
    /// <param name="range">The parsed <c>-r</c> range, or <see langword="null" /> for the whole resource.</param>
    /// <param name="resumeFrom">The <c>-C</c> offset, already resolved for <c>-C -</c>.</param>
    /// <param name="headerOutput">Where the <c>-D</c> header lines go, or <see langword="null" /> without <c>-D</c>.</param>
    /// <param name="formBody">The <c>-F</c> body built for this transfer, or <see langword="null" /> without <c>-F</c>.</param>
    /// <returns>
    /// The context. Its <see cref="TransferContext.NoBody" /> is <c>-I</c>, and its
    /// <see cref="TransferContext.HeaderOutput" /> is <see cref="HeaderOutputOf" />'s.
    /// </returns>
    internal TransferContext Create(
        CommandLineOptions options,
        Uri url,
        Stream output,
        ByteRange? range,
        long? resumeFrom,
        Stream? headerOutput,
        HttpRequestBody? formBody = null) =>
        new()
        {
            Url = url,
            Output = output,
            HeaderOutput = HeaderOutputOf(options, output, headerOutput),
            NoBody = options.NoBody,
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
            PathAsIs = options.PathAsIs,
            ConnectTimeout = options.ConnectTimeout,
            MaxTime = options.MaxTime,
            TimeCondition = options.TimeCondition,
            Http = HttpRequestOptionsMapping.FromCommandLine(options, formBody),
        };

    /// <summary>
    /// Chooses where a transfer's header lines go. <c>-i</c> and <c>-I</c> send them to the
    /// body output as well as to any <c>-D</c> output, as curl 8.21.0 does.
    /// </summary>
    /// <param name="options">The parsed command line.</param>
    /// <param name="output">Where the transfer's body goes.</param>
    /// <param name="dumpHeaderOutput">Where the <c>-D</c> header lines go, or <see langword="null" /> without <c>-D</c>.</param>
    /// <returns>
    /// <paramref name="dumpHeaderOutput" /> without <c>-i</c> or <c>-I</c>; with either,
    /// <paramref name="output" /> when there is no <c>-D</c>, otherwise a
    /// <see cref="HeaderLineTeeStream" /> writing each line to both.
    /// </returns>
    private static Stream? HeaderOutputOf(CommandLineOptions options, Stream output, Stream? dumpHeaderOutput)
    {
        if (!options.ShowHeaders && !options.NoBody)
        {
            return dumpHeaderOutput;
        }

        return dumpHeaderOutput is null ? output : new HeaderLineTeeStream(dumpHeaderOutput, output);
    }
}
