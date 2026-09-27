using System.Buffers;

namespace Curl.Core.Multipart;

/// <summary>
/// One <see cref="MultipartPartEncoder" /> at work on one part's data, fed a piece at a time
/// as the data is read, so a file part is encoded as it is sent rather than read whole.
/// </summary>
internal abstract class MultipartDataEncoding
{
    /// <summary>
    /// Encodes as much of <paramref name="data" /> as can be encoded without seeing what follows
    /// it, or all of it when <paramref name="isFinal" /> says nothing follows.
    /// </summary>
    /// <param name="data">The data not yet encoded, starting where the last call stopped.</param>
    /// <param name="isFinal">Whether <paramref name="data" /> runs to the end of the part's data.</param>
    /// <param name="output">Receives the encoded bytes.</param>
    /// <param name="consumed">How many bytes of <paramref name="data" /> were encoded; the rest must be given again.</param>
    /// <returns><see langword="false" /> when the encoder cannot carry the data.</returns>
    internal abstract bool TryEncode(ReadOnlySpan<byte> data, bool isFinal, IBufferWriter<byte> output, out int consumed);
}
