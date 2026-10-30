using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.SegmentedDownload;

/// <summary>One response curl's handler received into memory, to be checked before any of it is written on.</summary>
/// <param name="Result">The handler's result.</param>
/// <param name="Headers">What it wrote to the header output (<c>-i</c>).</param>
/// <param name="DumpedHeaders">What it wrote to the dumped-header output (<c>-D</c>).</param>
/// <param name="Body">The body.</param>
internal sealed record CapturedResponse(TransferResult Result, byte[] Headers, byte[] DumpedHeaders, byte[] Body)
{
    /// <summary>Gets the response code, or 0 when the transfer failed or reported none.</summary>
    public int Code => Result is { IsSuccess: true, Report: { } report } ? report.ResponseCode : 0;

    /// <summary>Runs <paramref name="next" /> on <paramref name="context" /> with its outputs captured in memory.</summary>
    public static async Task<CapturedResponse> CaptureAsync(ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next)
    {
        using MemoryStream body = new();
        using MemoryStream headers = new();
        using MemoryStream dumpedHeaders = new();
        var result = await next(context.WithOutputs(body, headers, context.DumpHeaderOutput is null ? null : dumpedHeaders));
        return new CapturedResponse(result, headers.ToArray(), dumpedHeaders.ToArray(), body.ToArray());
    }

    /// <summary>Gives the first value of the response header field <paramref name="name" />; only for a response with a <see cref="Code" />.</summary>
    public string? Field(string name) =>
        Result.Report!.ResponseHeaders.FirstOrDefault(field => string.Equals(field.Key, name, StringComparison.OrdinalIgnoreCase)).Value;

    /// <summary>Writes the captured headers to <paramref name="context" />'s header outputs, those it has.</summary>
    public async Task WriteHeadersAsync(ITransferContext context)
    {
        await WriteIfGivenAsync(context.HeaderOutput, Headers, context.CancellationToken);
        await WriteIfGivenAsync(context.DumpHeaderOutput, DumpedHeaders, context.CancellationToken);
    }

    /// <summary>Writes the captured headers and body to <paramref name="context" />'s outputs and gives the result.</summary>
    public async Task<TransferResult> WriteAsync(ITransferContext context)
    {
        await WriteHeadersAsync(context);
        await context.Output.WriteAsync(Body, context.CancellationToken);
        return Result;
    }

    private static async Task WriteIfGivenAsync(Stream? output, byte[] bytes, CancellationToken cancellationToken)
    {
        if (output is not null)
        {
            await output.WriteAsync(bytes, cancellationToken);
        }
    }
}
