using System.Globalization;
using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// An <see cref="IConnector"/> that emulates upstream's MQTT test server, <c>mqttd</c>
/// (<c>tests/server/mqttd.c</c> at <c>curl-8_21_0</c>), on <see cref="MqttPort"/>, and hands every
/// other connection to the server it wraps. No socket is opened.
/// </summary>
/// <remarks>
/// Each connection is an <see cref="MqttServerConnection"/>. <see cref="ProtocolLog"/> holds the
/// lines mqttd writes to its protocol dump, across connections, for comparison with
/// <c>&lt;verify&gt;&lt;protocol&gt;</c>.
/// </remarks>
/// <param name="testCase">The test case, parsed after <see cref="UpstreamTestFileExpander"/> has expanded it.</param>
/// <param name="backend">The server every connection not to <see cref="MqttPort"/> reaches.</param>
public sealed class MqttServerConnector(UpstreamTestCase testCase, IConnector backend) : IConnector
{
    /// <summary>The port of upstream's MQTT server, <c>%MQTTPORT</c>.</summary>
    public const int MqttPort = 8998;

    private readonly MqttServerConfiguration configuration = MqttServerConfiguration.Read(testCase);

    private readonly SwsServerRecording recording = new();

    /// <summary>The protocol dump: one line per packet, as mqttd's <c>logprotocol</c> writes it.</summary>
    public ReadOnlyMemory<byte> ProtocolLog => recording.Bytes;

    /// <summary>Opens an in-memory connection to the mqttd emulation when <paramref name="target"/>'s port is <see cref="MqttPort"/>, otherwise a connection to the wrapped server.</summary>
    /// <param name="target">The host and port to connect to.</param>
    /// <param name="cancellationToken">Passed to the wrapped server.</param>
    /// <returns>The connection's result.</returns>
    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
        target.Port == MqttPort
            ? ValueTask.FromResult(ConnectResult.Connected(new MqttServerConnection(configuration, recording)
            {
                RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, MqttPort),
            }))
            : backend.ConnectAsync(target, cancellationToken);
}

/// <summary>mqttd's configuration, as a case's <c>&lt;servercmd&gt;</c> gives it, with the <c>&lt;reply&gt;&lt;data&gt;</c> it publishes.</summary>
/// <param name="PublishBeforeSuback">Whether a SUBSCRIBE is answered with PUBLISH before SUBACK (<c>PUBLISH-before-SUBACK</c>).</param>
/// <param name="ShortPublish">Whether a PUBLISH is sent two bytes short, then the connection closed (<c>short-PUBLISH</c>).</param>
/// <param name="ExcessiveRemaining">Whether a PUBLISH's remaining length is sent as the illegal <c>ff ff ff 80</c> (<c>excessive-remaining</c>).</param>
/// <param name="PingrespAsConnack">Whether a CONNECT is answered with a PINGRESP of remaining length 2 (<c>PINGRESP-as-CONNACK</c>).</param>
/// <param name="MalformedDisconnect">Whether the server's DISCONNECT has remaining length 2 (<c>DISCONNECT-malformed</c>).</param>
/// <param name="ConnackReturnCode">The CONNACK's return code (<c>error-CONNACK</c>).</param>
/// <param name="ConnackRemainingLength">The CONNACK's remaining length (<c>remlen-CONNACK</c>, 2 when not given).</param>
/// <param name="Payload">The <c>&lt;reply&gt;&lt;data&gt;</c> a SUBSCRIBE is answered with, or <see langword="null"/> when the case has none.</param>
internal sealed record MqttServerConfiguration(
    bool PublishBeforeSuback,
    bool ShortPublish,
    bool ExcessiveRemaining,
    bool PingrespAsConnack,
    bool MalformedDisconnect,
    byte ConnackReturnCode,
    byte ConnackRemainingLength,
    byte[]? Payload)
{
    public static MqttServerConfiguration Read(UpstreamTestCase testCase)
    {
        Dictionary<string, string> values = new(StringComparer.Ordinal);
        string text = Encoding.Latin1.GetString((testCase.Find("reply", "servercmd")?.Content ?? ReadOnlyMemory<byte>.Empty).Span);
        foreach (string line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] words = line.Split(' ', 2, StringSplitOptions.TrimEntries);
            values[words[0]] = words.Length > 1 ? words[1] : string.Empty;
        }

        return new MqttServerConfiguration(
            values.ContainsKey("PUBLISH-before-SUBACK"),
            values.ContainsKey("short-PUBLISH"),
            values.ContainsKey("excessive-remaining"),
            values.ContainsKey("PINGRESP-as-CONNACK"),
            values.ContainsKey("DISCONNECT-malformed"),
            Number(values, "error-CONNACK", 0),
            Number(values, "remlen-CONNACK", 2),
            testCase.Find("reply", "data") is { } data ? UpstreamTestPartBodies.Decoded(data) : null);
    }

    private static byte Number(Dictionary<string, string> values, string key, byte absent) =>
        byte.TryParse(values.GetValueOrDefault(key), NumberStyles.None, CultureInfo.InvariantCulture, out byte number) ? number : absent;
}
