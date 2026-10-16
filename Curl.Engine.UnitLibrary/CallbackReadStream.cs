using Curl.Api.Callbacks;
using Curl.Api.EngineContracts;

namespace Curl.Engine;

/// <summary>
/// A read-only <see cref="Stream" /> over an easy handle's <c>CURLOPT_READFUNCTION</c>
/// (<see cref="ReadCallback" />), the upload source a handler reads (ADR-0542): each read asks the
/// callback to fill the buffer, and a return of zero is the end of the upload, as libcurl takes it.
/// <see cref="ReadCallbackResult.Abort" /> marks the stream <see cref="Aborted" /> and fails the read with
/// libcurl's <c>operation aborted by callback</c>, which the handle reports as
/// <c>CURLE_ABORTED_BY_CALLBACK</c>; <see cref="ReadCallbackResult.Pause" /> pauses sending on
/// <paramref name="pauseGate" /> and the read waits until the handle is unpaused and asks the callback
/// again, or, with no gate or on the synchronous path, fails with libcurl's
/// <c>Read callback asked for PAUSE when not supported</c> (ADR-0569). Any other return below zero or
/// past the buffer's length fails the read with libcurl's <c>CURLE_READ_ERROR</c> text.
/// </summary>
/// <param name="callback">The read callback.</param>
/// <param name="pauseGate">The handle's pause state, or <see langword="null" /> when the transfer cannot pause.</param>
internal sealed class CallbackReadStream(ReadCallback callback, EasyPauseGate? pauseGate = null) : Stream
{
    /// <summary>libcurl's text for a read callback that returned more than it was asked for.</summary>
    internal const string FunnyValueText = "read function returned funny value";

    /// <summary>libcurl's <c>CURLE_ABORTED_BY_CALLBACK</c> text for a read callback that returned <c>CURL_READFUNC_ABORT</c>.</summary>
    internal const string AbortedText = "operation aborted by callback";

    /// <summary>libcurl's <c>CURLE_READ_ERROR</c> text for a pause the transfer cannot honour.</summary>
    internal const string PauseNotSupportedText = "Read callback asked for PAUSE when not supported";

    /// <summary>Gets a value indicating whether the read callback returned <see cref="ReadCallbackResult.Abort" />.</summary>
    internal bool Aborted { get; private set; }

    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override void Flush()
    {
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    /// <inheritdoc />
    public override int Read(Span<byte> buffer)
    {
        long read = callback(buffer);
        return read == ReadCallbackResult.Pause ? throw new IOException(PauseNotSupportedText) : Took(buffer.Length, read);
    }

    /// <inheritdoc />
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        long read = callback(buffer.Span);
        while (read == ReadCallbackResult.Pause)
        {
            EasyPauseGate gate = pauseGate ?? throw new IOException(PauseNotSupportedText);
            gate.Pause(CurlPause.Send);
            await gate.WhenResumedAsync(CurlPause.Send, cancellationToken).ConfigureAwait(false);
            read = callback(buffer.Span);
        }

        return Took(buffer.Length, read);
    }

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <summary>The byte count the callback filled, or the failure its return means.</summary>
    /// <param name="length">The buffer's length.</param>
    /// <param name="read">What the callback returned.</param>
    /// <returns>The byte count.</returns>
    /// <exception cref="IOException">The callback aborted, or returned below zero or past the buffer.</exception>
    private int Took(int length, long read)
    {
        if (read == ReadCallbackResult.Abort)
        {
            Aborted = true;
            throw new IOException(AbortedText);
        }

        return read >= 0 && read <= length ? (int)read : throw new IOException(FunnyValueText);
    }
}
