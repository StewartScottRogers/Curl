using Curl.Extension.OciRegistry.Client;
using Curl.Extension.OciRegistry.Http;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.OciRegistry.Push;

/// <summary>
/// Pushes one blob or manifest to a repository (OCI Distribution Specification 1.1, "Pushing blobs", "Pushing
/// manifests", ADR-1094). A blob is first checked with <c>HEAD</c> and, when the registry lacks it, uploaded as a
/// monolithic upload: <c>POST /v2/&lt;name&gt;/blobs/uploads/</c> opens a session, and <c>PUT &lt;Location&gt;?digest=</c>
/// sends the whole blob. A manifest is one <c>PUT /v2/&lt;name&gt;/manifests/&lt;reference&gt;</c> with its
/// <c>mediaType</c> as <c>Content-Type</c>. The first 401 is answered with a token for <see cref="Scope"/>, which every
/// later request to the registry carries; a <c>Location</c> on another host gets no token.
/// </summary>
/// <param name="session">Sends each request and fetches the token.</param>
/// <param name="registry">The registry's base URL, <c>https://registry[:port]/</c>.</param>
/// <param name="repository">The repository name, such as <c>library/alpine</c>.</param>
internal sealed class OciPushFlow(OciRegistrySession session, Uri registry, string repository)
{
    /// <summary>The <c>Content-Type</c> of a blob upload, the specification's own.</summary>
    internal const string BlobContentType = "application/octet-stream";

    private string? authorization;

    /// <summary>Gets the token scope asked for: <c>repository:&lt;name&gt;:push,pull</c>, since a push also reads the repository.</summary>
    public string Scope => $"repository:{repository}:push,pull";

    /// <summary>Pushes a blob unless the registry already has it.</summary>
    /// <param name="digest">The blob's digest, <c>sha256:</c> and its hash.</param>
    /// <param name="content">The blob.</param>
    /// <param name="cancellationToken">Cancels the push.</param>
    /// <returns>The body of the final response: the upload's 201, or the <c>HEAD</c>'s 200 when the blob exists.</returns>
    /// <exception cref="OciTransferException">The registry refused a step.</exception>
    public async Task<byte[]> PushBlobAsync(string digest, byte[] content, CancellationToken cancellationToken)
    {
        var existing = await SendAsync("HEAD", new Uri(registry, $"/v2/{repository}/blobs/{digest}"), null, null, cancellationToken);
        if (existing.StatusCode == 200)
        {
            return existing.Body;
        }

        if (existing.StatusCode != 404)
        {
            throw OciRegistryError.From(existing);
        }

        var upload = await SendAsync("POST", new Uri(registry, $"/v2/{repository}/blobs/uploads/"), null, [], cancellationToken);
        if (upload.StatusCode != 202)
        {
            throw OciRegistryError.From(upload);
        }

        return Completed(await SendAsync("PUT", UploadTarget(upload, digest), BlobContentType, content, cancellationToken));
    }

    /// <summary>Pushes a manifest under a tag or digest.</summary>
    /// <param name="reference">The tag or digest to push it as.</param>
    /// <param name="manifest">The manifest.</param>
    /// <param name="cancellationToken">Cancels the push.</param>
    /// <returns>The body of the registry's 201.</returns>
    /// <exception cref="OciTransferException">The registry refused the manifest.</exception>
    public async Task<byte[]> PushManifestAsync(string reference, byte[] manifest, CancellationToken cancellationToken) =>
        Completed(await SendAsync(
            "PUT", new Uri(registry, $"/v2/{repository}/manifests/{reference}"), OciPushContent.MediaTypeOf(manifest), manifest, cancellationToken));

    private static byte[] Completed(OciHttpResponse response) =>
        response.StatusCode is >= 200 and < 300 ? response.Body : throw OciRegistryError.From(response);

    // The session's Location may be relative, and may already carry a query the digest parameter joins.
    private Uri UploadTarget(OciHttpResponse response, string digest)
    {
        var target = response.FindHeader("Location") is { } location && Uri.TryCreate(registry, location, out var url)
            ? url
            : throw new OciTransferException(CurlExitCode.WeirdServerReply, $"The registry answered {response.StatusCode} without a usable Location.");
        var query = target.Query.TrimStart('?');
        return new UriBuilder(target) { Query = (query.Length == 0 ? string.Empty : query + "&") + "digest=" + Uri.EscapeDataString(digest) }.Uri;
    }

    private async Task<OciHttpResponse> SendAsync(string method, Uri url, string? contentType, byte[]? body, CancellationToken cancellationToken)
    {
        var response = await session.ReceiveAsync(Request(method, url, contentType, body), cancellationToken);
        if (response.StatusCode != 401 || authorization is not null)
        {
            return response;
        }

        authorization = await session.RequestAuthorizationAsync(response, Scope, cancellationToken);
        return await session.ReceiveAsync(Request(method, url, contentType, body), cancellationToken);
    }

    private OciHttpRequest Request(string method, Uri url, string? contentType, byte[]? body)
    {
        List<KeyValuePair<string, string>> headers = [];
        if (contentType is not null)
        {
            headers.Add(new("Content-Type", contentType));
        }

        if (authorization is not null && Uri.Compare(url, registry, UriComponents.SchemeAndServer, UriFormat.UriEscaped, StringComparison.OrdinalIgnoreCase) == 0)
        {
            headers.Add(new("Authorization", authorization));
        }

        return new OciHttpRequest(method, url, headers, body);
    }
}
