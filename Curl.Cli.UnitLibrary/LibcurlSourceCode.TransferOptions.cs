using System.Collections.Frozen;
using Curl.Protocol.Abstractions;

namespace Curl.Cli;

/// <summary>
/// The <c>--libcurl</c> lines for the transfer, redirect, speed, time-condition and socket options BL-1106
/// measured with curl 8.21.0 (Schannel) on 2026-10-02: each method writes its options' lines in the order
/// curl writes them, and <see cref="LibcurlSourceCode" />'s other parts call each at its place in that order.
/// </summary>
public static partial class LibcurlSourceCode
{
    private static readonly FrozenDictionary<RequestedHttpVersion, string> HttpVersionNames = new Dictionary<RequestedHttpVersion, string>
    {
        [RequestedHttpVersion.Http10] = "(long)CURL_HTTP_VERSION_1_0",
        [RequestedHttpVersion.Http11] = "(long)CURL_HTTP_VERSION_1_1",
    }.ToFrozenDictionary();

    /// <summary>The <c>--request-target</c>, <c>-l</c> and <c>-a</c> lines, after <c>CURLOPT_FAILONERROR</c> and before the netrc lines.</summary>
    private static List<string> TransferModeLines(CommandLineOptions options)
    {
        List<string> lines = [];
        AddStringIf(lines, "CURLOPT_REQUEST_TARGET", options.RequestTarget);
        AddIf(lines, options.ListOnly, SetoptOn("CURLOPT_DIRLISTONLY"));
        AddIf(lines, options.Append, SetoptOn("CURLOPT_APPEND"));
        return lines;
    }

    /// <summary>
    /// The redirect lines of an HTTP transfer: <c>CURLOPT_FOLLOWLOCATION</c> is <c>2L</c> for <c>--follow</c>
    /// and <c>1L</c> for <c>-L</c>, whichever came last.
    /// </summary>
    private static string FollowLocationLine(CommandLineOptions options) =>
        Setopt("CURLOPT_FOLLOWLOCATION", options.FollowRedirectsPerSpec ? "2L" : "1L");

    /// <summary>
    /// The HTTP lines after <c>CURLOPT_MAXREDIRS</c>: the HTTP version curl's Schannel build accepts (1.0 or
    /// 1.1) and the <c>--post30x</c> bits.
    /// </summary>
    private static List<string> HttpVersionAndPostRedirectLines(CommandLineOptions options)
    {
        List<string> lines = [];
        string? httpVersion = options.HttpVersion is { } version ? HttpVersionNames.GetValueOrDefault(version) : null;
        AddIf(lines, httpVersion is not null, () => Setopt("CURLOPT_HTTP_VERSION", httpVersion!));
        uint postRedirect = Bits(options.KeepPostAfter301, 1) | Bits(options.KeepPostAfter302, 2) | Bits(options.KeepPostAfter303, 4);
        AddIf(lines, postRedirect != 0, () => Setopt("CURLOPT_POSTREDIR", $"{postRedirect}L"));
        return lines;
    }

    /// <summary>The HTTP lines after <c>CURLOPT_ACCEPT_ENCODING</c> and before the cookie lines.</summary>
    private static List<string> HttpTransferLines(CommandLineOptions options)
    {
        List<string> lines = [];
        AddIf(lines, options.TransferEncoding, SetoptOn("CURLOPT_TRANSFER_ENCODING"));
        AddIf(lines, options.AllowHttp09Reply, SetoptOn("CURLOPT_HTTP09_ALLOWED"));
        AddStringIf(lines, "CURLOPT_ALTSVC", options.AltSvcFile);
        AddStringIf(lines, "CURLOPT_HSTS", options.HstsFile);
        AddIf(lines, options.Expect100Timeout > TimeSpan.Zero, () => SetoptMilliseconds("CURLOPT_EXPECT_100_TIMEOUT_MS", options.Expect100Timeout!.Value));
        return lines;
    }

    /// <summary>
    /// The <c>-Y</c>/<c>-y</c> and <c>-C</c> lines, after the scheme's lines: <c>-Y</c> alone also writes a
    /// 30-second time and <c>-y</c> alone a limit of 1, and a zero limit, time or offset is not written.
    /// </summary>
    private static List<string> SpeedAndResumeLines(CommandLineOptions options)
    {
        long limit = options.SpeedLimit ?? (options.SpeedTimeSeconds is null ? 0 : 1);
        long time = options.SpeedTimeSeconds ?? (options.SpeedLimit is null ? 0 : 30);
        List<string> lines = [];
        AddIf(lines, limit > 0, () => Setopt("CURLOPT_LOW_SPEED_LIMIT", $"{limit}L"));
        AddIf(lines, time > 0, () => Setopt("CURLOPT_LOW_SPEED_TIME", $"{time}L"));
        AddIf(lines, options.ResumeFrom > 0, () => Setopt("CURLOPT_RESUME_FROM_LARGE", $"(curl_off_t){options.ResumeFrom}"));
        return lines;
    }

