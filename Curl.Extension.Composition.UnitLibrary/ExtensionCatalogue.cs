using Curl.Protocol.Abstractions;

namespace Curl.Extension.Composition;

/// <summary>
/// Every extension's protocol handler factory, one entry per line (ADR-0999). Each extension
/// task adds its own line and nothing else here; the file is marked <c>merge=union</c> in
/// <c>.gitattributes</c> and listed in <c>Tasks/SharedListFiles.txt</c>, so lanes adding
/// different extensions do not overlap. Plain constructor calls, no reflection, so native AOT
/// sees every type.
/// </summary>
internal static class ExtensionCatalogue
{
    /// <summary>Gets the factories, in the order their handlers are registered.</summary>
    internal static IReadOnlyList<Func<ProtocolHandlerServices, IProtocolHandler>> Entries { get; } =
    [
        SocketIo.SocketIoExtension.CreateHandler,
        Coap.Client.CoapExtension.CreateHandler,
        Stomp.Client.StompExtension.CreateHandler,
        Nats.NatsExtension.CreateHandler,
        Xmpp.Client.XmppExtension.CreateHandler,
        McpClient.Client.McpClientExtension.CreateHandler,
        Kafka.Client.KafkaExtension.CreateHandler,
        Redis.Client.RedisExtension.CreateHandler,
        SignalR.SignalRExtension.CreateHandler,
        Mqtt5.Mqtt5Extension.CreateHandler,
        Amqp1.Client.Amqp1Extension.CreateHandler,
        WebTransport.WebTransportExtension.CreateHandler,
    ];
}
