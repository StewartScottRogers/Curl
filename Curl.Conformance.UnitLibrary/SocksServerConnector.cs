using System.Globalization;
using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// An <see cref="IConnector"/> that emulates upstream's SOCKS test server, <c>socksd</c>
/// (<c>tests/server/socksd.c</c> at <c>curl-8_21_0</c>), on <see cref="SocksPort"/>, and hands
/// every other connection to the server it wraps. No socket is opened.
/// </summary>
/// <remarks>
/// A connection to <see cref="SocksPort"/> takes a SOCKS4 or SOCKS4a request, or a SOCKS5
/// greeting, the username/password exchange when the case's <c>&lt;servercmd&gt;</c> gives
/// <c>method 2</c>, and a CONNECT to an IPv4 address, IPv6 address or host name; it then opens a
/// connection to the wrapped server at the requested port, or at <c>backendport</c> when the case
/// gives one, and relays every byte both ways. As socksd's configuration, <c>&lt;servercmd&gt;</c>
/// is read for <c>method</c> (the method the SOCKS5 greeting is answered with, 0 when not given),
/// <c>user</c> and <c>password</c> (the credentials accepted, <c>user</c> and <c>password</c> when
/// not given) and <c>backendport</c>; wrong credentials are answered with status 1 and the
/// connection closed. <see cref="ConnectLog"/> holds the line socksd appends to its dump file for each
/// SOCKS5 CONNECT, for <c>&lt;verify&gt;&lt;socks&gt;</c>.
/// </remarks>
/// <param name="testCase">The test case, parsed after <see cref="UpstreamTestFileExpander"/> has expanded it.</param>
/// <param name="backend">The server CONNECT requests reach, and every connection not to <see cref="SocksPort"/>.</param>
public sealed class SocksServerConnector(UpstreamTestCase testCase, IConnector backend) : IConnector
{
    /// <summary>The port of upstream's SOCKS server, <c>%SOCKSPORT</c>.</summary>
    public const int SocksPort = 8994;

    /// <summary>The name of upstream's SOCKS5 server on a Unix socket, <c>socks5unix</c>, which this emulation also stands in for.</summary>
    public const string UnixSocketServer = "socks5unix";

    /// <summary>
    /// The value of <c>%SOCKSUNIXPATH</c>, the Unix socket upstream's <c>socks5unix</c> server listens on: absolute, as
    /// <c>runtests.pl</c> makes it, since curl reads <c>localhost%SOCKSUNIXPATH</c> as a host and a path. Nothing
    /// listens on it: a dial to it reaches this emulation in memory, as a dial to <see cref="SocksPort"/> does.
    /// </summary>
    public const string SocksUnixPath = "/log/server/socks5.sock";

    private readonly SocksServerConfiguration configuration = SocksServerConfiguration.Read(testCase);

    private readonly StringBuilder connectLog = new();

    /// <summary>
    /// What socksd writes to its dump file (<c>tests/server/socksd.c</c> at <c>curl-8_22_0</c>), one line per
    /// SOCKS5 CONNECT: <c>atyp 1 =&gt; 12.34.56.78</c>, <c>atyp 3 =&gt; host.name</c>, or
    /// <c>atyp 4 =&gt;</c> and each IPv6 address byte as two lowercase hex digits after a space.
    /// </summary>
    public string ConnectLog
    {
        get
        {
            lock (connectLog)
            {
                return connectLog.ToString();
            }
        }
    }

    /// <summary>
    /// Opens an in-memory connection to the SOCKS emulation when <paramref name="target"/>'s port is <see cref="SocksPort"/>
    /// or its host is <see cref="SocksUnixPath"/> (a Unix socket dial, <see cref="UpstreamCaseRunner.HttpUnixSocketTarget"/>),
    /// otherwise a connection to the wrapped server.
    /// </summary>
    /// <param name="target">The host and port to connect to.</param>
    /// <param name="cancellationToken">Passed to the wrapped server.</param>
    /// <returns>The connection's result.</returns>
    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
        target.Port == SocksPort || target.Host == SocksUnixPath
            ? ValueTask.FromResult(ConnectResult.Connected(new SocksServerConnection(configuration, backend, LogConnect)
            {
                RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, SocksPort),
            }))
            : backend.ConnectAsync(target, cancellationToken);

    private void LogConnect(string line)
    {
        lock (connectLog)
        {
            connectLog.Append(line);
        }
    }
}

/// <summary>socksd's configuration, as a case's <c>&lt;servercmd&gt;</c> gives it.</summary>
/// <param name="Method">The method a SOCKS5 greeting is answered with.</param>
/// <param name="User">The user name accepted.</param>
/// <param name="Password">The password accepted.</param>
/// <param name="BackendPort">The port a CONNECT reaches instead of the requested one; 0 for the requested one.</param>
internal sealed record SocksServerConfiguration(byte Method, string User, string Password, int BackendPort)
{
    public static SocksServerConfiguration Read(UpstreamTestCase testCase)
    {
        Dictionary<string, string> values = new(StringComparer.Ordinal);
        string text = Encoding.Latin1.GetString((testCase.Find("reply", "servercmd")?.Content ?? ReadOnlyMemory<byte>.Empty).Span);
        foreach (string line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] words = line.Split(' ', 2, StringSplitOptions.TrimEntries);
            values[words[0]] = words.Length > 1 ? words[1] : string.Empty;
        }

        return new SocksServerConfiguration(
            (byte)Number(values, "method"),
            values.GetValueOrDefault("user", "user"),
            values.GetValueOrDefault("password", "password"),
            Number(values, "backendport"));
    }

    private static int Number(Dictionary<string, string> values, string key) =>
        int.TryParse(values.GetValueOrDefault(key), NumberStyles.None, CultureInfo.InvariantCulture, out int number) ? number : 0;
}
