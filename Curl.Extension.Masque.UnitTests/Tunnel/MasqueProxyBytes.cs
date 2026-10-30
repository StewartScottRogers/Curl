using System.Text;
using Curl.Extension.Masque.Wire;
using Curl.HttpDatagram;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Masque.Tunnel;

/// <summary>What a fake MASQUE proxy sends, and the tunnels the tests open through one.</summary>
internal static class MasqueProxyBytes
{
    public const string UdpTemplate = "https://proxy.example:4443/masque{?target_host,target_port}";

    public const string IpTemplate = "https://proxy.example/ip{?target,ipproto}";

    public static byte[] SwitchingProtocols(string token) =>
        Encoding.ASCII.GetBytes($"HTTP/1.1 101 Switching Protocols\r\nUpgrade: {token}\r\nConnection: Upgrade\r\n\r\n");

    public static byte[] Capsule(ulong type, params byte[] value) => CapsuleCodec.Encode(new Capsule(type, value));

    public static byte[] Datagram(ReadOnlySpan<byte> content) => Capsule(CapsuleType.Datagram, MasqueDatagramPayload.Wrap(content));

    public static async Task<(IDatagramChannel Channel, ScriptedProxyConnection Connection)> OpenAsync(
        MasqueTunnelKind kind, string host, int port, params byte[][] replies)
    {
        var connection = new ScriptedProxyConnection();
        connection.Enqueue(replies);
        var template = kind == MasqueTunnelKind.ConnectUdp ? UdpTemplate : IpTemplate;
        var result = await new MasqueDatagramConnector(new RecordingConnector(ConnectResult.Connected(connection)), template, kind)
            .OpenAsync(host, port, CancellationToken.None);
        Assert.IsNotNull(result.Channel, result.ErrorMessage);
        return (result.Channel, connection);
    }
}
