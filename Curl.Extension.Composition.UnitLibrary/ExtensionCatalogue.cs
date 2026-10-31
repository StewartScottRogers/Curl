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
        Curl.Extended.Protocol.SocketIo.SocketIoExtension.CreateHandler,
        Curl.Extended.Protocol.Coap.Client.CoapExtension.CreateHandler,
        Curl.Extended.Protocol.Stomp.Client.StompExtension.CreateHandler,
        Curl.Extended.Protocol.Nats.NatsExtension.CreateHandler,
        Curl.Extended.Protocol.Xmpp.Client.XmppExtension.CreateHandler,
        Curl.Extended.Protocol.McpClient.Client.McpClientExtension.CreateHandler,
        Curl.Extended.Protocol.Kafka.Client.KafkaExtension.CreateHandler,
        Curl.Extended.Protocol.Redis.Client.RedisExtension.CreateHandler,
        Curl.Extended.Protocol.SignalR.SignalRExtension.CreateHandler,
        Curl.Extended.Protocol.Mqtt5.Mqtt5Extension.CreateHandler,
        Curl.Extended.Protocol.Amqp1.Client.Amqp1Extension.CreateHandler,
        Curl.Extended.Protocol.WebTransport.WebTransportExtension.CreateHandler,
        Curl.Extended.Protocol.S3.Client.S3Extension.CreateHandler,
        Curl.Extended.Protocol.Amqp0.Client.Amqp0Extension.CreateHandler,
        Curl.Extended.Protocol.OciRegistry.Client.OciRegistryExtension.CreateHandler,
        Curl.Extended.Protocol.GoogleCloudStorage.Client.GcsExtension.CreateHandler,
        Curl.Extended.Protocol.AzureStorage.Client.AzureStorageExtension.CreateHandler,
        Curl.Extended.Protocol.DnsQuery.Client.DnsQueryExtension.CreateHandler,
        services => Curl.Extended.Protocol.Plugin.PluginProtocolExtension.CreateHandler(services, ExtensionDescriptionCatalogue.Entries.SelectMany(description => description.Schemes)),
        Curl.Extended.Protocol.Grpc.Client.GrpcExtension.CreateHandler,
    ];
}
