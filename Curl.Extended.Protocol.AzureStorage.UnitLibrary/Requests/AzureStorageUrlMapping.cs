namespace Curl.Extended.Protocol.AzureStorage.Requests;

/// <summary>
/// Maps <c>az://account/container/blob</c> to the Blob service endpoint ("Addressing Blob Service Resources") and
/// <c>abfs://filesystem@account.dfs.core.windows.net/path</c> (or <c>abfss://</c>) to the Data Lake Storage Gen2 DFS
/// endpoint (the ADLS Gen2 URI). A bare account name gets the public cloud's suffix; a host with a dot is used as it is.
/// </summary>
public static class AzureStorageUrlMapping
{
    /// <summary>The host suffix a bare account name gets for the Blob service.</summary>
    public const string BlobHostSuffix = ".blob.core.windows.net";

    /// <summary>The host suffix a bare account name gets for the DFS (Data Lake Storage Gen2) service.</summary>
    public const string DfsHostSuffix = ".dfs.core.windows.net";

    /// <summary>Maps an Azure Storage URL to the endpoint, path and query to request.</summary>
    /// <param name="url">The <c>az</c>, <c>abfs</c> or <c>abfss</c> URL.</param>
    /// <returns>The endpoint to request.</returns>
    /// <exception cref="AzureStorageRequestException">The scheme is not supported, or the URL names no account or no file system.</exception>
    public static AzureStorageEndpoint Map(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
        return url.Scheme switch
        {
            "az" => new AzureStorageEndpoint(AccountHost(url, BlobHostSuffix), url.AbsolutePath, Query(url)),
            "abfs" or "abfss" => MapDfs(url),
            _ => throw new AzureStorageRequestException(AzureStorageRequestFailure.UnsupportedScheme, $"Azure Storage does not handle the {url.Scheme} scheme."),
        };
    }

    private static AzureStorageEndpoint MapDfs(Uri url)
    {
        var host = AccountHost(url, DfsHostSuffix);
        var fileSystem = url.UserInfo;
        if (fileSystem.Length == 0)
        {
            throw new AzureStorageRequestException(AzureStorageRequestFailure.MissingFileSystem, $"The URL {url.OriginalString} names no file system before the @.");
        }

        var path = url.AbsolutePath == "/" ? string.Empty : url.AbsolutePath;
        return new AzureStorageEndpoint(host, "/" + fileSystem + path, Query(url));
    }

    private static string AccountHost(Uri url, string suffix)
    {
        var host = url.Host;
        if (host.Length == 0)
        {
            throw new AzureStorageRequestException(AzureStorageRequestFailure.MissingAccount, $"The URL {url.OriginalString} names no storage account.");
        }

        return host.Contains('.', StringComparison.Ordinal) ? host : host + suffix;
    }

    private static string Query(Uri url) => url.Query.TrimStart('?');
}
