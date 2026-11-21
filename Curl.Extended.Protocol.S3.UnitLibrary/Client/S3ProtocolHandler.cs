using Curl.Extended.Protocol.S3.Http;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.S3.Client;

/// <summary>
/// Serves <c>s3://bucket/key</c> (ADR-1090): with <c>-T</c> it uploads to the key, with an empty key or one ending in
/// <c>/</c> it lists the keys under it, otherwise it downloads the object. Where it connects and how it signs come from
/// <see cref="S3Configuration"/> and <see cref="S3Endpoint"/>; the requests are <see cref="S3Operations"/>.
/// </summary>
/// <param name="connector">Opens each connection.</param>
/// <param name="environment">Gives an environment variable's value, or <see langword="null"/> when it is unset.</param>
/// <param name="partSize">The size of each part of a multipart upload.</param>
internal sealed class S3ProtocolHandler(IConnector connector, Func<string, string?> environment, int partSize) : IProtocolHandler
{
    /// <inheritdoc />
    public IReadOnlyCollection<string> SupportedSchemes { get; } = ["s3"];

    /// <inheritdoc />
    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (S3RequestMethod.RefusalOf(context) is { } refusal)
        {
            return TransferResult.Failure(CurlExitCode.NotBuiltIn, refusal);
        }

        string bucket = context.Url.Host;
        S3Configuration configuration = S3Configuration.From(environment, context.Credentials);
        if (S3Endpoint.For(bucket, configuration.Region, configuration.EndpointUrl) is not { } endpoint)
        {
            return TransferResult.Failure(CurlExitCode.UrlMalformat, $"The S3 endpoint \"{configuration.EndpointUrl}\" is not an http or https URL");
        }

        S3HttpHeadReporter headReporter = new(context.Events, context.HeaderOutput);
        S3Operations operations = new(new S3Client(connector, context, configuration, endpoint, headReporter), endpoint, bucket, partSize, context.CancellationToken);
        try
        {
            long downloaded = await TransferAsync(operations, context, KeyOf(context.Url));
            return TransferResult.Success(downloaded) with { Report = headReporter.ReportWith(context.Upload is null ? downloaded : 0) };
        }
        catch (S3TransferException failure)
        {
            return TransferResult.Failure(failure.ExitCode, failure.Message) with { Report = headReporter.Report };
        }
        catch (S3HttpException failure)
        {
            return TransferResult.Failure(CurlExitCode.WeirdServerReply, failure.Message) with { Report = headReporter.Report };
        }
    }

    private static string KeyOf(CurlUrl url)
    {
        string path = Uri.UnescapeDataString(url.AbsolutePath);
        return path[Math.Min(1, path.Length)..];
    }

    // A bucket or prefix URL is refused rather than deleting the bucket: -X DELETE removes one object (BL-5647).
    private static Task<long> DeleteObjectAsync(S3Operations operations, ITransferContext context, string key) =>
        IsPrefix(key)
            ? throw new S3TransferException(CurlExitCode.UrlMalformat, "An S3 delete needs an object key in the URL")
            : operations.DeleteAsync(key, S3Endpoint.QueryOf(context.Url.Query));

    private static Task<long> TransferAsync(S3Operations operations, ITransferContext context, string key)
    {
        context.Progress.ReportTransferStarted();
        return context.Upload is { } upload
            ? UploadObjectAsync(operations, upload, key)
            : context.Http?.CustomMethod == "DELETE" ? DeleteObjectAsync(operations, context, key) : ReadAsync(operations, context, key);
    }

    private static Task<long> ReadAsync(S3Operations operations, ITransferContext context, string key) =>
        IsPrefix(key)
            ? operations.ListAsync(key, S3Endpoint.QueryOf(context.Url.Query), context.Output)
            : operations.DownloadAsync(key, S3Endpoint.QueryOf(context.Url.Query), context.Output);

    private static Task<long> UploadObjectAsync(S3Operations operations, Stream upload, string key) =>
        IsPrefix(key)
            ? throw new S3TransferException(CurlExitCode.UrlMalformat, "An S3 upload needs an object key in the URL")
            : operations.UploadAsync(key, upload);

    private static bool IsPrefix(string key) => key.Length == 0 || key.EndsWith('/');
}
