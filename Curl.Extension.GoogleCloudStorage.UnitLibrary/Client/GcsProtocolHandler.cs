using Curl.Extension.GoogleCloudStorage.Http;
using Curl.Extension.GoogleCloudStorage.Signing;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.GoogleCloudStorage.Client;

/// <summary>
/// Serves <c>gs://bucket/object</c> (ADR-1099): with <c>-T</c> it uploads the object, with an empty name or one ending in
/// <c>/</c> it lists the names under it, otherwise it downloads the object. <c>--oauth2-bearer</c> sends an OAuth 2.0
/// access token; otherwise <c>--user ACCESS_ID:SECRET</c> signs with an HMAC key; with neither the requests go
/// unauthenticated, for public objects. The server is <see cref="GcsEndpoint"/>; the requests are <see cref="GcsOperations"/>.
/// </summary>
/// <param name="connector">Opens each connection.</param>
/// <param name="environment">Gives an environment variable's value, or <see langword="null"/> when it is unset.</param>
internal sealed class GcsProtocolHandler(IConnector connector, Func<string, string?> environment) : IProtocolHandler
{
    /// <inheritdoc />
    public IReadOnlyCollection<string> SupportedSchemes { get; } = ["gs"];

    /// <inheritdoc />
    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        string? emulatorHost = environment("STORAGE_EMULATOR_HOST");
        if (GcsEndpoint.For(emulatorHost) is not { } endpoint)
        {
            return TransferResult.Failure(CurlExitCode.UrlMalformat, $"The Cloud Storage endpoint \"{emulatorHost}\" is not an http or https URL");
        }

        GcsHmacKey? hmacKey = context.Credentials is { } user ? new GcsHmacKey(user.UserName, user.Password) : null;
        GcsOperations operations = new(new GcsClient(connector, context, endpoint, BearerTokenOf(context), hmacKey), context.Url.Host, context.CancellationToken);
        try
        {
            return TransferResult.Success(await TransferAsync(operations, context, ObjectNameOf(context.Url)));
        }
        catch (GcsTransferException failure)
        {
            return TransferResult.Failure(failure.ExitCode, failure.Message);
        }
        catch (GcsHttpException failure)
        {
            return TransferResult.Failure(CurlExitCode.WeirdServerReply, failure.Message);
        }
    }

    private static string? BearerTokenOf(ITransferContext context) =>
        context.Http?.BearerToken is { } token && !string.IsNullOrWhiteSpace(token) ? token : null;

    private static string ObjectNameOf(CurlUrl url)
    {
        string path = Uri.UnescapeDataString(url.AbsolutePath);
        return path[Math.Min(1, path.Length)..];
    }

    private static Task<long> TransferAsync(GcsOperations operations, ITransferContext context, string objectName)
    {
        bool namesPrefix = objectName.Length == 0 || objectName.EndsWith('/');
        context.Progress.ReportTransferStarted();
        return context.Upload switch
        {
            null when namesPrefix => operations.ListAsync(objectName, context.Output),
            null => operations.DownloadAsync(objectName, context.Output),
            _ when namesPrefix => throw new GcsTransferException(CurlExitCode.UrlMalformat, "A Cloud Storage upload needs an object name in the URL"),
            { } upload => operations.UploadAsync(objectName, upload),
        };
    }
}
