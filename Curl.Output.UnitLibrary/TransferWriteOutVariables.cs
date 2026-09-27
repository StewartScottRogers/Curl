using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Output;

/// <summary>
/// Supplies the <c>-w</c> / <c>--write-out</c> variables of one finished transfer from its
/// <see cref="TransferResult"/>, its <see cref="TransferReport"/> and the URL it was given,
/// formatted as curl 8.21.0 prints them.
/// </summary>
/// <remarks>
/// <para>
/// It knows <c>response_code</c>, <c>http_code</c>, <c>http_connect</c>,
/// <c>http_version</c>, <c>method</c>, <c>content_type</c>, <c>redirect_url</c>,
/// <c>url_effective</c>, <c>num_redirects</c>, <c>size_header</c>, <c>size_request</c>,
/// <c>size_download</c>, <c>size_upload</c>, <c>num_connects</c>, <c>num_headers</c>,
/// <c>local_ip</c>, <c>local_port</c>, <c>remote_ip</c>, <c>remote_port</c>,
/// <c>exitcode</c>, <c>errormsg</c>, <c>url</c>, <c>urlnum</c>, <c>scheme</c>,
/// <c>time_namelookup</c>, <c>time_connect</c>, <c>time_appconnect</c>,
/// <c>time_pretransfer</c>, <c>time_posttransfer</c>, <c>time_starttransfer</c>,
/// <c>time_redirect</c>, <c>time_total</c>, <c>time_queue</c>, <c>speed_download</c>,
/// <c>speed_upload</c>, <c>ssl_verify_result</c>, <c>proxy_ssl_verify_result</c>,
/// <c>tls_earlydata</c>, <c>num_retries</c>, <c>ftp_entry_path</c>, <c>num_certs</c>,
/// <c>certs</c>, <c>proxy_used</c>, and
/// <c>url.&lt;part&gt;</c> and <c>urle.&lt;part&gt;</c> for the parts <c>scheme</c>,
/// <c>user</c>, <c>password</c>, <c>options</c>, <c>host</c>, <c>port</c>, <c>path</c>,
/// <c>query</c>, <c>fragment</c> and <c>zoneid</c>. Any other name is reported unknown.
/// </para>
/// <para>
/// <c>url.</c> parts come from the URL as given and <c>urle.</c> parts from the
/// <c>url_effective</c> URL, each parsed with <see cref="CurlUrl"/>; a part the URL does
/// not have, or a URL that does not parse, prints nothing. See BL-304.
/// </para>
/// <para>
/// <c>num_certs</c> counts <see cref="TransferReport.PeerCertificates"/> and <c>certs</c>
/// prints each with <see cref="PeerCertificateText"/>, one after another; a transfer
/// without TLS has none, so <c>0</c> and nothing, as measured on 2026-09-26 against curl
/// 8.21.0 (mingw, Schannel) for file:// and http://. See ADR-0054 and BL-303.
/// </para>
/// <para>
/// <c>proxy_used</c> is <c>1</c> when <see cref="TransferReport.UsedProxy"/> says the
/// transfer was set to go through a proxy and <c>0</c> otherwise, as measured on 2026-09-27
/// against curl 8.21.0 (mingw, Schannel). See ADR-0058 and BL-302.
/// </para>
/// <para>
/// <c>time_queue</c> is the handler's start, the moment the transfer left the queue, so
/// one microsecond with timings and <c>0.000000</c> without. The last five print the
/// fixed text curl 8.21.0 (Schannel) prints for every transfer this tool can run: a
/// verify result of <c>0</c>, which Schannel reports even for a failed verification;
/// <c>0</c> early-data bytes, which Schannel never sends; <c>0</c> retries and an empty
/// FTP entry path, because no <c>--retry</c> and no FTP handler exist yet. See ADR-0043.
/// </para>
/// <para>
/// A <c>time_*</c> value is the seconds from <see cref="TransferTimings.Started"/> to its
/// event, in whole microseconds printed with six decimals; an event that happened is at
/// least one microsecond after the start, and one that did not, or a transfer with no
/// <see cref="TransferReport.Timings"/>, prints <c>0.000000</c>. A <c>speed_*</c> value is
/// the bytes per second over <c>time_total</c>, truncated to a whole number, and <c>0</c>
/// without timings. See ADR-0035.
/// </para>
/// <para>
/// What was not learned prints as curl prints it: a status code as <c>000</c>, the HTTP
/// version as <c>0</c>, the method as <c>GET</c>, a port as <c>-1</c>, and a text value
/// as nothing. Measured on 2026-09-26 against curl 8.21.0 (mingw, Schannel); the commands
/// are in BL-225's Notes. Without a report, <c>size_download</c> is
/// <see cref="TransferResult.BytesTransferred"/> and <c>size_upload</c> is <c>0</c>.
/// </para>
/// </remarks>
/// <param name="result">The finished transfer.</param>
/// <param name="url">The URL exactly as given on the command line, printed by <c>%{url}</c>.</param>
/// <param name="urlNumber">The zero-based position of the URL among the transfers, printed by <c>%{urlnum}</c>.</param>
/// <param name="requestUrl">The URL handed to the protocol handler, printed by <c>%{url_effective}</c> when the report names no other.</param>
/// <param name="scheme">The lower-case scheme of the handler that ran, or <see langword="null"/> when no handler supports the URL; printed by <c>%{scheme}</c>.</param>
/// <param name="timeProvider">The provider whose <see cref="TimeProvider.GetTimestamp"/> took the report's <see cref="TransferTimings"/>, used to turn them into durations.</param>
public sealed class TransferWriteOutVariables(
    TransferResult result,
    string url,
    int urlNumber,
    string requestUrl,
    string? scheme,
    TimeProvider timeProvider) : IWriteOutVariableSource
{
    private const int UnknownPort = -1;

    private const long MicrosecondsPerSecond = 1_000_000;

    /// <summary>
    /// The URL parts <c>url.</c> and <c>urle.</c> name, in the order curl 8.21.0's
    /// <c>tool_writeout.c</c> lists them, each read from a parsed <see cref="CurlUrl"/>.
    /// </summary>
    private static readonly (string Name, Func<CurlUrl, string?> Read)[] UrlParts =
    [
        ("scheme", url => url.Scheme),
        ("user", url => url.User),
        ("password", url => url.Password),
        ("options", url => url.Options),
        ("host", url => url.Host),
        ("port", FormatUrlPort),
        ("path", url => url.AbsolutePath),
        ("query", url => url.Query),
        ("fragment", url => url.Fragment),
        ("zoneid", url => url.ZoneId),
    ];

    private static readonly Dictionary<string, Func<TransferWriteOutVariables, string>> VariableFormatters = AddUrlPartFormatters(new(StringComparer.Ordinal)
    {
        ["response_code"] = variables => FormatStatusCode(variables.report.ResponseCode),
        ["http_code"] = variables => FormatStatusCode(variables.report.ResponseCode),
        ["http_connect"] = variables => FormatStatusCode(variables.report.ProxyConnectResponseCode),
        ["http_version"] = variables => FormatHttpVersion(variables.report.HttpVersion),
        ["method"] = variables => variables.report.Method ?? "GET",
        ["content_type"] = variables => variables.report.ContentType ?? string.Empty,
        ["redirect_url"] = variables => variables.report.RedirectUrl ?? string.Empty,
        ["url_effective"] = variables => variables.EffectiveUrl,
        ["num_redirects"] = variables => FormatNumber(variables.report.RedirectCount),
        ["size_header"] = variables => FormatNumber(variables.report.HeaderSize),
        ["size_request"] = variables => FormatNumber(variables.report.RequestSize),
        ["size_download"] = variables => FormatNumber(variables.DownloadSize),
        ["size_upload"] = variables => FormatNumber(variables.report.UploadSize),
        ["num_connects"] = variables => FormatNumber(variables.report.ConnectionCount),
        ["num_headers"] = variables => FormatNumber(
            variables.report.ResponseHeaders.Count + variables.report.PseudoHeaders.Count),
        ["local_ip"] = variables => FormatAddress(variables.report.LocalEndPoint),
        ["local_port"] = variables => FormatPort(variables.report.LocalEndPoint),
        ["remote_ip"] = variables => FormatAddress(variables.report.RemoteEndPoint),
        ["remote_port"] = variables => FormatPort(variables.report.RemoteEndPoint),
        ["exitcode"] = variables => FormatNumber((int)variables.result.ExitCode),
        ["errormsg"] = variables => variables.result.ErrorMessage ?? string.Empty,
        ["url"] = variables => variables.url,
        ["urlnum"] = variables => FormatNumber(variables.urlNumber),
        ["scheme"] = variables => variables.scheme ?? string.Empty,
        ["time_queue"] = variables => FormatSeconds(variables.MicrosecondsSinceStart(variables.Timings?.Started)),
        ["time_namelookup"] = variables => FormatSeconds(variables.MicrosecondsSinceStart(variables.Timings?.Connect?.NameResolved)),
        ["time_connect"] = variables => FormatSeconds(variables.MicrosecondsSinceStart(variables.Timings?.Connect?.Connected)),
        ["time_appconnect"] = variables => FormatSeconds(variables.MicrosecondsSinceStart(variables.Timings?.Connect?.TlsHandshakeCompleted)),
        ["time_pretransfer"] = variables => FormatSeconds(variables.MicrosecondsSinceStart(variables.Timings?.RequestReady)),
        ["time_posttransfer"] = variables => FormatSeconds(variables.MicrosecondsSinceStart(variables.Timings?.RequestSent)),
        ["time_starttransfer"] = variables => FormatSeconds(variables.MicrosecondsSinceStart(variables.Timings?.FirstByteReceived)),
        ["time_redirect"] = variables => FormatSeconds(ToMicroseconds(variables.Timings?.RedirectDuration ?? TimeSpan.Zero)),
        ["time_total"] = variables => FormatSeconds(variables.TotalMicroseconds),
        ["speed_download"] = variables => FormatNumber(ComputeBytesPerSecond(variables.DownloadSize, variables.TotalMicroseconds)),
        ["speed_upload"] = variables => FormatNumber(ComputeBytesPerSecond(variables.report.UploadSize, variables.TotalMicroseconds)),
        ["ssl_verify_result"] = _ => "0",
        ["proxy_ssl_verify_result"] = _ => "0",
        ["tls_earlydata"] = _ => "0",
        ["num_retries"] = _ => "0",
        ["ftp_entry_path"] = _ => string.Empty,
        ["num_certs"] = variables => FormatNumber(variables.report.PeerCertificates.Count),
        ["proxy_used"] = variables => variables.report.UsedProxy ? "1" : "0",
        ["certs"] = variables => string.Concat(variables.report.PeerCertificates.Select(PeerCertificateText.Format)),
    });

    private static readonly char[] HeaderValueWhitespace = [' ', '\t'];

    private readonly TransferResult result = result ?? throw new ArgumentNullException(nameof(result));
    private readonly TransferReport report = result.Report ?? new TransferReport();
    private readonly string url = url ?? throw new ArgumentNullException(nameof(url));
    private readonly string requestUrl = requestUrl ?? throw new ArgumentNullException(nameof(requestUrl));
    private readonly int urlNumber = urlNumber;
    private readonly string? scheme = scheme;
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    private long DownloadSize => result.Report?.DownloadSize ?? result.BytesTransferred;

    private TransferTimings? Timings => report.Timings;

    private string EffectiveUrl => report.EffectiveUrl ?? requestUrl;

    private long TotalMicroseconds => MicrosecondsSinceStart(Timings?.Completed);

    /// <inheritdoc/>
    public bool TransferFailed => !result.IsSuccess;

    /// <inheritdoc/>
    public bool TryGetVariableText(string name, [NotNullWhen(true)] out string? text)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (VariableFormatters.TryGetValue(name, out Func<TransferWriteOutVariables, string>? format))
        {
            text = format(this);
            return true;
        }

        text = null;
        return false;
    }

    /// <inheritdoc/>
    /// <remarks>The value is trimmed of spaces and tabs at both ends, as curl prints it.</remarks>
    public string? FindFirstHeaderValue(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        foreach (KeyValuePair<string, string> header in report.ResponseHeaders)
        {
            if (string.Equals(header.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                return header.Value.Trim(HeaderValueWhitespace);
            }
        }

        return null;
    }

    /// <summary>
    /// The whole microseconds from the start to <paramref name="timestamp"/>, at least one
    /// as curl's <c>Curl_pgrsTime</c> makes it; zero when the event did not happen.
    /// </summary>
    private long MicrosecondsSinceStart(long? timestamp)
    {
        return timestamp is long reached
            ? Math.Max(1, ToMicroseconds(timeProvider.GetElapsedTime(Timings!.Started, reached)))
            : 0;
    }

    /// <summary>
    /// Adds <c>url.&lt;part&gt;</c>, read from the URL as given, and <c>urle.&lt;part&gt;</c>,
    /// read from the effective URL, for every part in <see cref="UrlParts"/>.
    /// </summary>
    private static Dictionary<string, Func<TransferWriteOutVariables, string>> AddUrlPartFormatters(
        Dictionary<string, Func<TransferWriteOutVariables, string>> formatters)
    {
        foreach ((string name, Func<CurlUrl, string?> read) in UrlParts)
        {
            formatters["url." + name] = variables => FormatUrlPart(variables.url, read);
            formatters["urle." + name] = variables => FormatUrlPart(variables.EffectiveUrl, read);
        }

        return formatters;
    }

    /// <summary>
    /// One part of <paramref name="text"/> as curl's <c>urlpart</c> prints it: parsed
    /// without path-as-is, and nothing when the URL does not parse or has no such part.
    /// </summary>
    private static string FormatUrlPart(string text, Func<CurlUrl, string?> read)
    {
        return CurlUrl.TryParse(text, pathAsIs: false, out CurlUrl? parsed) ? read(parsed) ?? string.Empty : string.Empty;
    }

    /// <summary>
    /// The port as <c>curl_url_get</c> with <c>CURLU_DEFAULT_PORT</c> gives it: the one
    /// written, else the scheme's default, which is <c>0</c> for <c>file</c>; nothing for
    /// a scheme curl does not know.
    /// </summary>
    private static string FormatUrlPort(CurlUrl url)
    {
        if (url.Port != UnknownPort)
        {
            return FormatNumber(url.Port);
        }

        return url.Scheme == "file" ? "0" : string.Empty;
    }

    private static long ToMicroseconds(TimeSpan duration)
    {
        return duration.Ticks / TimeSpan.TicksPerMicrosecond;
    }

    /// <summary>Prints microseconds as seconds with six decimals, as curl's <c>-w</c> prints a time.</summary>
    private static string FormatSeconds(long microseconds)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{microseconds / MicrosecondsPerSecond}.{microseconds % MicrosecondsPerSecond:D6}");
    }

    /// <summary>
    /// Bytes per second as curl's <c>trspeed</c> computes it: truncated, without overflowing
    /// for a size too large to multiply by a million. <paramref name="microseconds"/> of zero
    /// means no timings, which prints <c>0</c>.
    /// </summary>
    private static long ComputeBytesPerSecond(long size, long microseconds)
    {
        if (microseconds == 0)
        {
            return 0;
        }

        if (size < long.MaxValue / MicrosecondsPerSecond)
        {
            return size * MicrosecondsPerSecond / microseconds;
        }

        return microseconds >= MicrosecondsPerSecond ? size / (microseconds / MicrosecondsPerSecond) : long.MaxValue;
    }

    private static string FormatStatusCode(int code)
    {
        return code.ToString("D3", CultureInfo.InvariantCulture);
    }

    private static string FormatHttpVersion(Version? version)
    {
        return version switch
        {
            { Major: 1 } => version.Minor == 0 ? "1" : "1.1",
            { Major: 2 or 3 } => FormatNumber(version.Major),
            _ => "0",
        };
    }

    private static string FormatNumber(long number)
    {
        return number.ToString(CultureInfo.InvariantCulture);
    }

    private static string FormatAddress(IPEndPoint? endPoint)
    {
        return endPoint?.Address.ToString() ?? string.Empty;
    }

    private static string FormatPort(IPEndPoint? endPoint)
    {
        return FormatNumber(endPoint?.Port ?? UnknownPort);
    }
}
