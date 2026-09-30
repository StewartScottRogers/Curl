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
/// <c>size_download</c>, <c>size_delivered</c>, <c>size_upload</c>, <c>num_connects</c>, <c>num_headers</c>,
/// <c>local_ip</c>, <c>local_port</c>, <c>remote_ip</c>, <c>remote_port</c>,
/// <c>exitcode</c>, <c>errormsg</c>, <c>url</c>, <c>urlnum</c>, <c>scheme</c>,
/// <c>time_namelookup</c>, <c>time_connect</c>, <c>time_appconnect</c>,
/// <c>time_pretransfer</c>, <c>time_posttransfer</c>, <c>time_starttransfer</c>,
/// <c>time_redirect</c>, <c>time_total</c>, <c>time_queue</c>, <c>speed_download</c>,
/// <c>speed_upload</c>, <c>ssl_verify_result</c>, <c>proxy_ssl_verify_result</c>,
/// <c>tls_earlydata</c>, <c>num_retries</c>, <c>ftp_entry_path</c>, <c>num_certs</c>,
/// <c>certs</c>, <c>proxy_used</c>, <c>referer</c>, <c>filename_effective</c>,
/// <c>conn_id</c>, <c>xfer_id</c>, <c>json</c>, <c>header_json</c>, and
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
/// <c>json</c> prints every other variable here as one JSON object, in ordinal name order,
/// then <c>curl_version</c> from <see cref="LibraryVersion"/>: a number unquoted (a status
/// code without its leading zeros), a time as its seconds, and a text value quoted, or
/// <c>null</c> where curl has no value, as for a missing content type, error message or URL
/// part. <c>header_json</c> prints the response headers as <see cref="WriteOutJson"/>
/// describes. Measured on 2026-09-27 against curl 8.21.0 (mingw, Schannel); see ADR-0063
/// and BL-227.
/// </para>
/// <para>
/// <c>size_download</c> prints <see cref="TransferReport.DownloadSize"/>, the body bytes
/// after chunked framing is removed and before content decoding, and <c>size_delivered</c>
/// prints <see cref="TransferReport.DeliveredSize"/>, the bytes after content decoding, or
/// the <c>size_download</c> value when the report has no decoded count because nothing was
/// decoded. Under <c>--compressed</c> a gzip body of 51 bytes that decodes to 501 prints
/// <c>51 501</c>, as measured on 2026-09-28 against curl 8.21.0 (mingw, Schannel); see BL-516.
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
/// <c>referer</c>, <c>filename_effective</c>, <c>conn_id</c> and <c>xfer_id</c> are known
/// to the command line and the process, not to a handler, so they are set by the caller
/// through <see cref="Referer"/>, <see cref="OutputFileName"/>, <see cref="ConnectionId"/>
/// and <see cref="TransferId"/>. Measured on 2026-09-26 against curl 8.21.0 (mingw,
/// Schannel): no <c>-e</c> and output to standard output print nothing, and a URL curl
/// rejects prints <c>conn_id</c> <c>-1</c>. See BL-305.
/// </para>
/// <para>
/// <c>time_queue</c> is the handler's start, the moment the transfer left the queue, so
/// one microsecond with timings and <c>0.000000</c> without. <c>ssl_verify_result</c> and
/// <c>proxy_ssl_verify_result</c> are <see cref="SslVerifyResult"/> and
/// <see cref="ProxySslVerifyResult"/>, set by the caller: <c>0</c> in the Schannel build,
/// which reports it even for a failed verification (ADR-0043), and OpenSSL's verify code in
/// the OpenSSL build (BL-661). <c>tls_earlydata</c> prints the <c>0</c> early-data bytes
/// Schannel never sends. <c>num_retries</c>
/// is <see cref="RetryCount"/>, set by the caller (BL-513). <c>ftp_entry_path</c> is
/// <see cref="TransferReport.FtpEntryPath"/>: nothing, and <c>null</c> in <c>json</c>,
/// for a transfer that is not FTP or whose <c>PWD</c> reply named no directory (BL-514).
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

    private const int UnreportedLocalPort = 0;

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

    private static readonly Dictionary<string, Func<TransferWriteOutVariables, WriteOutValue>> VariableFormatters = AddUrlPartFormatters(new(StringComparer.Ordinal)
    {
        ["response_code"] = variables => WriteOutValue.FromStatusCode(variables.report.ResponseCode),
        ["http_code"] = variables => WriteOutValue.FromStatusCode(variables.report.ResponseCode),
        ["http_connect"] = variables => WriteOutValue.FromStatusCode(variables.report.ProxyConnectResponseCode),
        ["http_version"] = variables => WriteOutValue.FromText(FormatHttpVersion(variables.report.HttpVersion)),
        ["method"] = variables => WriteOutValue.FromText(variables.report.Method ?? "GET"),
        ["content_type"] = variables => WriteOutValue.FromText(variables.report.ContentType),
        ["redirect_url"] = variables => WriteOutValue.FromText(variables.report.RedirectUrl),
        ["url_effective"] = variables => WriteOutValue.FromText(variables.EffectiveUrl),
        ["num_redirects"] = variables => WriteOutValue.FromNumber(variables.report.RedirectCount),
        ["size_header"] = variables => WriteOutValue.FromNumber(variables.report.HeaderSize),
        ["size_request"] = variables => WriteOutValue.FromNumber(variables.report.RequestSize),
        ["size_download"] = variables => WriteOutValue.FromNumber(variables.DownloadSize),
        ["size_delivered"] = variables => WriteOutValue.FromNumber(variables.DeliveredSize),
        ["size_upload"] = variables => WriteOutValue.FromNumber(variables.report.UploadSize),
        ["num_connects"] = variables => WriteOutValue.FromNumber(variables.report.ConnectionCount),
        ["num_headers"] = variables => WriteOutValue.FromNumber(
            variables.report.ResponseHeaders.Count + variables.report.PseudoHeaders.Count),
        ["local_ip"] = variables => WriteOutValue.FromText(FormatAddress(variables.report.LocalEndPoint)),
        ["local_port"] = variables => WriteOutValue.FromNumber(FindLocalPort(variables.report)),
        ["remote_ip"] = variables => WriteOutValue.FromText(FormatAddress(variables.report.RemoteEndPoint)),
        ["remote_port"] = variables => WriteOutValue.FromNumber(FindPort(variables.report.RemoteEndPoint)),
        ["exitcode"] = variables => WriteOutValue.FromNumber((int)variables.result.ExitCode),
        ["errormsg"] = variables => WriteOutValue.FromText(variables.result.ErrorMessage),
        ["url"] = variables => WriteOutValue.FromText(variables.url),
        ["urlnum"] = variables => WriteOutValue.FromNumber(variables.urlNumber),
        ["scheme"] = variables => WriteOutValue.FromText(variables.scheme),
        ["time_queue"] = variables => FormatSeconds(variables.MicrosecondsSinceStart(variables.Timings?.Started)),
        ["time_namelookup"] = variables => FormatSeconds(variables.MicrosecondsSinceStart(variables.Timings?.Connect?.NameResolved)),
        ["time_connect"] = variables => FormatSeconds(variables.MicrosecondsSinceStart(variables.Timings?.Connect?.Connected)),
        ["time_appconnect"] = variables => FormatSeconds(variables.MicrosecondsSinceStart(variables.Timings?.Connect?.TlsHandshakeCompleted)),
        ["time_pretransfer"] = variables => FormatSeconds(variables.MicrosecondsSinceStart(variables.Timings?.RequestReady)),
        ["time_posttransfer"] = variables => FormatSeconds(variables.MicrosecondsSinceStart(variables.Timings?.RequestSent)),
        ["time_starttransfer"] = variables => FormatSeconds(variables.MicrosecondsSinceStart(variables.Timings?.FirstByteReceived)),
        ["time_redirect"] = variables => FormatSeconds(ToMicroseconds(variables.Timings?.RedirectDuration ?? TimeSpan.Zero)),
        ["time_total"] = variables => FormatSeconds(variables.TotalMicroseconds),
        ["speed_download"] = variables => WriteOutValue.FromNumber(ComputeBytesPerSecond(variables.DownloadSize, variables.TotalMicroseconds)),
        ["speed_upload"] = variables => WriteOutValue.FromNumber(ComputeBytesPerSecond(variables.report.UploadSize, variables.TotalMicroseconds)),
        ["ssl_verify_result"] = variables => WriteOutValue.FromNumber(variables.SslVerifyResult),
        ["proxy_ssl_verify_result"] = variables => WriteOutValue.FromNumber(variables.ProxySslVerifyResult),
        ["tls_earlydata"] = _ => WriteOutValue.FromNumber(0),
        ["num_retries"] = variables => WriteOutValue.FromNumber(variables.RetryCount),
        ["ftp_entry_path"] = variables => WriteOutValue.FromText(variables.report.FtpEntryPath),
        ["num_certs"] = variables => WriteOutValue.FromNumber(variables.report.PeerCertificates.Count),
        ["proxy_used"] = variables => WriteOutValue.FromNumber(variables.report.UsedProxy ? 1 : 0),
        ["certs"] = variables => WriteOutValue.FromText(string.Concat(variables.report.PeerCertificates.Select(PeerCertificateText.Format))),
        ["referer"] = variables => WriteOutValue.FromText(variables.Referer),
        ["filename_effective"] = variables => WriteOutValue.FromText(variables.OutputFileName),
        ["conn_id"] = variables => WriteOutValue.FromNumber(variables.ConnectionId),
        ["xfer_id"] = variables => WriteOutValue.FromNumber(variables.TransferId),
    });

    /// <summary>The names <c>%{json}</c> prints before <c>curl_version</c>, in curl's order: ordinal.</summary>
    private static readonly string[] JsonMemberNames = [.. VariableFormatters.Keys.Order(StringComparer.Ordinal)];

    private static readonly char[] HeaderValueWhitespace = [' ', '\t'];

    private readonly TransferResult result = result ?? throw new ArgumentNullException(nameof(result));
    private readonly TransferReport report = result.Report ?? new TransferReport();
    private readonly string url = url ?? throw new ArgumentNullException(nameof(url));
    private readonly string requestUrl = requestUrl ?? throw new ArgumentNullException(nameof(requestUrl));
    private readonly int urlNumber = urlNumber;
    private readonly string? scheme = scheme;
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <summary>
    /// Gets the <c>Referer</c> the request was sent with, printed by <c>%{referer}</c>, or
    /// <see langword="null"/> (printed as nothing) when it was sent without one.
    /// </summary>
    public string? Referer { get; init; }

    /// <summary>
    /// Gets the file the body was saved to, printed by <c>%{filename_effective}</c>, or
    /// <see langword="null"/> (printed as nothing) when it went to standard output.
    /// </summary>
    public string? OutputFileName { get; init; }

    /// <summary>
    /// Gets the process-wide number of the connection the transfer used, printed by
    /// <c>%{conn_id}</c>; <c>-1</c>, the default, when it used none.
    /// </summary>
    public long ConnectionId { get; init; } = -1;

    /// <summary>
    /// Gets the process-wide zero-based number of the transfer, printed by <c>%{xfer_id}</c>.
    /// </summary>
    public long TransferId { get; init; }

    /// <summary>
    /// Gets how many times <c>--retry</c> ran the transfer again before this, its last attempt,
    /// printed by <c>%{num_retries}</c>; <c>0</c>, the default, when it ran once. The caller
    /// counts the retries <c>Curl.Core</c>'s <c>TransferRetrier</c> announces. curl 8.21.0
    /// printed <c>2</c> for <c>--retry 2</c> against 503, 503, 200 and against three 503s
    /// (measured 2026-09-28, BL-513).
    /// </summary>
    public int RetryCount { get; init; }

    /// <summary>
    /// Gets the OpenSSL <c>X509_V_</c> code the origin's certificate check ended with,
    /// printed by <c>%{ssl_verify_result}</c>; <c>0</c>, the default, without TLS and in the
    /// Schannel build. curl 8.18.0's OpenSSL build printed <c>18</c> under <c>-k</c> for a
    /// self-signed certificate and <c>0</c> once <c>--cacert</c> trusted it (measured
    /// 2026-09-30, BL-661).
    /// </summary>
    public long SslVerifyResult { get; init; }

    /// <summary>
    /// Gets the OpenSSL <c>X509_V_</c> code an HTTPS proxy's certificate check ended with,
    /// printed by <c>%{proxy_ssl_verify_result}</c>; <c>0</c>, the default, without an HTTPS
    /// proxy and in the Schannel build (BL-661).
    /// </summary>
    public long ProxySslVerifyResult { get; init; }

    /// <summary>
    /// Gets the library version <c>%{json}</c> prints last, as <c>curl_version</c>; by
    /// default <see cref="FormatLibraryVersion"/> for the running system. See ADR-0063.
    /// </summary>
    public string LibraryVersion { get; init; } = FormatLibraryVersion(OperatingSystem.IsWindows(), OperatingSystem.IsMacOS());

    private long DownloadSize => result.Report?.DownloadSize ?? result.BytesTransferred;

    private long DeliveredSize => result.Report?.DeliveredSize ?? DownloadSize;

    private TransferTimings? Timings => report.Timings;

    private string EffectiveUrl => report.EffectiveUrl ?? requestUrl;

    private long TotalMicroseconds => MicrosecondsSinceStart(Timings?.Completed);

    /// <summary>
    /// The library part of <c>-V</c>'s first line, which ADR-0021 limits to the TLS backend
    /// <c>SslStream</c> sits on: <c>libcurl/8.21.0 Schannel</c> on Windows,
    /// <c>libcurl/8.21.0 SecureTransport</c> on macOS and <c>libcurl/8.21.0 OpenSSL</c>
    /// anywhere else.
    /// </summary>
    /// <param name="isWindows">Whether the running system is Windows.</param>
    /// <param name="isMacOS">Whether the running system is macOS; read only when <paramref name="isWindows"/> is <see langword="false"/>.</param>
    /// <returns>The library version text.</returns>
    public static string FormatLibraryVersion(bool isWindows, bool isMacOS)
    {
        return isWindows ? "libcurl/8.21.0 Schannel"
            : isMacOS ? "libcurl/8.21.0 SecureTransport"
            : "libcurl/8.21.0 OpenSSL";
    }

    /// <inheritdoc/>
    public bool TransferFailed => !result.IsSuccess;

    /// <inheritdoc/>
    public bool TryGetVariableText(string name, [NotNullWhen(true)] out string? text)
    {
        ArgumentNullException.ThrowIfNull(name);
        text = name switch
        {
            "json" => FormatJson(),
            "header_json" => WriteOutJson.FormatHeaders(report.ResponseHeaders, HeaderValueWhitespace),
            _ => VariableFormatters.TryGetValue(name, out Func<TransferWriteOutVariables, WriteOutValue>? format) ? format(this).Text : null,
        };
        return text is not null;
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
    /// The object <c>%{json}</c> prints on one line: every variable in
    /// <see cref="JsonMemberNames"/>, then <c>curl_version</c>.
    /// </summary>
    private string FormatJson()
    {
        IEnumerable<string> members = JsonMemberNames
            .Select(name => $"{WriteOutJson.Quote(name)}:{VariableFormatters[name](this).Json}")
            .Append($"\"curl_version\":{WriteOutJson.Quote(LibraryVersion)}");
        return "{" + string.Join(',', members) + "}";
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
    private static Dictionary<string, Func<TransferWriteOutVariables, WriteOutValue>> AddUrlPartFormatters(
        Dictionary<string, Func<TransferWriteOutVariables, WriteOutValue>> formatters)
    {
        foreach ((string name, Func<CurlUrl, string?> read) in UrlParts)
        {
            formatters["url." + name] = variables => WriteOutValue.FromText(FindUrlPart(variables.url, read));
            formatters["urle." + name] = variables => WriteOutValue.FromText(FindUrlPart(variables.EffectiveUrl, read));
        }

        return formatters;
    }

    /// <summary>
    /// One part of <paramref name="text"/> as curl's <c>urlpart</c> prints it: parsed
    /// without path-as-is, and <see langword="null"/>, which prints nothing, when the URL
    /// does not parse or has no such part.
    /// </summary>
    private static string? FindUrlPart(string text, Func<CurlUrl, string?> read)
    {
        return CurlUrl.TryParse(text, pathAsIs: false, out CurlUrl? parsed) ? read(parsed) : null;
    }

    /// <summary>
    /// The port as <c>curl_url_get</c> with <c>CURLU_DEFAULT_PORT</c> gives it: the one
    /// written, else the scheme's default, which is <c>0</c> for <c>file</c>;
    /// <see langword="null"/> for a scheme curl does not know.
    /// </summary>
    private static string? FormatUrlPort(CurlUrl url)
    {
        if (url.Port != UnknownPort)
        {
            return FormatNumber(url.Port);
        }

        return url.Scheme == "file" ? "0" : null;
    }

    private static long ToMicroseconds(TimeSpan duration)
    {
        return duration.Ticks / TimeSpan.TicksPerMicrosecond;
    }

    /// <summary>Prints microseconds as seconds with six decimals, as curl's <c>-w</c> prints a time.</summary>
    private static WriteOutValue FormatSeconds(long microseconds)
    {
        return WriteOutValue.FromFormattedNumber(string.Create(
            CultureInfo.InvariantCulture,
            $"{microseconds / MicrosecondsPerSecond}.{microseconds % MicrosecondsPerSecond:D6}"));
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

    private static int FindPort(IPEndPoint? endPoint)
    {
        return endPoint?.Port ?? UnknownPort;
    }

    /// <summary>
    /// The <c>%{local_port}</c> of <paramref name="report" />: its local end point's port;
    /// <c>0</c> when a connection was made but has no local end to report, as curl 8.21.0
    /// prints for its unconnected TFTP socket; <c>-1</c> when no connection was made
    /// (measured, BL-515 Notes; ADR-0119).
    /// </summary>
    private static int FindLocalPort(TransferReport report)
    {
        return report.LocalEndPoint?.Port ?? (report.RemoteEndPoint is null ? UnknownPort : UnreportedLocalPort);
    }
}
