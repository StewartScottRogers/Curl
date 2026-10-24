using Curl.Conformance.Differential.Protocols.Core;

namespace Curl.Conformance.Differential.Protocols.Text;

/// <summary>
/// Answers a Telnet connection the way proto2_servers.py's <c>Telnet</c> class does (port 18420): sends
/// <c>IAC DO TERMINAL-TYPE, IAC WILL ECHO</c> and <c>login: </c>, logs the Python <c>repr</c> of the first chunk
/// the client sends (up to 4096 bytes), answers <c>welcome</c>, then holds the connection open for 0.3 seconds.
/// </summary>
public sealed class TelnetResponder(ProtocolWireLog log, TimeProvider timeProvider)
{
    private const int ReceiveBytes = 4096;
    private static readonly byte[] Greeting = [0xff, 0xfd, 0x18, 0xff, 0xfb, 0x01, .. "login: "u8];

    /// <summary>Serves one connection.</summary>
    public async Task RespondAsync(Stream connection, CancellationToken cancellationToken)
    {
        var writer = new ProtocolLineWriter(connection);
        await writer.SendAsync(Greeting, cancellationToken);
        var received = new byte[ReceiveBytes];
        var count = await connection.ReadAsync(received, cancellationToken);
        log.Add(ProtocolBytesRepr.Format(received.AsSpan(0, count)));
        await writer.SendAsync("welcome\r\n", cancellationToken);
        await Task.Delay(TimeSpan.FromMilliseconds(300), timeProvider, cancellationToken);
    }
}
