using Curl.Extension.Abstractions.Description;

namespace Curl.Extension.Composition;

/// <summary>
/// Every extension's description, one entry per line (ADR-1008). Each extension adds its own line and
/// nothing else here; the file is marked <c>merge=union</c> in <c>.gitattributes</c> and listed in
/// <c>Tasks/SharedListFiles.txt</c>, so lanes adding different extensions do not overlap.
/// </summary>
internal static class ExtensionDescriptionCatalogue
{
    /// <summary>Gets the descriptions, in the order <c>--extensions</c> lists them.</summary>
    internal static IReadOnlyList<ExtensionDescription> Entries { get; } =
    [
        Curl.Extension.NamedPipe.NamedPipeExtension.Description,
        Curl.Extension.VirtualMachineSocket.VirtualMachineSocketExtension.Description,
        GraphQl.GraphQlOptions.Description,
        Curl.Extension.JsonRpc.JsonRpcExtension.Description,
        ConnectRpc.ConnectRpcExtension.Description,
        Curl.Extension.WebSocketSubprotocols.WebSocketSubprotocolsExtension.Description,
        HttpFile.HttpFileOptions.Description,
        OpenApi.OpenApiOptions.Description,
        SocketIo.SocketIoExtension.Description,
        Coap.Client.CoapExtension.Description,
        Stomp.Client.StompExtension.Description,
        Nats.NatsExtension.Description,
        WebTransport.WebTransportExtension.Description,
        Curl.Extension.WebSocketExtendedConnect.Option.WebSocketExtendedConnectExtension.Description,
        Curl.Extension.ServerSentEvents.ServerSentEventsExtension.Description,
        Xmpp.Client.XmppExtension.Description,
        Curl.Extension.HttpCache.HttpCacheExtension.Description,
        Curl.Extension.SegmentedDownload.SegmentedDownloadExtension.Description,
        Curl.Extension.DeltaDownload.DeltaDownloadExtension.Description,
        Curl.Extension.Checksum.ChecksumExtension.Description,
        Curl.Extension.Sigstore.SigstoreExtension.Description,
        Curl.Extension.EncryptedDns.EncryptedDnsExtension.Description,
        Curl.Extension.ObliviousDoh.ObliviousDohExtension.Description,
        McpClient.Client.McpClientExtension.Description,
        OAuth2.Options.OAuth2Options.Description,
        PrivacyPass.Options.PrivacyPassOptions.Description,
        Kafka.Client.KafkaExtension.Description,
        Redis.Client.RedisExtension.Description,
        Curl.Extension.WebSocketRepl.WebSocketReplExtension.Description,
        SignalR.SignalRExtension.Description,
        Mqtt5.Mqtt5Extension.Description,
        Amqp1.Client.Amqp1Extension.Description,
        S3.Client.S3Extension.Description,
        Amqp0.Client.Amqp0Extension.Description,
        OciRegistry.Client.OciRegistryExtension.Description,
        Curl.Extension.ContentDigest.ContentDigestExtension.Description,
        Curl.Extension.Har.HarExtension.Description,
        Curl.Extension.ClientHints.ClientHintsExtension.Description,
        Curl.Extension.CertificateTransparency.CertificateTransparencyExtension.Description,
        Curl.Extension.OpenTelemetry.OpenTelemetryExtension.Description,
        Curl.Extension.CodeGeneration.CodeGenerationExtension.Description,
        Curl.Extension.RateLimit.RateLimitExtension.Description,
        Curl.Extension.MlDsaCertificates.MlDsaCertificatesExtension.Description,
        GoogleCloudStorage.Client.GcsExtension.Description,
        AzureStorage.Client.AzureStorageExtension.Description,
        Curl.Extension.GrpcWeb.GrpcWebExtension.Description,
        Curl.Extension.RecordReplay.RecordReplayExtension.Description,
        DnsQuery.Client.DnsQueryExtension.Description,
        PluginProtocol.PluginProtocolExtension.Description,
        Curl.Extension.OsCredentialStore.OsCredentialStoreExtension.Description,
        Curl.Extension.Dpop.DpopExtension.Description,
        Grpc.Client.GrpcExtension.Description,
    ];
}
