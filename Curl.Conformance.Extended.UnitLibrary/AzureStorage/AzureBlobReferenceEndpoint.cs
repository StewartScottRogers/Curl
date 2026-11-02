using System.Text;
using System.Xml.Linq;

namespace Curl.Conformance.Extended.AzureStorage;

/// <summary>
/// A reference Azure Blob endpoint written from the Blob service REST API (<c>x-ms-version: 2025-01-05</c>): Get Blob,
/// Put Blob, Put Block, Put Block List (recording each blob's committed block IDs in order) and List Blobs
/// (<c>EnumerationResults</c>, paged by <c>NextMarker</c>), answering a failure with an <c>Error</c> body. A container
/// without anonymous read access answers an anonymous request <c>404 ResourceNotFound</c>.
/// </summary>
/// <param name="framing">How every response body is framed.</param>
/// <param name="listPageSize">The most entries one List Blobs response holds before it gives a <c>NextMarker</c>.</param>
public sealed class AzureBlobReferenceEndpoint(AzureStorageResponseFraming framing, int listPageSize = 5000) : AzureStorageReferenceEndpoint(framing)
{
    private readonly Dictionary<string, bool> containers = new(StringComparer.Ordinal);

    private readonly Dictionary<string, byte[]> blobs = new(StringComparer.Ordinal);

    private readonly Dictionary<string, byte[]> stagedBlocks = new(StringComparer.Ordinal);

    private readonly Dictionary<string, IReadOnlyList<string>> committedBlockIds = new(StringComparer.Ordinal);

    private readonly Lock gate = new();

    /// <summary>Creates a container.</summary>
    /// <param name="name">The container name.</param>
    /// <param name="anonymousRead">Whether anonymous requests may read it (public access level <c>blob</c>).</param>
    public void CreateContainer(string name, bool anonymousRead = false)
    {
        lock (gate)
        {
            containers[name] = anonymousRead;
        }
    }

    /// <summary>Stores a blob, as a Put Blob would.</summary>
    /// <param name="container">The container name.</param>
    /// <param name="blob">The blob name.</param>
    /// <param name="content">The blob's bytes.</param>
    public void StoreBlob(string container, string blob, byte[] content)
    {
        lock (gate)
        {
            blobs[container + "/" + blob] = content;
        }
    }

    /// <summary>Gives a blob's bytes.</summary>
    /// <param name="container">The container name.</param>
    /// <param name="blob">The blob name.</param>
    /// <returns>The bytes, or <see langword="null" /> when there is no such blob.</returns>
    public byte[]? BlobContent(string container, string blob)
    {
        lock (gate)
        {
            return blobs.GetValueOrDefault(container + "/" + blob);
        }
    }

    /// <summary>Gives the block IDs the last Put Block List of a blob committed, in the list's order.</summary>
    /// <param name="container">The container name.</param>
    /// <param name="blob">The blob name.</param>
    /// <returns>The block IDs, or <see langword="null" /> when no block list was committed.</returns>
    public IReadOnlyList<string>? CommittedBlockIds(string container, string blob)
    {
        lock (gate)
        {
            return committedBlockIds.GetValueOrDefault(container + "/" + blob);
        }
    }

    /// <inheritdoc />
    protected override AzureStorageHttpResponse Answer(AzureStorageHttpRequest request)
    {
        string[] path = request.Path.TrimStart('/').Split('/', 2);
        lock (gate)
        {
            if (!containers.TryGetValue(path[0], out bool anonymousRead))
            {
                return AzureStorageHttpResponse.Error(404, "The specified container does not exist.", "ContainerNotFound");
            }

            if (!anonymousRead && !IsAuthorized(request))
            {
                return AzureStorageHttpResponse.Error(404, "The specified resource does not exist.", "ResourceNotFound");
            }

            string key = path[0] + "/" + path.ElementAtOrDefault(1);
            return Operations.TryGetValue(request.Method + " " + request.QueryValue("comp"), out Operation? operation)
                ? operation(this, path[0], key, request)
                : AzureStorageHttpResponse.Error(405, "The resource doesn't support the specified HTTP verb.", "UnsupportedHttpVerb");
        }
    }

    // One operation: the endpoint, the container, the container/blob key and the request.
    private delegate AzureStorageHttpResponse Operation(AzureBlobReferenceEndpoint endpoint, string container, string key, AzureStorageHttpRequest request);

