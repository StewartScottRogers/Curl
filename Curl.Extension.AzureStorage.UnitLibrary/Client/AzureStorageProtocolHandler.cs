using Curl.Extension.AzureStorage.Http;
using Curl.Extension.AzureStorage.Requests;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.AzureStorage.Client;

/// <summary>
/// Serves <c>az://account/container/blob</c> through <see cref="AzureBlobOperations"/> and
/// <c>abfs[s]://filesystem@account/path</c> through <see cref="AzureDataLakeOperations"/> (ADR-1101): with <c>-T</c> it
/// uploads, with an empty name or one ending in <c>/</c> it lists, otherwise it downloads. The URL is mapped by
/// <see cref="AzureStorageUrlMapping"/> and authorized with the credential <see cref="AzureStorageCredential.Choose"/> picks.
/// </summary>
/// <param name="connector">Opens each connection.</param>
/// <param name="environment">Gives an environment variable's value, or <see langword="null"/> when it is unset.</param>
/// <param name="blockSize">The size of each uploaded block, and the size from which an upload is sent in blocks.</param>
internal sealed class AzureStorageProtocolHandler(IConnector connector, Func<string, string?> environment, int blockSize) : IProtocolHandler
{
    /// <inheritdoc />
    public IReadOnlyCollection<string> SupportedSchemes { get; } = ["az", "abfs", "abfss"];

    /// <inheritdoc />
    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            AzureStorageEndpoint endpoint = AzureStorageUrlMapping.Map(new Uri(context.Url.OriginalString));
            AzureStorageCredential credential = AzureStorageCredential.Choose(context.Http?.BearerToken, environment);
            AzureStorageClient client = new(connector, context, new AzureStorageRequestAuthorizer(context.TimeProvider), credential);
            context.Progress.ReportTransferStarted();
            Task<long> transfer = context.Url.Scheme == "az"
                ? new AzureBlobOperations(client, blockSize).TransferAsync(endpoint, context.Upload, context.Output)
                : new AzureDataLakeOperations(client, blockSize).TransferAsync(endpoint, context.Upload, context.Output);
            return TransferResult.Success(await transfer);
        }
        catch (AzureStorageRequestException failure)
        {
            return TransferResult.Failure(ExitCodeOf(failure.Failure), failure.Message);
        }
        catch (AzureStorageTransferException failure)
        {
            return TransferResult.Failure(failure.ExitCode, failure.Message);
        }
        catch (AzureStorageHttpException failure)
        {
            return TransferResult.Failure(CurlExitCode.WeirdServerReply, failure.Message);
        }
    }

    private static CurlExitCode ExitCodeOf(AzureStorageRequestFailure failure) =>
        failure is AzureStorageRequestFailure.InvalidSasToken or AzureStorageRequestFailure.InvalidBearerToken
            ? CurlExitCode.LoginDenied
            : CurlExitCode.UrlMalformat;
}
