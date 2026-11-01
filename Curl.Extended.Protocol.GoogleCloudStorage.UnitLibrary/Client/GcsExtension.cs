using Curl.Extension.Abstractions.Description;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.GoogleCloudStorage.Client;

/// <summary>The <c>gs://</c> extension's description and handler factory (ADR-1099).</summary>
public static class GcsExtension
{
    /// <summary>Gets what <c>curl --extensions-help</c> and <c>curl --ai-help extensions</c> say of it.</summary>
    public static ExtensionDescription Description { get; } = new(
        "gs",
        "Reads and writes Google Cloud Storage objects at gs://bucket/object over the XML API: an object name downloads it, -T uploads it, an empty name or one ending in / lists the names under it; --oauth2-bearer TOKEN sends an OAuth access token, --user ACCESS_ID:SECRET signs with an HMAC key (GOOG4-HMAC-SHA256), and STORAGE_EMULATOR_HOST names an emulator.",
        ["gs"],
        [],
        new Uri("https://cloud.google.com/storage/docs/xml-api/overview"));

    /// <summary>Creates the handler over curl's connector, reading <c>STORAGE_EMULATOR_HOST</c> from the process environment.</summary>
    /// <param name="services">Curl's connection services.</param>
    /// <returns>The handler.</returns>
    public static IProtocolHandler CreateHandler(ProtocolHandlerServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return new GcsProtocolHandler(services.Connector, Environment.GetEnvironmentVariable);
    }
}
