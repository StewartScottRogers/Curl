using Curl.Extension.Abstractions.Description;

namespace Curl.Extension.Composition;

/// <summary>
/// Every extension's description, one entry per line (ADR-1008). Each extension adds its own line and
/// nothing else here; the file is marked <c>merge=union</c> in <c>.gitattributes</c> and listed in
/// <c>Tasks/SharedListFiles.txt</c>, so lanes adding different extensions do not overlap.
/// </summary>
internal static class ExtensionDescriptionCatalogue
{
    /// <summary>Gets the descriptions, in the order <c>--extended-help</c> lists them.</summary>
    internal static IReadOnlyList<ExtensionDescription> Entries { get; } =
    [
        Curl.Extension.NamedPipe.NamedPipeExtension.Description,
        Curl.Extension.SshJump.SshJumpExtension.Description,
        Curl.Extension.VirtualMachineSocket.VirtualMachineSocketExtension.Description,
        Curl.Extension.QuicMigration.QuicMigrationExtension.Description,
        Curl.Extension.WireGuard.WireGuardExtension.Description,
        GraphQl.GraphQlOptions.Description,
        Curl.Extension.JsonRpc.JsonRpcExtension.Description,
        ConnectRpc.ConnectRpcExtension.Description,
        Curl.Extension.WebSocketSubprotocols.WebSocketSubprotocolsExtension.Description,
        HttpFile.HttpFileOptions.Description,
        OpenApi.OpenApiOptions.Description,
        Curl.Extended.Protocol.SocketIo.SocketIoExtension.Description,
        Curl.Extended.Protocol.Coap.Client.CoapExtension.Description,
        Curl.Extended.Protocol.Stomp.Client.StompExtension.Description,
        Curl.Extended.Protocol.Nats.NatsExtension.Description,
        Curl.Extended.Protocol.WebTransport.WebTransportExtension.Description,
        Curl.Extension.WebSocketExtendedConnect.Option.WebSocketExtendedConnectExtension.Description,
        Curl.Extension.ServerSentEvents.ServerSentEventsExtension.Description,
        Curl.Extended.Protocol.Xmpp.Client.XmppExtension.Description,
        Curl.Extension.HttpCache.HttpCacheExtension.Description,
        Curl.Extension.SegmentedDownload.SegmentedDownloadExtension.Description,
        Curl.Extension.ResumableUpload.ResumableUploadExtension.Description,
        Curl.Extension.WebDavTools.WebDavToolsExtension.Description,
        Curl.Extension.DeltaDownload.DeltaDownloadExtension.Description,
        Curl.Extension.Checksum.ChecksumExtension.Description,
        Curl.Extension.Sigstore.SigstoreExtension.Description,
        Curl.Extension.EncryptedDns.EncryptedDnsExtension.Description,
        Curl.Extension.ObliviousDoh.ObliviousDohExtension.Description,
        Curl.Extended.Protocol.McpClient.Client.McpClientExtension.Description,
        OAuth2.Options.OAuth2Options.Description,
        PrivacyPass.Options.PrivacyPassOptions.Description,
        Curl.Extended.Protocol.Kafka.Client.KafkaExtension.Description,
        Curl.Extended.Protocol.Redis.Client.RedisExtension.Description,
        Curl.Extension.WebSocketRepl.WebSocketReplExtension.Description,
        Curl.Extended.Protocol.SignalR.SignalRExtension.Description,
        Curl.Extended.Protocol.Mqtt5.Mqtt5Extension.Description,
        Curl.Extended.Protocol.Amqp1.Client.Amqp1Extension.Description,
        Curl.Extended.Protocol.S3.Client.S3Extension.Description,
        Curl.Extended.Protocol.Amqp0.Client.Amqp0Extension.Description,
        Curl.Extended.Protocol.OciRegistry.Client.OciRegistryExtension.Description,
        Curl.Extension.ContentDigest.ContentDigestExtension.Description,
        Curl.Extension.StructuredFields.StructuredFieldsExtension.Description,
        Curl.Extension.Har.HarExtension.Description,
        Curl.Extension.ClientHints.ClientHintsExtension.Description,
        Curl.Extension.CertificateTransparency.CertificateTransparencyExtension.Description,
        Curl.Extension.Dane.DaneExtension.Description,
        Curl.Extension.OpenTelemetry.OpenTelemetryExtension.Description,
        Curl.Extension.CodeGeneration.CodeGenerationExtension.Description,
        Curl.Extension.RateLimit.RateLimitExtension.Description,
        Curl.Extension.MlDsaCertificates.MlDsaCertificatesExtension.Description,
        Curl.Extended.Protocol.GoogleCloudStorage.Client.GcsExtension.Description,
        Curl.Extended.Protocol.AzureStorage.Client.AzureStorageExtension.Description,
        Curl.Extension.GrpcWeb.GrpcWebExtension.Description,
        Curl.Extension.RecordReplay.RecordReplayExtension.Description,
        Curl.Extended.Protocol.DnsQuery.Client.DnsQueryExtension.Description,
        Curl.Extended.Protocol.Plugin.PluginProtocolExtension.Description,
        Curl.Extension.OsCredentialStore.OsCredentialStoreExtension.Description,
        Curl.Extension.Dpop.DpopExtension.Description,
        Curl.Extended.Protocol.Grpc.Client.GrpcExtension.Description,
        Curl.Extension.Masque.MasqueExtension.Description,
        Curl.Extension.CompressionDictionary.CompressionDictionaryExtension.Description,
        Curl.Extension.ObliviousHttp.ObliviousHttpExtension.Description,
    ];
}
