using System.Text;
using System.Xml.Linq;
using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.GoogleCloudStorage;

/// <summary>
/// A Cloud Storage XML API endpoint written from the specification (cloud.google.com/storage/docs/xml-api), path-style
/// (<c>/bucket/object</c>): GET Object, PUT Object and List Objects (<c>list-type=2</c> with <c>prefix</c>, <c>delimiter</c> and
/// <c>continuation-token</c>, answering <c>ListBucketResult</c>), with the XML API error responses. Every request is first put
/// to its <see cref="IGcsReferenceAuthorizer" />, then must name a <c>Host</c> header and a bucket it holds.
/// </summary>
/// <param name="authorizer">Decides which requests it serves.</param>
/// <param name="pageSize">The most entries (objects and common prefixes) one List Objects page holds.</param>
/// <param name="pagination">How a truncated listing names the next page.</param>
public sealed class GcsReferenceServer(IGcsReferenceAuthorizer authorizer, int pageSize = 1000, GcsListPagination pagination = GcsListPagination.NextContinuationToken) : IReferenceServer
{
    private static readonly XNamespace ListNamespace = "http://doc.s3.amazonaws.com/2006-03-01";

    private readonly Dictionary<string, SortedDictionary<string, byte[]>> buckets = new(StringComparer.Ordinal);

    /// <summary>Gets the requests served, in the order they arrived.</summary>
    public IList<GcsReferenceRequest> Requests { get; } = [];

    /// <summary>Stores an object, making its bucket when it has none.</summary>
    /// <param name="bucket">The bucket name.</param>
    /// <param name="objectName">The object name.</param>
    /// <param name="content">The object's bytes.</param>
    public void PutObject(string bucket, string objectName, byte[] content)
    {
        ArgumentNullException.ThrowIfNull(bucket);
        if (!buckets.TryGetValue(bucket, out SortedDictionary<string, byte[]>? objects))
        {
            objects = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
            buckets[bucket] = objects;
        }

        objects[objectName] = content;
    }

    /// <summary>Gives a stored object's bytes.</summary>
    /// <param name="bucket">The bucket name.</param>
    /// <param name="objectName">The object name.</param>
    /// <returns>The bytes, or <see langword="null" /> when no such object is stored.</returns>
    public byte[]? GetObject(string bucket, string objectName) =>
        buckets.GetValueOrDefault(bucket)?.GetValueOrDefault(objectName);

    /// <inheritdoc />
    public async Task ServeAsync(ReferenceConnection connection, CancellationToken cancellationToken)
    {
        GcsReferenceRequestReader reader = new(connection);
        while (await reader.ReadAsync(cancellationToken) is { } request)
        {
            Requests.Add(request);
            await connection.WriteAsync(Answer(request).ToBytes(), cancellationToken);
        }
    }

    /// <summary>Answers one request.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The response.</returns>
    internal GcsReferenceResponse Answer(GcsReferenceRequest request)
    {
        if (authorizer.Refuse(request) is { } refusal)
        {
            return refusal;
        }

        if (!request.Headers.ContainsKey("Host"))
        {
            return GcsReferenceResponse.Error(400, "Bad Request", "InvalidArgument", "Invalid argument.", "The request has no Host header.");
        }

        string[] bucketAndObject = request.Path.TrimStart('/').Split('/', 2);
        string bucket = bucketAndObject[0];
        string objectName = bucketAndObject.ElementAtOrDefault(1) ?? string.Empty;
        return buckets.TryGetValue(bucket, out SortedDictionary<string, byte[]>? objects)
            ? AnswerInBucket(request, bucket, objectName, objects)
            : GcsReferenceResponse.Error(404, "Not Found", "NoSuchBucket", "The specified bucket does not exist.", $"No such bucket: {bucket}");
    }

    private GcsReferenceResponse AnswerInBucket(GcsReferenceRequest request, string bucket, string objectName, SortedDictionary<string, byte[]> objects) =>
        (request.Method, objectName.Length) switch
        {
            ("GET", 0) => List(request, bucket, objects),
            ("GET", _) => objects.TryGetValue(objectName, out byte[]? content)
                ? GcsReferenceResponse.Ok("application/octet-stream", content)
                : GcsReferenceResponse.Error(404, "Not Found", "NoSuchKey", "The specified key does not exist.", $"No such object: {bucket}/{objectName}"),
            ("PUT", _) => Put(request, objectName, objects),
            _ => GcsReferenceResponse.Error(405, "Method Not Allowed", "MethodNotAllowed", "The specified method is not allowed against this resource.", request.Method),
        };

