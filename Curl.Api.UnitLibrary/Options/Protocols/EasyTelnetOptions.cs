namespace Curl.Api.Options.Protocols;

/// <summary>The TELNET options of one easy handle, each defaulting to libcurl 8.22.0's default.</summary>
public sealed record EasyTelnetOptions
{
    /// <summary>
    /// <c>CURLOPT_TELNETOPTIONS</c>: <c>OPTION=VALUE</c> entries (<c>TTYPE</c>, <c>XDISPLOC</c>,
    /// <c>NEW_ENV</c>) negotiated with the server. Default empty (libcurl's <see langword="null"/> list).
    /// </summary>
    public IReadOnlyList<string> TelnetOptions { get; init; } = [];
}
