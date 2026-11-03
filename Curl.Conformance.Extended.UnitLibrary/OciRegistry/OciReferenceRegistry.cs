using System.Security.Cryptography;
using System.Text;
using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.OciRegistry;

/// <summary>
/// A registry written from the OCI Distribution Specification v1.1.0 (pull and push workflows, error codes) and Docker's
/// Token Authentication Specification. One instance serves three roles on whichever routes a case gives it: the
/// registry API under <c>/v2/</c>, the token endpoint at <c>/token</c> and the blob storage a blob GET redirects to at
/// <c>/blobstore/&lt;digest&gt;</c>.
/// </summary>
public sealed class OciReferenceRegistry : IReferenceServer
{
    private const string UploadSession = "session-1";

    private static readonly string[] ApiKinds = ["blobs/uploads", "manifests", "blobs"];

    private static readonly byte[] HeadEnd = "\r\n\r\n"u8.ToArray();

    private readonly Dictionary<string, byte[]> blobs = new(StringComparer.Ordinal);

    private readonly Dictionary<string, (string MediaType, byte[] Content)> manifests = new(StringComparer.Ordinal);

    /// <summary>Gets the token every <c>/v2/</c> request must carry as <c>Authorization: Bearer</c>; <see langword="null" /> serves anonymously.</summary>
    public string? RequiredToken { get; init; }

    /// <summary>Gets the <c>realm</c> of the Bearer challenge; the token endpoint is its <c>/token</c> path.</summary>
    public string Realm { get; init; } = "https://auth.example:9443/token";

    /// <summary>Gets the <c>user:password</c> the token endpoint needs as Basic credentials; <see langword="null" /> issues tokens anonymously.</summary>
    public string? TokenCredentials { get; init; }

    /// <summary>Gets the token response's member that holds the token: <c>token</c> or <c>access_token</c>.</summary>
    public string TokenField { get; init; } = "token";

    /// <summary>Gets the scheme and authority a blob GET redirects to with a 307; <see langword="null" /> serves blobs directly.</summary>
    public string? BlobRedirect { get; init; }

    /// <summary>Gets what precedes the upload session's path in the POST's <c>Location</c>: empty for a relative one, a scheme and authority for an absolute one.</summary>
    public string UploadLocationAuthority { get; init; } = string.Empty;

    /// <summary>Gets the query the upload session's <c>Location</c> carries; <see langword="null" /> for none.</summary>
    public string? UploadLocationQuery { get; init; }

    /// <summary>Gets the error code of <see cref="OciReferenceResponse.ErrorCodes" /> every <c>/v2/</c> request is answered with; <see langword="null" /> for none.</summary>
    public string? ForcedError { get; init; }

    /// <summary>Gets the requests served, in the order they arrived.</summary>
    public IList<OciReferenceRequest> Requests { get; } = [];

    /// <summary>Stores a blob under a digest, which a case may make not match the content.</summary>
    /// <param name="digest">The digest it is stored under.</param>
    /// <param name="content">The blob's bytes.</param>
    public void PutBlob(string digest, byte[] content) => blobs[digest] = content;

    /// <summary>Gives a stored blob.</summary>
    /// <param name="digest">The digest it is stored under.</param>
    /// <returns>The bytes, or <see langword="null" /> when none is stored.</returns>
    public byte[]? GetBlob(string digest) => blobs.GetValueOrDefault(digest);

    /// <summary>Stores a manifest under a repository and a tag or digest.</summary>
    /// <param name="repository">The repository name.</param>
    /// <param name="reference">The tag or digest.</param>
    /// <param name="mediaType">The media type it is served with.</param>
    /// <param name="content">The manifest's bytes.</param>
    public void PutManifest(string repository, string reference, string mediaType, byte[] content) =>
        manifests[repository + ":" + reference] = (mediaType, content);

    /// <summary>Gives a stored manifest's bytes.</summary>
    /// <param name="repository">The repository name.</param>
    /// <param name="reference">The tag or digest.</param>
    /// <returns>The bytes, or <see langword="null" /> when none is stored.</returns>
    public byte[]? GetManifest(string repository, string reference) =>
        manifests.TryGetValue(repository + ":" + reference, out (string MediaType, byte[] Content) manifest) ? manifest.Content : null;

    /// <summary>Gives the <c>sha256:</c> digest of some bytes.</summary>
    /// <param name="content">The bytes.</param>
    /// <returns>The digest.</returns>
    public static string DigestOf(byte[] content) => "sha256:" + Convert.ToHexStringLower(SHA256.HashData(content));

    /// <inheritdoc />
    public async Task ServeAsync(ReferenceConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        List<byte> pending = [];
        while (await ReadAsync(connection, pending, cancellationToken) is { } request)
        {
            Requests.Add(request);
            await connection.WriteAsync(Answer(request).ToBytes(request.Method != "HEAD"), cancellationToken);
        }
    }

    /// <summary>Answers one request.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The response.</returns>
    internal OciReferenceResponse Answer(OciReferenceRequest request)
    {
        if (request.Path == "/token")
        {
            return Token(request);
        }

        if (request.Path.StartsWith("/blobstore/", StringComparison.Ordinal))
        {
            return Blob(request.Path["/blobstore/".Length..]);
        }

        return ForcedError is null ? Authorized(request) : OciReferenceResponse.Error(ForcedError);
    }

    private OciReferenceResponse Token(OciReferenceRequest request) =>
        TokenCredentials is null || request.Headers.GetValueOrDefault("Authorization") == "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(TokenCredentials))
            ? OciReferenceResponse.Of(200, Encoding.UTF8.GetBytes($$"""{"{{TokenField}}":"{{RequiredToken}}","expires_in":300}"""), "Content-Type", "application/json")
            : OciReferenceResponse.Error("UNAUTHORIZED");

