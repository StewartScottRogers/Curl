using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// Remembers the HTTP version an HTTP/1.x connection's responses named, held by a pooled
/// connection (<see cref="IConnection.TryHoldSession" />) for the next request on it, as
/// curl 8.21.0 keeps <c>conn->httpversion_seen</c>: a connection that answered HTTP/1.0 is
/// sent HTTP/1.0 requests from then on (upstream test1074), and a later response naming
/// another major version, or none, fails with exit 8 (test471, test1479).
/// </summary>
internal sealed class Http1VersionSeen : IConnectionSession
{
    /// <summary>Gets or sets the version the connection's last response named.</summary>
    internal required Version Version { get; set; }

    /// <summary>
    /// Gives the version a connection's earlier responses named, or <see langword="null" />
    /// when it has carried none this side of a pool.
    /// </summary>
    /// <param name="connection">The connection about to carry a request.</param>
    internal static Version? Of(IConnection connection) => (connection.Session as Http1VersionSeen)?.Version;

    /// <summary>
    /// Tells whether a request on <paramref name="connection" /> goes out as HTTP/1.0 because
    /// its earlier response did.
    /// </summary>
    /// <param name="connection">The connection about to carry a request.</param>
    internal static bool DowngradesToHttp10(IConnection connection) => Of(connection) == HttpVersion.Version10;

    /// <summary>
    /// Gives the version a response head names: HTTP/2 or HTTP/3 when its status line begins
    /// <c>HTTP/2</c> or <c>HTTP/3</c>, which over HTTP/1.x is parsed as an assumed
    /// <c>HTTP/1.0 200</c> (<see cref="HttpStatusLine.Parse" />), and else the parsed version.
    /// </summary>
    /// <param name="headBytes">The head as received.</param>
    /// <param name="parsed">The version its status line was parsed as.</param>
    internal static Version VersionNamed(ReadOnlySpan<byte> headBytes, Version parsed) =>
        headBytes.StartsWith("HTTP/2"u8) ? HttpVersion.Version20
            : headBytes.StartsWith("HTTP/3"u8) ? HttpVersion.Version30
            : parsed;

    /// <summary>
    /// Fails a response whose major version differs from the one the connection's earlier
    /// response named, as curl does with <c>Version mismatch (from HTTP/1 to HTTP/2)</c>.
    /// </summary>
    /// <param name="connection">The connection the response came on.</param>
    /// <param name="version">The version the response named.</param>
    /// <exception cref="HttpTransferException">The major versions differ (exit 8).</exception>
    internal static void ThrowIfMismatched(IConnection connection, Version version)
    {
        if (Of(connection) is { } seen && seen.Major != version.Major)
        {
            throw new HttpTransferException(
                CurlExitCode.WeirdServerReply,
                $"Version mismatch (from HTTP/{seen.Major} to HTTP/{version.Major})");
        }
    }

    /// <summary>
    /// Records the version an HTTP/1.x response named on <paramref name="connection" />,
    /// handing the connection a new record to hold when it holds none.
    /// </summary>
    /// <param name="connection">The connection the response came on.</param>
    /// <param name="version">The version the response named.</param>
    internal static void Record(IConnection connection, Version version)
    {
        if (connection.Session is Http1VersionSeen seen)
        {
            seen.Version = version;
        }
        else if (connection.Session is null && version.Major == 1)
        {
            connection.TryHoldSession(new Http1VersionSeen { Version = version });
        }
    }

    /// <inheritdoc />
    public ValueTask ShutDownAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;
}
