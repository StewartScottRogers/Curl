namespace Curl.Conformance.Extended.AzureStorage;

/// <summary>How an Azure Storage reference endpoint frames each response body (RFC 9112 section 6).</summary>
public enum AzureStorageResponseFraming
{
    /// <summary>A <c>Content-Length</c> field gives the body's length; the connection stays open.</summary>
    ContentLength,

    /// <summary><c>Transfer-Encoding: chunked</c>: the body is one chunk and the last chunk; the connection stays open.</summary>
    Chunked,

    /// <summary><c>Connection: close</c> and no length: the endpoint closes the connection after the body.</summary>
    Close,
}
