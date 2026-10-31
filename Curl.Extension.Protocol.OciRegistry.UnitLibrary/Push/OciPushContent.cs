using System.Security.Cryptography;
using System.Text.Json;
using Curl.Extension.Protocol.OciRegistry.Client;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Protocol.OciRegistry.Push;

/// <summary>
/// Reads what a push needs from the uploaded bytes (OCI Distribution Specification 1.1, "Pushing blobs", "Pushing
/// manifests"; OCI Image Specification, "Digests"): a blob's SHA-256 digest, checked against the URL's, and a manifest's
/// <c>mediaType</c>.
/// </summary>
internal static class OciPushContent
{
    /// <summary>The <c>Content-Type</c> of a manifest without a <c>mediaType</c> string, the OCI image manifest's.</summary>
    internal const string DefaultManifestMediaType = "application/vnd.oci.image.manifest.v1+json";

    /// <summary>Computes the digest of <paramref name="content"/> and checks it is the one the URL names.</summary>
    /// <param name="urlDigest">The URL's digest.</param>
    /// <param name="content">The blob.</param>
    /// <returns>The digest, <c>sha256:</c> and the lowercase hexadecimal hash.</returns>
    /// <exception cref="OciTransferException">The digests differ, including a URL digest of another algorithm (exit 25).</exception>
    public static string VerifyDigest(string urlDigest, byte[] content)
    {
        var digest = "sha256:" + Convert.ToHexStringLower(SHA256.HashData(content));
        return digest == urlDigest
            ? digest
            : throw new OciTransferException(CurlExitCode.UploadFailed, $"The upload's digest {digest} does not match the URL's {urlDigest}.");
    }

    /// <summary>Reads a manifest's top-level <c>mediaType</c> string.</summary>
    /// <param name="manifest">The manifest.</param>
    /// <returns>Its <c>mediaType</c>, or <see cref="DefaultManifestMediaType"/> when it is not a JSON object with one.</returns>
    public static string MediaTypeOf(byte[] manifest)
    {
        try
        {
            using var document = JsonDocument.Parse(manifest);
            var root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object && root.TryGetProperty("mediaType", out var mediaType) && mediaType.ValueKind == JsonValueKind.String
                ? mediaType.GetString()!
                : DefaultManifestMediaType;
        }
        catch (JsonException)
        {
            return DefaultManifestMediaType;
        }
    }
}
