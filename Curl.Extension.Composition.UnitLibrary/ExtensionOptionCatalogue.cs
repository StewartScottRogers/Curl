using Curl.Extension.Abstractions;

namespace Curl.Extension.Composition;

/// <summary>
/// Every extension's long command-line option, one entry per line (ADR-1006). Each extension adds
/// its own line and nothing else here; the file is marked <c>merge=union</c> in <c>.gitattributes</c>
/// and listed in <c>Tasks/SharedListFiles.txt</c>, so lanes adding different options do not overlap.
/// </summary>
internal static class ExtensionOptionCatalogue
{
    /// <summary>Gets the options, in the order they are registered.</summary>
    internal static IReadOnlyList<ExtensionOption> Entries { get; } =
    [
        Curl.Extension.NamedPipe.NamedPipeExtension.Option,
        Curl.Extension.VirtualMachineSocket.VirtualMachineSocketExtension.Option,
        GraphQl.GraphQlOptions.Document,
        GraphQl.GraphQlOptions.Variables,
        GraphQl.GraphQlOptions.Operation,
        GraphQl.GraphQlOptions.Introspect,
        Curl.Extension.JsonRpc.JsonRpcExtension.Option,
        Curl.Extension.WebSocketSubprotocols.WebSocketSubprotocolsExtension.Option,
        Coap.Observe.CoapObserveTransferHook.Option,
        Curl.Extension.ServerSentEvents.ServerSentEventsExtension.Option,
        Curl.Extension.HttpCache.HttpCacheExtension.Option,
        Curl.Extension.SegmentedDownload.SegmentedDownloadExtension.Option,
        Curl.Extension.DeltaDownload.DeltaDownloadExtension.Option,
        Curl.Extension.DeltaDownload.DeltaDownloadExtension.SeedOption,
        Curl.Extension.Checksum.ChecksumExtension.Option,
        Curl.Extension.Sigstore.SigstoreExtension.BundleOption,
        Curl.Extension.Sigstore.SigstoreExtension.IdentityOption,
        Curl.Extension.Sigstore.SigstoreExtension.IssuerOption,
        Curl.Extension.Sigstore.SigstoreExtension.RootOption,
        Curl.Extension.EncryptedDns.EncryptedDnsExtension.DnsOverTlsOption,
        Curl.Extension.EncryptedDns.EncryptedDnsExtension.DnsOverQuicOption,
        HttpFile.HttpFileOptions.File,
        HttpFile.HttpFileOptions.Environment,
        OAuth2.Options.OAuth2Options.ClientCredentials,
        OAuth2.Options.OAuth2Options.Device,
        OAuth2.Options.OAuth2Options.Pkce,
        OAuth2.Options.OAuth2Options.ClientId,
        OAuth2.Options.OAuth2Options.ClientSecret,
        OAuth2.Options.OAuth2Options.Scope,
        OAuth2.Options.OAuth2Options.DeviceUrl,
        OAuth2.Options.OAuth2Options.AuthorizeUrl,
        OAuth2.Options.OAuth2Options.RedirectPort,
        OAuth2.Options.OAuth2Options.TokenCache,
        Curl.Extension.WebSocketRepl.WebSocketReplExtension.Option,
        OpenApi.OpenApiOptions.Specification,
        OpenApi.OpenApiOptions.Operation,
        OpenApi.OpenApiOptions.Server,
        Curl.Extension.ContentDigest.ContentDigestExtension.Option,
        Curl.Extension.ClientHints.ClientHintsExtension.Option,
        Curl.Extension.RateLimit.RateLimitExtension.Option,
    ];
}
