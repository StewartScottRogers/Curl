using System.Security.Cryptography;
using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.DeltaDownload;

/// <summary>
/// The <c>--zsync</c> transfer hook (ADR-1092). For a plain HTTP GET it reads the zsync 0.6 control file, finds the
/// target's blocks in the local seed file, asks the URL only for the ranges of the blocks still missing
/// (<c>Range: bytes=first-last</c>, one request per run of missing blocks) and writes the joined file once its SHA-1
/// equals the control file's. A 200 to a range, the server ignoring <c>Range</c>, is written as received; a failed
/// request fails the transfer with nothing written; a range answered with anything else, or a joined file whose
/// SHA-1 differs, runs the transfer again as curl runs it.
/// </summary>
/// <param name="controlPath">The control file's path.</param>
/// <param name="seedPath">The seed file's path, or <see langword="null" /> for the control file's <c>Filename</c> in the current directory.</param>
internal sealed class DeltaDownloadTransferHook(string controlPath, string? seedPath) : ITransferHook
{
    private const int Ok = 200;
    private const int PartialContent = 206;

    /// <summary>Gets the control file's path.</summary>
    public string ControlPath => controlPath;

    /// <summary>Gets the seed file's path, or <see langword="null" /> for the control file's <c>Filename</c>.</summary>
    public string? SeedPath => seedPath;

    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next)
    {
        if (!WholeResourceGet.Is(context))
        {
            return await next(context);
        }

        if (await ReadAsync(controlPath, context.CancellationToken) is not { } bytes)
        {
            return TransferResult.Failure(CurlExitCode.ReadError, $"--zsync could not read the control file {controlPath}");
        }

        if (ZsyncControlFile.Parse(bytes) is not { } control)
        {
            return TransferResult.Failure(CurlExitCode.ReadError, $"--zsync: {controlPath} is not a zsync control file");
        }

        var seed = await ReadAsync(seedPath ?? Path.GetFileName(control.FileName) ?? string.Empty, context.CancellationToken) ?? [];
        return await FetchMissingAsync(control, SeedBlocks.Find(control, seed), context, next);
    }

    private static async Task<TransferResult> FetchMissingAsync(ZsyncControlFile control, SeedBlocks blocks, ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next)
    {
        CapturedResponse? first = null;
        foreach (var (start, last) in blocks.MissingRanges())
        {
            var part = await CapturedResponse.CaptureAsync(context.WithRequestHeaders([$"Range: bytes={start}-{last}"]), next);
            if (!part.Result.IsSuccess || part.Code == Ok)
            {
                return part.Result.IsSuccess ? await part.WriteAsync(context) : part.Result;
            }

            if (!IsRange(part, start, last, control.Length))
            {
                return await next(context);
            }

            part.Body.CopyTo(blocks.Target, start);
            first ??= part;
        }

        var file = blocks.Target.AsMemory(0, control.Length);
        return SHA1.HashData(file.Span).AsSpan().SequenceEqual(control.Sha1)
            ? await WriteAsync(first, file, context)
            : await next(context);
    }

    private static bool IsRange(CapturedResponse part, long first, long last, int length) =>
        part.Code == PartialContent
        && part.Field("Content-Range") == $"bytes {first}-{last}/{length}"
        && part.Body.LongLength == last - first + 1;

    private static async Task<TransferResult> WriteAsync(CapturedResponse? first, ReadOnlyMemory<byte> file, ITransferContext context)
    {
        if (first is not null)
        {
            await first.WriteHeadersAsync(context);
        }

        await context.Output.WriteAsync(file, context.CancellationToken);
        return first is null ? TransferResult.Success(file.Length) : first.Result with { BytesTransferred = file.Length };
    }

    private static async Task<byte[]?> ReadAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            return await File.ReadAllBytesAsync(path, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }
}
