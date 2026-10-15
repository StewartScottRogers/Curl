using System.Text;

using Curl.Api.Options.Http;
using Curl.Cli;
using Curl.Networking;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Engine;

/// <summary>
/// Maps the HTTP request options of an option group onto the <see cref="HttpRequestOptions" /> an HTTP handler
/// reads. From <see cref="EasyHttpOptions" />: <c>--request-target</c>, <c>-H</c> with the headers <c>--json</c>
/// adds, <c>-A</c>, <c>-e</c>, the <c>-d</c> family body, <c>-L</c>, <c>--max-redirs</c>, the HTTP version,
/// <c>--compressed</c>, <c>--tr-encoding</c>, <c>--raw</c>, <c>--ignore-content-length</c>, <c>--http0.9</c> and
/// <c>--expect100-timeout</c>. From the command line, until their groups' tasks retarget them: <c>-X</c>,
/// <c>-f</c>, <c>--fail-with-body</c>, the authentication options, the proxy headers and tunnel,
/// <c>--unix-socket</c> and <c>--happy-eyeballs-timeout-ms</c>; with the <c>-F</c> body the caller built.
/// </summary>
/// <remarks>
/// Measured with curl 8.21.0 (mingw, Schannel) against a loopback recorder on 2026-09-26
/// (BL-231 Notes). <c>--json</c>'s <c>Content-Type</c> and <c>Accept</c> lines and the <c>-G</c> rule that
/// sends no <c>-d</c> body are applied where <see cref="EasyHttpOptions" /> is built
/// (<c>EasyOptionsFromCommandLine.HttpOf</c>); a <see cref="EasyHttpOptions.PostFields" /> body is sent as
/// <c>application/x-www-form-urlencoded</c>.
/// </remarks>
internal static class HttpRequestOptionsMapping
{
    /// <summary>The <c>Content-Type</c> curl sends a <c>-d</c> family body with.</summary>
    internal const string FormUrlEncoded = "application/x-www-form-urlencoded";

    // The longest delay a .NET timer takes, about 49.7 days; a longer --happy-eyeballs-timeout-ms is held to it.
    private const double LongestTimerDelayMilliseconds = uint.MaxValue - 1;

