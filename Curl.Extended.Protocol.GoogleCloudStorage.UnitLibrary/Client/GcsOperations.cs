using System.Globalization;
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
    /// <summary>
    /// Writes the object <paramref name="objectName"/> to <paramref name="output"/> (GET Object), or only the part
    /// <paramref name="resumeFrom"/> or <paramref name="rangeText"/> asks for, sent as a <c>Range</c> header and treated
    /// as over https: a resume answered 416, or answered whole when the object is exactly the size already held, writes
    /// nothing, and a resume answered whole otherwise fails with exit 33 (ADR-1359).
    /// </summary>
    /// <param name="objectName">The object name.</param>
    /// <param name="output">Where the object's bytes go.</param>
    /// <param name="resumeFrom">The offset <c>-C</c> resumes from; when above 0 it wins over <paramref name="rangeText"/>.</param>
    /// <param name="rangeText">The <c>-r</c> text, sent as <c>bytes=</c> and that text, or <see langword="null"/>.</param>
    /// <returns>The number of bytes written.</returns>
    internal Task<long> DownloadAsync(string objectName, Stream output, long? resumeFrom, string? rangeText)
    {
        long? offset = resumeFrom > 0 ? resumeFrom : null;
        string? range = offset is null
            ? rangeText is null ? null : "bytes=" + rangeText
            : string.Create(CultureInfo.InvariantCulture, $"bytes={offset}-");
        return client.ExchangeAsync("GET", GcsEndpoint.PathOf(bucket, objectName), [], null, response => ReadObjectAsync(response, output, offset), range, offset is not null);
    }

    /// <summary>Writes, one per line, the common prefixes and then the object names of each page of the listing under <paramref name="prefix"/> (List objects).</summary>
    /// <param name="prefix">The prefix the names start with, empty for the whole bucket.</param>
    /// <param name="output">Where the lines go.</param>
    /// <returns>The number of bytes written.</returns>
    /// <param name="urlQuery">The URL's query parameters: a <c>prefix</c> narrows the list further, a <c>delimiter</c> replaces <c>/</c>, any other parameter is passed on.</param>
    internal async Task<long> ListAsync(string prefix, IReadOnlyList<KeyValuePair<string, string>> urlQuery, Stream output)
    {
        long written = 0;
        string? continuationToken = null;
        do
        {
            GcsObjectListing listing = await client.ExchangeAsync("GET", GcsEndpoint.PathOf(bucket, string.Empty), ListQuery(prefix, urlQuery, continuationToken), null, ReadListingAsync);
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

    private static List<KeyValuePair<string, string>> ListQuery(string prefix, IReadOnlyList<KeyValuePair<string, string>> urlQuery, string? continuationToken)
    {
        string[] owned = ["list-type", "continuation-token", "prefix", "delimiter"];
        List<KeyValuePair<string, string>> query = [new("list-type", "2")];
        query.Add(urlQuery.FirstOrDefault(parameter => parameter.Key == "delimiter") is { Key: not null } delimiter ? delimiter : new("delimiter", "/"));
        query.Add(new("prefix", prefix + urlQuery.FirstOrDefault(parameter => parameter.Key == "prefix").Value));
        query.AddRange(urlQuery.Where(parameter => !owned.Contains(parameter.Key)));
        if (continuationToken is not null)
        {
            query.Add(new("continuation-token", continuationToken));
        }

        return query;
    }

    // A 416 reaches here only for a resume, which then has nothing to add.
    private async Task<long> ReadObjectAsync(GcsHttpResponse response, Stream output, long? resumeFrom)
    {
        if (response.StatusCode == GcsClient.RangeNotSatisfiable)
        {
            return 0;
        }

        return resumeFrom is { } offset && response.StatusCode != GcsClient.PartialContent
            ? NothingLeftOf(response, offset)
            : await CopyAsync(response.Body, output);
    }

    // A resume answered with the whole object: only one exactly the size already held has nothing to add, as over https.
    private static long NothingLeftOf(GcsHttpResponse response, long offset) =>
        long.TryParse(response.FindHeader("Content-Length"), CultureInfo.InvariantCulture, out long length) && length == offset
            ? 0
            : throw new GcsTransferException(CurlExitCode.RangeError, "HTTP server does not seem to support byte ranges. Cannot resume.");

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
