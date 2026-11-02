using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.S3;

/// <summary>
/// A reference Amazon S3 endpoint written from the S3 API reference: GetObject, PutObject,
/// CreateMultipartUpload, UploadPart, CompleteMultipartUpload and ListObjectsV2 (<c>prefix</c>,
/// <c>delimiter</c>, <c>continuation-token</c>) over in-memory buckets, answering a failure with an
/// <c>Error</c> body. It serves one request per connection, virtual-hosted or path style, and
/// records every request it read.
/// </summary>
/// <param name="serviceHost">The service host a virtual-hosted bucket is a label of, such as <c>s3.us-east-1.amazonaws.com</c>.</param>
/// <param name="authorizer">What checks each request first; <see cref="S3WellFormedAuthorization" /> when <see langword="null" />.</param>
public sealed class S3ReferenceEndpoint(string serviceHost, IS3RequestAuthorizer? authorizer = null) : IReferenceServer
{
    private const string RequestId = "4442587FB7D0A2F9";

    private static readonly XNamespace S3Namespace = "http://s3.amazonaws.com/doc/2006-03-01/";

    private static readonly S3ReferenceError InternalError = new(500, "InternalError", "We encountered an internal error. Please try again.");

    private readonly IS3RequestAuthorizer requestAuthorizer = authorizer ?? new S3WellFormedAuthorization();

    private readonly Lock gate = new();

    private readonly Dictionary<string, SortedDictionary<string, byte[]>> buckets = new(StringComparer.Ordinal);

    private readonly Dictionary<string, SortedDictionary<int, byte[]>> partsByUploadId = new(StringComparer.Ordinal);

    private readonly List<S3ReferenceRequest> requests = [];

    /// <summary>Gets the most entries (keys and common prefixes) one ListObjectsV2 page holds; 1000, S3's default, unless set.</summary>
    public int ListPageSize { get; init; } = 1000;

    /// <summary>Gets the part number whose UploadPart fails with <c>500 InternalError</c>; none when <see langword="null" />.</summary>
    public int? FailingPartNumber { get; init; }

    /// <summary>Gets whether CompleteMultipartUpload answers <c>200 OK</c> with an <c>Error</c> body, as the API reference says it can.</summary>
    public bool CompletionAnswersError { get; init; }

    /// <summary>Gets every request read, in order.</summary>
    public IReadOnlyList<S3ReferenceRequest> Requests
    {
        get
        {
            lock (gate)
            {
                return [.. requests];
            }
        }
    }

    /// <summary>Creates an empty bucket.</summary>
    /// <param name="bucket">The bucket name.</param>
    public void CreateBucket(string bucket)
    {
        lock (gate)
        {
            buckets[bucket] = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
        }
    }

    /// <summary>Stores an object in a bucket <see cref="CreateBucket" /> made.</summary>
    /// <param name="bucket">The bucket name.</param>
    /// <param name="key">The object key.</param>
    /// <param name="content">The object's bytes.</param>
    public void PutObject(string bucket, string key, byte[] content)
    {
        lock (gate)
        {
            buckets[bucket][key] = content;
        }
    }

    /// <summary>Finds an object.</summary>
    /// <param name="bucket">The bucket name.</param>
    /// <param name="key">The object key.</param>
    /// <returns>The object's bytes; <see langword="null" /> when the bucket holds no such key.</returns>
    public byte[]? FindObject(string bucket, string key)
    {
        lock (gate)
        {
            return buckets[bucket].GetValueOrDefault(key);
        }
    }

    /// <inheritdoc />
    public async Task ServeAsync(ReferenceConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        S3ReferenceRequest request = await S3ReferenceRequestReader.ReadAsync(connection, serviceHost, cancellationToken).ConfigureAwait(false);
        S3ReferenceResponse response;
        lock (gate)
        {
            requests.Add(request);
            response = Answer(request);
        }

        await connection.WriteAsync(response.ToBytes(), cancellationToken).ConfigureAwait(false);
    }

