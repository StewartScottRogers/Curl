using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Puts a transfer's <c>-T</c> source and <c>-F</c> body back where they stood before its first attempt, so a
/// <c>--retry</c> attempt sends the whole body again, as curl 8.22.0 rewinds its readers for each attempt
/// (CGR-002, BL-3531). A stream that cannot seek (<c>-T -</c>) is left where it is.
/// </summary>
/// <param name="upload">The <c>-T</c> source, or <see langword="null" />.</param>
/// <param name="formBody">The <c>-F</c> body, or <see langword="null" />.</param>
internal sealed class AttemptUploadRewinder(Stream? upload, HttpRequestBody? formBody)
{
    private readonly Stream? formContent = (formBody as StreamBody)?.Content;

    private readonly long? uploadStart = StartOf(upload);

    private readonly long? formStart = StartOf((formBody as StreamBody)?.Content);

    /// <summary>
    /// Seeks each seekable source back to the position it had when this rewinder was made.
    /// </summary>
    public void Rewind()
    {
        RewindTo(upload, uploadStart);
        RewindTo(formContent, formStart);
    }

    private static long? StartOf(Stream? stream) => stream is { CanSeek: true } ? stream.Position : null;

    private static void RewindTo(Stream? stream, long? start)
    {
        if (stream is not null && start is { } position)
        {
            stream.Position = position;
        }
    }
}