    private static GcsReferenceResponse Put(GcsReferenceRequest request, string objectName, SortedDictionary<string, byte[]> objects)
    {
        if (request.ContentLength is null || objectName.Length == 0)
        {
            return GcsReferenceResponse.Error(411, "Length Required", "MissingContentLength", "You must provide the Content-Length HTTP header.", "PUT Object needs an object name and a Content-Length.");
        }

        objects[objectName] = request.Body;
        return GcsReferenceResponse.Ok("text/html; charset=UTF-8", []);
    }

    private GcsReferenceResponse List(GcsReferenceRequest request, string bucket, SortedDictionary<string, byte[]> objects)
    {
        string prefix = request.Query.GetValueOrDefault("prefix") ?? string.Empty;
        string delimiter = request.Query.GetValueOrDefault("delimiter") ?? string.Empty;
        string[] entries = EntriesAfter(request, objects, prefix, delimiter);
        string[] page = [.. entries.Take(pageSize)];
        bool isTruncated = entries.Length > page.Length;
        return GcsReferenceResponse.Ok("application/xml; charset=UTF-8", GcsReferenceResponse.XmlBody(ListBucketResultOf(bucket, prefix, delimiter, page, isTruncated, objects)));
    }

    private XElement ListBucketResultOf(string bucket, string prefix, string delimiter, string[] page, bool isTruncated, SortedDictionary<string, byte[]> objects) =>
        new(
            ListNamespace + "ListBucketResult",
            new XElement(ListNamespace + "Name", bucket),
            new XElement(ListNamespace + "Prefix", prefix),
            new XElement(ListNamespace + "Delimiter", delimiter),
            new XElement(ListNamespace + "KeyCount", page.Length),
            new XElement(ListNamespace + "MaxKeys", pageSize),
            new XElement(ListNamespace + "IsTruncated", isTruncated ? "true" : "false"),
            page.Where(entry => objects.ContainsKey(entry)).Select(name => ContentsOf(name, objects[name])),
            page.Where(entry => !objects.ContainsKey(entry)).Select(commonPrefix => new XElement(ListNamespace + "CommonPrefixes", new XElement(ListNamespace + "Prefix", commonPrefix))),
            isTruncated ? NextPageOf(page[^1]) : null);

    private string[] EntriesAfter(GcsReferenceRequest request, SortedDictionary<string, byte[]> objects, string prefix, string delimiter)
    {
        string after = request.Query.TryGetValue("continuation-token", out string? token) ? NameOf(token) : string.Empty;
        return [.. objects.Keys.Where(name => name.StartsWith(prefix, StringComparison.Ordinal))
            .Select(name => EntryOf(name, prefix, delimiter)).Distinct().Where(entry => string.CompareOrdinal(entry, after) > 0)];
    }

    private static string EntryOf(string name, string prefix, string delimiter)
    {
        int end = delimiter.Length == 0 ? -1 : name.IndexOf(delimiter, prefix.Length, StringComparison.Ordinal);
        return end < 0 ? name : name[..(end + delimiter.Length)];
    }

    private static XElement ContentsOf(string name, byte[] content) =>
        new(
            ListNamespace + "Contents",
            new XElement(ListNamespace + "Key", name),
            new XElement(ListNamespace + "LastModified", "2026-01-01T00:00:00.000Z"),
            new XElement(ListNamespace + "ETag", "\"" + Convert.ToHexStringLower(System.Security.Cryptography.MD5.HashData(content)) + "\""),
            new XElement(ListNamespace + "Size", content.Length));

    private XElement NextPageOf(string lastEntry) =>
        pagination == GcsListPagination.NextMarker
            ? new XElement(ListNamespace + "NextMarker", lastEntry)
            : new XElement(ListNamespace + "NextContinuationToken", Convert.ToBase64String(Encoding.UTF8.GetBytes(lastEntry)));

    private string NameOf(string token) =>
        pagination == GcsListPagination.NextMarker ? token : Encoding.UTF8.GetString(Convert.FromBase64String(token));
}
