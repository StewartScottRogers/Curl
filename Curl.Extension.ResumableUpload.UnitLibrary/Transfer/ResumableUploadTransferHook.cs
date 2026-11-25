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
/// A non-seekable upload, or one already read part way, is read whole into memory first, since both protocols announce
/// the upload's length on creation and a resumption sends again from the server's offset.
/// </remarks>
public sealed class ResumableUploadTransferHook : ITransferHook
{
    /// <summary>How many times a broken step is tried again unless the constructor says otherwise.</summary>
    public const int DefaultMaxResumptions = 3;

    /// <summary>Initializes a new instance of the <see cref="ResumableUploadTransferHook" /> class.</summary>
    /// <param name="maxResumptions">How many times a step broken by its connection is tried again.</param>
    public ResumableUploadTransferHook(int maxResumptions = DefaultMaxResumptions)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxResumptions);
        MaxResumptions = maxResumptions;
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

        var body = await SeekableAsync(context.Upload!, context.CancellationToken);
        var target = TargetOf(context.Url);
        var session = new ResumableUploadSession(context, next, target);
        try
        {
            var options = await session.SendAsync("OPTIONS", target, []);
            IResumableUploadProtocol protocol = OffersTus(options) ? TusUploadProtocol.Instance : new IetfUploadProtocol(context.Http?.CustomMethod ?? "PUT");
            var response = await new ResumableUploadFlow(protocol, session, MaxResumptions).UploadAsync(target, body);
            return await ResumableUploadOutcome.WriteAsync(context, response, body.Length);
        }
        catch (ResumableUploadTransferException exception)
        {
            return TransferResult.Failure(exception.ExitCode, exception.Message);
        }
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

    private static async Task<Stream> SeekableAsync(Stream upload, CancellationToken cancellationToken)
    {
        if (upload.CanSeek && upload.Position == 0)
        {
            return upload;
        }

        var buffer = new MemoryStream();
        await upload.CopyToAsync(buffer, cancellationToken);
        return buffer;
    }
}