    /// <summary>
    /// Copies the HTTP request options from <paramref name="http" /> and <paramref name="options" />.
    /// </summary>
    /// <param name="options">The parsed command line, for the options no <see cref="EasyHttpOptions" /> member carries.</param>
    /// <param name="http">The option group's HTTP options.</param>
    /// <param name="formBody">
    /// The <c>-F</c> / <c>--form-string</c> body built for this transfer, or <see langword="null" />
    /// without <c>-F</c>; the parser refuses <c>-F</c> with a <c>-d</c> family body, so at most one is given.
    /// </param>
    /// <param name="proxy">
    /// The proxy chosen for this transfer (<see cref="TransferProxySelection" />), or
    /// <see langword="null" /> to connect directly.
    /// </param>
    /// <param name="commandLineTextEncoding">
    /// The platform curl's argument encoding the <c>-H</c>, <c>--proxy-header</c>, <c>-A</c> and
    /// <c>-e</c> text is sent in (ADR-0067), or <see langword="null" /> for Latin-1.
    /// </param>
    /// <param name="ifNoneMatchHeaders">
    /// The <c>If-None-Match</c> lines <c>--etag-compare</c> added to the option group so far, one per
    /// transfer, as curl 8.21.0 adds them to its header list (BL-619 Notes); <see langword="null" /> for none.
    /// </param>
    /// <returns>
    /// The options: <see cref="EasyHttpOptions.RequestTarget" />, <see cref="EasyHttpOptions.UserAgent" />,
    /// <see cref="EasyHttpOptions.Referer" /> and <see cref="EasyHttpOptions.AutoReferer" /> verbatim;
    /// <see cref="EasyHttpOptions.HttpHeaders" /> followed by <paramref name="ifNoneMatchHeaders" />;
    /// <paramref name="formBody" /> when given, otherwise <see cref="EasyHttpOptions.PostFields" /> as a
    /// <see cref="BytesBody" />; <see cref="EasyHttpOptions.FollowLocation" /> other than
    /// <see cref="FollowLocation.None" /> as <see cref="HttpRequestOptions.FollowRedirects" />;
    /// <see cref="EasyHttpOptions.MaxRedirects" /> verbatim; <see cref="EasyHttpOptions.HttpVersion" /> as
    /// <see cref="HttpRequestOptions.Version" />, as <see cref="HttpVersionMapping.ToHttpVersionPreference" />
    /// maps it; a set <see cref="EasyHttpOptions.AcceptEncoding" /> as <see cref="HttpRequestOptions.Compressed" />;
    /// both decodings off (<c>--raw</c>) as <see cref="HttpRequestOptions.Raw" />;
    /// <see cref="EasyHttpOptions.TransferEncoding" />, <see cref="EasyHttpOptions.IgnoreContentLength" /> and
    /// <see cref="EasyHttpOptions.Http09Allowed" /> verbatim; and <see cref="EasyHttpOptions.Expect100Timeout" /> as
    /// <see cref="HttpRequestOptions.ContinueWait" />. And from <paramref name="options" />:
    /// <see cref="CommandLineOptions.RequestMethod" /> and <see cref="CommandLineOptions.ProxyHeaders" /> verbatim;
    /// <see cref="CommandLineOptions.FailMode" /> as <see cref="HttpRequestOptions.Fail" />;
    /// <see cref="CommandLineOptions.AuthSchemes" />, <see cref="CommandLineOptions.BearerToken" /> and
    /// <see cref="CommandLineOptions.AwsSigV4" /> verbatim; the <c>--httpsig-*</c> options as
    /// <see cref="HttpRequestOptions.HttpSignature" /> (<see cref="HttpSignatureSetup.Of" />), which leave
    /// <see cref="HttpRequestOptions.AuthSchemes" /> empty so a redirect hop that drops the signature sends no
    /// Basic from its URL's credentials (upstream test5025, BL-2201); <paramref name="proxy" /> as
    /// <see cref="HttpRequestOptions.ForwardProxy" /> with <see cref="CommandLineOptions.ProxyTunnel" /> as
    /// <see cref="HttpRequestOptions.ProxyTunnel" />; whether <see cref="CommandLineOptions.UnixSocketPath" /> is
    /// set as <see cref="HttpRequestOptions.OverUnixSocket" />; <paramref name="commandLineTextEncoding" /> as
    /// <see cref="HttpRequestOptions.CommandLineTextEncoding" />; and
    /// <see cref="CommandLineOptions.HappyEyeballsTimeout" /> as <see cref="HttpRequestOptions.HappyEyeballsTimeout" /> (<see cref="HappyEyeballsTimeoutOf" />).
    /// </returns>
    internal static HttpRequestOptions Of(
        CommandLineOptions options,
        EasyHttpOptions http,
        HttpRequestBody? formBody = null,
        ProxyEndpoint? proxy = null,
        Encoding? commandLineTextEncoding = null,
        IReadOnlyList<string>? ifNoneMatchHeaders = null) =>
        new()
        {
            CustomMethod = options.RequestMethod,
            RequestTarget = http.RequestTarget,
            Headers = [.. http.HttpHeaders, .. ifNoneMatchHeaders ?? []],
            ProxyHeaders = options.ProxyHeaders,
            CommandLineTextEncoding = commandLineTextEncoding ?? Encoding.Latin1,
            UserAgent = http.UserAgent,
            Referer = http.Referer,
            AutoReferer = http.AutoReferer,
            Body = formBody ?? PostFieldsBodyOf(http),
            Fail = options.FailMode,
            FollowRedirects = http.FollowLocation != FollowLocation.None,
            MaxRedirects = (int)http.MaxRedirects,
            AuthSchemes = HttpSignatureSetup.Of(options).Settings is null ? options.AuthSchemes : HttpAuthSchemes.None,
            BearerToken = options.BearerToken,
            AwsSigV4 = options.AwsSigV4,
            HttpSignature = HttpSignatureSetup.Of(options).Settings,
            Version = HttpVersionMapping.ToHttpVersionPreference(http.HttpVersion),
            Compressed = http.AcceptEncoding is not null,
            TransferEncoding = http.TransferEncoding,
            Raw = !http.HttpContentDecoding && !http.HttpTransferDecoding,
            IgnoreContentLength = http.IgnoreContentLength,
            AllowHttp09Reply = http.Http09Allowed,
            ForwardProxy = proxy,
            ProxyTunnel = options.ProxyTunnel,
            OverUnixSocket = options.UnixSocketPath is not null,
            ContinueWait = http.Expect100Timeout,
            HappyEyeballsTimeout = HappyEyeballsTimeoutOf(options),
        };

    /// <summary>
    /// How long a <c>--http3</c> transfer waits for the QUIC handshake before it also connects over
    /// TCP: the <c>--happy-eyeballs-timeout-ms</c> value, held to the longest delay a .NET timer takes
    /// (about 49.7 days) as <see cref="TcpConnector.HappyEyeballsTimeout" /> is, or curl's 200 ms
    /// (<see cref="TcpConnector.DefaultHappyEyeballsTimeout" />) when it was not given (BL-889).
    /// </summary>
    /// <param name="options">The parsed command line.</param>
    /// <returns>How long the QUIC handshake runs alone.</returns>
    internal static TimeSpan HappyEyeballsTimeoutOf(CommandLineOptions options) =>
        options.HappyEyeballsTimeout is { } given
            ? TimeSpan.FromMilliseconds(Math.Min(given.TotalMilliseconds, LongestTimerDelayMilliseconds))
            : TcpConnector.DefaultHappyEyeballsTimeout;

    /// <summary>
    /// The <see cref="EasyHttpOptions.PostFields" /> body, or <see langword="null" /> when there is none, as
    /// under <c>-G</c>, which moved it into the query.
    /// </summary>
    private static BytesBody? PostFieldsBodyOf(EasyHttpOptions http) =>
        http.PostFields is { } data ? new BytesBody(data, FormUrlEncoded) : null;
}
