using Curl.Api.Callbacks;
using Curl.Api.EngineContracts;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Ws;

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
/// <c>Read callback asked for PAUSE when not supported</c> (ADR-0573). Any other return below zero or
/// past the buffer's length fails the read with libcurl's <c>CURLE_READ_ERROR</c> text.
/// <see cref="SeekingCallbackReadStream" /> adds the seek callback that rewinds it. With
/// <see cref="Trailer" /> set (<c>CURLOPT_TRAILERFUNCTION</c>), the HTTP handler asks it, through
/// <see cref="IUploadTrailerSource" />, for a chunked upload's trailers once the body's end is read (BL-2475).
/// With <see cref="UploadFrames" /> set, a WebSocket upload goes out read by read in those frames, which
/// <c>curl_ws_start_frame</c> inside the callback starts (<see cref="IWsUploadFrameSource" />, BL-2611).
/// With <see cref="DeclaredLength" /> set (<c>CURLOPT_INFILESIZE</c>), the stream is that long: it asks the
/// callback for no more than is left and ends there without asking again, as curl 8.22.0's <c>cr_in_read</c>
/// stops at <c>total_len</c>, and it answers <see cref="Length" /> and <see cref="Position" />, so the HTTP
/// handler sends it with a <c>Content-Length</c> (upstream tests 1525 to 1527, BL-2645).
/// </summary>
/// <param name="callback">The read callback.</param>
/// <param name="pauseGate">The handle's pause state, or <see langword="null" /> when the transfer cannot pause.</param>
internal class CallbackReadStream(ReadCallback callback, EasyPauseGate? pauseGate = null) : Stream, IUploadTrailerSource, IWsUploadFrameSource
{
    /// <inheritdoc />
    public WsUploadFrames? UploadFrames { get; init; }

    /// <summary>libcurl's text for a read callback that returned more than it was asked for.</summary>
    internal const string FunnyValueText = "read function returned funny value";

    /// <summary>libcurl's <c>CURLE_ABORTED_BY_CALLBACK</c> text for a read callback that returned <c>CURL_READFUNC_ABORT</c>.</summary>
    internal const string AbortedText = "operation aborted by callback";

    /// <summary>libcurl's <c>CURLE_READ_ERROR</c> text for a pause the transfer cannot honour.</summary>
    internal const string PauseNotSupportedText = "Read callback asked for PAUSE when not supported";

    /// <summary>libcurl's <c>CURLE_SEND_FAIL_REWIND</c> text for a sized upload that cannot be moved back where asked.</summary>
    internal const string RewindImpossibleText = "necessary data rewind wasn't possible";

    /// <summary>Gets a value indicating whether the read callback returned <see cref="ReadCallbackResult.Abort" />.</summary>
    internal bool Aborted { get; private set; }

    /// <summary>Gets or sets how many bytes the read callback has given since the stream was made or last rewound.</summary>
    internal long BytesReadSinceRewind { get; set; }

    /// <summary>
    /// Gets the upload's declared length (<c>CURLOPT_INFILESIZE</c>), or <see langword="null" /> when it is
    /// unknown and the upload ends only when the callback returns 0.
    /// </summary>
    internal long? DeclaredLength { get; init; }

    /// <summary>Gets or sets how far forward seeks moved past the bytes read: what a resumed upload skipped before.</summary>
    private long SkippedAhead { get; set; }

    /// <summary>Gets the handle's trailer callback, or <see langword="null" /> when none is set.</summary>
    internal TrailerCallback? Trailer { get; init; }

    /// <inheritdoc />
    public bool SuppliesTrailers => Trailer is not null;

    /// <inheritdoc />
    public bool TryTakeTrailers(out IReadOnlyList<string> lines)
    {
        List<string> trailers = [];
        lines = trailers;
        return Trailer!(trailers) == TrailerResult.Ok;
    }

    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanSeek => DeclaredLength is not null;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => DeclaredLength ?? throw new NotSupportedException();

