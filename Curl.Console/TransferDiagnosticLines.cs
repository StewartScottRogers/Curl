using System.Globalization;
using Curl.Cli;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// The messages the runner writes to the diagnostic log about each transfer (ADR-0222): what it
/// is about to do, how it ended and why it failed. None of them carries a credential, a header
/// value or a <c>-d</c> body (decision 7): a transfer with credentials says only that it has them.
/// </summary>
internal static class TransferDiagnosticLines
{
    /// <summary>
    /// The <c>info</c> message of a transfer about to start: its scheme and host, its <c>-X</c>
    /// method, where its body goes, and which of <c>-v</c>, <c>--trace</c> and <c>-s</c> are on.
    /// </summary>
    /// <param name="transferId">The transfer's <c>%{xfer_id}</c>.</param>
    /// <param name="options">The transfer's option group.</param>
    /// <param name="url">The URL about to be transferred, its scheme guessed.</param>
    /// <param name="writesToFile">Whether the body goes to a file rather than standard output.</param>
    /// <returns>The message.</returns>
    internal static string Started(long transferId, CommandLineOptions options, string url, bool writesToFile)
    {
        CurlUrl.TryParse(url, pathAsIs: true, out CurlUrl? parsed);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"transfer {transferId} started: {TargetOf(parsed)}, method {options.RequestMethod ?? "default"}, output {(writesToFile ? "file" : "standard output")}, verbose {OnOrOff(options.Trace == TraceKind.Verbose)}, trace {OnOrOff(options.Trace is TraceKind.HexDump or TraceKind.AsciiDump)}, silent {OnOrOff(options.Silent)}{CredentialsOf(options, parsed)}");
    }

    /// <summary>
    /// The <c>info</c> message of a transfer that has ended, whatever its outcome.
    /// </summary>
    /// <param name="transferId">The transfer's <c>%{xfer_id}</c>.</param>
    /// <param name="result">The transfer's result.</param>
    /// <param name="elapsed">The time from its start to its end.</param>
    /// <returns>The message, with the exit code, the bytes transferred and the elapsed milliseconds.</returns>
    internal static string Ended(long transferId, TransferResult result, TimeSpan elapsed) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"transfer {transferId} ended: exit {(int)result.ExitCode}, {result.BytesTransferred} bytes, {(long)elapsed.TotalMilliseconds} ms");

    /// <summary>
    /// The <c>error</c> message of a transfer that failed.
    /// </summary>
    /// <param name="transferId">The transfer's <c>%{xfer_id}</c>.</param>
    /// <param name="exitCode">The failing exit code.</param>
    /// <returns>The message, with the exit code's number and <see cref="CurlExitCode" /> name.</returns>
    internal static string Failed(long transferId, CurlExitCode exitCode) =>
        string.Create(CultureInfo.InvariantCulture, $"transfer {transferId} failed: exit {(int)exitCode} {exitCode}");

    /// <summary>
    /// The scheme and host of <paramref name="url" />, never its user information.
    /// </summary>
    /// <param name="url">The parsed URL, or <see langword="null" /> for one that did not parse.</param>
    /// <returns>The text.</returns>
    private static string TargetOf(CurlUrl? url) =>
        url is null ? "URL not parsed" : $"scheme {url.Scheme}, host {url.Host}";

    /// <summary>
    /// Says that the transfer has credentials, from <c>-u</c> or the URL, without saying what they are.
    /// </summary>
    /// <param name="options">The transfer's option group.</param>
    /// <param name="url">The parsed URL, or <see langword="null" />.</param>
    /// <returns><c>, credentials given</c>, or the empty string.</returns>
    private static string CredentialsOf(CommandLineOptions options, CurlUrl? url) =>
        options.Credentials is not null || url?.User is not null ? ", credentials given" : string.Empty;

    /// <summary>
    /// The word for a switch.
    /// </summary>
    /// <param name="on">Whether it is on.</param>
    /// <returns><c>on</c> or <c>off</c>.</returns>
    private static string OnOrOff(bool on) => on ? "on" : "off";
}
