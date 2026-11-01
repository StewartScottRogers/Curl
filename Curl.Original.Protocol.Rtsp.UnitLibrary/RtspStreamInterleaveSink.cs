namespace Curl.Original.Protocol.Rtsp;

/// <summary>
/// The interleave sink a transfer uses when none is injected: writes each frame, header
/// included, to the body output, as curl 8.22.0's <c>rtp_client_write</c> falls back to the
/// write function when no <c>CURLOPT_INTERLEAVEFUNCTION</c> is set (BL-2477).
/// </summary>
/// <param name="output">The body output.</param>
public sealed class RtspStreamInterleaveSink(Stream output) : IRtspInterleaveSink
{
    private readonly Stream output = output ?? throw new ArgumentNullException(nameof(output));

    /// <inheritdoc />
    /// <remarks>A write that fails with an <see cref="IOException" /> takes nothing, which fails the transfer with 23.</remarks>
    public async ValueTask<long> WriteAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken)
    {
        try
        {
            await output.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
            return frame.Length;
        }
        catch (IOException)
        {
            return 0;
        }
    }
}
