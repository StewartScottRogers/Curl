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
/// <c>exitcode</c>, <c>errormsg</c>, <c>url</c>, <c>urlnum</c> and <c>scheme</c>. Any
/// other name is reported unknown.
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
public sealed class TransferWriteOutVariables(
    TransferResult result,
    string url,
    int urlNumber,
    string requestUrl,
    string? scheme) : IWriteOutVariableSource
{
    private const int UnknownPort = -1;

    private static readonly Dictionary<string, Func<TransferWriteOutVariables, string>> VariableFormatters = new(StringComparer.Ordinal)
    {
        ["response_code"] = variables => FormatStatusCode(variables.report.ResponseCode),
        ["http_code"] = variables => FormatStatusCode(variables.report.ResponseCode),
        ["http_connect"] = variables => FormatStatusCode(variables.report.ProxyConnectResponseCode),
        ["http_version"] = variables => FormatHttpVersion(variables.report.HttpVersion),
        ["method"] = variables => variables.report.Method ?? "GET",
        ["content_type"] = variables => variables.report.ContentType ?? string.Empty,
        ["redirect_url"] = variables => variables.report.RedirectUrl ?? string.Empty,
        ["url_effective"] = variables => variables.report.EffectiveUrl ?? variables.requestUrl,
        ["num_redirects"] = variables => FormatNumber(variables.report.RedirectCount),
        ["size_header"] = variables => FormatNumber(variables.report.HeaderSize),
        ["size_request"] = variables => FormatNumber(variables.report.RequestSize),
        ["size_download"] = variables => FormatNumber(variables.DownloadSize),
        ["size_upload"] = variables => FormatNumber(variables.report.UploadSize),
        ["num_connects"] = variables => FormatNumber(variables.report.ConnectionCount),
        ["num_headers"] = variables => FormatNumber(variables.report.ResponseHeaders.Count),
        ["local_ip"] = variables => FormatAddress(variables.report.LocalEndPoint),
        ["local_port"] = variables => FormatPort(variables.report.LocalEndPoint),
        ["remote_ip"] = variables => FormatAddress(variables.report.RemoteEndPoint),
        ["remote_port"] = variables => FormatPort(variables.report.RemoteEndPoint),
        ["exitcode"] = variables => FormatNumber((int)variables.result.ExitCode),
        ["errormsg"] = variables => variables.result.ErrorMessage ?? string.Empty,
        ["url"] = variables => variables.url,
        ["urlnum"] = variables => FormatNumber(variables.urlNumber),
        ["scheme"] = variables => variables.scheme ?? string.Empty,
    };

    private static readonly char[] HeaderValueWhitespace = [' ', '\t'];

    private readonly TransferResult result = result ?? throw new ArgumentNullException(nameof(result));
    private readonly TransferReport report = result.Report ?? new TransferReport();
    private readonly string url = url ?? throw new ArgumentNullException(nameof(url));
    private readonly string requestUrl = requestUrl ?? throw new ArgumentNullException(nameof(requestUrl));
    private readonly int urlNumber = urlNumber;
    private readonly string? scheme = scheme;

    private long DownloadSize => result.Report?.DownloadSize ?? result.BytesTransferred;

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
