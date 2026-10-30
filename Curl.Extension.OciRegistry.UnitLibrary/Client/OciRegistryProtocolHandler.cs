using Curl.Extension.OciRegistry.Http;
using Curl.Extension.OciRegistry.Push;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.OciRegistry.Client;

/// <summary>
/// Serves <c>oci://registry[:port]/name[:tag]</c> and <c>oci://registry[:port]/name@digest</c> over HTTPS, port 443 by
/// default: without <c>-T</c> it pulls the manifest or blob the path names (<see cref="OciPullReference"/>) through
/// <see cref="OciPullFlow"/> (ADR-1093); with <c>-T</c> it pushes the upload as that manifest, or as that blob once its
/// SHA-256 digest matches the URL's, through <see cref="OciPushFlow"/> (ADR-1097). Either way it writes the final
/// response's body to the output.
/// </summary>
/// <param name="connector">Opens the connections to the registry, its token endpoint and any redirect target.</param>
internal sealed class OciRegistryProtocolHandler(IConnector connector) : IProtocolHandler
{
    /// <summary>
    /// The <c>Accept</c> value of a manifest pull: the OCI image manifest and index and the Docker manifest and manifest
    /// list, so a registry answers with whichever it holds rather than converting it.
    /// </summary>
    internal const string ManifestMediaTypes =
        "application/vnd.oci.image.manifest.v1+json, application/vnd.oci.image.index.v1+json, "
        + "application/vnd.docker.distribution.manifest.v2+json, application/vnd.docker.distribution.manifest.list.v2+json";

    /// <summary>The <c>Accept</c> value of a blob pull, curl's own default.</summary>
    internal const string BlobMediaTypes = "*/*";

    /// <summary>The message of a URL whose path is not a repository name with a tag or a digest.</summary>
    internal const string NoReferenceMessage = "The oci:// URL's path is not a repository name followed by :tag or @digest.";

    /// <inheritdoc />
    public IReadOnlyCollection<string> SupportedSchemes { get; } = ["oci"];

    /// <inheritdoc />
    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return OciPullReference.Parse(context.Url.AbsolutePath) is { } reference
            ? await TransferAsync(context, reference)
            : TransferResult.Failure(CurlExitCode.UrlMalformat, NoReferenceMessage);
    }

    private async ValueTask<TransferResult> TransferAsync(ITransferContext context, OciPullReference reference)
    {
        var session = new OciRegistrySession(new OciHttpClient(connector), context.Credentials);
        context.Progress.ReportTransferStarted();
        try
        {
            var body = context.Upload is { } upload
                ? await PushAsync(context, session, reference, upload)
                : await new OciPullFlow(session).PullAsync(
                    RegistryUrl(context, reference.ApiPath), reference.IsBlob ? BlobMediaTypes : ManifestMediaTypes, context.CancellationToken);
            await context.Output.WriteAsync(body, context.CancellationToken);
            return TransferResult.Success(body.Length);
        }
        catch (OciTransferException failure)
        {
            return TransferResult.Failure(failure.ExitCode, failure.Message);
        }
    }

    private static async Task<byte[]> PushAsync(ITransferContext context, OciRegistrySession session, OciPullReference reference, Stream upload)
    {
        using var buffer = new MemoryStream();
        await upload.CopyToAsync(buffer, context.CancellationToken);
        var content = buffer.ToArray();
        var flow = new OciPushFlow(session, RegistryUrl(context, "/"), reference.Repository);
        return reference.IsBlob
            ? await flow.PushBlobAsync(OciPushContent.VerifyDigest(reference.Reference, content), content, context.CancellationToken)
            : await flow.PushManifestAsync(reference.Reference, content, context.CancellationToken);
    }

    // A port of -1 is UriBuilder's "the scheme's default", 443 for https.
    private static Uri RegistryUrl(ITransferContext context, string path) =>
        new UriBuilder(Uri.UriSchemeHttps, context.Url.IdnHost, context.Url.Port, path).Uri;
}
