using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.WebTransport.Session;

/// <summary>
/// What one <c>webtransport://host[:port]/path[?query]</c> transfer does (ADR-1089): the CONNECT's
/// <c>:authority</c> and <c>:path</c>, how the data travels (the query field <c>wt=bidi</c>, the default,
/// <c>wt=uni</c> or <c>wt=datagram</c>, which is not sent to the server) and the data itself.
/// </summary>
/// <param name="Host">The host to dial.</param>
/// <param name="Port">The UDP port to dial.</param>
/// <param name="Authority">The <c>:authority</c> pseudo-header.</param>
/// <param name="Path">The <c>:path</c> pseudo-header.</param>
/// <param name="Mode">How the data travels.</param>
/// <param name="Data">The data to send, empty without <c>-d</c>.</param>
internal sealed record WebTransportRequest(string Host, int Port, string Authority, string Path, WebTransportMode Mode, ReadOnlyMemory<byte> Data)
{
    /// <summary>The port a URL without one dials, HTTPS's.</summary>
    public const int DefaultPort = 443;

    private const string ModeField = "wt=";

    /// <summary>Reads the request from a URL and the transfer's data.</summary>
    /// <param name="url">The <c>webtransport://</c> URL.</param>
    /// <param name="data">The <c>-d</c> data, or <see langword="null" />.</param>
    /// <returns>The request.</returns>
    /// <exception cref="WebTransportException">The <c>wt</c> field names no mode (exit 3).</exception>
    public static WebTransportRequest Of(CurlUrl url, ReadOnlyMemory<byte>? data)
    {
        string[] fields = url.Query is { Length: > 0 } query ? query.Split('&') : [];
        string? modeField = fields.LastOrDefault(IsModeField);
        return new WebTransportRequest(url.IdnHost, PortOf(url), AuthorityOf(url), PathOf(url, fields), ModeOf(modeField), data ?? ReadOnlyMemory<byte>.Empty);
    }

    private static int PortOf(CurlUrl url) => url.Port >= 0 ? url.Port : DefaultPort;

    private static string AuthorityOf(CurlUrl url) => url.Port >= 0 ? $"{url.IdnHost}:{url.Port}" : url.IdnHost;

    private static string PathOf(CurlUrl url, string[] fields)
    {
        string[] sent = [.. fields.Where(field => !IsModeField(field))];
        return sent.Length == 0 ? url.RequestPath : $"{url.RequestPath}?{string.Join('&', sent)}";
    }

    private static bool IsModeField(string field) => field.StartsWith(ModeField, StringComparison.Ordinal);

    private static WebTransportMode ModeOf(string? field) => field?[ModeField.Length..] switch
    {
        null or "bidi" => WebTransportMode.Bidirectional,
        "uni" => WebTransportMode.Unidirectional,
        "datagram" => WebTransportMode.Datagram,
        _ => throw new WebTransportException(CurlExitCode.UrlMalformat, $"Unknown WebTransport mode \"{field}\": use wt=bidi, wt=uni or wt=datagram"),
    };
}
