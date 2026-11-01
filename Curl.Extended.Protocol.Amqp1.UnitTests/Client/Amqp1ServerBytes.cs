using Curl.Extended.Protocol.Amqp1.Frames;
using Curl.Extended.Protocol.Amqp1.Types;

namespace Curl.Extended.Protocol.Amqp1.Client;

/// <summary>The bytes an AMQP 1.0 server sends, frame by frame, and a reader for the frames the client wrote.</summary>
internal static class Amqp1ServerBytes
{
    public static byte[] SaslHeader => Amqp1ProtocolHeader.Write(Amqp1ProtocolHeader.SaslProtocolId);

    public static byte[] AmqpHeader => Amqp1ProtocolHeader.Write(Amqp1ProtocolHeader.AmqpProtocolId);

    public static byte[] Heartbeat => Amqp1FrameCodec.Write(Amqp1Frame.Heartbeat());

    public static byte[] Mechanisms(object? offered) => Sasl(Amqp1CompositeType.SaslMechanisms, ("sasl-server-mechanisms", offered));

    public static byte[] Mechanisms(params string[] names) =>
        Mechanisms(new Amqp1Array(Amqp1FormatCode.Sym8, [.. names.Select(name => (object?)new Amqp1Symbol(name))]));

    public static byte[] Outcome(byte code) => Sasl(Amqp1CompositeType.SaslOutcome, ("code", code));

    public static byte[] Sasl(Amqp1CompositeType type, params (string, object?)[] fields) =>
        Amqp1FrameCodec.Write(Amqp1Frame.ForSasl(Amqp1Composite.Create(type, fields)));

    /// <summary>SASL ANONYMOUS accepted, then the AMQP header: what every flow starts with.</summary>
    public static byte[] Authenticated => [.. SaslHeader, .. Mechanisms("PLAIN", "ANONYMOUS"), .. Outcome(0), .. AmqpHeader];

    public static byte[] Performative(Amqp1CompositeType type, params (string, object?)[] fields) =>
        Amqp1FrameCodec.Write(Amqp1Frame.ForPerformative(0, Amqp1Composite.Create(type, fields)));

    public static byte[] Open(uint? maxFrameSize = null) =>
        Performative(Amqp1CompositeType.Open, ("container-id", "broker"), ("max-frame-size", maxFrameSize));

    public static byte[] Begin => Performative(Amqp1CompositeType.Begin,
        ("remote-channel", (ushort)0), ("next-outgoing-id", 7u), ("incoming-window", 100u), ("outgoing-window", 100u));

    public static byte[] Attach(bool role) => Performative(Amqp1CompositeType.Attach, ("name", "curl"), ("handle", 0u), ("role", role));

    public static byte[] Opened(bool serverRole, uint? maxFrameSize = null) => [.. Open(maxFrameSize), .. Begin, .. Attach(serverRole)];

    public static byte[] Flow(uint credit) => Performative(Amqp1CompositeType.Flow,
        ("incoming-window", 100u), ("next-outgoing-id", 7u), ("outgoing-window", 100u), ("handle", 0u), ("link-credit", credit));

    public static byte[] Disposition(Amqp1Composite? state) =>
        Performative(Amqp1CompositeType.Disposition, ("role", true), ("first", 0u), ("settled", true), ("state", state?.ToDescribed()));

    public static Amqp1Composite State(Amqp1CompositeType type, params (string, object?)[] fields) => Amqp1Composite.Create(type, fields);

    public static object Error(object condition, string? description = null) =>
        Amqp1Composite.Create(Amqp1CompositeType.Error, ("condition", condition), ("description", description)).ToDescribed();

    public static byte[] Detach(object? error = null) => Performative(Amqp1CompositeType.Detach, ("handle", 0u), ("closed", true), ("error", error));

    public static byte[] End(object? error = null) => Performative(Amqp1CompositeType.End, ("error", error));

    public static byte[] Close(object? error = null) => Performative(Amqp1CompositeType.Close, ("error", error));

    public static byte[] Closed => [.. Detach(), .. End(), .. Close()];

    public static byte[] Transfer(byte[] payload, bool more = false, bool settled = false) =>
        Amqp1FrameCodec.Write(Amqp1Frame.ForPerformative(0, Amqp1Composite.Create(Amqp1CompositeType.Transfer,
            ("handle", 0u), ("delivery-id", 3u), ("delivery-tag", new byte[] { 9 }), ("message-format", 0u), ("settled", settled), ("more", more)), payload));

    public static byte[] Message(params Amqp1Section[] sections) => Amqp1MessageCodec.Write(sections);

    public static byte[] DataMessage(string text) => Message(new Amqp1Section(Amqp1SectionType.Data, System.Text.Encoding.UTF8.GetBytes(text)));

    /// <summary>What the client wrote: null for a protocol header, otherwise each frame's body and payload, in order.</summary>
    public static List<(Amqp1Composite? Performative, byte[] Payload)> Written(ScriptedAmqp1Connection connection)
    {
        var bytes = connection.Writes.SelectMany(write => write).ToArray();
        var written = new List<(Amqp1Composite?, byte[])>();
        var offset = 0;
        while (offset < bytes.Length)
        {
            if (bytes.AsSpan(offset).StartsWith("AMQP"u8))
            {
                written.Add((null, []));
                offset += Amqp1ProtocolHeader.Length;
                continue;
            }

            Assert.IsTrue(Amqp1FrameCodec.TryRead(bytes.AsMemory(offset), out var frame, out var consumed));
            var (performative, payload) = frame!.ReadBody();
            written.Add((performative, payload.ToArray()));
            offset += consumed;
        }

        return written;
    }

    public static string Names(ScriptedAmqp1Connection connection) =>
        string.Join(",", Written(connection).Select(entry => entry.Performative?.Type.Name ?? "header"));
}
