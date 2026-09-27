using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ftp;

/// <summary>
/// The part of a file a download asks for, in the two numbers curl 8.21.0's
/// <c>lib/ftp.c</c> keeps: the offset it sends with <c>REST</c> and the most bytes it
/// reads before it sends <c>ABOR</c>.
/// </summary>
/// <param name="Offset">
/// The requested start. Zero sends no <c>REST</c>; a negative value asks for that many
/// bytes from the end, resolved against the <c>SIZE</c> count.
/// </param>
/// <param name="MaxDownload">
/// The most bytes to read, or <see langword="null" /> to read to the end of the data.
/// A limit makes the download end with <c>ABOR</c> and skip the check of the
/// transfer-complete reply, as curl does for <c>-r first-last</c> and <c>-r -n</c>.
/// </param>
internal readonly record struct FtpDownloadWindow(long Offset, long? MaxDownload)
{
    /// <summary>
    /// Reads the window from <c>-r</c> (<see cref="ITransferContext.Range" />) or, when
    /// there is none, <c>-C</c> (<see cref="ITransferContext.ResumeFrom" />). A range wins
    /// when both are set, as curl's range handling overwrites the resume offset.
    /// </summary>
    /// <param name="context">The transfer being performed.</param>
    /// <returns>The window to download.</returns>
    public static FtpDownloadWindow Of(ITransferContext context) => context.Range switch
    {
        { Kind: ByteRangeKind.Bounded } range => new(
            range.FirstBytePosition.GetValueOrDefault(),
            range.LastBytePosition.GetValueOrDefault() - range.FirstBytePosition.GetValueOrDefault() + 1),
        { Kind: ByteRangeKind.FromOffset } range => new(range.FirstBytePosition.GetValueOrDefault(), null),
        { } suffix => new(-suffix.SuffixLength.GetValueOrDefault(), suffix.SuffixLength),
        null => new(context.ResumeFrom.GetValueOrDefault(), null),
    };
}
