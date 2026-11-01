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
        string bucket = context.Url.Host;
        S3Configuration configuration = S3Configuration.From(environment, context.Credentials);
        if (S3Endpoint.For(bucket, configuration.Region, configuration.EndpointUrl) is not { } endpoint)
        {
            return TransferResult.Failure(CurlExitCode.UrlMalformat, $"The S3 endpoint \"{configuration.EndpointUrl}\" is not an http or https URL");
        }

        S3Operations operations = new(new S3Client(connector, context, configuration, endpoint), endpoint, bucket, partSize, context.CancellationToken);
        try
        {
            return TransferResult.Success(await TransferAsync(operations, context, KeyOf(context.Url)));
        }
        catch (S3TransferException failure)
        {
            return TransferResult.Failure(failure.ExitCode, failure.Message);
        }
        catch (S3HttpException failure)
        {
            return TransferResult.Failure(CurlExitCode.WeirdServerReply, failure.Message);
        }
    }

    private static string KeyOf(CurlUrl url)
    {
        string path = Uri.UnescapeDataString(url.AbsolutePath);
        return path[Math.Min(1, path.Length)..];
    }

    private static Task<long> TransferAsync(S3Operations operations, ITransferContext context, string key)
    {
        bool namesPrefix = key.Length == 0 || key.EndsWith('/');
        context.Progress.ReportTransferStarted();
        return context.Upload switch
        {
            null when namesPrefix => operations.ListAsync(key, context.Output),
            null => operations.DownloadAsync(key, context.Output),
            _ when namesPrefix => throw new S3TransferException(CurlExitCode.UrlMalformat, "An S3 upload needs an object key in the URL"),
            { } upload => operations.UploadAsync(key, upload),
        };
    }
}
