using System.Globalization;
using Curl.Extension.AzureStorage.Bodies;
using Curl.Extension.AzureStorage.Http;
using Curl.Extension.AzureStorage.Requests;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.AzureStorage.Client;

/// <summary>
/// What an <c>abfs[s]://filesystem@account/path</c> URL does (ADR-1105), each as the Data Lake Storage Gen2 REST API names
/// it: Path Read, Path Create then a Path Update append per block and a Path Update flush, and Path List of one directory,
/// following the <c>x-ms-continuation</c> header to the last page.
/// </summary>
/// <param name="client">Sends each request.</param>
/// <param name="blockSize">The most bytes each append sends.</param>
internal sealed class AzureDataLakeOperations(AzureStorageClient client, int blockSize)
{
    /// <summary>Downloads, uploads or lists what <paramref name="endpoint"/> names.</summary>
    /// <param name="endpoint">The URL's endpoint, whose path is <c>/filesystem/path</c>.</param>
    /// <param name="upload">The bytes to upload, or <see langword="null"/> to download or list.</param>
    /// <param name="output">Where downloaded bytes or listed names go.</param>
    /// <returns>The number of bytes written or uploaded.</returns>
    internal Task<long> TransferAsync(AzureStorageEndpoint endpoint, Stream? upload, Stream output)
    {
        (string fileSystem, string path) = AzureStorageClient.Split(endpoint.Path);
        bool namesDirectory = path.Length == 0 || path.EndsWith('/');
        return upload switch
        {
            null when namesDirectory => ListAsync(endpoint, fileSystem, path.TrimEnd('/'), output),
            null => client.ExchangeAsync("GET", endpoint, [], null, response => client.CopyAsync(response.Body, output)),
            _ when namesDirectory => throw new AzureStorageTransferException(CurlExitCode.UrlMalformat, "An Azure Storage upload needs a file name in the URL"),
            { } bytes => UploadAsync(endpoint, bytes),
        };
    }

    private async Task<long> ListAsync(AzureStorageEndpoint endpoint, string fileSystem, string directory, Stream output)
    {
        long written = 0;
        string? continuation = null;
        string baseQuery = "resource=filesystem&recursive=false" + (directory.Length == 0 ? string.Empty : "&directory=" + Uri.EscapeDataString(directory));
        do
        {
            string query = baseQuery + (continuation is null ? string.Empty : "&continuation=" + Uri.EscapeDataString(continuation));
            (AzureStoragePathList list, continuation) = await client.ExchangeAsync("GET", AzureStorageClient.Operation(endpoint, "/" + Uri.EscapeDataString(fileSystem), query), [], null, ReadPathListAsync);
            written += await client.WriteLinesAsync(list.Paths.Select(path => path.IsDirectory ? path.Name + "/" : path.Name), output);
        }
        while (continuation is not null);
        return written;
    }

    private async Task<long> UploadAsync(AzureStorageEndpoint endpoint, Stream upload)
    {
        await client.ExchangeAsync("PUT", AzureStorageClient.Operation(endpoint, endpoint.Path, "resource=file"), [], [], _ => Task.FromResult(0));
        long position = 0;
        for (byte[] block = await client.ReadBlockAsync(upload, blockSize); block.Length > 0; block = await client.ReadBlockAsync(upload, blockSize))
        {
            await client.ExchangeAsync("PATCH", AzureStorageClient.Operation(endpoint, endpoint.Path, "action=append&position=" + Position(position)), [], block, _ => Task.FromResult(0));
            position += block.Length;
        }

        return await client.ExchangeAsync("PATCH", AzureStorageClient.Operation(endpoint, endpoint.Path, "action=flush&position=" + Position(position)), [], [], _ => Task.FromResult(position));
    }

    private static string Position(long position) => position.ToString(CultureInfo.InvariantCulture);

    private async Task<(AzureStoragePathList List, string? Continuation)> ReadPathListAsync(AzureStorageHttpResponse response)
    {
        string? continuation = response.FindHeader("x-ms-continuation");
        AzureStoragePathList list = AzureStorageClient.ValueOf(AzureStoragePathListParser.Parse(await client.ReadTextAsync(response)));
        return (list, string.IsNullOrEmpty(continuation) ? null : continuation);
    }
}
