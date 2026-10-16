using System.Globalization;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.Options;

namespace Curl.Engine;

/// <summary>
/// <c>CURLOPT_RESUME_FROM</c>'s forward seek over a read-callback upload, as curl 8.22.0's <c>cr_in_resume_from</c>
/// (<c>lib/sendf.c</c>) skips the bytes the server already holds: the seek callback is called with
/// <c>(offset, SEEK_SET)</c>; on <see cref="SeekResult.CantSeek" />, or with no seek callback, that many bytes are read
/// through the read callback in pieces of at most <see cref="ScratchSize" /> and discarded; and when
/// <c>CURLOPT_INFILESIZE</c> is known the offset must leave some of it to send (BL-2473, ADR-0641).
/// </summary>
internal static class CallbackUploadResume
{
    /// <summary>The size of upstream's scratch buffer, the most one discarding read asks for.</summary>
    internal const int ScratchSize = 4 * 1024;

    /// <summary>libcurl's <c>CURLE_READ_ERROR</c> text for a seek callback that answered neither <see cref="SeekResult.Ok" /> nor <see cref="SeekResult.CantSeek" />.</summary>
    internal const string CouldNotSeekText = "Could not seek stream";

    /// <summary>libcurl's <c>CURLE_PARTIAL_FILE</c> text for an offset that leaves nothing of the known size to send.</summary>
    internal const string AlreadyCompletelyUploadedText = "File already completely uploaded";

    /// <summary>
    /// Skips the first <see cref="Api.Options.Behaviour.EasyTransferOptions.ResumeFrom" /> bytes of the handle's
    /// read-callback upload, when the handle uploads through a read callback from a positive offset and the curl tool's
    /// runner, which resumes its own <c>-T</c> source, gave no <paramref name="toolInputs" />.
    /// </summary>
    /// <param name="toolInputs">The runner's chosen pieces, or <see langword="null" /> for a handle's own.</param>
    /// <param name="options">The handle's options.</param>
    /// <returns><see langword="null" /> when the upload may be sent; otherwise why the perform ends.</returns>
    internal static UploadResumeFailure? SkipUnlessToolGave(EngineToolTransferInputs? toolInputs, EasyOptions options) =>
        toolInputs is null && options.Transfer is { Upload: true, ResumeFrom: > 0 } && options.Callbacks.Read is { } read
            ? Skip(read, options.Callbacks.Seek, options.Transfer.ResumeFrom, options.Transfer.InFileSize)
            : null;

    /// <summary>Skips <paramref name="offset" /> bytes of the upload as <c>cr_in_resume_from</c> does.</summary>
    /// <param name="read">The read callback.</param>
    /// <param name="seek">The seek callback, or <see langword="null" /> when none is set.</param>
    /// <param name="offset">The positive offset.</param>
    /// <param name="totalLength">The <c>CURLOPT_INFILESIZE</c>, -1 when unknown.</param>
    /// <returns><see langword="null" /> when the upload may be sent; otherwise why the perform ends.</returns>
    internal static UploadResumeFailure? Skip(ReadCallback read, SeekCallback? seek, long offset, long totalLength) =>
        SeekOrDiscard(read, SeekOf(seek, offset), offset) ?? CompletelyUploadedUnlessLeft(offset, totalLength);

    /// <summary>The seek callback's answer to <c>(offset, SEEK_SET)</c>, or <see cref="SeekResult.CantSeek" /> with none.</summary>
    /// <param name="seek">The seek callback, or <see langword="null" />.</param>
    /// <param name="offset">The offset.</param>
    /// <returns>The answer.</returns>
    private static SeekResult SeekOf(SeekCallback? seek, long offset) =>
        seek is null ? SeekResult.CantSeek : seek(offset, SeekOrigin.Begin);

    /// <summary>What follows the seek: nothing after <see cref="SeekResult.Ok" />, the discarding reads after <see cref="SeekResult.CantSeek" />, else a failure.</summary>
    /// <param name="read">The read callback.</param>
    /// <param name="result">The seek's answer.</param>
    /// <param name="offset">The offset.</param>
    /// <returns><see langword="null" /> when the offset is passed; otherwise why the perform ends.</returns>
    private static UploadResumeFailure? SeekOrDiscard(ReadCallback read, SeekResult result, long offset) => result switch
    {
        SeekResult.Ok => null,
        SeekResult.CantSeek => Discard(read, offset),
        _ => new UploadResumeFailure(CurlCode.ReadError, CouldNotSeekText),
    };

    /// <summary>The failure when the known <paramref name="totalLength" /> leaves nothing past <paramref name="offset" />.</summary>
    /// <param name="offset">The offset.</param>
    /// <param name="totalLength">The <c>CURLOPT_INFILESIZE</c>, -1 when unknown.</param>
    /// <returns>The failure, or <see langword="null" />.</returns>
    private static UploadResumeFailure? CompletelyUploadedUnlessLeft(long offset, long totalLength) =>
        totalLength > 0 && totalLength <= offset ? new UploadResumeFailure(CurlCode.PartialFile, AlreadyCompletelyUploadedText) : null;

    /// <summary>
    /// Reads and discards <paramref name="offset" /> bytes through <paramref name="read" />: a read that gives nothing or
    /// more than it was asked for (as <see cref="ReadCallbackResult.Abort" /> and <see cref="ReadCallbackResult.Pause" />
    /// do) fails with <c>Could only read N bytes from the input</c>, N counting that read's return as upstream does.
    /// </summary>
    /// <param name="read">The read callback.</param>
    /// <param name="offset">The bytes to discard.</param>
    /// <returns><see langword="null" /> once they are discarded; otherwise why the perform ends.</returns>
    private static UploadResumeFailure? Discard(ReadCallback read, long offset)
    {
        byte[] scratch = new byte[ScratchSize];
        long passed = 0;
        do
        {
            int asked = (int)Math.Min(ScratchSize, offset - passed);
            long actuallyRead = read(scratch.AsSpan(0, asked));
            passed += actuallyRead;
            if (actuallyRead <= 0 || actuallyRead > asked)
            {
                return new UploadResumeFailure(CurlCode.ReadError, CouldOnlyReadText(passed));
            }
        }
        while (passed < offset);

        return null;
    }

    /// <summary>libcurl's <c>CURLE_READ_ERROR</c> text for a discarding read that came up short.</summary>
    /// <param name="passed">The bytes counted as passed.</param>
    /// <returns>The text.</returns>
    internal static string CouldOnlyReadText(long passed) =>
        string.Create(CultureInfo.InvariantCulture, $"Could only read {passed} bytes from the input");
}