    private static byte[] XmlBytes(XElement root) =>
        Encoding.UTF8.GetBytes("<?xml version=\"1.0\" encoding=\"UTF-8\"?>" + root.ToString(SaveOptions.DisableFormatting));

    private static S3ReferenceResponse Ok(XElement root) => new(200, null, XmlBytes(root));

    private static S3ReferenceResponse ErrorResponse(S3ReferenceError error, S3ReferenceRequest request, int? statusCode = null) =>
        new(statusCode ?? error.StatusCode, null, XmlBytes(new XElement(
            "Error",
            new XElement("Code", error.Code),
            new XElement("Message", error.Message),
            new XElement("Resource", "/" + request.Bucket + "/" + request.Key),
            new XElement("RequestId", RequestId))));

    private static string ETagOf(byte[] content) => "\"" + Convert.ToHexStringLower(MD5.HashData(content)) + "\"";

    private static S3ReferenceResponse GetObject(S3ReferenceRequest request, SortedDictionary<string, byte[]> objects) =>
        objects.TryGetValue(request.Key, out byte[]? content)
            ? new S3ReferenceResponse(200, ETagOf(content), content)
            : ErrorResponse(new S3ReferenceError(404, "NoSuchKey", "The specified key does not exist."), request);

    private static S3ReferenceResponse PutObject(S3ReferenceRequest request, SortedDictionary<string, byte[]> objects)
    {
        objects[request.Key] = request.Body;
        return new S3ReferenceResponse(200, ETagOf(request.Body), []);
    }

    // A key with the delimiter after the prefix rolls up into the common prefix ending at that delimiter (ListObjectsV2, "delimiter").
    private static string RollUp(string key, string prefix, string delimiter)
    {
        int at = delimiter.Length == 0 ? -1 : key.IndexOf(delimiter, prefix.Length, StringComparison.Ordinal);
        return at < 0 ? key : key[..(at + delimiter.Length)];
    }

    private S3ReferenceResponse Answer(S3ReferenceRequest request)
    {
        if (requestAuthorizer.Authorize(request) is { } denied)
        {
            return ErrorResponse(denied, request);
        }

        if (!buckets.TryGetValue(request.Bucket, out SortedDictionary<string, byte[]>? objects))
        {
            return ErrorResponse(new S3ReferenceError(404, "NoSuchBucket", "The specified bucket does not exist."), request);
        }

        return AnswerOperation(request, objects);
    }

    private S3ReferenceResponse AnswerOperation(S3ReferenceRequest request, SortedDictionary<string, byte[]> objects) =>
        request.Method switch
        {
            "GET" => AnswerGet(request, objects),
            "PUT" => AnswerPut(request, objects),
            "POST" => AnswerPost(request, objects),
            _ => ErrorResponse(new S3ReferenceError(405, "MethodNotAllowed", "The specified method is not allowed against this resource."), request),
        };

    private S3ReferenceResponse AnswerGet(S3ReferenceRequest request, SortedDictionary<string, byte[]> objects) =>
        request.Key.Length == 0 ? ListObjectsV2(request, objects) : GetObject(request, objects);

    private S3ReferenceResponse AnswerPut(S3ReferenceRequest request, SortedDictionary<string, byte[]> objects) =>
        request.Query.ContainsKey("partNumber") ? UploadPart(request) : PutObject(request, objects);

    private S3ReferenceResponse AnswerPost(S3ReferenceRequest request, SortedDictionary<string, byte[]> objects) =>
        request.Query.ContainsKey("uploads") ? CreateMultipartUpload(request) : CompleteMultipartUpload(request, objects);

    private S3ReferenceResponse CreateMultipartUpload(S3ReferenceRequest request)
    {
        string uploadId = "upload-" + (partsByUploadId.Count + 1).ToString(CultureInfo.InvariantCulture);
        partsByUploadId[uploadId] = [];
        return Ok(new XElement(
            S3Namespace + "InitiateMultipartUploadResult",
            new XElement(S3Namespace + "Bucket", request.Bucket),
            new XElement(S3Namespace + "Key", request.Key),
            new XElement(S3Namespace + "UploadId", uploadId)));
    }

