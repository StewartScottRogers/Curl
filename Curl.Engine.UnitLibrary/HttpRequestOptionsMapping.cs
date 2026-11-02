using System.Text;

using Curl.Api.Options.Authentication;
using Curl.Api.Options.Behaviour;
using Curl.Api.Options.Http;
using Curl.Networking;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Engine;

/// <summary>
/// Maps the HTTP request options of an option group onto the <see cref="HttpRequestOptions" /> an HTTP handler
/// reads. From <see cref="EasyHttpOptions" />: <c>--request-target</c>, <c>-H</c> with the headers <c>--json</c>
/// adds, <c>-A</c>, <c>-e</c>, the <c>-d</c> family body, <c>-L</c>, <c>--max-redirs</c>, the HTTP version,
/// <c>--compressed</c>, <c>--tr-encoding</c>, <c>--raw</c>, <c>--ignore-content-length</c>, <c>--http0.9</c> and
/// <c>--expect100-timeout</c>. From <see cref="EasyAuthenticationOptions" />: the HTTP authentication methods, the bearer
/// token, <c>--aws-sigv4</c> and the <c>--httpsig-*</c> options. From <see cref="EasyTransferOptions" />: <c>-X</c>; from
/// <see cref="EasyBehaviourOptions" />: <c>-f</c>. From the command line: <c>--fail-with-body</c>, which curl's tool
/// checks itself (ADR-0528), and, until their groups' tasks retarget them, the proxy headers and tunnel,
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
    /// Copies the HTTP request options from <paramref name="http" /> and <paramref name="settings" />.
    /// </summary>
    /// <param name="settings">The tool settings, for the options no <see cref="EasyHttpOptions" /> member carries.</param>
    /// <param name="http">The option group's HTTP options.</param>
    /// <param name="behaviour">The option group's behaviour options, whose <see cref="EasyBehaviourOptions.FailOnError" /> is <c>-f</c>.</param>
    /// <param name="transfer">The option group's transfer options, whose <see cref="EasyTransferOptions.CustomRequest" /> is <c>-X</c>.</param>
    /// <param name="authentication">The option group's authentication options: the HTTP methods, the bearer token, AWS SigV4 and the HTTP signature settings.</param>
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
    /// maps it; a set <see cref="EasyHttpOptions.AcceptEncoding" /> as <see cref="HttpRequestOptions.Compressed" />
    /// and, when not empty, exactly as given as <see cref="HttpRequestOptions.AcceptEncoding" /> (ADR-0777);
    /// <see cref="EasyHttpOptions.HttpContentDecoding" /> and <see cref="EasyHttpOptions.HttpTransferDecoding" /> off
    /// (each half of <c>--raw</c>) as <see cref="HttpRequestOptions.PassesContentCoding" /> and
    /// <see cref="HttpRequestOptions.PassesTransferCoding" />, each on its own (ADR-0780);
    /// <see cref="EasyHttpOptions.TransferEncoding" />, <see cref="EasyHttpOptions.IgnoreContentLength" /> and
    /// <see cref="EasyHttpOptions.Http09Allowed" /> verbatim; and <see cref="EasyHttpOptions.Expect100Timeout" /> as
    /// <see cref="HttpRequestOptions.ContinueWait" />. <see cref="EasyTransferOptions.CustomRequest" /> as
    /// <see cref="HttpRequestOptions.CustomMethod" />, and <see cref="EasyBehaviourOptions.FailOnError" /> as
    /// <see cref="HttpRequestOptions.Fail" /> <see cref="HttpFailMode.Fail" />, otherwise <c>--fail-with-body</c>'s
    /// <see cref="EngineTransferSettings.FailMode" />; <see cref="EasyBehaviourOptions.KeepSendingOnError" /> verbatim;
    /// a positive <see cref="EasyTransferOptions.MaxSendSpeed" /> as <see cref="HttpRequestOptions.SendBytesPerSecond" />;
    /// a positive <see cref="EasyTransferOptions.MaxReceiveSpeed" /> as <see cref="HttpRequestOptions.ReceiveBytesPerSecond" />. From <paramref name="authentication" />:
    /// <see cref="EasyAuthenticationOptions.HttpAuthentication" /> as <see cref="HttpRequestOptions.AuthSchemes" />
    /// (<see cref="AuthSchemesOf" />), <see cref="EasyAuthenticationOptions.XOAuth2Bearer" /> as
    /// <see cref="HttpRequestOptions.BearerToken" />, <see cref="EasyAuthenticationOptions.SaslAuthorizationIdentity" /> as
    /// <see cref="HttpRequestOptions.SaslAuthorizationIdentity" /> and <see cref="EasyAuthenticationOptions.AwsSigV4" /> verbatim;
    /// the <c>--httpsig-*</c> options as <see cref="HttpRequestOptions.HttpSignature" />
    /// (<see cref="HttpSignatureSetup.Of" />), which leave <see cref="HttpRequestOptions.AuthSchemes" /> empty so a
    /// redirect hop that drops the signature sends no Basic from its URL's credentials (upstream test5025,
    /// BL-2201). And from <paramref name="settings" />: <see cref="EngineTransferSettings.ProxyHeaders" /> verbatim;
    /// <paramref name="proxy" /> as
    /// <see cref="HttpRequestOptions.ForwardProxy" /> with <see cref="EngineTransferSettings.ProxyTunnel" /> as
    /// <see cref="HttpRequestOptions.ProxyTunnel" />; whether <see cref="EngineTransferSettings.OverUnixSocket" /> is
    /// set as <see cref="HttpRequestOptions.OverUnixSocket" />; <paramref name="commandLineTextEncoding" /> as
    /// <see cref="HttpRequestOptions.CommandLineTextEncoding" />; and
    /// <see cref="EngineTransferSettings.HappyEyeballsTimeout" /> as <see cref="HttpRequestOptions.HappyEyeballsTimeout" /> (<see cref="HappyEyeballsTimeoutOf" />).
    /// </returns>
    internal static HttpRequestOptions Of(
        EngineTransferSettings settings,
        EasyHttpOptions http,
        EasyBehaviourOptions behaviour,
        EasyTransferOptions transfer,
        EasyAuthenticationOptions authentication,
        HttpRequestBody? formBody = null,
        ProxyEndpoint? proxy = null,
        Encoding? commandLineTextEncoding = null,
        IReadOnlyList<string>? ifNoneMatchHeaders = null) =>
        new()
        {
            CustomMethod = transfer.CustomRequest,
            RequestTarget = http.RequestTarget,
            Headers = [.. http.HttpHeaders, .. ifNoneMatchHeaders ?? []],
            ProxyHeaders = settings.ProxyHeaders,
            CommandLineTextEncoding = commandLineTextEncoding ?? Encoding.Latin1,
            UserAgent = http.UserAgent,
            Referer = http.Referer,
            CookieStrings = http.Cookie,
            HasCookieFiles = http.CookieFiles.Count > 0,
            AutoReferer = http.AutoReferer,
            Body = RequestBodyOf(http, transfer, formBody),
            Fail = behaviour.FailOnError ? HttpFailMode.Fail : settings.FailMode,
            KeepSendingOnError = behaviour.KeepSendingOnError,
            FollowRedirects = http.FollowLocation != FollowLocation.None,
            MaxRedirects = (int)http.MaxRedirects,
            AuthSchemes = HttpSignatureSetup.Of(authentication).Settings is null ? AuthSchemesOf(authentication) : HttpAuthSchemes.None,
            BearerToken = authentication.XOAuth2Bearer,
            SaslAuthorizationIdentity = authentication.SaslAuthorizationIdentity,
            AwsSigV4 = authentication.AwsSigV4,
            HttpSignature = HttpSignatureSetup.Of(authentication).Settings,
            Version = HttpVersionMapping.ToHttpVersionPreference(http.HttpVersion),
            Compressed = http.AcceptEncoding is not null,
            AcceptEncoding = http.AcceptEncoding is { Length: > 0 } given ? given : HttpRequestOptions.DefaultAcceptEncoding,
            TransferEncoding = http.TransferEncoding,
            PassesContentCoding = !http.HttpContentDecoding,
            PassesTransferCoding = !http.HttpTransferDecoding,
            IgnoreContentLength = http.IgnoreContentLength,
            AllowHttp09Reply = http.Http09Allowed,
            Http200Aliases = http.Http200Aliases,
            ForwardProxy = proxy,
            ProxyTunnel = settings.ProxyTunnel,
            OverUnixSocket = settings.OverUnixSocket,
            ContinueWait = http.Expect100Timeout,
            HappyEyeballsTimeout = settings.HappyEyeballsTimeout,
            SendBytesPerSecond = transfer.MaxSendSpeed > 0 ? transfer.MaxSendSpeed : null,
            ReceiveBytesPerSecond = transfer.MaxReceiveSpeed > 0 ? transfer.MaxReceiveSpeed : null,
        };

    /// <summary>
    /// How long a <c>--http3</c> transfer waits for the QUIC handshake before it also connects over
    /// TCP: the <c>--happy-eyeballs-timeout-ms</c> value, held to the longest delay a .NET timer takes
    /// (about 49.7 days) as <see cref="TcpConnector.HappyEyeballsTimeout" /> is, or curl's 200 ms
    /// (<see cref="TcpConnector.DefaultHappyEyeballsTimeout" />) when it was not given (BL-889).
    /// </summary>
    /// <param name="happyEyeballsTimeout">The <c>--happy-eyeballs-timeout-ms</c> value, or <see langword="null" /> when it was not given.</param>
    /// <returns>How long the QUIC handshake runs alone.</returns>
    internal static TimeSpan HappyEyeballsTimeoutOf(TimeSpan? happyEyeballsTimeout) =>
        happyEyeballsTimeout is { } given
            ? TimeSpan.FromMilliseconds(Math.Min(given.TotalMilliseconds, LongestTimerDelayMilliseconds))
            : TcpConnector.DefaultHappyEyeballsTimeout;

    /// <summary>
    /// The schemes the HTTP handler may answer a challenge with, as the curl tool's parsed command line
    /// gives them (ADR-0516): <see cref="HttpAuthenticationMethods.Any" /> is every scheme the engine knows, each
    /// other <c>CURLAUTH_*</c> bit its scheme, and <see cref="HttpAuthSchemes.Bearer" /> only with
    /// <see cref="EasyAuthenticationOptions.XOAuth2Bearer" /> set. The <c>CURLAUTH_AWS_SIGV4</c> bit gives no scheme, as
    /// signing reads <see cref="EasyAuthenticationOptions.AwsSigV4" />; with no scheme left, Basic, libcurl's default, unless that
    /// bit is set: libcurl keeps <c>CURLAUTH_AWS_SIGV4</c> picked after <c>CURLOPT_AWS_SIGV4</c> is set back to <c>NULL</c> and then
    /// sends no <c>Authorization</c> at all (upstream test1978, BL-2748), so that gives <see cref="HttpAuthSchemes.None" />.
    /// </summary>
    /// <param name="authentication">The option group's authentication options.</param>
    /// <returns>The schemes.</returns>
    internal static HttpAuthSchemes AuthSchemesOf(EasyAuthenticationOptions authentication)
    {
        HttpAuthenticationMethods methods = authentication.HttpAuthentication;
        HttpAuthSchemes wanted = (methods & HttpAuthenticationMethods.Any) == HttpAuthenticationMethods.Any
            ? HttpAuthSchemes.Any | HttpAuthSchemes.Bearer
            : SchemesOf(methods);
        HttpAuthSchemes allowed = authentication.XOAuth2Bearer is null ? wanted & ~HttpAuthSchemes.Bearer : wanted;
        return allowed == HttpAuthSchemes.None && (methods & HttpAuthenticationMethods.AwsSigV4) == 0 ? HttpAuthSchemes.Basic : allowed;
    }

    /// <summary>
    /// The schemes the CONNECT tunnel and the HTTP handler may answer a proxy's challenge with, as the curl tool's
    /// the curl tool's parsed command line gives them (BL-2353): <see cref="HttpAuthenticationMethods.Any" /> is
    /// <see cref="HttpAuthSchemes.Any" />, each other <c>CURLAUTH_*</c> bit its scheme, and with no scheme left, Basic,
    /// libcurl's default.
    /// </summary>
    /// <param name="authentication">The option group's authentication options.</param>
    /// <returns>The schemes.</returns>
    internal static HttpAuthSchemes ProxyAuthSchemesOf(EasyAuthenticationOptions authentication)
    {
        HttpAuthenticationMethods methods = authentication.ProxyAuthentication;
        HttpAuthSchemes allowed = (methods & HttpAuthenticationMethods.Any) == HttpAuthenticationMethods.Any
            ? HttpAuthSchemes.Any
            : SchemesOf(methods);
        return allowed == HttpAuthSchemes.None ? HttpAuthSchemes.Basic : allowed;
    }

    /// <summary>Gives the scheme of each of Basic, Digest, NTLM, Negotiate and Bearer set in <paramref name="methods" />.</summary>
    private static HttpAuthSchemes SchemesOf(HttpAuthenticationMethods methods) =>
        SchemeOf(methods, HttpAuthenticationMethods.Basic, HttpAuthSchemes.Basic)
        | SchemeOf(methods, HttpAuthenticationMethods.Digest, HttpAuthSchemes.Digest)
        | SchemeOf(methods, HttpAuthenticationMethods.Ntlm, HttpAuthSchemes.Ntlm)
        | SchemeOf(methods, HttpAuthenticationMethods.Negotiate, HttpAuthSchemes.Negotiate)
        | SchemeOf(methods, HttpAuthenticationMethods.Bearer, HttpAuthSchemes.Bearer);

    private static HttpAuthSchemes SchemeOf(HttpAuthenticationMethods methods, HttpAuthenticationMethods method, HttpAuthSchemes scheme) =>
        (methods & method) != 0 ? scheme : HttpAuthSchemes.None;

    /// <summary>
    /// The request body: none when <see cref="EasyTransferOptions.NoBody" /> is set, as libcurl's
    /// <c>CURLOPT_NOBODY</c> set after <c>CURLOPT_POST</c> sends a HEAD (upstream test514, BL-2591; the curl
    /// tool refuses <c>-I</c> with a body), otherwise <paramref name="formBody" /> when given, otherwise
    /// <see cref="EasyHttpOptions.PostFields" />.
    /// </summary>
    private static HttpRequestBody? RequestBodyOf(EasyHttpOptions http, EasyTransferOptions transfer, HttpRequestBody? formBody) =>
        transfer.NoBody ? null : formBody ?? PostFieldsBodyOf(http);

    /// <summary>
    /// The <see cref="EasyHttpOptions.PostFields" /> body, or <see langword="null" /> when there is none, as
    /// under <c>-G</c>, which moved it into the query. Only its first <see cref="EasyHttpOptions.PostFieldSize" />
    /// bytes are sent when that is not negative (<c>CURLOPT_POSTFIELDSIZE</c>, BL-3190), never more than the data
    /// holds (ADR-0773).
    /// </summary>
    private static BytesBody? PostFieldsBodyOf(EasyHttpOptions http) =>
        http.PostFields is { } data ? new BytesBody(SizedPostFieldsOf(data, http.PostFieldSize), FormUrlEncoded) : null;

    /// <summary>
    /// <paramref name="data" /> cut to <paramref name="size" /> bytes when that is not negative and shorter, as
    /// libcurl sends <c>CURLOPT_POSTFIELDSIZE</c> bytes of <c>CURLOPT_POSTFIELDS</c>; a larger size sends the whole
    /// data rather than reading past it (ADR-0773).
    /// </summary>
    private static ReadOnlyMemory<byte> SizedPostFieldsOf(ReadOnlyMemory<byte> data, long size) =>
        size < 0 || size >= data.Length ? data : data[..(int)size];
}
