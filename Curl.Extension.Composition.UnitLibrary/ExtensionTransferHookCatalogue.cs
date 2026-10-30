using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.Composition;

/// <summary>
/// Every extension's transfer hook activator, one entry per line (ADR-1007): each gives the extension's
/// <see cref="ITransferHook" /> when an option group gave its option, otherwise <see langword="null" />. Each
/// extension task adds its own line and nothing else here; the file is marked <c>merge=union</c> in
/// <c>.gitattributes</c> and listed in <c>Tasks/SharedListFiles.txt</c>, so lanes adding different extensions
/// do not overlap. A host passes them to <see cref="TransferHooks.ActivatedBy" /> for each option group. Plain
/// method references, no reflection, so native AOT sees every type.
/// </summary>
public static class ExtensionTransferHookCatalogue
{
    /// <summary>Gets the activators, in the order their hooks see the request.</summary>
    public static IReadOnlyList<Func<ExtensionOptionValues, ITransferHook?>> Entries { get; } =
    [
        GraphQl.GraphQlTransferHook.ActivatedBy,
        Curl.Extension.JsonRpc.JsonRpcExtension.HookFor,
        ConnectRpc.ConnectRpcExtension.HookFor,
        Curl.Extension.WebSocketSubprotocols.WebSocketSubprotocolsExtension.HookFor,
        Coap.Observe.CoapObserveTransferHook.ActivatedBy,
        Curl.Extension.ServerSentEvents.ServerSentEventsExtension.HookFor,
        Curl.Extension.HttpCache.HttpCacheExtension.HookFor,
        Curl.Extension.SegmentedDownload.SegmentedDownloadExtension.HookFor,
        Curl.Extension.ResumableUpload.ResumableUploadExtension.HookFor,
        Curl.Extension.DeltaDownload.DeltaDownloadExtension.HookFor,
        Curl.Extension.Checksum.ChecksumExtension.HookFor,
        Curl.Extension.Sigstore.SigstoreExtension.HookFor,
        OAuth2.Options.OAuth2Options.ActivatedBy,
        PrivacyPass.Options.PrivacyPassOptions.ActivatedBy,
        Curl.Extension.WebSocketRepl.WebSocketReplExtension.HookFor,
        Curl.Extension.ContentDigest.ContentDigestExtension.HookFor,
        Curl.Extension.StructuredFields.StructuredFieldsExtension.HookFor,
        Curl.Extension.Har.HarExtension.HookFor,
        Curl.Extension.ClientHints.ClientHintsExtension.HookFor,
        Curl.Extension.OpenTelemetry.OpenTelemetryExtension.HookFor,
        Curl.Extension.CodeGeneration.CodeGenerationExtension.HookFor,
        Curl.Extension.RateLimit.RateLimitExtension.HookFor,
        Curl.Extension.WebSocketExtendedConnect.Option.WebSocketExtendedConnectExtension.HookFor,
        Curl.Extension.GrpcWeb.GrpcWebExtension.HookFor,
        Curl.Extension.RecordReplay.RecordReplayExtension.HookFor,
        DnsQuery.Client.DnsQueryExtension.HookFor,
        Curl.Extension.OsCredentialStore.OsCredentialStoreExtension.HookFor,
        Curl.Extension.Dpop.DpopExtension.HookFor,
        Grpc.Client.GrpcExtension.HookFor,
    ];
}
