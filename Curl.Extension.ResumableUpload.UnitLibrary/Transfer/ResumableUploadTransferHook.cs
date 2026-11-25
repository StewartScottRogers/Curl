using Curl.Extension.Abstractions.Transfer;
using Curl.Extension.ResumableUpload.Fields;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.ResumableUpload.Transfer;

/// <summary>
/// Uploads an http or https transfer's <c>-T</c> body resumably in place of curl's handler: by tus 1.0 when the server's
/// <c>OPTIONS</c> response offers version 1.0.0 with the "creation" extension, otherwise by the IETF resumable upload
/// draft, resuming from the server's offset after a broken connection. Any other transfer goes to curl's handler unchanged.
/// </summary>
/// <remarks>
/// Both protocols announce the upload's length on creation and a resumption sends again from the server's offset, so a
/// non-seekable upload is copied first: into memory up to a limit, past it into a temporary file deleted when the
/// transfer ends (ADR-1401). It ends with <see cref="CurlExitCode.ReadError" /> when the upload cannot be read and
/// <see cref="CurlExitCode.WriteError" /> when the temporary file cannot be written. A seekable upload is sent from
/// where it stands without a copy.
/// </remarks>
public sealed class ResumableUploadTransferHook : ITransferHook
{
    /// <summary>How many times a broken step is tried again unless the constructor says otherwise.</summary>
    public const int DefaultMaxResumptions = 3;

    /// <summary>How many bytes of a non-seekable upload are held in memory unless the constructor says otherwise.</summary>
    public const int DefaultMemoryLimit = 16 * 1024 * 1024;

    private readonly int memoryLimit;
    private readonly Func<Stream> createSpoolFile;

    /// <summary>Initializes a new instance of the <see cref="ResumableUploadTransferHook" /> class.</summary>
    /// <param name="maxResumptions">How many times a step broken by its connection is tried again.</param>
    /// <param name="memoryLimit">The most bytes of a non-seekable upload held in memory.</param>
    /// <param name="createSpoolFile">
    /// Creates the empty, seekable stream a longer non-seekable upload is copied into, disposed when the transfer ends;
    /// <see langword="null" /> for a temporary file.
    /// </param>
    public ResumableUploadTransferHook(int maxResumptions = DefaultMaxResumptions, int memoryLimit = DefaultMemoryLimit, Func<Stream>? createSpoolFile = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxResumptions);
        ArgumentOutOfRangeException.ThrowIfNegative(memoryLimit);
        MaxResumptions = maxResumptions;
        this.memoryLimit = memoryLimit;
        this.createSpoolFile = createSpoolFile ?? RereadableUpload.CreateTemporaryFile;
    }

    /// <summary>Gets how many times a step broken by its connection is tried again.</summary>
    public int MaxResumptions { get; }

    /// <inheritdoc />
    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        if (!IsResumableUpload(context))
        {
            return await next(context);
        }

        try
        {
            return await UploadAsync(context, next);
        }
        catch (ResumableUploadTransferException exception)
        {
            return TransferResult.Failure(exception.ExitCode, exception.Message);
        }
    }

    private async Task<TransferResult> UploadAsync(ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next)
    {
        await using var body = await RereadableUpload.OpenAsync(context.Upload!, memoryLimit, createSpoolFile, context.CancellationToken);
        var target = TargetOf(context.Url);
        var session = new ResumableUploadSession(context, next, target);
        var options = await session.SendAsync("OPTIONS", target, []);
        IResumableUploadProtocol protocol = OffersTus(options) ? TusUploadProtocol.Instance : new IetfUploadProtocol(context.Http?.CustomMethod ?? "PUT");
        var response = await new ResumableUploadFlow(protocol, session, MaxResumptions).UploadAsync(target, body);
        return await ResumableUploadOutcome.WriteAsync(context, response, body.Length);
    }

    private static bool IsResumableUpload(ITransferContext context) =>
        context.Upload is not null
        && (context.Url.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) || context.Url.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase));

    private static Uri TargetOf(CurlUrl url) =>
        new($"{url.Scheme.ToLowerInvariant()}://{url.Host}:{url.Port}{url.RequestPath}{(url.Query is null ? string.Empty : "?" + url.Query)}");

    private static bool OffersTus(ResumableUploadExchange options) =>
        options.IsSuccess
        && TusVersionField.TryParse(options.FindHeader(TusVersionField.Name), out var versions) && versions.Contains(TusResumableField.Version)
        && TusExtensionField.TryParse(options.FindHeader(TusExtensionField.Name), out var extensions) && extensions.Contains("creation");
}
