using System.Globalization;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// Writes one HTTP exchange's steps to Curl's own diagnostic log (ADR-0222, BL-922), under
/// component <c>http</c>, <c>http2</c> or <c>http3</c> by the version the exchange runs on:
/// the failure that ends it as <c>error</c>; a downgrade or a request sent again as
/// <c>warning</c>; the request sent, the reply's status line, the body's framing and the
/// exchange's end as <c>info</c>; and the version chosen, each header name and value, and the
/// decoder chain as <c>verbose</c>.
/// </summary>
/// <param name="log">Where the lines go; <see cref="NoDiagnosticLog.Instance" /> writes nothing.</param>
/// <param name="component">The <see cref="DiagnosticLogComponents" /> name every line carries.</param>
/// <remarks>
/// Every method tests <see cref="IDiagnosticLog.IsEnabled" /> before it builds its message, so
/// a disabled level costs no formatting (ADR-0222, decision 8). The values of
/// <c>Authorization</c>, <c>Proxy-Authorization</c>, <c>Cookie</c> and <c>Set-Cookie</c> are
/// never logged (decision 7), and the request is named by its URL's path alone, never its query.
/// </remarks>
internal sealed class HttpExchangeLog(IDiagnosticLog log, string component)
{
    /// <summary>The headers whose values carry credentials, written as <see cref="ValueNotLogged" />.</summary>
    private static readonly string[] SecretHeaders = ["Authorization", "Proxy-Authorization", "Cookie", "Set-Cookie"];

    /// <summary>What a secret header's value is written as.</summary>
    internal const string ValueNotLogged = "(value not logged)";

    /// <summary>Gets a log that writes nothing, for a body reader no exchange set one on.</summary>
    internal static HttpExchangeLog Silent { get; } = new(NoDiagnosticLog.Instance, DiagnosticLogComponents.Http);

    /// <summary>Gets the component every line carries.</summary>
    internal string Component => component;

    /// <summary>
    /// Makes the log of an exchange on <paramref name="streams" />: component <c>http2</c> for
    /// an HTTP/2 session, <c>http3</c> for an HTTP/3 one, and <c>http</c> for HTTP/1.x.
    /// </summary>
    /// <param name="log">Where the lines go.</param>
    /// <param name="streams">The session the exchange runs on, or <see langword="null" /> for HTTP/1.x.</param>
    /// <returns>The exchange's log.</returns>
    internal static HttpExchangeLog For(IDiagnosticLog log, IHttpStreamSession? streams) =>
        new(log, ComponentOf(streams?.VersionName));

    /// <summary>Gives the component for a version name: <c>http2</c>, <c>http3</c>, else <c>http</c>.</summary>
    /// <param name="versionName">The session's version name, or <see langword="null" /> for HTTP/1.x.</param>
    /// <returns>The component.</returns>
    internal static string ComponentOf(string? versionName) =>
        versionName switch
        {
            "HTTP/2" => DiagnosticLogComponents.Http2,
            "HTTP/3" => DiagnosticLogComponents.Http3,
            _ => DiagnosticLogComponents.Http,
        };

