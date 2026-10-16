using Curl.Api.Callbacks;

namespace Curl.Api;

/// <summary>
/// The read and seek callbacks of one upload from a stream. Neither lets an exception reach the
/// engine: a read that throws aborts the transfer, a seek that throws fails it.
/// </summary>
/// <param name="stream">The stream being uploaded.</param>
/// <param name="start">The stream's position when the upload began; offsets from the start count from here.</param>
internal sealed class StreamUpload(Stream stream, long start)
{
    /// <summary>The <see cref="ReadCallback"/>: fills <paramref name="buffer"/> from the stream.</summary>
    /// <param name="buffer">The buffer to fill.</param>
    /// <returns>The bytes read (0 at the end), or <see cref="ReadCallbackResult.Abort"/> when the read threw.</returns>
#pragma warning disable CA1031 // An exception must not cross into the engine; it becomes CURLE_ABORTED_BY_CALLBACK.
    public long Read(Span<byte> buffer)
    {
        try
        {
            return stream.Read(buffer);
        }
        catch (Exception)
        {
            return ReadCallbackResult.Abort;
        }
    }

    /// <summary>
    /// The <see cref="SeekCallback"/>: moves the stream to <paramref name="offset"/>, counted from the
    /// upload's start for <see cref="SeekOrigin.Begin"/>.
    /// </summary>
    /// <param name="offset">The position to move to.</param>
    /// <param name="origin">Where <paramref name="offset"/> counts from.</param>
    /// <returns>
    /// <see cref="SeekResult.Ok"/>; <see cref="SeekResult.CantSeek"/> for a non-seekable stream;
    /// <see cref="SeekResult.Fail"/> when the seek threw.
    /// </returns>
    public SeekResult Seek(long offset, SeekOrigin origin)
    {
        if (!stream.CanSeek)
        {
            return SeekResult.CantSeek;
        }

        try
        {
            stream.Seek(origin == SeekOrigin.Begin ? start + offset : offset, origin);
            return SeekResult.Ok;
        }
        catch (Exception)
        {
            return SeekResult.Fail;
        }
    }
#pragma warning restore CA1031
}
