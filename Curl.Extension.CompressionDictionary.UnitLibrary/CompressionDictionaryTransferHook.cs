using Curl.Extension.Abstractions.Transfer;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.CompressionDictionary;

/// <summary>
/// The <c>--compression-dictionary</c> transfer hook (ADR-1154), a client of RFC 9842. On an HTTP or HTTPS
/// transfer it looks up the dictionary whose <c>match</c> fits the request URL best (§2.2.2) and, when curl's
/// handler is not decoding content itself, sends <c>Available-Dictionary</c> (§2.2), <c>Dictionary-ID</c> (§2.3)
/// and <c>Accept-Encoding: dcb, dcz</c> (§6.1). A <c>dcb</c> or <c>dcz</c> response compressed against it is decoded
/// (§4, §5) before curl writes the body; one that does not decode fails the transfer with exit 61, as curl fails
/// a body it cannot decode. A successful response that carries <c>Use-As-Dictionary</c> (§2.1) is stored, decoded,
/// as a dictionary. A directory that cannot be written leaves the transfer as it was.
/// </summary>
/// <param name="directory">Where the dictionaries are stored.</param>
internal sealed class CompressionDictionaryTransferHook(string directory) : ITransferHook
{
    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next)
    {
        if (DictionaryTransferRequest.TargetUriOf(context) is not { } target)
        {
            return await next(context);
        }

        var store = new DictionaryStore(directory, context.TimeProvider);
        var offered = DictionaryTransferRequest.MayAdvertise(context) ? await FindOfferAsync(store, target, context.CancellationToken) : null;
        using MemoryStream received = new();
        var result = await next(Advertising(offered, context).WithOutputs(received, context.HeaderOutput, context.DumpHeaderOutput));
        var body = received.ToArray();
        var coding = DictionaryCompressedBody.CodingOf(DictionaryTransferRequest.ResponseFieldOf(result, "Content-Encoding"));
        if (offered is not null && coding is { } encoding && result.IsSuccess)
        {
            try
            {
                body = DictionaryCompressedBody.Decode(encoding, body, offered.Dictionary, offered.Content);
            }
            catch (InvalidDataException exception)
            {
                return TransferResult.Failure(CurlExitCode.BadContentEncoding, "Failed to decode the " + encoding + " body: " + exception.Message, result.BytesTransferred) with { Report = result.Report };
            }
        }

        await context.Output.WriteAsync(body, context.CancellationToken);
        await TryStoreAsync(store, target, result, body, context.CancellationToken);
        return result;
    }

    private static async Task<DictionaryOffer?> FindOfferAsync(DictionaryStore store, Uri target, CancellationToken cancellationToken)
    {
        var dictionary = await store.FindBestMatchAsync(target, cancellationToken);
        var content = dictionary is null ? null : await store.ReadContentAsync(dictionary, cancellationToken);
        return content is null ? null : new DictionaryOffer(dictionary!, content);
    }

    private static ITransferContext Advertising(DictionaryOffer? offered, ITransferContext context)
    {
        if (offered is null)
        {
            return context;
        }

        List<string> headers = ["Available-Dictionary: " + DictionaryRequestHeaders.BuildAvailableDictionary(offered.Dictionary.Hash)];
        if (DictionaryRequestHeaders.BuildDictionaryId(offered.Dictionary.Directives.Id) is { } id)
        {
            headers.Add("Dictionary-ID: " + id);
        }

        headers.Add("Accept-Encoding: dcb, dcz");
        return context.WithRequestHeaders(headers);
    }

    private static async Task TryStoreAsync(DictionaryStore store, Uri target, TransferResult result, byte[] body, CancellationToken cancellationToken)
    {
        if (DirectivesToStore(result) is not { } directives)
        {
            return;
        }

        try
        {
            await store.StoreAsync(target, directives, body, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A store that cannot be written leaves the transfer as it was.
        }
    }

    private static UseAsDictionary? DirectivesToStore(TransferResult result) =>
        IsSuccessfulResponse(result)
        && DictionaryTransferRequest.ResponseFieldOf(result, "Use-As-Dictionary") is { } field
            ? UseAsDictionary.Parse(field)
            : null;

    private static bool IsSuccessfulResponse(TransferResult result) =>
        result is { IsSuccess: true, Report.ResponseCode: >= 200 and < 300 };

    /// <summary>The dictionary advertised on a request and its bytes.</summary>
    private sealed record DictionaryOffer(StoredDictionary Dictionary, byte[] Content);
}
