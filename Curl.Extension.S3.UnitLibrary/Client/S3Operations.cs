using System.Globalization;
using System.Text;
using Curl.Extension.S3.Http;
using Curl.Extension.S3.Xml;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.S3.Client;

/// <summary>
/// The three things an <c>s3://</c> URL does (ADR-1090), each as the Amazon S3 API reference names it: GetObject, PutObject
/// or a multipart upload (CreateMultipartUpload, UploadPart, CompleteMultipartUpload) once the upload fills a part, and
/// ListObjectsV2 with the <c>/</c> delimiter, following continuation tokens to the last page.
/// </summary>
/// <param name="client">Sends each request.</param>
/// <param name="endpoint">Names the paths.</param>
/// <param name="bucket">The bucket.</param>
/// <param name="partSize">The size of each part, and the size from which an upload is sent in parts.</param>
/// <param name="cancellationToken">Cancels the transfer.</param>
internal sealed class S3Operations(S3Client client, S3Endpoint endpoint, string bucket, int partSize, CancellationToken cancellationToken)
{
    /// <summary>Writes the object at <paramref name="key"/> to <paramref name="output"/> (GetObject).</summary>
    /// <param name="key">The object key.</param>
    /// <param name="output">Where the object's bytes go.</param>
    /// <returns>The number of bytes written.</returns>
    internal Task<long> DownloadAsync(string key, Stream output) =>
        client.ExchangeAsync("GET", endpoint.PathOf(bucket, key), [], null, response => CopyAsync(response.Body, output));

    /// <summary>Writes, one per line, the common prefixes and then the keys of each page of the listing under <paramref name="prefix"/> (ListObjectsV2).</summary>
    /// <param name="prefix">The prefix the keys start with, empty for the whole bucket.</param>
    /// <param name="output">Where the lines go.</param>
    /// <returns>The number of bytes written.</returns>
    internal async Task<long> ListAsync(string prefix, Stream output)
    {
        long written = 0;
        string? continuationToken = null;
        do
        {
            S3ObjectListing listing = await client.ExchangeAsync("GET", endpoint.PathOf(bucket, string.Empty), ListQuery(prefix, continuationToken), null, ReadListingAsync);
            byte[] lines = Encoding.UTF8.GetBytes(string.Concat(listing.CommonPrefixes.Concat(listing.Objects.Select(listed => listed.Key)).Select(name => name + "\n")));
            await output.WriteAsync(lines, cancellationToken);
            written += lines.Length;
            continuationToken = listing.IsTruncated ? listing.NextContinuationToken : null;
        }
        while (continuationToken is not null);
        return written;
    }

    /// <summary>
    /// Uploads <paramref name="upload"/> to <paramref name="key"/>: in one PutObject when it is shorter than a part, otherwise in
    /// parts of <c>partSize</c> bytes, the last one shorter.
    /// </summary>
    /// <param name="key">The object key.</param>
    /// <param name="upload">The bytes to upload.</param>
    /// <returns>The number of bytes uploaded.</returns>
    internal async Task<long> UploadAsync(string key, Stream upload)
    {
        string path = endpoint.PathOf(bucket, key);
        byte[] first = await ReadPartAsync(upload);
        if (first.Length < partSize)
        {
            return await client.ExchangeAsync("PUT", path, [], first, _ => Task.FromResult((long)first.Length));
        }

        S3MultipartUpload started = await client.ExchangeAsync("POST", path, [new("uploads", string.Empty)], [], ReadInitiatedAsync);
        List<S3UploadedPart> parts = [];
        long sent = 0;
        for (byte[] part = first; part.Length > 0; part = await ReadPartAsync(upload))
        {
            int number = parts.Count + 1;
            KeyValuePair<string, string>[] query = [new("partNumber", number.ToString(CultureInfo.InvariantCulture)), new("uploadId", started.UploadId)];
            parts.Add(new S3UploadedPart(number, await client.ExchangeAsync("PUT", path, query, part, ETagOf)));
            sent += part.Length;
        }

        byte[] completion = Encoding.UTF8.GetBytes(S3CompleteMultipartUploadWriter.Write(parts));
        return await client.ExchangeAsync("POST", path, [new("uploadId", started.UploadId)], completion, response => ReadCompletedAsync(response, sent));
    }

    private static KeyValuePair<string, string>[] ListQuery(string prefix, string? continuationToken) =>
        continuationToken is null
            ? [new("list-type", "2"), new("delimiter", "/"), new("prefix", prefix)]
            : [new("list-type", "2"), new("delimiter", "/"), new("prefix", prefix), new("continuation-token", continuationToken)];

    private static Task<string> ETagOf(S3HttpResponse response) =>
        Task.FromResult(response.FindHeader("ETag") ?? throw new S3TransferException(CurlExitCode.WeirdServerReply, "S3 sent no ETag for an uploaded part"));

    private static T ValueOf<T>(S3XmlParseResult<T> result)
        where T : class =>
        result.Value ?? throw new S3TransferException(CurlExitCode.WeirdServerReply, $"S3 sent a body curl cannot read: {result.Failure}");

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

    private async Task<byte[]> ReadPartAsync(Stream upload)
    {
        byte[] buffer = new byte[partSize];
        int read = await upload.ReadAtLeastAsync(buffer, partSize, throwOnEndOfStream: false, cancellationToken);
        return buffer[..read];
    }

    private async Task<S3ObjectListing> ReadListingAsync(S3HttpResponse response) =>
        ValueOf(S3ListBucketResultParser.Parse(await client.ReadTextAsync(response)));

    private async Task<S3MultipartUpload> ReadInitiatedAsync(S3HttpResponse response) =>
        ValueOf(S3InitiateMultipartUploadResultParser.Parse(await client.ReadTextAsync(response)));

    // CompleteMultipartUpload can answer 200 OK with an Error body (API reference, CompleteMultipartUpload, "Description").
    private async Task<long> ReadCompletedAsync(S3HttpResponse response, long sent)
    {
        string body = await client.ReadTextAsync(response);
        return S3ErrorResponseParser.Parse(body).Succeeded ? throw S3Client.ErrorOf(response.StatusCode, body) : sent;
    }
}
