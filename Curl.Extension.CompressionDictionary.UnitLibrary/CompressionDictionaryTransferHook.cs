using Curl.Extension.Abstractions.Transfer;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.CompressionDictionary;

/// <summary>
/// The <c>--compression-dictionary</c> transfer hook (ADR-1154), a client of RFC 9842. On an HTTP or HTTPS
/// transfer it looks up the dictionary whose <c>match</c> fits the request URL best (§2.2.2) and sends
/// <c>Available-Dictionary</c> (§2.2), <c>Dictionary-ID</c> (§2.3) and <c>Accept-Encoding: dcb, dcz</c> (§6.1), after
/// curl's own <c>--compressed</c> codings when that is given, running curl's handler with content decoding off so the
/// hook decodes them itself (BL-4351). A <c>dcb</c> or <c>dcz</c> response compressed against it is decoded
/// (§4, §5) before curl writes the body; one that does not decode fails the transfer with exit 61, as curl fails
/// a body it cannot decode. A successful response that carries <c>Use-As-Dictionary</c> (§2.1) is stored, decoded,
/// as a dictionary. A directory that cannot be written leaves the transfer as it was.
/// </summary>
/// <param name="directory">Where the dictionaries are stored.</param>
internal sealed class CompressionDictionaryTransferHook(string directory) : ITransferHook
{
    /// <summary>The most of a streamed body kept to store as a dictionary; a longer one streams but is not stored (ADR-1171).</summary>
    internal const int StoredBodyLimit = 16 * 1024 * 1024;

    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next)
    {
        if (DictionaryTransferRequest.TargetUriOf(context) is not { } target)
        {
            return await next(context);
        }

        var store = new DictionaryStore(directory, context.TimeProvider);
        var offered = DictionaryTransferRequest.MayAdvertise(context) ? await FindOfferAsync(store, target, context.CancellationToken) : null;
        if (offered is null)
        {
            return await StreamAsync(store, target, context, next);
        }

        var decodesForCurl = context.Http!.Compressed;
        using MemoryStream received = new();
        var result = await next(Advertising(offered, context, decodesForCurl).WithOutputs(received, context.HeaderOutput, context.DumpHeaderOutput));
        byte[] body;
        try
        {
            body = DecodedBody(offered, result, decodesForCurl, received.ToArray());
        }
        catch (InvalidDataException exception)
        {
            return TransferResult.Failure(CurlExitCode.BadContentEncoding, exception.Message, result.BytesTransferred) with { Report = result.Report };
        }

        await context.Output.WriteAsync(body, context.CancellationToken);
        await TryStoreAsync(store, EffectiveUriOf(result, target), result, body, context.CancellationToken);
        return result;
    }

    /// <summary>
    /// Runs a transfer that advertised no dictionary, so its body needs no decoding: the body goes to the output as it
    /// arrives, and a copy of up to <see cref="StoredBodyLimit" /> bytes is kept in case the response turns out to be a dictionary.
    /// </summary>
    private static async ValueTask<TransferResult> StreamAsync(DictionaryStore store, Uri target, ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next)
    {
        CopyingStream copying = new(context.Output, StoredBodyLimit);
        var result = await next(context.WithOutputs(copying, context.HeaderOutput, context.DumpHeaderOutput));
        if (!copying.Overflowed)
        {
            await TryStoreAsync(store, EffectiveUriOf(result, target), result, copying.Copied, context.CancellationToken);
        }

        return result;
    }

    /// <summary>The URL the response came from (RFC 9842 section 2.1): after <c>-L</c>, not the one requested.</summary>
    private static Uri EffectiveUriOf(TransferResult result, Uri requested) =>
        result.Report?.EffectiveUrl is { } url && Uri.TryCreate(url, UriKind.Absolute, out var effective) ? effective : requested;
    /// <summary>The body curl writes: <paramref name="received" /> with the codings the hook decodes undone.</summary>
    /// <exception cref="InvalidDataException">A coding does not decode; the message names the codings.</exception>
    private static byte[] DecodedBody(DictionaryOffer offered, TransferResult result, bool decodesForCurl, byte[] received)
    {
        var codings = result.IsSuccess ? CodingsToDecode(result, decodesForCurl) : [];
        try
        {
            return codings.Count == 0 ? received : CompressedResponseBody.Decode(codings, received, offered.Dictionary, offered.Content);
        }
        catch (InvalidDataException exception)
        {
            throw new InvalidDataException("Failed to decode the " + string.Join(", ", codings) + " body: " + exception.Message, exception);
        }
    }

    /// <summary>
    /// The codings the hook decodes: with <c>--compressed</c>, which it took over from curl's handler, every coding the
    /// response names; without it only a <c>Content-Encoding</c> of exactly <c>dcb</c> or <c>dcz</c>, as curl leaves the rest.
    /// </summary>
    private static IReadOnlyList<string> CodingsToDecode(TransferResult result, bool decodesForCurl)
    {
        if (decodesForCurl)
        {
            return CompressedResponseBody.CodingsOf(DictionaryTransferRequest.ResponseFieldsOf(result, "Content-Encoding"));
        }

        var field = DictionaryTransferRequest.ResponseFieldOf(result, "Content-Encoding");
        return DictionaryCompressedBody.CodingOf(field) is null ? [] : [field!];
    }

    private static async Task<DictionaryOffer?> FindOfferAsync(DictionaryStore store, Uri target, CancellationToken cancellationToken)
    {
        var dictionary = await store.FindBestMatchAsync(target, cancellationToken);
        var content = dictionary is null ? null : await store.ReadContentAsync(dictionary, cancellationToken);
        return content is null ? null : new DictionaryOffer(dictionary!, content);
    }

    private static ITransferContext Advertising(DictionaryOffer offered, ITransferContext context, bool decodesForCurl)
    {
        List<string> headers = ["Available-Dictionary: " + DictionaryRequestHeaders.BuildAvailableDictionary(offered.Dictionary.Hash)];
        if (DictionaryRequestHeaders.BuildDictionaryId(offered.Dictionary.Directives.Id) is { } id)
        {
            headers.Add("Dictionary-ID: " + id);
        }

        // RFC 9842 section 6: with --compressed, curl's own codings come first and the handler leaves decoding to the hook.
        headers.Add("Accept-Encoding: " + (decodesForCurl ? context.Http!.AcceptEncoding + ", " : string.Empty) + "dcb, dcz");
        return (decodesForCurl ? context.WithContentDecodingOff() : context).WithRequestHeaders(headers);
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

    /// <summary>Writes through to <paramref name="destination" /> and keeps a copy of the first <paramref name="limit" /> bytes.</summary>
    internal sealed class CopyingStream(Stream destination, int limit) : Stream
    {
        private readonly MemoryStream _copy = new();

        public bool Overflowed { get; private set; }

        public byte[] Copied => _copy.ToArray();

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => destination.Flush();

        public override Task FlushAsync(CancellationToken cancellationToken) => destination.FlushAsync(cancellationToken);

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            destination.Write(buffer);
            Keep(buffer);
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await destination.WriteAsync(buffer, cancellationToken);
            Keep(buffer.Span);
        }

        private void Keep(ReadOnlySpan<byte> buffer)
        {
            if (Overflowed || _copy.Length + buffer.Length > limit)
            {
                Overflowed = true;
                return;
            }

            _copy.Write(buffer);
        }
    }
}
