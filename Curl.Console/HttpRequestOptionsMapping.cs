using System.Text;

using Curl.Cli;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Console;

/// <summary>
/// Maps the HTTP request options of a parsed command line onto the
/// <see cref="HttpRequestOptions" /> an HTTP handler reads: <c>-X</c>, <c>--request-target</c>, <c>-H</c>, <c>-A</c>,
/// <c>-e</c>, the <c>-d</c> family, <c>--json</c>, <c>-G</c>, <c>-f</c>, <c>--fail-with-body</c>
/// <c>-L</c>, the authentication options <c>--basic</c>, <c>--digest</c>, <c>--anyauth</c> and
/// <c>--oauth2-bearer</c>, and the transfer-encoding options <c>-0</c> / <c>--http1.0</c>,
/// <c>--http1.1</c>, <c>--compressed</c>, <c>--tr-encoding</c>, <c>--raw</c> and
/// <c>--ignore-content-length</c>, with the <c>-F</c> body the caller built.
/// </summary>
/// <remarks>
/// Measured with curl 8.21.0 (mingw, Schannel) against a loopback recorder on 2026-09-26
/// (BL-231 Notes). <c>--json</c> adds <c>Content-Type: application/json</c> and
/// <c>Accept: application/json</c> after every <c>-H</c> header, each only when no <c>-H</c>
/// header already starts with that name and a colon, compared without case, and adds them
/// under <c>-G</c> too. A <c>-d</c> family body is sent as
/// <c>application/x-www-form-urlencoded</c>, and not at all under <c>-G</c>, which moves it
/// into the URL query (<see cref="QueryUrl" />).
/// </remarks>
internal static class HttpRequestOptionsMapping
{
    /// <summary>The <c>Content-Type</c> curl sends a <c>-d</c> family body with.</summary>
    internal const string FormUrlEncoded = "application/x-www-form-urlencoded";

    private const string JsonContentType = "Content-Type: application/json";

    private const string JsonAccept = "Accept: application/json";

    /// <summary>
    /// Copies the HTTP request options from <paramref name="options" />.
    /// </summary>
    /// <param name="options">The parsed command line.</param>
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
    /// The options: <see cref="CommandLineOptions.RequestMethod" />,
    /// <see cref="CommandLineOptions.RequestTarget" />,
    /// <see cref="CommandLineOptions.UserAgent" />, <see cref="CommandLineOptions.Referer" /> and
    /// <see cref="CommandLineOptions.AutoReferer" /> verbatim; the <c>-H</c> headers followed by the ones <c>--json</c> adds
    /// and then <paramref name="ifNoneMatchHeaders" />; and
    /// <see cref="CommandLineOptions.ProxyHeaders" /> verbatim; and
    /// <paramref name="formBody" /> when given, otherwise
    /// <see cref="CommandLineOptions.PostData" /> as a <see cref="BytesBody" />, unless
    /// <see cref="CommandLineOptions.DataInQuery" /> moved it into the query; and
    /// <see cref="CommandLineOptions.FailMode" /> as <see cref="HttpRequestOptions.Fail" />; and
    /// <see cref="CommandLineOptions.FollowRedirects" /> as <see cref="HttpRequestOptions.FollowRedirects" />; and
    /// <see cref="CommandLineOptions.MaxRedirects" /> as <see cref="HttpRequestOptions.MaxRedirects" />; and
    /// <see cref="CommandLineOptions.AuthSchemes" />, <see cref="CommandLineOptions.BearerToken" /> and <see cref="CommandLineOptions.AwsSigV4" /> verbatim; and
    /// <see cref="CommandLineOptions.HttpVersion" /> as <see cref="HttpRequestOptions.Version" />, as
    /// <see cref="HttpVersionMapping.ToHttpVersionPreference" /> maps it; and <see cref="CommandLineOptions.Compressed" />,
    /// <see cref="CommandLineOptions.TransferEncoding" />, <see cref="CommandLineOptions.Raw" /> and
    /// <see cref="CommandLineOptions.IgnoreContentLength" /> verbatim; and
    /// <paramref name="proxy" /> as <see cref="HttpRequestOptions.ForwardProxy" /> with
    /// <see cref="CommandLineOptions.ProxyTunnel" /> as <see cref="HttpRequestOptions.ProxyTunnel" />; and
    /// <paramref name="commandLineTextEncoding" /> as <see cref="HttpRequestOptions.CommandLineTextEncoding" />; and
    /// <see cref="CommandLineOptions.Expect100Timeout" /> as <see cref="HttpRequestOptions.ContinueWait" /> (<see cref="ContinueWaitOf" />).
    /// </returns>
    internal static HttpRequestOptions FromCommandLine(
        CommandLineOptions options,
        HttpRequestBody? formBody = null,
        ProxyEndpoint? proxy = null,
        Encoding? commandLineTextEncoding = null,
        IReadOnlyList<string>? ifNoneMatchHeaders = null) =>
        new()
        {
            CustomMethod = options.RequestMethod,
            RequestTarget = options.RequestTarget,
            Headers = [.. HeadersOf(options), .. ifNoneMatchHeaders ?? []],
            ProxyHeaders = options.ProxyHeaders,
            CommandLineTextEncoding = commandLineTextEncoding ?? Encoding.Latin1,
            UserAgent = options.UserAgent,
            Referer = options.Referer,
            AutoReferer = options.AutoReferer,
            Body = formBody ?? PostDataBodyOf(options),
            Fail = options.FailMode,
            FollowRedirects = options.FollowRedirects,
            MaxRedirects = options.MaxRedirects,
            AuthSchemes = options.AuthSchemes,
            BearerToken = options.BearerToken,
            AwsSigV4 = options.AwsSigV4,
            Version = HttpVersionMapping.ToHttpVersionPreference(options.HttpVersion),
            Compressed = options.Compressed,
            TransferEncoding = options.TransferEncoding,
            Raw = options.Raw,
            IgnoreContentLength = options.IgnoreContentLength,
            ForwardProxy = proxy,
            ProxyTunnel = options.ProxyTunnel,
            ContinueWait = ContinueWaitOf(options),
        };

