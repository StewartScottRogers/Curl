using Curl.Api.Info;

namespace Curl.Mcp;

/// <summary>
/// The <see cref="TransferInfo" /> values the <c>transfer</c> tool returns (ADR-0548, ADR-0593),
/// named for a reader rather than as <c>CURLINFO_*</c> constants; times are in seconds.
/// </summary>
/// <param name="TotalTimeSeconds"><c>CURLINFO_TOTAL_TIME</c>: the whole transfer's time.</param>
/// <param name="NameLookupTimeSeconds"><c>CURLINFO_NAMELOOKUP_TIME</c>: the time to the end of name resolution.</param>
/// <param name="ConnectTimeSeconds"><c>CURLINFO_CONNECT_TIME</c>: the time to the end of the connect.</param>
/// <param name="AppConnectTimeSeconds"><c>CURLINFO_APPCONNECT_TIME</c>: the time to the end of the TLS handshake.</param>
/// <param name="StartTransferTimeSeconds"><c>CURLINFO_STARTTRANSFER_TIME</c>: the time to the first response byte.</param>
/// <param name="EffectiveUrl"><c>CURLINFO_EFFECTIVE_URL</c>: the last URL used.</param>
/// <param name="EffectiveMethod"><c>CURLINFO_EFFECTIVE_METHOD</c>: the last request method used.</param>
/// <param name="RedirectCount"><c>CURLINFO_REDIRECT_COUNT</c>: how many redirects were followed.</param>
/// <param name="ContentType"><c>CURLINFO_CONTENT_TYPE</c>: the received <c>Content-Type</c>; <see langword="null" /> when none.</param>
/// <param name="SizeDownload"><c>CURLINFO_SIZE_DOWNLOAD</c>: the body bytes downloaded, before the cap.</param>
/// <param name="SizeUpload"><c>CURLINFO_SIZE_UPLOAD</c>: the body bytes uploaded.</param>
/// <param name="HeaderSize"><c>CURLINFO_HEADER_SIZE</c>: the bytes of all headers received.</param>
/// <param name="RequestSize"><c>CURLINFO_REQUEST_SIZE</c>: the bytes of all requests sent.</param>
/// <param name="SpeedDownload"><c>CURLINFO_SPEED_DOWNLOAD</c>: the average download speed in bytes per second.</param>
/// <param name="SpeedUpload"><c>CURLINFO_SPEED_UPLOAD</c>: the average upload speed in bytes per second.</param>
/// <param name="HttpVersion"><c>CURLINFO_HTTP_VERSION</c> as <c>1.0</c>, <c>1.1</c>, <c>2</c> or <c>3</c>; <see langword="null" /> when no HTTP connection was made.</param>
/// <param name="PrimaryIp"><c>CURLINFO_PRIMARY_IP</c>: the peer's IP address.</param>
/// <param name="PrimaryPort"><c>CURLINFO_PRIMARY_PORT</c>: the peer's port.</param>
public sealed record McpTransferInfo(
    double TotalTimeSeconds,
    double NameLookupTimeSeconds,
    double ConnectTimeSeconds,
    double AppConnectTimeSeconds,
    double StartTransferTimeSeconds,
    string EffectiveUrl,
    string EffectiveMethod,
    long RedirectCount,
    string? ContentType,
    long SizeDownload,
    long SizeUpload,
    long HeaderSize,
    long RequestSize,
    long SpeedDownload,
    long SpeedUpload,
    string? HttpVersion,
    string PrimaryIp,
    long PrimaryPort)
{
    /// <summary>The values <paramref name="info" /> holds.</summary>
    /// <param name="info">What the transfer reported.</param>
    /// <returns>The named values.</returns>
    public static McpTransferInfo From(TransferInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        return new McpTransferInfo(
            info.TotalTime.TotalSeconds,
            info.NameLookupTime.TotalSeconds,
            info.ConnectTime.TotalSeconds,
            info.AppConnectTime.TotalSeconds,
            info.StartTransferTime.TotalSeconds,
            info.EffectiveUrl,
            info.EffectiveMethod,
            info.RedirectCount,
            info.ContentType,
            info.SizeDownload,
            info.SizeUpload,
            info.HeaderSize,
            info.RequestSize,
            info.SpeedDownload,
            info.SpeedUpload,
            HttpVersionText(info.HttpVersion),
            info.PrimaryIp,
            info.PrimaryPort);
    }

    /// <summary>
    /// The <c>CURL_HTTP_VERSION_*</c> value <c>CURLINFO_HTTP_VERSION</c> answers, as the version's
    /// number: 1 is 1.0, 2 is 1.1, 3 is 2 and 30 is 3; anything else, 0 included, is no version.
    /// </summary>
    private static string? HttpVersionText(long version) => HttpVersionNumbers.GetValueOrDefault(version);

    private static readonly Dictionary<long, string> HttpVersionNumbers = new()
    {
        [1] = "1.0",
        [2] = "1.1",
        [3] = "2",
        [30] = "3",
    };
}
