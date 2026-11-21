using Curl.Extension.Abstractions.Description;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.AzureStorage.Client;

/// <summary>The <c>az://</c> and <c>abfs://</c> extension's description and handler factory (ADR-1105).</summary>
public static class AzureStorageExtension
{
    /// <summary>The size of each Put Block or Path Update append, and the size from which an upload is sent in them: 8 MiB.</summary>
    internal const int DefaultBlockSize = 8 * 1024 * 1024;

    /// <summary>Gets what <c>curl --extended-help</c> and <c>curl --ai-help extensions</c> say of it.</summary>
    public static ExtensionDescription Description { get; } = new(
        "azure-storage",
        "Reads and writes Azure Blob Storage at az://account/container/blob (an Azurite-style emulator at an IP address or localhost: az://127.0.0.1:10000/account/container/blob) and Data Lake Storage Gen2 at abfs[s]://filesystem@account/path: a blob or file downloads, -T uploads it (in 8 MiB blocks from 8 MiB), -X DELETE on az:// deletes the blob (Delete Blob; a container URL or a name ending in / is refused), an empty name or one ending in / lists the names under it (a ?prefix=, ?delimiter= or ?maxresults= in the URL narrows the list; the URL's query is sent and signed with the request); --oauth2-bearer or AZURE_STORAGE_BEARER_TOKEN sends an Entra ID token, AZURE_STORAGE_SAS_TOKEN a shared access signature, and -u account:base64key signs each request with the account key (Shared Key; a bad key exits 67, a wrong one 22). An az://, abfs:// or abfss:// URL uses az_proxy, abfs_proxy or abfss_proxy respectively, else https_proxy or HTTPS_PROXY, else ALL_PROXY, as wss:// does (ADR-1297).",
        ["az", "abfs", "abfss"],
        [],
        new Uri("https://learn.microsoft.com/rest/api/storageservices/blob-service-rest-api"));

    /// <summary>Creates the handler over curl's connector, reading the Azure Storage tokens from the run's environment
    /// (<see cref="ProtocolHandlerServices.ReadEnvironmentVariable" />), else the process environment (BL-4567).</summary>
    /// <param name="services">Curl's connection services.</param>
    /// <returns>The handler.</returns>
    public static IProtocolHandler CreateHandler(ProtocolHandlerServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return new AzureStorageProtocolHandler(services.Connector, services.ReadEnvironmentVariable ?? Environment.GetEnvironmentVariable, DefaultBlockSize, AzureBlobOperations.MaxBlockCount);
    }
}
