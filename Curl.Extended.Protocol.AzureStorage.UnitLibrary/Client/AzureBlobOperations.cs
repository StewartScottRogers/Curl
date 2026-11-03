using System.Globalization;
using System.Text;
using Curl.Extended.Protocol.AzureStorage.Bodies;
using Curl.Extended.Protocol.AzureStorage.Http;
using Curl.Extended.Protocol.AzureStorage.Requests;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.AzureStorage.Client;

/// <summary>
/// What an <c>az://account/container/blob</c> URL does (ADR-1105), each as the Blob service REST API names it: Get Blob,
/// Put Blob or, once the upload fills a block, Put Block for each block and Put Block List, and List Blobs with the
/// <c>/</c> delimiter, following <c>NextMarker</c> to the last page.
/// </summary>
/// <param name="client">Sends each request.</param>
/// <param name="blockSize">The size of each block, and the size from which an upload is sent in blocks.</param>
/// <param name="maxBlockCount">The most blocks a blob is committed from (ADR-1213).</param>
internal sealed class AzureBlobOperations(AzureStorageClient client, int blockSize, int maxBlockCount)
{
    /// <summary>The most blocks Put Block List commits a blob from (Blob service REST API 2025-01-05).</summary>
    internal const int MaxBlockCount = 50_000;

    /// <summary>
    /// Gives the block size an upload of <paramref name="length"/> bytes is sent in: <paramref name="blockSize"/>, or the
    /// smallest size that keeps it within <paramref name="maxBlockCount"/> blocks (ADR-1213).
    /// </summary>
    /// <param name="length">The bytes left to upload.</param>
    /// <param name="blockSize">The block size an upload starts from.</param>
    /// <param name="maxBlockCount">The most blocks the blob may be committed from.</param>
    /// <returns>The block size.</returns>
    /// <exception cref="AzureStorageTransferException">Exit 63 when even the largest block an array holds needs more blocks.</exception>
    internal static int BlockSizeFor(long length, int blockSize, int maxBlockCount)
    {
        long needed = Math.Max(blockSize, (length + maxBlockCount - 1) / maxBlockCount);
        return needed <= Array.MaxLength
            ? (int)needed
            : throw new AzureStorageTransferException(CurlExitCode.FilesizeExceeded, $"An Azure Storage upload of {length} bytes needs more than {maxBlockCount} blocks");
    }
    /// <summary>Downloads, uploads or lists what <paramref name="endpoint"/> names.</summary>
    /// <param name="endpoint">The URL's endpoint, whose path is <c>/container/blob</c>.</param>
    /// <param name="upload">The bytes to upload, or <see langword="null"/> to download or list.</param>
    /// <param name="output">Where downloaded bytes or listed names go.</param>
    /// <returns>The number of bytes written or uploaded.</returns>
    internal Task<long> TransferAsync(AzureStorageEndpoint endpoint, Stream? upload, Stream output)
    {
        (string container, string blob) = AzureStorageClient.Split(endpoint.Path);
        if (container.Length == 0)
        {
            throw new AzureStorageTransferException(CurlExitCode.UrlMalformat, "An az:// URL needs a container after the account");
        }

        bool namesPrefix = blob.Length == 0 || blob.EndsWith('/');
        return upload switch
        {
            null when namesPrefix => ListAsync(endpoint, container, blob, output),
            null => client.ExchangeAsync("GET", endpoint, [], null, response => client.CopyAsync(response.Body, output)),
            _ when namesPrefix => throw new AzureStorageTransferException(CurlExitCode.UrlMalformat, "An Azure Storage upload needs a blob name in the URL"),
            { } bytes => UploadAsync(endpoint, bytes),
        };
    }

    private async Task<long> ListAsync(AzureStorageEndpoint endpoint, string container, string prefix, Stream output)
    {
        long written = 0;
        string? marker = null;
        do
        {
            string query = $"restype=container&comp=list&delimiter=%2F&prefix={Uri.EscapeDataString(prefix)}" + (marker is null ? string.Empty : $"&marker={Uri.EscapeDataString(marker)}");
            AzureBlobListing listing = await client.ExchangeAsync("GET", AzureStorageClient.Operation(endpoint, "/" + Uri.EscapeDataString(container), query), [], null, ReadListingAsync);
            written += await client.WriteLinesAsync(listing.BlobPrefixes.Concat(listing.Blobs.Select(blob => blob.Name)), output);
            marker = listing.NextMarker;
        }
        while (marker is not null);
        return written;
    }

    private async Task<long> UploadAsync(AzureStorageEndpoint endpoint, Stream upload)
    {
        int size = upload.CanSeek ? BlockSizeFor(upload.Length - upload.Position, blockSize, maxBlockCount) : blockSize;
        byte[] first = await client.ReadBlockAsync(upload, size);
        if (first.Length < size)
        {
            return await client.ExchangeAsync("PUT", endpoint, [new("x-ms-blob-type", "BlockBlob")], first, _ => Task.FromResult((long)first.Length));
        }

        List<AzureBlockListEntry> blocks = [];
        long sent = 0;
        for (byte[] block = first; block.Length > 0; block = await client.ReadBlockAsync(upload, size))
        {
            // An upload of unknown length stops before the block that would take the blob past the limit (ADR-1213).
            if (blocks.Count == maxBlockCount)
            {
                throw new AzureStorageTransferException(CurlExitCode.FilesizeExceeded, $"An Azure Storage upload needs more than {maxBlockCount} blocks of {size} bytes");
            }

            // Every block ID of a blob must have the same length (Put Block, "URI parameters"), so the index is zero-padded.
            string blockId = Convert.ToBase64String(Encoding.ASCII.GetBytes(blocks.Count.ToString("D6", CultureInfo.InvariantCulture)));
            await client.ExchangeAsync("PUT", AzureStorageClient.Operation(endpoint, endpoint.Path, "comp=block&blockid=" + Uri.EscapeDataString(blockId)), [], block, _ => Task.FromResult(0));
            blocks.Add(new AzureBlockListEntry(blockId, AzureBlockListSource.Latest));
            sent += block.Length;
        }

        byte[] blockList = Encoding.UTF8.GetBytes(AzureBlockListWriter.Write(blocks));
        return await client.ExchangeAsync("PUT", AzureStorageClient.Operation(endpoint, endpoint.Path, "comp=blocklist"), [], blockList, _ => Task.FromResult(sent));
    }

    private async Task<AzureBlobListing> ReadListingAsync(AzureStorageHttpResponse response) =>
        AzureStorageClient.ValueOf(AzureBlobListingParser.Parse(await client.ReadTextAsync(response)));
}
