using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

internal static partial class TransferInfoFromReport
{
    /// <summary>The <c>CURLPROTO_*</c> bit of each scheme, as curl 8.22.0's <c>curl.h</c> numbers them.</summary>
    private static readonly Dictionary<string, long> CurlProtoBitsByScheme = new(StringComparer.Ordinal)
    {
        ["http"] = 1L << 0,
        ["https"] = 1L << 1,
        ["ftp"] = 1L << 2,
        ["ftps"] = 1L << 3,
        ["scp"] = 1L << 4,
        ["sftp"] = 1L << 5,
        ["telnet"] = 1L << 6,
        ["ldap"] = 1L << 7,
        ["ldaps"] = 1L << 8,
        ["dict"] = 1L << 9,
        ["file"] = 1L << 10,
        ["tftp"] = 1L << 11,
        ["imap"] = 1L << 12,
        ["imaps"] = 1L << 13,
        ["pop3"] = 1L << 14,
        ["pop3s"] = 1L << 15,
        ["smtp"] = 1L << 16,
        ["smtps"] = 1L << 17,
        ["rtsp"] = 1L << 18,
        ["gopher"] = 1L << 25,
        ["smb"] = 1L << 26,
        ["smbs"] = 1L << 27,
        ["mqtt"] = 1L << 28,
        ["gophers"] = 1L << 29,
        ["ws"] = 1L << 30,
        ["wss"] = 1L << 31,
    };

    /// <summary>
    /// The scheme <c>%{scheme}</c> prints for a transfer of <paramref name="requestUrl" />: the URL's, in lower case, or
    /// <see langword="null" /> (printed as nothing) when the URL cannot be parsed or no handler serves its scheme, as
    /// curl 8.21.0 prints it for <c>dict://exa mple.com/</c> and <c>xyz://a/b</c>.
    /// </summary>
    /// <param name="requestUrl">The URL, with any <c>-G</c> / <c>--url-query</c> query.</param>
    /// <param name="result">The transfer's result.</param>
    /// <returns>The scheme, or <see langword="null" />.</returns>
    internal static string? RequestSchemeOf(string requestUrl, TransferResult result) =>
        result.ExitCode != CurlExitCode.UnsupportedProtocol ? SchemeOf(requestUrl) : null;

    private static string? SchemeOf(string effectiveUrl) =>
        CurlUrl.TryParse(effectiveUrl, pathAsIs: false, out CurlUrl? url) ? url.Scheme : null;

    private static long ProtocolOf(string effectiveUrl) =>
        SchemeOf(effectiveUrl) is { } scheme ? CurlProtoBitsByScheme.GetValueOrDefault(scheme) : 0;
}
