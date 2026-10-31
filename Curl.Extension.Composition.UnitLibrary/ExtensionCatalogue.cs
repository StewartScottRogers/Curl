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
        Protocol.SocketIo.SocketIoExtension.CreateHandler,
        Protocol.Coap.Client.CoapExtension.CreateHandler,
        Protocol.Stomp.Client.StompExtension.CreateHandler,
        Protocol.Nats.NatsExtension.CreateHandler,
        Protocol.Xmpp.Client.XmppExtension.CreateHandler,
        Protocol.McpClient.Client.McpClientExtension.CreateHandler,
        Protocol.Kafka.Client.KafkaExtension.CreateHandler,
        Protocol.Redis.Client.RedisExtension.CreateHandler,
        Protocol.SignalR.SignalRExtension.CreateHandler,
        Protocol.Mqtt5.Mqtt5Extension.CreateHandler,
        Protocol.Amqp1.Client.Amqp1Extension.CreateHandler,
        Protocol.WebTransport.WebTransportExtension.CreateHandler,
        Protocol.S3.Client.S3Extension.CreateHandler,
        Protocol.Amqp0.Client.Amqp0Extension.CreateHandler,
        Protocol.OciRegistry.Client.OciRegistryExtension.CreateHandler,
        Protocol.GoogleCloudStorage.Client.GcsExtension.CreateHandler,
        Protocol.AzureStorage.Client.AzureStorageExtension.CreateHandler,
        Protocol.DnsQuery.Client.DnsQueryExtension.CreateHandler,
        services => Protocol.Plugin.PluginProtocolExtension.CreateHandler(services, ExtensionDescriptionCatalogue.Entries.SelectMany(description => description.Schemes)),
        Protocol.Grpc.Client.GrpcExtension.CreateHandler,
    ];
}
