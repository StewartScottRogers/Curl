using Curl.Extension.Abstractions.Description;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.AzureStorage.Client;

/// <summary>The <c>az://</c> and <c>abfs://</c> extension's description and handler factory (ADR-1105).</summary>
public static class AzureStorageExtension
{
    /// <summary>The size of each Put Block or Path Update append, and the size from which an upload is sent in them: 8 MiB.</summary>
    internal const int DefaultBlockSize = 8 * 1024 * 1024;

    /// <summary>Gets what <c>curl --extensions-help</c> and <c>curl --ai-help extensions</c> say of it.</summary>
    public static ExtensionDescription Description { get; } = new(
        "azure-storage",
        "Reads and writes Azure Blob Storage at az://account/container/blob and Data Lake Storage Gen2 at abfs[s]://filesystem@account/path: a blob or file downloads, -T uploads it (in 8 MiB blocks from 8 MiB), an empty name or one ending in / lists the names under it; --oauth2-bearer or AZURE_STORAGE_BEARER_TOKEN sends an Entra ID token, AZURE_STORAGE_SAS_TOKEN a shared access signature.",
        ["az", "abfs", "abfss"],
        [],
        new Uri("https://learn.microsoft.com/rest/api/storageservices/blob-service-rest-api"));

    /// <summary>Creates the handler over curl's connector, reading the Azure Storage tokens from the process environment.</summary>
    /// <param name="services">Curl's connection services.</param>
    /// <returns>The handler.</returns>
    public static IProtocolHandler CreateHandler(ProtocolHandlerServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return new AzureStorageProtocolHandler(services.Connector, Environment.GetEnvironmentVariable, DefaultBlockSize);
    }
}
