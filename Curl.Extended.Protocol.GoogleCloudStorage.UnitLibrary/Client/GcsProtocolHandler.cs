using Curl.Extended.Protocol.GoogleCloudStorage.Http;
using Curl.Extended.Protocol.GoogleCloudStorage.Signing;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.GoogleCloudStorage.Client;

/// <summary>
/// Serves <c>gs://bucket/object</c> (ADR-1099): with <c>-T</c> it uploads the object, with an empty name or one ending in
/// <c>/</c> it lists the names under it, otherwise it downloads the object. <c>--oauth2-bearer</c> sends an OAuth 2.0
/// access token; otherwise <c>--user ACCESS_ID:SECRET</c> signs with an HMAC key; with neither the requests go
/// unauthenticated, for public objects. A region other than <c>auto</c> is named in curl's own
/// <c>--aws-sigv4 provider1:provider2:region:service</c> form (ADR-1191). The server is <see cref="GcsEndpoint"/>; the requests are <see cref="GcsOperations"/>.
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
        if (GcsRequestMethod.RefusalOf(context) is { } refusal)
        {
            return TransferResult.Failure(CurlExitCode.NotBuiltIn, refusal);
        }

        string? emulatorHost = environment("STORAGE_EMULATOR_HOST");
        if (GcsEndpoint.For(emulatorHost) is not { } endpoint)
        {
            return TransferResult.Failure(CurlExitCode.UrlMalformat, $"The Cloud Storage endpoint \"{emulatorHost}\" is not an http or https URL");
        }

        GcsHmacKey? hmacKey = context.Credentials is { } user ? new GcsHmacKey(user.UserName, user.Password, RegionOf(context)) : null;
        GcsHttpHeadReporter headReporter = new(context.Events, context.HeaderOutput);
        GcsOperations operations = new(new GcsClient(connector, context, endpoint, BearerTokenOf(context), hmacKey, headReporter), context.Url.Host, context.CancellationToken);
        try
        {
<<<<<<< HEAD
            long bytes = await TransferAsync(operations, context, ObjectNameOf(context.Url));
            return TransferResult.Success(bytes) with { Report = headReporter.Report?.WithDownloadSize(bytes) };
=======
            long downloaded = await TransferAsync(operations, context, ObjectNameOf(context.Url));
            return TransferResult.Success(downloaded) with { Report = headReporter.ReportWith(context.Upload is null ? downloaded : 0) };
>>>>>>> 6462aa6a1 (fix(extended): report size_download for -w on s3, gs, az, abfs, oci and grpc downloads (BL-4997))
        }
        catch (GcsTransferException failure)
        {
            return TransferResult.Failure(failure.ExitCode, failure.Message) with { Report = headReporter.Report };
        }
        catch (GcsHttpException failure)
        {
            return TransferResult.Failure(CurlExitCode.WeirdServerReply, failure.Message) with { Report = headReporter.Report };
        }
    }

    /// <summary>The third field of <c>--aws-sigv4</c> (<c>goog4:goog:us-east1:storage</c> gives <c>us-east1</c>), or <c>auto</c> when it names none.</summary>
    private static string RegionOf(ITransferContext context)
    {
        string[] fields = (context.Http?.AwsSigV4 ?? string.Empty).Split(':');
        return fields.Length > 2 && fields[2].Length > 0 ? fields[2] : GcsHmacKey.AutoRegion;
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
