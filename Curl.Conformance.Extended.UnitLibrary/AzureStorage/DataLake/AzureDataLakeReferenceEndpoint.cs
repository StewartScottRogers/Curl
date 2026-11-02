using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Curl.Conformance.Extended.AzureStorage.DataLake;

/// <summary>
/// A reference DFS endpoint written from the Azure Data Lake Storage Gen2 REST API (<c>x-ms-version: 2025-01-05</c>):
/// Path - Read, Path - Create (<c>resource=file</c>), Path - Update (<c>action=append</c> at <c>position</c>, then
/// <c>action=flush</c> at the file's final length) and Path - List (a JSON <c>paths</c> array, paged by the
/// <c>x-ms-continuation</c> header), answering a failure with a JSON <c>error</c> body. A file's appended bytes are
/// uncommitted until a flush commits them; a Path - Read sees only committed bytes.
/// </summary>
/// <param name="framing">How every response body is framed.</param>
/// <param name="listPageSize">The most entries one Path - List response holds before it gives an <c>x-ms-continuation</c>.</param>
public sealed class AzureDataLakeReferenceEndpoint(AzureStorageResponseFraming framing, int listPageSize = 5000) : AzureStorageReferenceEndpoint(framing)
{
    private const string LastModified = "Mon, 02 Nov 2026 12:00:00 GMT";

    private readonly HashSet<string> fileSystems = new(StringComparer.Ordinal);

    private readonly Dictionary<string, byte[]> files = new(StringComparer.Ordinal);

    private readonly Dictionary<string, List<byte>> uncommitted = new(StringComparer.Ordinal);

    private readonly Lock gate = new();

    // One operation: the endpoint, the file system, the path inside it and the request.
    private delegate AzureStorageHttpResponse Operation(AzureDataLakeReferenceEndpoint endpoint, string fileSystem, string path, AzureStorageHttpRequest request);

    // The operations by method and resource or action parameter: a new operation is one more line.
    private static readonly Dictionary<string, Operation> Operations = new(StringComparer.Ordinal)
    {
        ["GET filesystem"] = (endpoint, fileSystem, _, request) => endpoint.ListPaths(fileSystem, request),
        ["GET "] = (endpoint, fileSystem, path, _) => endpoint.ReadPath(fileSystem + "/" + path),
        ["PUT file"] = (endpoint, fileSystem, path, _) => endpoint.CreateFile(fileSystem + "/" + path),
        ["PATCH append"] = (endpoint, fileSystem, path, request) => endpoint.Append(fileSystem + "/" + path, request),
        ["PATCH flush"] = (endpoint, fileSystem, path, request) => endpoint.Flush(fileSystem + "/" + path, request),
    };

    /// <summary>Creates a file system.</summary>
    /// <param name="name">The file system name.</param>
    public void CreateFileSystem(string name)
    {
        lock (gate)
        {
            fileSystems.Add(name);
        }
    }

    /// <summary>Stores a file with committed content, as Path - Create, an append and a flush would.</summary>
    /// <param name="fileSystem">The file system name.</param>
    /// <param name="path">The file's path inside the file system, without a leading <c>/</c>.</param>
    /// <param name="content">The file's bytes.</param>
    public void StoreFile(string fileSystem, string path, byte[] content)
    {
        lock (gate)
        {
            files[fileSystem + "/" + path] = content;
        }
    }

    /// <summary>Gives a file's committed bytes.</summary>
    /// <param name="fileSystem">The file system name.</param>
    /// <param name="path">The file's path inside the file system.</param>
    /// <returns>The bytes, or <see langword="null" /> when there is no such file.</returns>
    public byte[]? FileContent(string fileSystem, string path)
    {
        lock (gate)
        {
            return files.GetValueOrDefault(fileSystem + "/" + path);
        }
    }

    /// <summary>
    /// A Data Lake error response: <c>x-ms-error-code</c> names <paramref name="errorCode" />, and the body is the JSON
    /// object <c>{"error":{"code":...,"message":...}}</c> the DFS endpoint answers every failure with.
    /// </summary>
    /// <param name="statusCode">The status code.</param>
    /// <param name="message">The reason phrase, which is also the error message.</param>
    /// <param name="errorCode">The error code, such as <c>PathNotFound</c>.</param>
    /// <returns>The response.</returns>
    public static AzureStorageHttpResponse Error(int statusCode, string message, string errorCode)
    {
        byte[] body = JsonObjectBytes(writer =>
        {
            writer.WriteStartObject("error");
            writer.WriteString("code", errorCode);
            writer.WriteString("message", message);
            writer.WriteEndObject();
        });
        return AzureStorageHttpResponse.Success(statusCode, message, body, new("Content-Type", "application/json;charset=utf-8"), new("x-ms-error-code", errorCode));
    }

    /// <inheritdoc />
    protected override AzureStorageHttpResponse Answer(AzureStorageHttpRequest request)
    {
        string[] path = request.Path.TrimStart('/').Split('/', 2);
        lock (gate)
        {
            if (!fileSystems.Contains(path[0]))
            {
                return Error(404, "The specified filesystem does not exist.", "FilesystemNotFound");
            }

            string operation = request.Method + " " + (request.QueryValue("resource") ?? request.QueryValue("action"));
            return Operations.TryGetValue(operation, out Operation? answer)
                ? answer(this, path[0], path.ElementAtOrDefault(1) ?? string.Empty, request)
                : Error(405, "The resource doesn't support the specified HTTP verb.", "UnsupportedHttpVerb");
        }
    }

