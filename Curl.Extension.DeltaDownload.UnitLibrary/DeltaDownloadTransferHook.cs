using System.Security.Cryptography;
using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.DeltaDownload;

/// <summary>
/// The <c>--zsync</c> transfer hook (ADR-1092). For a plain HTTP GET it reads the zsync 0.6 control file, finds the
/// target's blocks in the local seed file, asks the URL only for the ranges of the blocks still missing
/// (<c>Range: bytes=first-last</c>, one request per run of missing blocks) and writes the joined file once its SHA-1
/// equals the control file's. A range answered with anything but that range or a 200, or a joined file whose SHA-1
/// differs, runs the transfer again as curl runs it. A failed request fails the transfer with nothing written. A
/// whole response (a 200 to a range, the server ignoring <c>Range</c>, or the transfer run again) is written only
/// when its body's SHA-1 equals the control file's; otherwise the transfer fails with exit 61
/// (<see cref="CurlExitCode.BadContentEncoding" />) and nothing is written (ADR-1347).
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
            if (await EndBeforeJoiningAsync(part, start, last, control, context, next) is { } ended)
            {
                return ended;
            }

            part.Body.CopyTo(blocks.Target, start);
            first ??= part;
        }

        return await WriteIfIntactAsync(control, blocks, first, context, next);
    }

    private static async Task<TransferResult?> EndBeforeJoiningAsync(CapturedResponse part, long start, long last, ZsyncControlFile control, ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next)
    {
        if (!part.Result.IsSuccess)
        {
            return part.Result;
        }

        if (part.Code == Ok)
        {
            return await WriteWholeIfIntactAsync(control, part, context);
        }

        return IsRange(part, start, last, control.Length) ? null : await RunAgainAsync(control, context, next);
    }

    private static async Task<TransferResult> WriteIfIntactAsync(ZsyncControlFile control, SeedBlocks blocks, CapturedResponse? first, ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next)
    {
        var file = blocks.Target.AsMemory(0, control.Length);
        return SHA1.HashData(file.Span).AsSpan().SequenceEqual(control.Sha1)
            ? await WriteAsync(first, file, context)
            : await RunAgainAsync(control, context, next);
    }

    private static async Task<TransferResult> RunAgainAsync(ZsyncControlFile control, ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next)
    {
        var whole = await CapturedResponse.CaptureAsync(context, next);
        return whole.Result.IsSuccess ? await WriteWholeIfIntactAsync(control, whole, context) : whole.Result;
    }

    private static async Task<TransferResult> WriteWholeIfIntactAsync(ZsyncControlFile control, CapturedResponse whole, ITransferContext context)
    {
        var sha1 = SHA1.HashData(whole.Body);
        return sha1.AsSpan().SequenceEqual(control.Sha1)
            ? await whole.WriteAsync(context)
            : whole.Result with
            {
                ExitCode = CurlExitCode.BadContentEncoding,
                ErrorMessage = $"--zsync: SHA-1 mismatch: expected {Convert.ToHexStringLower(control.Sha1)}, got {Convert.ToHexStringLower(sha1)}",
            };
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
