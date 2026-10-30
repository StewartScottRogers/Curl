using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.SegmentedDownload;

/// <summary>
/// The <c>--segments</c> transfer hook (ADR-1085). A plain HTTP GET first asks for its first byte
/// (<c>Range: bytes=0-0</c>, RFC 9110 §14.2) to learn the complete length from the 206's <c>Content-Range</c>
/// (§14.4). It then asks for <paramref name="count" /> consecutive ranges in parallel, each conditional on the
/// first response's strong entity tag through <c>If-Range</c> (§13.1.5), and writes them in order once every one
/// is a 206 of exactly its range of the same representation. A server that ignores <c>Range</c> and sends 200
/// (§14.2) is written as received; a 200 to a segment, the representation having changed under <c>If-Range</c>,
/// is written in place of the parts; a failed segment fails the transfer with nothing written; anything else that
/// cannot be joined runs the transfer again as curl runs it.
/// </summary>
/// <param name="count">How many ranges to split into, at least 1.</param>
internal sealed class SegmentedDownloadTransferHook(int count) : ITransferHook
{
    private const int Ok = 200;
    private const int PartialContent = 206;

    /// <summary>Gets how many ranges the hook splits a transfer into.</summary>
    public int Count => count;

    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next)
    {
        if (count == 1 || !SegmentableRequest.Is(context))
        {
            return await next(context);
        }

        var probe = await CapturedResponse.CaptureAsync(context.WithRequestHeaders(["Range: bytes=0-0"]), next);
        if (probe.Code == Ok)
        {
            return await probe.WriteAsync(context);
        }

        return probe.Code == PartialContent && ContentRange.Parse(probe.Field("Content-Range")) is { } first
            ? await DownloadSegmentsAsync(probe, first.CompleteLength, context, next)
            : await next(context);
    }

    private async Task<TransferResult> DownloadSegmentsAsync(CapturedResponse probe, long completeLength, ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next)
    {
        var ranges = SegmentRange.Split(completeLength, count);
        var ifRange = IfRangeFor(probe);
        var segments = await Task.WhenAll(ranges.Select(range =>
            CapturedResponse.CaptureAsync(context.WithRequestHeaders([range.RangeHeader, .. ifRange]), next)));
        return await FinishAsync(new Download(probe, ranges, segments, completeLength), context, next);
    }

    private static async Task<TransferResult> FinishAsync(Download download, ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next)
    {
        if (download.Segments.FirstOrDefault(segment => !segment.Result.IsSuccess) is { } failed)
        {
            return failed.Result;
        }

        if (download.Segments.FirstOrDefault(segment => segment.Code == Ok) is { } whole)
        {
            return await whole.WriteAsync(context);
        }

        return download.IsJoinable
            ? await JoinAsync(download.Segments, download.CompleteLength, context)
            : await next(context);
    }

    private static string[] IfRangeFor(CapturedResponse probe) =>
        probe.Field("ETag") is { } tag && tag.StartsWith('"') ? ["If-Range: " + tag] : [];

    /// <summary>The first response, the ranges asked for and the segments received, in the same order.</summary>
    private sealed record Download(CapturedResponse Probe, IReadOnlyList<SegmentRange> Ranges, CapturedResponse[] Segments, long CompleteLength)
    {
        /// <summary>Gets whether every segment is a 206 of exactly its range of the first response's representation.</summary>
        public bool IsJoinable => Ranges.Select((range, index) => IsPartOf(Segments[index], range)).All(matches => matches);

        private bool IsPartOf(CapturedResponse segment, SegmentRange range) =>
            segment.Code == PartialContent
            && ContentRange.Parse(segment.Field("Content-Range")) == new ContentRange(range, CompleteLength)
            && segment.Body.LongLength == range.Length
            && segment.Field("ETag") == Probe.Field("ETag")
            && segment.Field("Last-Modified") == Probe.Field("Last-Modified");
    }

    private static async Task<TransferResult> JoinAsync(CapturedResponse[] segments, long completeLength, ITransferContext context)
    {
        await segments[0].WriteHeadersAsync(context);
        foreach (var segment in segments)
        {
            await context.Output.WriteAsync(segment.Body, context.CancellationToken);
        }

        return segments[0].Result with { BytesTransferred = completeLength };
    }
}
