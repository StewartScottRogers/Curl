namespace Curl.Extension.Protocol.AzureStorage.Bodies;

/// <summary>
/// One page of an ADLS Gen2 Path List response. The next page's continuation token is the
/// <c>x-ms-continuation</c> response header, which the caller reads; the body does not carry it.
/// </summary>
/// <param name="Paths">The paths on the page, in the order listed.</param>
public sealed record AzureStoragePathList(IReadOnlyList<AzureStoragePath> Paths);
