namespace Curl.Conformance.Extended.Amqp0;

/// <summary>What the reference broker offers in connection.start and proposes in connection.tune.</summary>
public sealed record Amqp0BrokerSettings
{
    /// <summary>Bytes the broker answers the client's protocol header with instead of connection.start, then closes (4.2.2); none by default.</summary>
    public byte[]? ProtocolHeaderReply { get; init; }

    /// <summary>The space-separated SASL mechanisms connection.start offers.</summary>
    public string Mechanisms { get; init; } = "PLAIN AMQPLAIN";

    /// <summary>The space-separated locales connection.start offers.</summary>
    public string Locales { get; init; } = "en_US";

    /// <summary>The channel-max connection.tune proposes; zero means no limit.</summary>
    public ushort ChannelMax { get; init; } = 2047;

    /// <summary>The frame-max connection.tune proposes; zero means no limit.</summary>
    public uint FrameMax { get; init; } = 131072;

    /// <summary>The heartbeat delay in seconds connection.tune proposes; zero means none.</summary>
    public ushort Heartbeat { get; init; } = 60;
}
