namespace Curl.Extension.Protocol.AzureStorage.Requests;

/// <summary>Why an Azure Storage URL could not be mapped to a request, or a request could not be authorized.</summary>
public enum AzureStorageRequestFailure
{
    /// <summary>The URL's scheme is not <c>az</c>, <c>abfs</c> or <c>abfss</c>.</summary>
    UnsupportedScheme,

    /// <summary>The URL names no storage account: <c>az:///container</c>, or an <c>abfs</c> URL with no host.</summary>
    MissingAccount,

    /// <summary>An <c>abfs</c> URL names no file system before the <c>@</c> (the ADLS Gen2 URI, <c>abfs://filesystem@account.dfs.core.windows.net/path</c>).</summary>
    MissingFileSystem,

    /// <summary>The shared access signature is not a query string of <c>name=value</c> pairs carrying a <c>sig</c> ("Create a service SAS").</summary>
    InvalidSasToken,

    /// <summary>The bearer token is empty or holds a character outside RFC 6750 §2.1's visible ASCII.</summary>
    InvalidBearerToken,
}
