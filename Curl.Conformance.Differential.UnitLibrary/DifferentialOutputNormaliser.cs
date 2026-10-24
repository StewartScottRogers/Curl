using System.Text;
using System.Text.RegularExpressions;

namespace Curl.Conformance.Differential;

/// <summary>
/// Rewrites what a binary wrote so that two runs differing only in things that change from run to
/// run (time stamps, ports, MIME boundaries, digest nonces, the working directory, the progress
/// meter) compare equal, as the tester's <c>norm()</c> did, and removes the lines only the reference
/// curl's debug build writes: its debug warnings and the <c>sendrecv_dl()</c> and
/// <c>Curl_sendrecv()</c> trace lines (ADR-0782).
/// </summary>
public static class DifferentialOutputNormaliser
{
    private static readonly Regex DebugEnabledWarning = new(@"WARNING: this libcurl is Debug-enabled, do not use in production\r?\n(\r?\n)?", RegexOptions.CultureInvariant);
    private static readonly Regex MimeBoundary = new(@"-{20,}[A-Za-z0-9]{22}", RegexOptions.CultureInvariant);
    private static readonly Regex DigestClientNonce = new("cnonce=\"[^\"]*\"", RegexOptions.CultureInvariant);
    private static readonly Regex DigestResponse = new("response=\"[^\"]*\"", RegexOptions.CultureInvariant);
    private static readonly Regex TimeStamp = new(@"[0-9]{2}:[0-9]{2}:[0-9]{2}\.[0-9]{6}", RegexOptions.CultureInvariant);
    private static readonly Regex DebugBuildWarning = new(@"[^\n]*!!! WARNING !!![^\n]*\n[^\n]*This is a debug build of libcurl, do not use in production\.[^\n]*\n", RegexOptions.CultureInvariant);
    private static readonly Regex UploadTraceLine = new(@"(<TS> |\[[0-9]+-[0-9]+\] )*\* (Curl_xfer_send|sending last upload chunk)[^\n]*\n", RegexOptions.CultureInvariant);
    private static readonly Regex LocalPort = new(@"from 127\.0\.0\.1 port [0-9]+", RegexOptions.CultureInvariant);
    private static readonly Regex Milliseconds = new(@"after [0-9]+ ms", RegexOptions.CultureInvariant);
    private static readonly Regex MillisecondsWord = new(@"after [0-9]+ milliseconds", RegexOptions.CultureInvariant);
    private static readonly Regex SecondsWithMicroseconds = new(@"[0-9]+\.[0-9]{6}", RegexOptions.CultureInvariant);
    private static readonly Regex TransferRateOrLocalPort = new("(speed_download|speed_upload|local_port)(\"?[:=])[0-9]+", RegexOptions.CultureInvariant);
    private static readonly Regex CurlVersionString = new("\"curl_version\":\"[^\"]*\"", RegexOptions.CultureInvariant);
    private static readonly Regex OnlyDigits = new(@"\A[0-9]{4,}\z", RegexOptions.CultureInvariant);
    private static readonly Regex ProxyProtocolSourcePort = new(@"PROXY (TCP[46]) (\S+) (\S+) [0-9]+ ", RegexOptions.CultureInvariant);
    private static readonly Regex WebSocketKey = new(@"(Sec-WebSocket-(Key|Accept): )[^\r\n]*", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex CookieJarExpiry = new(@"\t(TRUE|FALSE)\t[0-9]{9,}\t", RegexOptions.CultureInvariant);
    private static readonly Regex IfModifiedSince = new(@"(If-Modified-Since: )[^\r\n]*", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex ProgressMeter = new(@"  % Total.*?Speed\r\n(\r[^\r\n]*)*", RegexOptions.CultureInvariant | RegexOptions.Singleline);
    private static readonly Regex VerboseProgressLines = new(@"(\r *[0-9]+ +[0-9][^\r\n]*)+", RegexOptions.CultureInvariant);
    private static readonly Regex DebugBuildSendRecvLine = new(@"(<TS> |\[[0-9]+-[0-9]+\] )*\* (sendrecv_dl|Curl_sendrecv)\(\) -> [0-9]+\r?\n", RegexOptions.CultureInvariant);

    /// <summary>Normalises one output.</summary>
    /// <param name="output">The bytes a binary wrote.</param>
    /// <param name="workingDirectory">The run's working directory, replaced by <c>&lt;CWD&gt;</c>.</param>
    /// <returns>The normalised bytes.</returns>
    public static byte[] Normalise(byte[] output, string workingDirectory)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(workingDirectory);

        // Latin-1 maps every byte to one char and back, so the rewrite never touches other bytes.
        var text = Encoding.Latin1.GetString(output);
        text = DebugEnabledWarning.Replace(text, string.Empty);
        text = MimeBoundary.Replace(text, "<BOUNDARY>");
        text = DigestClientNonce.Replace(text, "cnonce=\"X\"");
        text = DigestResponse.Replace(text, "response=\"X\"");
        text = text.Replace(workingDirectory, "<CWD>", StringComparison.Ordinal)
            .Replace(workingDirectory.Replace('\\', '/'), "<CWD>", StringComparison.Ordinal);
        text = TimeStamp.Replace(text, "<TS>");
        text = DebugBuildWarning.Replace(text, string.Empty);
        text = UploadTraceLine.Replace(text, string.Empty);
        text = LocalPort.Replace(text, "from 127.0.0.1 port N");
        text = Milliseconds.Replace(text, "after N ms");
        text = MillisecondsWord.Replace(text, "after N milliseconds");
        text = SecondsWithMicroseconds.Replace(text, "<SECONDS>");
        text = TransferRateOrLocalPort.Replace(text, "$1$2N");
        text = CurlVersionString.Replace(text, "\"curl_version\":\"V\"");
        text = OnlyDigits.Replace(text, "<NUMBER>");
        text = ProxyProtocolSourcePort.Replace(text, "PROXY $1 $2 $3 N ");
        text = WebSocketKey.Replace(text, "$1<KEY>");
        text = CookieJarExpiry.Replace(text, "\t$1\t<EXPIRY>\t");
        text = IfModifiedSince.Replace(text, "$1<DATE>");
        text = ProgressMeter.Replace(text, "<PROGRESS>");
        text = VerboseProgressLines.Replace(text, "<PROGRESS>");
        text = DebugBuildSendRecvLine.Replace(text, string.Empty);
        return Encoding.Latin1.GetBytes(text);
    }

    /// <summary>Normalises every output of one run: standard output and error, its files and the wire.</summary>
    /// <param name="result">The run.</param>
    /// <param name="workingDirectory">The run's working directory, replaced by <c>&lt;CWD&gt;</c>.</param>
    /// <returns>The run with every output normalised.</returns>
    public static DifferentialRunResult Normalise(DifferentialRunResult result, string workingDirectory)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result with
        {
            StandardOutput = Normalise(result.StandardOutput, workingDirectory),
            StandardError = Normalise(result.StandardError, workingDirectory),
            Files = result.Files.ToDictionary(file => file.Key, file => Normalise(file.Value, workingDirectory), StringComparer.Ordinal),
            Wire = Normalise(result.Wire, workingDirectory),
        };
    }
}
