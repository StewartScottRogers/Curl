using System.Text;
using Curl.Extended.Protocol.GoogleCloudStorage.Http;
using Curl.Extended.Protocol.GoogleCloudStorage.Xml;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.GoogleCloudStorage.Client;

/// <summary>
/// The three things a <c>gs://</c> URL does (ADR-1099), each as the Cloud Storage XML API names it: "GET Object",
/// "PUT Object" in one request, and "List objects" (<c>list-type=2</c>) with the <c>/</c> delimiter, following
/// continuation tokens to the last page.
/// </summary>
/// <param name="client">Sends each request.</param>
/// <param name="bucket">The bucket.</param>
/// <param name="cancellationToken">Cancels the transfer.</param>
internal sealed class GcsOperations(GcsClient client, string bucket, CancellationToken cancellationToken)
{
    /// <summary>Writes the object <paramref name="objectName"/> to <paramref name="output"/> (GET Object).</summary>
    /// <param name="objectName">The object name.</param>
    /// <param name="output">Where the object's bytes go.</param>
    /// <returns>The number of bytes written.</returns>
    internal Task<long> DownloadAsync(string objectName, Stream output) =>
        client.ExchangeAsync("GET", GcsEndpoint.PathOf(bucket, objectName), [], null, response => CopyAsync(response.Body, output));

    /// <summary>Writes, one per line, the common prefixes and then the object names of each page of the listing under <paramref name="prefix"/> (List objects).</summary>
    /// <param name="prefix">The prefix the names start with, empty for the whole bucket.</param>
    /// <param name="output">Where the lines go.</param>
    /// <returns>The number of bytes written.</returns>
    internal async Task<long> ListAsync(string prefix, Stream output)
    {
        long written = 0;
        string? continuationToken = null;
        do
        {
            GcsObjectListing listing = await client.ExchangeAsync("GET", GcsEndpoint.PathOf(bucket, string.Empty), ListQuery(prefix, continuationToken), null, ReadListingAsync);
            byte[] lines = Encoding.UTF8.GetBytes(string.Concat(listing.CommonPrefixes.Concat(listing.Objects.Select(listed => listed.Key)).Select(name => name + "\n")));
            await output.WriteAsync(lines, cancellationToken);
            written += lines.Length;
            continuationToken = listing.IsTruncated ? listing.ContinuationValue : null;
        }
        while (continuationToken is not null);
        return written;
    }

    /// <summary>Uploads <paramref name="upload"/> as the object <paramref name="objectName"/> in one PUT Object, read whole first so its SHA-256 can be signed.</summary>
    /// <param name="objectName">The object name.</param>
    /// <param name="upload">The bytes to upload.</param>
    /// <returns>The number of bytes uploaded.</returns>
    internal async Task<long> UploadAsync(string objectName, Stream upload)
    {
        using MemoryStream buffered = new();
        await upload.CopyToAsync(buffered, cancellationToken);
        byte[] body = buffered.ToArray();
        return await client.ExchangeAsync("PUT", GcsEndpoint.PathOf(bucket, objectName), [], body, _ => Task.FromResult((long)body.Length));
    }

    private static KeyValuePair<string, string>[] ListQuery(string prefix, string? continuationToken) =>
        continuationToken is null
            ? [new("list-type", "2"), new("delimiter", "/"), new("prefix", prefix)]
            : [new("list-type", "2"), new("delimiter", "/"), new("prefix", prefix), new("continuation-token", continuationToken)];

    private async Task<long> CopyAsync(Stream source, Stream output)
    {
        byte[] buffer = new byte[81920];
        long copied = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            copied += read;
        }

        return copied;
    }

    private async Task<GcsObjectListing> ReadListingAsync(GcsHttpResponse response)
    {
        GcsXmlParseResult<GcsObjectListing> result = GcsListBucketResultParser.Parse(await client.ReadTextAsync(response));
        return result.Value ?? throw new GcsTransferException(CurlExitCode.WeirdServerReply, $"Cloud Storage sent a body curl cannot read: {result.Failure}");
    }
}