    /// <summary>
    /// The wait for <c>100 Continue</c>: the <c>--expect100-timeout</c> value, or curl's one
    /// second (<see cref="HttpRequestOptions.DefaultContinueWait" />) when none or 0 was given,
    /// as curl 8.21.0 waits one second for <c>--expect100-timeout 0</c> and <c>0.0001</c>
    /// (measured, BL-624 Notes).
    /// </summary>
    /// <param name="options">The parsed command line.</param>
    /// <returns>How long to wait before the body is sent anyway.</returns>
    internal static TimeSpan ContinueWaitOf(CommandLineOptions options) =>
        options.Expect100Timeout is { } given && given > TimeSpan.Zero ? given : HttpRequestOptions.DefaultContinueWait;

    /// <summary>
    /// The <c>-d</c> family body, or <see langword="null" /> when there is none or <c>-G</c> moved it into the query.
    /// </summary>
    private static BytesBody? PostDataBodyOf(CommandLineOptions options) =>
        options.PostData is { } data && !options.DataInQuery ? new BytesBody(data, FormUrlEncoded) : null;

    /// <summary>
    /// The <c>-H</c> headers, then the <c>--json</c> ones no <c>-H</c> header already names.
    /// </summary>
    private static List<string> HeadersOf(CommandLineOptions options)
    {
        List<string> headers = [.. options.Headers];
        if (options.SendsJson)
        {
            AddUnlessNamed(headers, JsonContentType);
            AddUnlessNamed(headers, JsonAccept);
        }

        return headers;
    }

    /// <summary>
    /// Appends <paramref name="header" /> unless a header in <paramref name="headers" /> starts
    /// with its name and colon, compared without case, as curl's tool checks.
    /// </summary>
    private static void AddUnlessNamed(List<string> headers, string header)
    {
        string nameAndColon = header[..(header.IndexOf(':', StringComparison.Ordinal) + 1)];
        if (!headers.Exists(existing => existing.StartsWith(nameAndColon, StringComparison.OrdinalIgnoreCase)))
        {
            headers.Add(header);
        }
    }
}