    private S3ReferenceResponse UploadPart(S3ReferenceRequest request)
    {
        int partNumber = int.Parse(request.Query["partNumber"], CultureInfo.InvariantCulture);
        if (partNumber == FailingPartNumber)
        {
            return ErrorResponse(InternalError, request);
        }

        partsByUploadId[request.Query["uploadId"]][partNumber] = request.Body;
        return new S3ReferenceResponse(200, ETagOf(request.Body), []);
    }

    // The object is the parts the body lists, in the order it lists them; the cases check the body itself.
    private S3ReferenceResponse CompleteMultipartUpload(S3ReferenceRequest request, SortedDictionary<string, byte[]> objects)
    {
        if (CompletionAnswersError)
        {
            return ErrorResponse(InternalError, request, 200);
        }

        SortedDictionary<int, byte[]> parts = partsByUploadId[request.Query["uploadId"]];
        byte[] content = [.. XDocument.Parse(Encoding.UTF8.GetString(request.Body)).Descendants()
            .Where(element => element.Name.LocalName == "PartNumber")
            .SelectMany(element => parts[int.Parse(element.Value, CultureInfo.InvariantCulture)])];
        objects[request.Key] = content;
        return Ok(new XElement(
            S3Namespace + "CompleteMultipartUploadResult",
            new XElement(S3Namespace + "Bucket", request.Bucket),
            new XElement(S3Namespace + "Key", request.Key),
            new XElement(S3Namespace + "ETag", ETagOf(content))));
    }

    private S3ReferenceResponse ListObjectsV2(S3ReferenceRequest request, SortedDictionary<string, byte[]> objects)
    {
        string prefix = request.Query.GetValueOrDefault("prefix", string.Empty);
        string delimiter = request.Query.GetValueOrDefault("delimiter", string.Empty);
        string[] entries = [.. objects.Keys.Where(key => key.StartsWith(prefix, StringComparison.Ordinal)).Select(key => RollUp(key, prefix, delimiter)).Distinct()];
        int start = request.Query.TryGetValue("continuation-token", out string? token) ? int.Parse(token["token-".Length..], CultureInfo.InvariantCulture) : 0;
        string[] page = [.. entries.Skip(start).Take(ListPageSize)];
        bool truncated = start + page.Length < entries.Length;
        return Ok(new XElement(
            S3Namespace + "ListBucketResult",
            new XElement(S3Namespace + "Name", request.Bucket),
            new XElement(S3Namespace + "Prefix", prefix),
            new XElement(S3Namespace + "KeyCount", page.Length),
            new XElement(S3Namespace + "MaxKeys", ListPageSize),
            new XElement(S3Namespace + "IsTruncated", truncated ? "true" : "false"),
            truncated ? new XElement(S3Namespace + "NextContinuationToken", "token-" + (start + page.Length).ToString(CultureInfo.InvariantCulture)) : null,
            page.Select(entry => objects.TryGetValue(entry, out byte[]? content)
                ? new XElement(S3Namespace + "Contents", new XElement(S3Namespace + "Key", entry), new XElement(S3Namespace + "LastModified", "2026-11-02T00:00:00.000Z"), new XElement(S3Namespace + "Size", content.Length), new XElement(S3Namespace + "ETag", ETagOf(content)))
                : new XElement(S3Namespace + "CommonPrefixes", new XElement(S3Namespace + "Prefix", entry)))));
    }

    private sealed record S3ReferenceResponse(int StatusCode, string? ETag, byte[] Body)
    {
        internal byte[] ToBytes()
        {
            string eTagField = ETag is null ? string.Empty : $"ETag: {ETag}\r\n";
            string head = $"HTTP/1.1 {StatusCode} {(HttpStatusCode)StatusCode}\r\nx-amz-request-id: {RequestId}\r\nContent-Length: {Body.Length}\r\n{eTagField}Connection: close\r\n\r\n";
            return [.. Encoding.Latin1.GetBytes(head), .. Body];
        }
    }
}
