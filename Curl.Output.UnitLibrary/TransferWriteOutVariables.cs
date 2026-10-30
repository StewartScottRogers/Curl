using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using Curl.Api.Info;
using Curl.Protocol.Abstractions;

namespace Curl.Output;

/// <summary>
/// Supplies the <c>-w</c> / <c>--write-out</c> variables of one finished transfer: every variable
/// curl's <c>tool_writeout.c</c> reads with <c>curl_easy_getinfo</c> from the transfer's
/// <see cref="TransferInfo"/>, and the tool's own from its <see cref="TransferResult"/>, its
/// <see cref="TransferReport"/> and the URL it was given, formatted as curl 8.21.0 prints them.
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
/// <c>query</c>, <c>fragment</c> and <c>zoneid</c>. Any other name prints the value an extension's
/// transfer hook gave it in <see cref="TransferReport.ExtensionWriteOutValues"/> (ADR-1130), and is
/// reported unknown when none did.
/// </para>
/// <para>
/// From the <see cref="TransferInfo"/> (ADR-0506, BL-2319): the response and <c>CONNECT</c> codes,
/// the HTTP version, method, content type, redirect URL, effective URL (and the <c>urle.</c>
/// parts), redirect count, header, request, download and upload sizes, connection count, the
/// addresses and ports, scheme, every <c>time_*</c>, the speeds, both verify results, the early data
/// bytes, the FTP entry path, <c>proxy_used</c>, <c>referer</c>, <c>conn_id</c> and <c>xfer_id</c>.
/// The rest are the tool's: <c>exitcode</c>, <c>errormsg</c>, <c>url</c>, <c>urlnum</c>,
/// <c>num_retries</c>, <c>filename_effective</c>, and, from the report, <c>size_delivered</c>,
/// <c>num_headers</c> and the headers (curl reads those through its header API), and <c>certs</c>
/// and <c>num_certs</c>, printed from the certificates themselves with
/// <see cref="PeerCertificateText"/>.
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
/// describes. <c>content_type</c>, <c>header_json</c> and the <c>%header{…}</c> values
/// are marked with <see cref="WriteOutTemplateRenderer.MarkHeaderBytes"/>, so they render as
/// the raw header bytes (CGR-021, ADR-0893). Measured on 2026-09-27 against curl 8.21.0 (mingw, Schannel); see ADR-0063
/// and BL-227.
/// </para>
/// <para>
/// <c>size_download</c> prints <see cref="TransferInfo.SizeDownload"/>, the body bytes
/// after chunked framing is removed and before content decoding, and <c>size_delivered</c>
/// prints <see cref="TransferReport.DeliveredSize"/>, the bytes after content decoding, or
/// the <c>size_download</c> value when the report has no decoded count because nothing was
/// decoded. Under <c>--compressed</c> a gzip body of 51 bytes that decodes to 501 prints
/// <c>51 501</c>, as measured on 2026-09-28 against curl 8.21.0 (mingw, Schannel); see BL-516.
/// </para>
/// <para>
/// <c>num_certs</c> counts <see cref="TransferReport.PeerCertificates"/> and <c>certs</c>
/// prints each with <see cref="PeerCertificateText"/> in the layout of <see cref="TlsBackend"/>'s curl build, one after another; a transfer
/// without TLS has none, so <c>0</c> and nothing, as measured on 2026-09-26 against curl
/// 8.21.0 (mingw, Schannel) for file:// and http://. See ADR-0054 and BL-303.
/// </para>
/// <para>
/// <c>proxy_used</c> is <see cref="TransferInfo.UsedProxy"/>: <c>1</c> when the transfer was set
/// to go through a proxy and <c>0</c> otherwise, as measured on 2026-09-27 against curl 8.21.0
/// (mingw, Schannel). See ADR-0058 and BL-302.
/// </para>
/// <para>
/// <c>filename_effective</c> and <c>num_retries</c> are known to the command line, not to a
/// handler, so they are set by the caller through <see cref="OutputFileName"/> and
/// <see cref="RetryCount"/>. Measured on 2026-09-26 against curl 8.21.0 (mingw, Schannel): no
/// <c>-e</c> and output to standard output print nothing, and a URL curl rejects prints
/// <c>conn_id</c> <c>-1</c>. See BL-305.
/// </para>
/// <para>
/// A <c>time_*</c> value is the info's time in whole microseconds printed with six decimals;
/// <see cref="WriteOutTransferInfo.FromReport"/> makes an event that happened at least one
/// microsecond after the start and one that did not <c>0.000000</c>. A <c>speed_*</c> value is
/// the info's bytes per second. See ADR-0035.
/// </para>
/// <para>
/// What was not learned prints as curl prints it: a status code as <c>000</c>, the HTTP
/// version as <c>0</c>, a port as <c>-1</c> (the info's <c>0</c>), and a text value as nothing.
/// Measured on 2026-09-26 against curl 8.21.0 (mingw, Schannel); the commands are in BL-225's
/// Notes.
/// </para>
/// </remarks>
public sealed class TransferWriteOutVariables : IWriteOutVariableSource
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
        ["response_code"] = variables => WriteOutValue.FromStatusCode((int)variables.info.ResponseCode),
        ["http_code"] = variables => WriteOutValue.FromStatusCode((int)variables.info.ResponseCode),
        ["http_connect"] = variables => WriteOutValue.FromStatusCode((int)variables.info.HttpConnectCode),
        ["http_version"] = variables => WriteOutValue.FromText(FormatHttpVersion(variables.info.HttpVersion)),
        ["method"] = variables => WriteOutValue.FromText(variables.info.EffectiveMethod),
        ["content_type"] = variables => WriteOutValue.FromText(MarkHeaderBytes(variables.info.ContentType)),
        ["redirect_url"] = variables => WriteOutValue.FromText(variables.info.RedirectUrl),
        ["url_effective"] = variables => WriteOutValue.FromText(variables.info.EffectiveUrl),
        ["num_redirects"] = variables => WriteOutValue.FromNumber(variables.info.RedirectCount),
        ["size_header"] = variables => WriteOutValue.FromNumber(variables.info.HeaderSize),
        ["size_request"] = variables => WriteOutValue.FromNumber(variables.info.RequestSize),
        ["size_download"] = variables => WriteOutValue.FromNumber(variables.info.SizeDownload),
        ["size_delivered"] = variables => WriteOutValue.FromNumber(variables.DeliveredSize),
        ["size_upload"] = variables => WriteOutValue.FromNumber(variables.info.SizeUpload),
        ["num_connects"] = variables => WriteOutValue.FromNumber(variables.info.NumConnects),
        ["num_headers"] = variables => WriteOutValue.FromNumber(
            variables.report.TrailerCount > 0 ? variables.report.TrailerCount : variables.report.ResponseHeaders.Count + variables.report.PseudoHeaders.Count),
        ["local_ip"] = variables => WriteOutValue.FromText(variables.info.LocalIp),
        ["local_port"] = variables => WriteOutValue.FromNumber(FindLocalPort(variables.info)),
        ["remote_ip"] = variables => WriteOutValue.FromText(variables.info.PrimaryIp),
        ["remote_port"] = variables => WriteOutValue.FromNumber(FindRemotePort(variables.info)),
        ["exitcode"] = variables => WriteOutValue.FromNumber((int)variables.result.ExitCode),
        ["errormsg"] = variables => WriteOutValue.FromText(variables.result.WriteOutErrorMessage ?? variables.result.ErrorMessage),
        ["url"] = variables => WriteOutValue.FromText(variables.url),
        ["urlnum"] = variables => WriteOutValue.FromNumber(variables.urlNumber),
        ["scheme"] = variables => WriteOutValue.FromText(variables.info.Scheme),
        ["time_queue"] = variables => FormatSeconds(variables.info.QueueTime),
        ["time_namelookup"] = variables => FormatSeconds(variables.info.NameLookupTime),
        ["time_connect"] = variables => FormatSeconds(variables.info.ConnectTime),
        ["time_appconnect"] = variables => FormatSeconds(variables.info.AppConnectTime),
        ["time_pretransfer"] = variables => FormatSeconds(variables.info.PreTransferTime),
        ["time_posttransfer"] = variables => FormatSeconds(variables.info.PostTransferTime),
        ["time_starttransfer"] = variables => FormatSeconds(variables.info.StartTransferTime),
        ["time_redirect"] = variables => FormatSeconds(variables.info.RedirectTime),
        ["time_total"] = variables => FormatSeconds(variables.info.TotalTime),
        ["speed_download"] = variables => WriteOutValue.FromNumber(variables.info.SpeedDownload),
        ["speed_upload"] = variables => WriteOutValue.FromNumber(variables.info.SpeedUpload),
        ["ssl_verify_result"] = variables => WriteOutValue.FromNumber(variables.info.SslVerifyResult),
        ["proxy_ssl_verify_result"] = variables => WriteOutValue.FromNumber(variables.info.ProxySslVerifyResult),
        ["tls_earlydata"] = variables => WriteOutValue.FromNumber(variables.info.EarlyDataSent),
        ["num_retries"] = variables => WriteOutValue.FromNumber(variables.RetryCount),
        ["ftp_entry_path"] = variables => WriteOutValue.FromText(variables.info.FtpEntryPath),
        ["num_certs"] = variables => WriteOutValue.FromNumber(variables.report.PeerCertificates.Count),
        ["proxy_used"] = variables => WriteOutValue.FromNumber(variables.info.UsedProxy),
        ["certs"] = variables => WriteOutValue.FromText(string.Concat(variables.report.PeerCertificates.Select(certificate => PeerCertificateText.FormatFor(certificate, variables.TlsBackend)))),
        ["referer"] = variables => WriteOutValue.FromText(variables.info.Referer),
        ["filename_effective"] = variables => WriteOutValue.FromText(variables.OutputFileName),
        ["conn_id"] = variables => WriteOutValue.FromNumber(variables.info.ConnectionId),
        ["xfer_id"] = variables => WriteOutValue.FromNumber(variables.info.TransferId),
    });

    /// <summary>The names <c>%{json}</c> prints before <c>curl_version</c>, in curl's order: ordinal.</summary>
    private static readonly string[] JsonMemberNames = [.. VariableFormatters.Keys.Order(StringComparer.Ordinal)];

    private static readonly char[] HeaderValueWhitespace = [' ', '\t'];

    /// <summary>The <c>%{http_version}</c> text of each <c>CURL_HTTP_VERSION_*</c> value a transfer reports.</summary>
    private static readonly Dictionary<long, string> HttpVersionTexts = new()
    {
        [1] = "1",
        [2] = "1.1",
        [3] = "2",
        [30] = "3",
    };

    private readonly TransferResult result;
    private readonly TransferReport report;
    private readonly string url;
    private readonly int urlNumber;
    private TransferInfo info;

    /// <summary>
    /// Creates the variables of a transfer whose <c>curl_easy_getinfo</c> answers are
    /// <paramref name="info"/>, as curl's <c>tool_writeout.c</c> reads each libcurl variable with
    /// <c>curl_easy_getinfo</c>; the tool's own variables come from <paramref name="result"/> and the
    /// other arguments.
    /// </summary>
    /// <param name="info">The transfer's info, the value of every libcurl variable.</param>
    /// <param name="result">The finished transfer, for <c>%{exitcode}</c>, <c>%{errormsg}</c>, <c>%{size_delivered}</c>, <c>%{num_headers}</c>, <c>%{certs}</c>, <c>%{num_certs}</c> and the headers.</param>
    /// <param name="url">The URL exactly as given on the command line, printed by <c>%{url}</c>.</param>
    /// <param name="urlNumber">The zero-based position of the URL among the transfers, printed by <c>%{urlnum}</c>.</param>
    public TransferWriteOutVariables(TransferInfo info, TransferResult result, string url, int urlNumber)
    {
        this.info = info ?? throw new ArgumentNullException(nameof(info));
        this.result = result ?? throw new ArgumentNullException(nameof(result));
        this.url = url ?? throw new ArgumentNullException(nameof(url));
        this.urlNumber = urlNumber;
        report = result.Report ?? new TransferReport();
    }

    /// <summary>
    /// Creates the variables of a transfer from its report alone: the info is
    /// <see cref="WriteOutTransferInfo.FromReport"/> with <paramref name="scheme"/>, no
    /// <c>Referer</c>, <c>%{xfer_id}</c> <c>0</c>, <c>%{conn_id}</c> <c>-1</c> and verify results of
    /// <c>0</c>, each of which its property may set.
    /// </summary>
    /// <param name="result">The finished transfer.</param>
    /// <param name="url">The URL exactly as given on the command line, printed by <c>%{url}</c>.</param>
    /// <param name="urlNumber">The zero-based position of the URL among the transfers, printed by <c>%{urlnum}</c>.</param>
    /// <param name="requestUrl">The URL handed to the protocol handler, printed by <c>%{url_effective}</c> when the report names no other.</param>
    /// <param name="scheme">The lower-case scheme of the handler that ran, or <see langword="null"/> when no handler supports the URL; printed by <c>%{scheme}</c>.</param>
    /// <param name="timeProvider">The provider whose <see cref="TimeProvider.GetTimestamp"/> took the report's <see cref="TransferTimings"/>, used to turn them into durations.</param>
    public TransferWriteOutVariables(
        TransferResult result,
        string url,
        int urlNumber,
        string requestUrl,
        string? scheme,
        TimeProvider timeProvider)
        : this(ReportOnlyInfoOf(result, requestUrl, scheme, timeProvider), result, url, urlNumber)
    {
    }

    /// <summary>
    /// Gets the <c>Referer</c> the request was sent with, printed by <c>%{referer}</c>, or
    /// <see langword="null"/> (printed as nothing) when it was sent without one: the info's
    /// <see cref="TransferInfo.Referer"/>.
    /// </summary>
    public string? Referer { get => info.Referer; init => info = info with { Referer = value }; }

    /// <summary>
    /// Gets the file the body was saved to, printed by <c>%{filename_effective}</c>, or
    /// <see langword="null"/> (printed as nothing) when it went to standard output.
    /// </summary>
    public string? OutputFileName { get; init; }

    /// <summary>
    /// Gets the process-wide number of the connection the transfer used, printed by
    /// <c>%{conn_id}</c>; <c>-1</c> when it used none. The info's <see cref="TransferInfo.ConnectionId"/>.
    /// </summary>
    public long ConnectionId { get => info.ConnectionId; init => info = info with { ConnectionId = value }; }

    /// <summary>
    /// Gets the process-wide zero-based number of the transfer, printed by <c>%{xfer_id}</c>: the
    /// info's <see cref="TransferInfo.TransferId"/>.
    /// </summary>
    public long TransferId { get => info.TransferId; init => info = info with { TransferId = value }; }

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
    /// printed by <c>%{ssl_verify_result}</c>; <c>0</c> without TLS and in the
    /// Schannel build. curl 8.18.0's OpenSSL build printed <c>18</c> under <c>-k</c> for a
    /// self-signed certificate and <c>0</c> once <c>--cacert</c> trusted it (measured
    /// 2026-09-30, BL-661). The info's <see cref="TransferInfo.SslVerifyResult"/>.
    /// </summary>
    public long SslVerifyResult { get => info.SslVerifyResult; init => info = info with { SslVerifyResult = value }; }

    /// <summary>
    /// Gets the OpenSSL <c>X509_V_</c> code an HTTPS proxy's certificate check ended with,
    /// printed by <c>%{proxy_ssl_verify_result}</c>; <c>0</c> without an HTTPS
    /// proxy and in the Schannel build (BL-661). The info's <see cref="TransferInfo.ProxySslVerifyResult"/>.
    /// </summary>
    public long ProxySslVerifyResult { get => info.ProxySslVerifyResult; init => info = info with { ProxySslVerifyResult = value }; }

    /// <summary>
    /// Gets the bytes the origin's connection sent as TLS 1.3 0-RTT early data, printed by
    /// <c>%{tls_earlydata}</c> (<c>CURLINFO_EARLYDATA_SENT_T</c>): negative when the server
    /// rejected them, and <c>0</c> without early data. curl 8.21.0's Schannel
    /// build sends none and printed <c>0</c> under <c>--tls-earlydata</c> (measured
    /// 2026-10-01, BL-906). The info's <see cref="TransferInfo.EarlyDataSent"/>.
    /// </summary>
    public long TlsEarlyDataSent { get => info.EarlyDataSent; init => info = info with { EarlyDataSent = value }; }

    /// <summary>
    /// Gets the library version <c>%{json}</c> prints last, as <c>curl_version</c>; by
    /// default <see cref="FormatLibraryVersion"/> for the running system. See ADR-0063.
    /// </summary>
    public string LibraryVersion { get; init; } = FormatLibraryVersion(OperatingSystem.IsWindows(), OperatingSystem.IsMacOS());

    /// <summary>
    /// Gets the TLS library whose curl build <c>%{certs}</c> matches; by default
    /// <see cref="PlatformTlsBackend.ForProcess"/> (CGR-091).
    /// </summary>
    public TlsBackend TlsBackend { get; init; } = PlatformTlsBackend.ForProcess;

    private long DeliveredSize => report.DeliveredSize ?? info.SizeDownload;

    /// <summary>
    /// The library part of <c>-V</c>'s first line, which ADR-0021 limits to the TLS backend
    /// <c>SslStream</c> sits on: <c>libcurl/8.22.0 Schannel</c> on Windows,
    /// <c>libcurl/8.22.0 SecureTransport</c> on macOS and <c>libcurl/8.22.0 OpenSSL</c>
    /// anywhere else.
    /// </summary>
    /// <param name="isWindows">Whether the running system is Windows.</param>
    /// <param name="isMacOS">Whether the running system is macOS; read only when <paramref name="isWindows"/> is <see langword="false"/>.</param>
    /// <returns>The library version text.</returns>
    public static string FormatLibraryVersion(bool isWindows, bool isMacOS)
    {
        return isWindows ? "libcurl/8.22.0 Schannel"
            : isMacOS ? "libcurl/8.22.0 SecureTransport"
            : "libcurl/8.22.0 OpenSSL";
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
            "header_json" => WriteOutTemplateRenderer.MarkHeaderBytes(WriteOutJson.FormatHeaders(report.ResponseHeaders, HeaderValueWhitespace)),
            _ => VariableFormatters.TryGetValue(name, out Func<TransferWriteOutVariables, WriteOutValue>? format) ? format(this).Text : report.ExtensionWriteOutValues.GetValueOrDefault(name),
        };
        return text is not null;
    }

    /// <inheritdoc/>
    /// <remarks>The value is trimmed of spaces and tabs at both ends, as curl prints it.</remarks>
    public string? FindFirstHeaderValue(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        string headerName = HeaderBytesOf(name);
        foreach (KeyValuePair<string, string> header in report.ResponseHeaders)
        {
            if (string.Equals(header.Key, headerName, StringComparison.OrdinalIgnoreCase))
            {
                return WriteOutTemplateRenderer.MarkHeaderBytes(header.Value.Trim(HeaderValueWhitespace));
            }
        }

        return null;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The earlier responses of a <c>-L</c> chain come first, then the last response; each
    /// value is trimmed of spaces and tabs at both ends.
    /// </remarks>
    public IReadOnlyList<string> FindAllHeaderValues(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        string headerName = HeaderBytesOf(name);
        return report.EarlierResponseHeaders
            .Concat(report.ResponseHeaders)
            .Where(header => string.Equals(header.Key, headerName, StringComparison.OrdinalIgnoreCase))
            .Select(header => WriteOutTemplateRenderer.MarkHeaderBytes(header.Value.Trim(HeaderValueWhitespace)))
            .ToArray();
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
    /// The info the report-only constructor starts from: <see cref="WriteOutTransferInfo.FromReport"/>
    /// with <paramref name="scheme"/> and the defaults the caller's properties replace.
    /// </summary>
    private static TransferInfo ReportOnlyInfoOf(TransferResult result, string requestUrl, string? scheme, TimeProvider timeProvider) =>
        WriteOutTransferInfo.FromReport(result, requestUrl, timeProvider) with
        {
            Scheme = scheme,
            Referer = null,
            TransferId = 0,
            ConnectionId = -1,
            SslVerifyResult = 0,
            ProxySslVerifyResult = 0,
        };

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
            formatters["urle." + name] = variables => WriteOutValue.FromText(FindUrlPart(variables.info.EffectiveUrl, read));
        }

        return formatters;
    }

    /// <summary>
    /// One part of <paramref name="text"/> as curl's <c>urlpart</c> prints it: parsed
    /// without path-as-is, and <see langword="null"/>, which prints nothing, when the URL
    /// does not parse or has no such part. Header bytes an effective URL carries marked
    /// (<see cref="HeaderByteText"/>) are parsed as their Latin-1 characters.
    /// </summary>
    private static string? FindUrlPart(string text, Func<CurlUrl, string?> read)
    {
        return CurlUrl.TryParse(HeaderByteText.Unmark(text), pathAsIs: false, out CurlUrl? parsed) ? read(parsed) : null;
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

    /// <summary>Prints a time as seconds with six decimals, in whole microseconds, as curl's <c>-w</c> prints a time.</summary>
    private static WriteOutValue FormatSeconds(TimeSpan time)
    {
        long microseconds = time.Ticks / TimeSpan.TicksPerMicrosecond;
        return WriteOutValue.FromFormattedNumber(string.Create(
            CultureInfo.InvariantCulture,
            $"{microseconds / MicrosecondsPerSecond}.{microseconds % MicrosecondsPerSecond:D6}"));
    }

    /// <summary>The <c>%{http_version}</c> text of a <c>CURL_HTTP_VERSION_*</c> value; <c>0</c> for none.</summary>
    private static string FormatHttpVersion(long version) => HttpVersionTexts.GetValueOrDefault(version, "0");

    /// <summary>
    /// A name from the template as the header bytes it matches: its UTF-8 bytes, one per
    /// character, as the response head holds a header name.
    /// </summary>
    private static string HeaderBytesOf(string name) => Encoding.Latin1.GetString(Encoding.UTF8.GetBytes(name));

    /// <summary>A content type, which is header text, with its bytes marked by <see cref="WriteOutTemplateRenderer.MarkHeaderBytes"/>.</summary>
    private static string? MarkHeaderBytes(string? headerText) => headerText is null ? null : WriteOutTemplateRenderer.MarkHeaderBytes(headerText);

    private static string FormatNumber(long number)
    {
        return number.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The <c>%{remote_port}</c> of <paramref name="info"/>: its primary port; <c>-1</c> when no
    /// connection was made, which <see cref="TransferInfo.PrimaryPort"/> answers as <c>0</c> or, after a failed transfer, <c>-1</c> (BL-3195).
    /// </summary>
    private static long FindRemotePort(TransferInfo info)
    {
        return info.PrimaryPort <= 0 ? UnknownPort : info.PrimaryPort;
    }

    /// <summary>
    /// The <c>%{local_port}</c> of <paramref name="info"/>: its local port when it has a local
    /// address; <c>0</c> when a connection was made but has no local end to report, as curl 8.21.0
    /// prints for its unconnected TFTP socket; <c>-1</c> when no connection was made (measured,
    /// BL-515 Notes; ADR-0119).
    /// </summary>
    private static long FindLocalPort(TransferInfo info)
    {
        if (info.LocalIp.Length != 0)
        {
            return info.LocalPort;
        }

        return info.PrimaryPort <= 0 ? UnknownPort : UnreportedLocalPort;
    }
}