    private OciReferenceResponse Authorized(OciReferenceRequest request)
    {
        if (ApiPathOf(request.Path) is not { } api)
        {
            return OciReferenceResponse.Error("NAME_INVALID");
        }

        string name = api.Name;
        return RequiredToken is null || request.Headers.GetValueOrDefault("Authorization") == "Bearer " + RequiredToken
            ? Api(request, name, api.Kind, api.Reference)
            : OciReferenceResponse.Error("UNAUTHORIZED", "WWW-Authenticate", $"Bearer realm=\"{Realm}\",service=\"registry.example\",scope=\"repository:{name}:pull\"");
    }

    private OciReferenceResponse Api(OciReferenceRequest request, string name, string kind, string reference) =>
        kind switch
        {
            "manifests" => Manifests(request, name, reference),
            "blobs" => Blobs(request, reference),
            _ => Uploads(request, name),
        };

    private OciReferenceResponse Manifests(OciReferenceRequest request, string name, string reference) =>
        request.Method switch
        {
            "GET" => Manifest(name, reference),
            "PUT" => StoreManifest(request, name, reference),
            _ => OciReferenceResponse.Error("UNSUPPORTED"),
        };

    private OciReferenceResponse Blobs(OciReferenceRequest request, string reference)
    {
        if (request.Method is not ("GET" or "HEAD"))
        {
            return OciReferenceResponse.Error("UNSUPPORTED");
        }

        return BlobRedirect is null ? Blob(reference) : OciReferenceResponse.Of(307, [], "Location", $"{BlobRedirect}/blobstore/{reference}");
    }

    private OciReferenceResponse Uploads(OciReferenceRequest request, string name) =>
        request.Method switch
        {
            "POST" => OciReferenceResponse.Of(
                202, [], "Location", $"{UploadLocationAuthority}/v2/{name}/blobs/uploads/{UploadSession}{(UploadLocationQuery is null ? string.Empty : "?" + UploadLocationQuery)}", "Docker-Upload-UUID", UploadSession),
            "PUT" => StoreBlob(request, name),
            _ => OciReferenceResponse.Error("UNSUPPORTED"),
        };

    private OciReferenceResponse Manifest(string name, string reference) =>
        manifests.TryGetValue(name + ":" + reference, out (string MediaType, byte[] Content) manifest)
            ? OciReferenceResponse.Of(200, manifest.Content, "Content-Type", manifest.MediaType, "Docker-Content-Digest", DigestOf(manifest.Content))
            : OciReferenceResponse.Error("MANIFEST_UNKNOWN");

    private OciReferenceResponse StoreManifest(OciReferenceRequest request, string name, string reference)
    {
        PutManifest(name, reference, request.Headers.GetValueOrDefault("Content-Type") ?? string.Empty, request.Body);
        return OciReferenceResponse.Of(201, [], "Location", $"/v2/{name}/manifests/{reference}", "Docker-Content-Digest", DigestOf(request.Body));
    }

    private OciReferenceResponse Blob(string digest) =>
        blobs.TryGetValue(digest, out byte[]? content)
            ? OciReferenceResponse.Of(200, content, "Content-Type", "application/octet-stream", "Docker-Content-Digest", digest)
            : OciReferenceResponse.Error("BLOB_UNKNOWN");

    private OciReferenceResponse StoreBlob(OciReferenceRequest request, string name)
    {
        string? digest = request.QueryValue("digest");
        if (digest != DigestOf(request.Body))
        {
            return OciReferenceResponse.Error("DIGEST_INVALID");
        }

        blobs[digest] = request.Body;
        return OciReferenceResponse.Of(201, [], "Location", $"/v2/{name}/blobs/{digest}", "Docker-Content-Digest", digest);
    }

    private static async Task<OciReferenceRequest?> ReadAsync(ReferenceConnection connection, List<byte> pending, CancellationToken cancellationToken)
    {
        int headLength;
        while ((headLength = pending.ToArray().AsSpan().IndexOf(HeadEnd)) < 0)
        {
            if (!await FillAsync(connection, pending, cancellationToken))
            {
                return null;
            }
        }

        OciReferenceRequest request = OciReferenceRequest.ParseHead(Encoding.Latin1.GetString([.. pending.GetRange(0, headLength)]));
        pending.RemoveRange(0, headLength + HeadEnd.Length);
        while (pending.Count < request.ContentLength && await FillAsync(connection, pending, cancellationToken))
        {
        }

        byte[] body = [.. pending.Take(request.ContentLength)];
        pending.RemoveRange(0, body.Length);
        return request with { Body = body };
    }

    private static async Task<bool> FillAsync(ReferenceConnection connection, List<byte> pending, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[4096];
        int read = await connection.ReadAsync(buffer, cancellationToken);
        pending.AddRange(buffer.AsSpan(0, read));
        return read > 0;
    }

    // "/v2/<name>/<kind>/<reference>", the name itself holding slashes; "blobs/uploads" is tried before "blobs".
    private static (string Name, string Kind, string Reference)? ApiPathOf(string path)
    {
        string rest = path.StartsWith("/v2/", StringComparison.Ordinal) ? path["/v2/".Length..] : string.Empty;
        int last = rest.LastIndexOf('/');
        string beforeReference = last < 0 ? string.Empty : rest[..last];
        string? kind = ApiKinds.FirstOrDefault(candidate => beforeReference.EndsWith("/" + candidate, StringComparison.Ordinal));
        return kind is null ? null : (beforeReference[..^(kind.Length + 1)], kind, rest[(last + 1)..]);
    }
}