    /// <summary>
    /// The lines after the TLS ones: <c>--path-as-is</c>, the file time <c>-I</c> or <c>-R</c> asks for,
    /// <c>--crlf</c> and the <c>-z</c> condition, its time in seconds since 1970.
    /// </summary>
    private static List<string> PathFileTimeAndConditionLines(CommandLineOptions options)
    {
        List<string> lines = [];
        AddIf(lines, options.PathAsIs, SetoptOn("CURLOPT_PATH_AS_IS"));
        AddIf(lines, options.NoBody || options.RemoteTime, SetoptOn("CURLOPT_FILETIME"));
        AddIf(lines, options.ConvertLineEndings, SetoptOn("CURLOPT_CRLF"));
        if (options.TimeCondition is { } condition)
        {
            string kind = condition.Kind == TimeConditionKind.IfModifiedSince ? "CURL_TIMECOND_IFMODSINCE" : "CURL_TIMECOND_IFUNMODSINCE";
            lines.Add(Setopt("CURLOPT_TIMECONDITION", $"(long){kind}"));
            lines.Add(Setopt("CURLOPT_TIMEVALUE_LARGE", $"(curl_off_t){condition.Value.ToUnixTimeSeconds()}"));
        }

        return lines;
    }

    /// <summary>The <c>-X</c>, <c>--interface</c>, <c>--connect-timeout</c>, <c>--doh-url</c>, <c>--max-filesize</c>, <c>-4</c> and <c>-6</c> lines, in that order.</summary>
    private static List<string> RequestAndConnectionLines(CommandLineOptions options)
    {
        List<string> lines = [];
        AddStringIf(lines, "CURLOPT_CUSTOMREQUEST", options.RequestMethod);
        AddStringIf(lines, "CURLOPT_INTERFACE", options.Interface?.Value);
        AddIf(lines, options.ConnectTimeout > TimeSpan.Zero, () => SetoptMilliseconds("CURLOPT_CONNECTTIMEOUT_MS", options.ConnectTimeout!.Value));
        AddStringIf(lines, "CURLOPT_DOH_URL", options.DohUrl);
        AddIf(lines, options.MaxFileSize > 0, () => Setopt("CURLOPT_MAXFILESIZE_LARGE", $"(curl_off_t){options.MaxFileSize}"));
        AddIf(lines, options.IpAddressFamily != IpAddressFamilyChoice.Either, () => Setopt("CURLOPT_IPRESOLVE", $"{(int)options.IpAddressFamily}L"));
        return lines;
    }

    /// <summary>
    /// The lines after the SOCKS and <c>--service-name</c> ones: <c>--ignore-content-length</c>, the
    /// <c>--local-port</c> range as its first port and its count, and the keepalive lines, which
    /// <c>--no-keepalive</c> drops, <c>--keepalive-time</c> giving both the idle time and the interval.
    /// </summary>
    private static List<string> SocketLines(CommandLineOptions options)
    {
        List<string> lines = [];
        AddIf(lines, options.IgnoreContentLength, SetoptOn("CURLOPT_IGNORE_CONTENT_LENGTH"));
        if (options.LocalPorts is { } ports)
        {
            lines.Add(Setopt("CURLOPT_LOCALPORT", $"{ports.First}L"));
            lines.Add(Setopt("CURLOPT_LOCALPORTRANGE", $"{ports.Count}L"));
        }

        if (options.TcpKeepAlive)
        {
            lines.Add(SetoptOn("CURLOPT_TCP_KEEPALIVE"));
            AddIf(lines, options.TcpKeepAliveSeconds > 0, () => Setopt("CURLOPT_TCP_KEEPIDLE", $"{options.TcpKeepAliveSeconds}L"));
            AddIf(lines, options.TcpKeepAliveSeconds > 0, () => Setopt("CURLOPT_TCP_KEEPINTVL", $"{options.TcpKeepAliveSeconds}L"));
            AddIf(lines, options.TcpKeepAliveProbeCount > 0, () => Setopt("CURLOPT_TCP_KEEPCNT", $"{options.TcpKeepAliveProbeCount}L"));
        }

        return lines;
    }

    /// <summary>The last lines of a transfer: the Unix socket, <c>--happy-eyeballs-timeout-ms</c> and <c>--disallow-username-in-url</c>.</summary>
    private static List<string> UnixSocketAndUrlLines(CommandLineOptions options)
    {
        List<string> lines = [];
        AddStringIf(lines, options.UnixSocketIsAbstract ? "CURLOPT_ABSTRACT_UNIX_SOCKET" : "CURLOPT_UNIX_SOCKET_PATH", options.UnixSocketPath);
        AddIf(lines, options.HappyEyeballsTimeout > TimeSpan.Zero, () => SetoptMilliseconds("CURLOPT_HAPPY_EYEBALLS_TIMEOUT_MS", options.HappyEyeballsTimeout!.Value));
        AddIf(lines, options.DisallowUsernameInUrl, SetoptOn("CURLOPT_DISALLOW_USERNAME_IN_URL"));
        return lines;
    }
}
