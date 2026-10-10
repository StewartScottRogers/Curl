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
/// connection closed.
/// </remarks>
/// <param name="testCase">The test case, parsed after <see cref="UpstreamTestFileExpander"/> has expanded it.</param>
/// <param name="backend">The server CONNECT requests reach, and every connection not to <see cref="SocksPort"/>.</param>
public sealed class SocksServerConnector(UpstreamTestCase testCase, IConnector backend) : IConnector
{
    /// <summary>The port of upstream's SOCKS server, <c>%SOCKSPORT</c>.</summary>
    public const int SocksPort = 8994;

    private readonly SocksServerConfiguration configuration = SocksServerConfiguration.Read(testCase);

    /// <summary>Opens an in-memory connection to the SOCKS emulation when <paramref name="target"/>'s port is <see cref="SocksPort"/>, otherwise a connection to the wrapped server.</summary>
    /// <param name="target">The host and port to connect to.</param>
    /// <param name="cancellationToken">Passed to the wrapped server.</param>
    /// <returns>The connection's result.</returns>
    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
        target.Port == SocksPort
            ? ValueTask.FromResult(ConnectResult.Connected(new SocksServerConnection(configuration, backend)
            {
                RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, SocksPort),
            }))
            : backend.ConnectAsync(target, cancellationToken);
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
