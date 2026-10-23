using System.Text;
using Curl.Conformance.Differential.Http.Core;
using Curl.Conformance.Differential.Protocols.Core;

namespace Curl.Conformance.Differential.Protocols.Proxy;

/// <summary>
/// Answers a connection to proxy_server.py's port (18300) the way its <c>P</c> handler does: a first byte of
/// <c>0x05</c> is SOCKS5 (any methods, answered with no authentication, then a CONNECT by IPv4, name or IPv6),
/// <c>0x04</c> is SOCKS4 or, for an address of <c>0.0.0.x</c>, SOCKS4a; each logs what the script logs, grants
/// the request and tunnels. Any other first byte goes to the <see cref="HttpProxyResponder"/>. A SOCKS request
/// cut short where the script's unpacking would fail ends the connection without an answer.
/// </summary>
public sealed class ProxyServerResponder(ProtocolWireLog log, IProxyTunnelConnector connector, HttpProxyResponder httpResponder)
{
    private const byte Socks5Version = 0x05;
    private const byte Socks4Version = 0x04;
    private const byte IPv4AddressType = 1;
    private const byte DomainNameAddressType = 3;

    private static readonly byte[] Socks5MethodChosen = [0x05, 0x00];
    private static readonly byte[] Socks5Granted = [0x05, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];

    /// <summary>Serves one connection until its tunnel or HTTP exchange ends, or the request is cut short.</summary>
    public async Task RespondAsync(Stream connection, CancellationToken cancellationToken)
    {
        var reader = new DifferentialHttpLineReader(connection);
        var first = await reader.ReadAsync(1, cancellationToken);
        try
        {
            switch (first)
            {
                case [Socks5Version]:
                    await RespondSocks5Async(connection, reader, cancellationToken);
                    break;
                case [Socks4Version]:
                    await RespondSocks4Async(connection, reader, cancellationToken);
                    break;
                default:
                    await httpResponder.RespondAsync(connection, reader, first, cancellationToken);
                    break;
            }
        }
        catch (EndOfStreamException)
        {
            // The script's handler raised on the short read and the connection closed.
        }
    }

    private async Task RespondSocks5Async(Stream connection, DifferentialHttpLineReader reader, CancellationToken cancellationToken)
    {
        var methodCount = (await ReadExactlyAsync(reader, 1, cancellationToken))[0];
        log.Add("SOCKS5 methods=" + Convert.ToHexStringLower(await reader.ReadAsync(methodCount, cancellationToken)));
        var writer = new ProtocolLineWriter(connection);
        await writer.SendAsync(Socks5MethodChosen, cancellationToken);
        var addressType = (await ReadExactlyAsync(reader, 4, cancellationToken))[3];
        var host = await ReadSocks5HostAsync(reader, addressType, cancellationToken);
        var port = ReadPort(await ReadExactlyAsync(reader, 2, cancellationToken), 0);
        log.Add($"SOCKS5 connect atyp={addressType} host={host} port={port}");
        await writer.SendAsync(Socks5Granted, cancellationToken);
        await ProxyTunnel.RelayAsync(connection, connector, host, port, cancellationToken);
    }

    private static async Task<string> ReadSocks5HostAsync(DifferentialHttpLineReader reader, byte addressType, CancellationToken cancellationToken) => addressType switch
    {
        IPv4AddressType => FormatIPv4(await ReadExactlyAsync(reader, 4, cancellationToken), 0),
        DomainNameAddressType => Encoding.UTF8.GetString(await reader.ReadAsync((await ReadExactlyAsync(reader, 1, cancellationToken))[0], cancellationToken)),
        _ => Convert.ToHexStringLower(await reader.ReadAsync(16, cancellationToken)),
    };

    private async Task RespondSocks4Async(Stream connection, DifferentialHttpLineReader reader, CancellationToken cancellationToken)
    {
        byte[] request = [Socks4Version, .. await ReadExactlyAsync(reader, 7, cancellationToken)];
        var port = ReadPort(request, 2);
        var address = FormatIPv4(request, 4);
        var userId = ProtocolBytesRepr.Format(await ReadNulTerminatedAsync(reader, cancellationToken));
        var host = address;
        if (request[4] == 0 && request[5] == 0 && request[6] == 0)
        {
            host = Encoding.UTF8.GetString(await ReadNulTerminatedAsync(reader, cancellationToken));
            log.Add($"SOCKS4a host={host} port={port} uid={userId}");
        }
        else
        {
            log.Add($"SOCKS4 ip={address} port={port} uid={userId}");
        }

        await new ProtocolLineWriter(connection).SendAsync([0x00, 0x5a, .. request.AsSpan(2, 6)], cancellationToken);
        await ProxyTunnel.RelayAsync(connection, connector, host, port, cancellationToken);
    }

    private static async Task<byte[]> ReadExactlyAsync(DifferentialHttpLineReader reader, int count, CancellationToken cancellationToken)
    {
        var bytes = await reader.ReadAsync(count, cancellationToken);
        return bytes.Length == count ? bytes : throw new EndOfStreamException();
    }

    private static async Task<byte[]> ReadNulTerminatedAsync(DifferentialHttpLineReader reader, CancellationToken cancellationToken)
    {
        using var text = new MemoryStream();
        while (await reader.ReadAsync(1, cancellationToken) is [not 0 and var value])
        {
            text.WriteByte(value);
        }

        return text.ToArray();
    }

    private static int ReadPort(byte[] bytes, int offset) => (bytes[offset] << 8) | bytes[offset + 1];

    private static string FormatIPv4(byte[] bytes, int offset) => string.Join('.', bytes[offset..(offset + 4)]);
}