    /// <summary>Logs, at <c>verbose</c>, the version the exchange uses and whether its connection is new.</summary>
    /// <param name="versionName">The version: <c>HTTP/1.x</c>, <c>HTTP/2</c> or <c>HTTP/3</c>.</param>
    /// <param name="newConnection">Whether the connection was opened for this exchange.</param>
    internal void VersionChosen(string versionName, bool newConnection)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Verbose))
        {
            Write(DiagnosticLogLevel.Verbose, "using " + versionName + (newConnection ? " on a new connection" : " on a reused connection"));
        }
    }

    /// <summary>
    /// Logs, at <c>warning</c>, a new connection that does not speak the version asked for:
    /// <c>--http3</c> that fell back to TCP, or <c>--http2</c> over TLS that ALPN left on HTTP/1.x.
    /// </summary>
    /// <param name="asked">The version the command line asked for.</param>
    /// <param name="secure">Whether the connection uses TLS.</param>
    /// <param name="versionName">The version used: <c>HTTP/1.x</c>, <c>HTTP/2</c> or <c>HTTP/3</c>.</param>
    internal void DowngradeOf(HttpVersionPreference asked, bool secure, string versionName)
    {
        if (DowngradeMessage(asked, secure, versionName) is { } message && log.IsEnabled(DiagnosticLogLevel.Warning))
        {
            Write(DiagnosticLogLevel.Warning, message);
        }
    }

    /// <summary>
    /// Gives the warning <see cref="DowngradeOf" /> logs, or <see langword="null" /> when the
    /// connection speaks the version asked for.
    /// </summary>
    private static string? DowngradeMessage(HttpVersionPreference asked, bool secure, string versionName)
    {
        if (asked == HttpVersionPreference.Http3 && versionName != "HTTP/3")
        {
            return "--http3 fell back to " + versionName + " over TCP";
        }

        return asked == HttpVersionPreference.Http2 && secure && versionName == Http1VersionName
            ? "--http2 refused by ALPN; using HTTP/1.x"
            : null;
    }

    /// <summary>The version name an HTTP/1.x exchange is logged with.</summary>
    internal const string Http1VersionName = "HTTP/1.x";

    /// <summary>
    /// Logs, at <c>info</c>, the method and URL path sent, and at <c>verbose</c> each request
    /// header of <paramref name="head" />, a secret one's value written as <see cref="ValueNotLogged" />.
    /// </summary>
    /// <param name="method">The request's method.</param>
    /// <param name="path">The URL's path, without its query.</param>
    /// <param name="head">The request head as sent: its request line, header lines and empty line.</param>
    internal void RequestSent(string method, string path, ReadOnlyMemory<byte> head)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Info))
        {
            Write(DiagnosticLogLevel.Info, method + " " + path + " sent");
        }

        if (log.IsEnabled(DiagnosticLogLevel.Verbose))
        {
            foreach (string line in HeaderLinesOf(head))
            {
                Write(DiagnosticLogLevel.Verbose, "header sent " + Loggable(line));
            }
        }
    }

    /// <summary>
    /// Logs, at <c>info</c>, the reply's status line, and at <c>verbose</c> each of its headers,
    /// a secret one's value written as <see cref="ValueNotLogged" />.
    /// </summary>
    /// <param name="head">The final head the exchange acts on.</param>
    internal void ReplyRead(HttpResponseHead head)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Info))
        {
            HttpStatusLine status = head.StatusLine;
            Write(DiagnosticLogLevel.Info, string.Create(
                CultureInfo.InvariantCulture,
                $"reply {status.StatusCode} {status.ReasonPhrase}").TrimEnd());
        }

        if (log.IsEnabled(DiagnosticLogLevel.Verbose))
        {
            foreach (HttpResponseHeader header in head.Headers)
            {
                Write(DiagnosticLogLevel.Verbose, "header received " + Loggable(header.Name, header.Value));
            }
        }
    }

    /// <summary>
    /// Logs, at <c>info</c>, how the body is framed - chunked, by its Content-Length, or until
    /// the connection closes - and at <c>verbose</c> the codings decoded, in order.
    /// </summary>
    /// <param name="framing">The body's framing.</param>
    /// <param name="decodedCodings">The Content-Encoding and Transfer-Encoding codings decoded.</param>
    internal void BodyFramed(HttpResponseBodyFraming framing, IReadOnlyList<string> decodedCodings)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Info))
        {
            Write(DiagnosticLogLevel.Info, "body framed " + FramingText(framing));
        }

        if (decodedCodings.Count > 0 && log.IsEnabled(DiagnosticLogLevel.Verbose))
        {
            Write(DiagnosticLogLevel.Verbose, "decoders " + string.Join(", ", decodedCodings));
        }
    }

    /// <summary>Gets whether a line at <c>info</c> is written, so a caller reads the clock only when it is.</summary>
    internal bool LogsInfo => log.IsEnabled(DiagnosticLogLevel.Info);

    /// <summary>Logs, at <c>info</c>, the end of an exchange: its status, its body bytes and its milliseconds.</summary>
    /// <param name="statusCode">The final status code.</param>
    /// <param name="bodyBytes">The body bytes read.</param>
    /// <param name="elapsed">How long the exchange took, from the request being ready.</param>
    internal void Exchanged(int statusCode, long bodyBytes, TimeSpan elapsed)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Info))
        {
            Write(DiagnosticLogLevel.Info, string.Create(
                CultureInfo.InvariantCulture,
                $"exchange done: status {statusCode}, {bodyBytes} body bytes in {(long)elapsed.TotalMilliseconds} ms"));
        }
    }

    /// <summary>
    /// Logs, at <c>error</c>, the failure that ends the exchange, with its <see cref="CurlExitCode" />
    /// and the exception behind it.
    /// </summary>
    /// <param name="exitCode">The exit code the transfer reports.</param>
    /// <param name="failure">The exception that ended the exchange.</param>
    internal void Failed(CurlExitCode exitCode, Exception failure)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Error))
        {
            Write(DiagnosticLogLevel.Error, string.Create(
                CultureInfo.InvariantCulture,
                $"exchange failed with {exitCode} (exit {(int)exitCode}): {failure.Message} [{failure.GetType().Name}]"));
        }
    }

    /// <summary>Logs, at <c>warning</c>, that a reused connection died before its reply and the request goes out again.</summary>
    /// <param name="retryCount">How many times the request has been sent again.</param>
    internal void RetryingOnFreshConnection(int retryCount)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Warning))
        {
            Write(DiagnosticLogLevel.Warning, string.Create(
                CultureInfo.InvariantCulture,
                $"reused connection died before the reply; sending the request again on a new connection (retry {retryCount})"));
        }
    }

    /// <summary>Gives the header lines of a request head: every line after the request line, up to the empty one.</summary>
    private static IEnumerable<string> HeaderLinesOf(ReadOnlyMemory<byte> head) =>
        Encoding.Latin1.GetString(head.Span)
            .Split("\r\n")
            .Skip(1)
            .TakeWhile(line => line.Length > 0);

    /// <summary>Gives a header line as it is logged: its value hidden when the header is a secret one.</summary>
    private static string Loggable(string line)
    {
        int colon = line.IndexOf(':', StringComparison.Ordinal);
        return colon < 0 ? line : Loggable(line[..colon], line[(colon + 1)..].TrimStart());
    }

    /// <summary>Gives <c>name: value</c>, the value hidden when <paramref name="name" /> is a secret header.</summary>
    private static string Loggable(string name, string value) =>
        name + ": " + (IsSecret(name) ? ValueNotLogged : value);

    /// <summary>Tells whether <paramref name="name" /> names a header whose value carries credentials.</summary>
    private static bool IsSecret(string name) =>
        SecretHeaders.Contains(name, StringComparer.OrdinalIgnoreCase);

    /// <summary>Gives the words for a body's framing.</summary>
    private static string FramingText(HttpResponseBodyFraming framing) =>
        framing switch
        {
            { IsChunked: true } => "chunked",
            { ContentLength: { } length } => "by Content-Length " + length.ToString(CultureInfo.InvariantCulture),
            _ => "until the connection closes",
        };

    private void Write(DiagnosticLogLevel level, string message) => log.Write(level, component, message);
}
