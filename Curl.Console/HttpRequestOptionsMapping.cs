using Curl.Cli;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Console;

/// <summary>
/// Maps the HTTP request options of a parsed command line onto the
/// <see cref="HttpRequestOptions" /> an HTTP handler reads: <c>-X</c>, <c>-H</c>, <c>-A</c>,
/// <c>-e</c>, the <c>-d</c> family, <c>--json</c>, <c>-G</c>, <c>-f</c>, <c>--fail-with-body</c>
/// and <c>-L</c>.
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
    /// <returns>
    /// The options: <see cref="CommandLineOptions.RequestMethod" />,
    /// <see cref="CommandLineOptions.UserAgent" /> and <see cref="CommandLineOptions.Referer" />
    /// verbatim; the <c>-H</c> headers followed by the ones <c>--json</c> adds; and
    /// <see cref="CommandLineOptions.PostData" /> as a <see cref="BytesBody" />, unless
    /// <see cref="CommandLineOptions.DataInQuery" /> moved it into the query; and
    /// <see cref="CommandLineOptions.FailMode" /> as <see cref="HttpRequestOptions.Fail" />; and
    /// <see cref="CommandLineOptions.FollowRedirects" /> as <see cref="HttpRequestOptions.FollowRedirects" />.
    /// </returns>
    internal static HttpRequestOptions FromCommandLine(CommandLineOptions options) =>
        new()
        {
            CustomMethod = options.RequestMethod,
            Headers = HeadersOf(options),
            UserAgent = options.UserAgent,
            Referer = options.Referer,
            Body = options.PostData is { } data && !options.DataInQuery ? new BytesBody(data, FormUrlEncoded) : null,
            Fail = options.FailMode,
            FollowRedirects = options.FollowRedirects,
        };

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