    /// <inheritdoc />
    /// <remarks>
    /// Only a stream of <see cref="DeclaredLength" /> has a position: the bytes read plus those a forward seek
    /// skipped. Setting it where it is does nothing, further on skips ahead (the bytes a resumed upload already
    /// discarded), and back to 0 rewinds through <see cref="IUploadRewinder" />; a failed rewind or any other
    /// move back throws an <see cref="IOException" />.
    /// </remarks>
    public override long Position
    {
        get => DeclaredLength is null ? throw new NotSupportedException() : BytesReadSinceRewind + SkippedAhead;
        set => MoveTo(value);
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
        buffer = buffer[..AskableLength(buffer.Length)];
        if (buffer.IsEmpty)
        {
            return 0;
        }

        long read = callback(buffer);
        return read == ReadCallbackResult.Pause ? throw new IOException(PauseNotSupportedText) : Took(buffer.Length, read);
    }

    /// <inheritdoc />
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        buffer = buffer[..AskableLength(buffer.Length)];
        return buffer.IsEmpty ? ValueTask.FromResult(0) : ReadFromCallbackAsync(buffer, cancellationToken);
    }

    /// <summary>
    /// Asks the read callback to fill <paramref name="buffer" />, waiting out each pause it asks for on the gate.
    /// </summary>
    /// <param name="buffer">The buffer, cut to what the callback may fill.</param>
    /// <param name="cancellationToken">Stops a wait for the unpause.</param>
    /// <returns>The byte count the callback filled.</returns>
    private async ValueTask<int> ReadFromCallbackAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        await (pauseGate?.WhenResumedAsync(CurlPause.Send, cancellationToken) ?? Task.CompletedTask).ConfigureAwait(false);
        int unpauses = pauseGate?.Unpauses ?? 0;
        long read = callback(buffer.Span);
        while (read == ReadCallbackResult.Pause)
        {
            EasyPauseGate gate = pauseGate ?? throw new IOException(PauseNotSupportedText);
            gate.Pause(CurlPause.Send, unpauses);
            await gate.WhenResumedAsync(CurlPause.Send, cancellationToken).ConfigureAwait(false);
            unpauses = gate.Unpauses;
            read = callback(buffer.Span);
        }

        return Took(buffer.Length, read);
    }

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin)
    {
        long length = Length;
        long target = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => Position + offset,
            _ => length + offset,
        };
        MoveTo(target);
        return target;
    }

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <summary>
    /// How much of a buffer of <paramref name="length" /> bytes the callback may fill: all of it with no
    /// <see cref="DeclaredLength" />, else no more than is left of it.
    /// </summary>
    /// <param name="length">The buffer's length.</param>
    /// <returns>The byte count to ask for; 0 at the declared end.</returns>
    private int AskableLength(int length) =>
        DeclaredLength is { } declared ? (int)Math.Clamp(declared - Position, 0, length) : length;

    /// <summary>Moves a stream of <see cref="DeclaredLength" /> to <paramref name="target" /> (<see cref="Position" />).</summary>
    /// <param name="target">The position to move to.</param>
    /// <exception cref="NotSupportedException">The stream has no declared length.</exception>
    /// <exception cref="IOException">The move goes back anywhere but 0, or the rewind to 0 failed.</exception>
    private void MoveTo(long target)
    {
        long position = Position;
        if (target >= position)
        {
            SkippedAhead += target - position;
            return;
        }

        string failure = target != 0 || this is not IUploadRewinder rewinder
            ? RewindImpossibleText
            : rewinder.RewindToStart() ?? string.Empty;
        if (failure.Length > 0)
        {
            throw new IOException(failure);
        }

        SkippedAhead = 0;
    }

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

        if (read < 0 || read > length)
        {
            throw new IOException(FunnyValueText);
        }

        BytesReadSinceRewind += read;
        return (int)read;
    }
}
