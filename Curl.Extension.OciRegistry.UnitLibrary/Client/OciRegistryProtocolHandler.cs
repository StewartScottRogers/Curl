using Curl.Extension.OciRegistry.Http;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.OciRegistry.Client;

/// <summary>
/// Serves <c>oci://registry[:port]/name[:tag]</c> and <c>oci://registry[:port]/name@digest</c> (ADR-1090): pulls the
/// manifest or blob the path names (<see cref="OciPullReference"/>) from the registry over HTTPS, port 443 by default,
/// through <see cref="OciPullFlow"/>, and writes its body to the output.
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
            ? await PullAsync(context, reference)
            : TransferResult.Failure(CurlExitCode.UrlMalformat, NoReferenceMessage);
    }

    private async ValueTask<TransferResult> PullAsync(ITransferContext context, OciPullReference reference)
    {
        // A port of -1 is UriBuilder's "the scheme's default", 443 for https.
        var url = new UriBuilder(Uri.UriSchemeHttps, context.Url.IdnHost, context.Url.Port, reference.ApiPath).Uri;
        var flow = new OciPullFlow(new OciHttpClient(connector), context.Credentials);
        context.Progress.ReportTransferStarted();
        try
        {
            var body = await flow.PullAsync(url, reference.IsBlob ? BlobMediaTypes : ManifestMediaTypes, context.CancellationToken);
            await context.Output.WriteAsync(body, context.CancellationToken);
            return TransferResult.Success(body.Length);
        }
        catch (OciPullException failure)
        {
            return TransferResult.Failure(failure.ExitCode, failure.Message);
        }
    }
}