    // The operations by method and comp parameter: a new operation is one more line.
    private static readonly Dictionary<string, Operation> Operations = new(StringComparer.Ordinal)
    {
        ["GET list"] = (endpoint, container, _, request) => endpoint.ListBlobs(container, request),
        ["GET "] = (endpoint, _, key, _) => endpoint.GetBlob(key),
        ["PUT "] = (endpoint, _, key, request) => endpoint.PutBlob(key, request),
        ["PUT block"] = (endpoint, _, key, request) => endpoint.PutBlock(key, request),
        ["PUT blocklist"] = (endpoint, _, key, request) => endpoint.PutBlockList(key, request),
    };

    private static AzureStorageHttpResponse Created() => AzureStorageHttpResponse.Success(201, "Created", []);

    private AzureStorageHttpResponse GetBlob(string key) =>
        blobs.TryGetValue(key, out byte[]? content)
            ? AzureStorageHttpResponse.Success(200, "OK", content, new("Content-Type", "application/octet-stream"), new("x-ms-blob-type", "BlockBlob"))
            : AzureStorageHttpResponse.Error(404, "The specified blob does not exist.", "BlobNotFound");

    private AzureStorageHttpResponse PutBlob(string key, AzureStorageHttpRequest request)
    {
        if (request.Header("x-ms-blob-type") != "BlockBlob")
        {
            return AzureStorageHttpResponse.Error(400, "An HTTP header that's mandatory for this request is not specified.", "MissingRequiredHeader");
        }

        blobs[key] = request.Body;
        return Created();
    }

    private AzureStorageHttpResponse PutBlock(string key, AzureStorageHttpRequest request)
    {
        stagedBlocks[key + "\n" + request.QueryValue("blockid")] = request.Body;
        return Created();
    }

    // Every block the list names is the one staged under its ID; one never staged fails the whole list.
    private AzureStorageHttpResponse PutBlockList(string key, AzureStorageHttpRequest request)
    {
        string[] ids = [.. XElement.Parse(Encoding.UTF8.GetString(request.Body)).Elements().Select(element => element.Value)];
        if (!ids.All(id => stagedBlocks.ContainsKey(key + "\n" + id)))
        {
            return AzureStorageHttpResponse.Error(400, "The specified block list is invalid.", "InvalidBlockList");
        }

        blobs[key] = [.. ids.SelectMany(id => stagedBlocks[key + "\n" + id])];
        committedBlockIds[key] = ids;
        return Created();
    }

    // Names under the prefix, each cut after the first delimiter past the prefix into a BlobPrefix, from the marker on.
    private AzureStorageHttpResponse ListBlobs(string container, AzureStorageHttpRequest request)
    {
        string prefix = request.QueryValue("prefix") ?? string.Empty;
        string? delimiter = request.QueryValue("delimiter");
        string marker = request.QueryValue("marker") ?? string.Empty;
        string[] entries = [.. blobs.Keys
            .Where(key => key.StartsWith(container + "/" + prefix, StringComparison.Ordinal))
            .Select(key => EntryOf(key[(container.Length + 1)..], prefix, delimiter))
            .Distinct()
            .Order(StringComparer.Ordinal)
            .Where(entry => string.CompareOrdinal(entry, marker) >= 0)];
        XElement listing = new(
            "Blobs",
            entries.Take(listPageSize).Select(entry => blobs.TryGetValue(container + "/" + entry, out byte[]? content)
                ? new XElement("Blob", new XElement("Name", entry), new XElement("Properties", new XElement("Content-Length", content.Length), new XElement("BlobType", "BlockBlob")))
                : new XElement("BlobPrefix", new XElement("Name", entry))));
        XElement results = new(
            "EnumerationResults",
            new XAttribute("ContainerName", container),
            new XElement("Prefix", prefix),
            new XElement("Marker", marker),
            new XElement("Delimiter", delimiter),
            listing,
            new XElement("NextMarker", entries.Skip(listPageSize).FirstOrDefault()));
        byte[] body = Encoding.UTF8.GetBytes("<?xml version=\"1.0\" encoding=\"utf-8\"?>" + results.ToString(SaveOptions.DisableFormatting));
        return AzureStorageHttpResponse.Success(200, "OK", body, new KeyValuePair<string, string>("Content-Type", "application/xml"));
    }

    private static string EntryOf(string name, string prefix, string? delimiter)
    {
        int cut = delimiter is null ? -1 : name.IndexOf(delimiter, prefix.Length, StringComparison.Ordinal);
        return cut < 0 ? name : name[..(cut + delimiter!.Length)];
    }
}