    private static AzureStorageHttpResponse Empty(int statusCode, string reasonPhrase) => AzureStorageHttpResponse.Success(statusCode, reasonPhrase, []);

    private AzureStorageHttpResponse ReadPath(string key) =>
        files.TryGetValue(key, out byte[]? content)
            ? AzureStorageHttpResponse.Success(200, "OK", content, new("Content-Type", "application/octet-stream"), new("x-ms-resource-type", "file"))
            : Error(404, "The specified path does not exist.", "PathNotFound");

    // A create replaces the file with an empty one and drops what was appended to it.
    private AzureStorageHttpResponse CreateFile(string key)
    {
        files[key] = [];
        uncommitted[key] = [];
        return Empty(201, "Created");
    }

    // Data is appended at the end of what was appended so far: any other position is no contiguous upload.
    private AzureStorageHttpResponse Append(string key, AzureStorageHttpRequest request)
    {
        if (!uncommitted.TryGetValue(key, out List<byte>? pending))
        {
            return Error(404, "The specified path does not exist.", "PathNotFound");
        }

        if (PositionOf(request) != pending.Count)
        {
            return Error(400, "The uploaded data is not contiguous or the position query parameter value is not equal to the length of the file after appending the uploaded data.", "InvalidFlushPosition");
        }

        pending.AddRange(request.Body);
        return Empty(202, "Accepted");
    }

    // A flush commits the appended data when its position is the file's length after all of it.
    private AzureStorageHttpResponse Flush(string key, AzureStorageHttpRequest request)
    {
        if (!uncommitted.TryGetValue(key, out List<byte>? pending))
        {
            return Error(404, "The specified path does not exist.", "PathNotFound");
        }

        if (PositionOf(request) != pending.Count)
        {
            return Error(400, "The uploaded data is not contiguous or the position query parameter value is not equal to the length of the file after appending the uploaded data.", "InvalidFlushPosition");
        }

        files[key] = [.. pending];
        return Empty(200, "OK");
    }

    private static long PositionOf(AzureStorageHttpRequest request) =>
        long.TryParse(request.QueryValue("position"), NumberStyles.None, CultureInfo.InvariantCulture, out long position) ? position : -1;

    // The entries directly in the directory (recursive=false): each file, and each subdirectory a deeper file implies,
    // from the continuation on, sorted by name.
    private AzureStorageHttpResponse ListPaths(string fileSystem, AzureStorageHttpRequest request)
    {
        string directory = request.QueryValue("directory") is { Length: > 0 } named ? named + "/" : string.Empty;
        string continuation = request.QueryValue("continuation") ?? string.Empty;
        string[] entries = [.. files.Keys
            .Where(key => key.StartsWith(fileSystem + "/" + directory, StringComparison.Ordinal))
            .Select(key => EntryOf(key[(fileSystem.Length + 1)..], directory.Length))
            .Distinct()
            .Order(StringComparer.Ordinal)
            .Where(entry => string.CompareOrdinal(entry, continuation) >= 0)];
        byte[] body = JsonObjectBytes(writer =>
        {
            writer.WriteStartArray("paths");
            foreach (string entry in entries.Take(listPageSize))
            {
                WritePath(writer, entry, files.GetValueOrDefault(fileSystem + "/" + entry));
            }

            writer.WriteEndArray();
        });
        string? next = entries.Skip(listPageSize).FirstOrDefault();
        return next is null
            ? AzureStorageHttpResponse.Success(200, "OK", body, new KeyValuePair<string, string>("Content-Type", "application/json;charset=utf-8"))
            : AzureStorageHttpResponse.Success(200, "OK", body, new("Content-Type", "application/json;charset=utf-8"), new("x-ms-continuation", next));
    }

    // A file's name, or the name of the subdirectory it lies in directly below the listed directory.
    private static string EntryOf(string name, int directoryLength)
    {
        int slash = name.IndexOf('/', directoryLength);
        return slash < 0 ? name : name[..slash];
    }

    // The service sends isDirectory and contentLength as strings; a directory's entry has contentLength "0".
    private static void WritePath(Utf8JsonWriter writer, string name, byte[]? content)
    {
        writer.WriteStartObject();
        writer.WriteString("name", name);
        if (content is null)
        {
            writer.WriteString("isDirectory", "true");
        }

        writer.WriteString("contentLength", (content?.Length ?? 0).ToString(CultureInfo.InvariantCulture));
        writer.WriteString("lastModified", LastModified);
        writer.WriteString("etag", "0x" + Convert.ToHexString(SHA256.HashData(content ?? []), 0, 8));
        writer.WriteEndObject();
    }

    private static byte[] JsonObjectBytes(Action<Utf8JsonWriter> writeMembers)
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            writer.WriteStartObject();
            writeMembers(writer);
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }
}
