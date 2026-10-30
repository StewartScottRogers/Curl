using Curl.Extension.Abstractions.Description;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.OciRegistry.Client;

/// <summary>The <c>oci://</c> extension's description and handler factory (ADR-1093).</summary>
public static class OciRegistryExtension
{
    /// <summary>Gets what <c>curl --extensions</c> and <c>curl --ai-help extensions</c> say of it.</summary>
    public static ExtensionDescription Description { get; } = new(
        "oci",
        "Pulls from and pushes to an OCI or Docker registry over HTTPS: oci://registry[:port]/name[:tag] writes the manifest (tag latest when none is given), oci://registry[:port]/name@digest writes the blob; a 401 is answered with the registry's Bearer token, anonymous or for --user, and a blob's redirect to storage is followed. With -T it pushes instead: to name@digest as a blob through a monolithic upload session (skipped when the registry has it), to name[:tag] as a manifest with its mediaType.",
        ["oci"],
        [],
        new Uri("https://github.com/opencontainers/distribution-spec/blob/v1.1.0/spec.md"));

    /// <summary>Creates the handler over curl's connector.</summary>
    /// <param name="services">Curl's connection services.</param>
    /// <returns>The handler.</returns>
    public static IProtocolHandler CreateHandler(ProtocolHandlerServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return new OciRegistryProtocolHandler(services.Connector);
    }
}
