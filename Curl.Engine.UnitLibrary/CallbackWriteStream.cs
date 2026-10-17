using System.Globalization;

using Curl.Api.Callbacks;
using Curl.Api.EngineContracts;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// A write-only stream that hands every write to a libcurl write or header callback, as libcurl
/// hands each chunk it receives to <c>CURLOPT_WRITEFUNCTION</c> or <c>CURLOPT_HEADERFUNCTION</c>.
/// A callback that returns anything but the chunk's length fails the write with an
/// <see cref="OutputWriteFailedException" /> carrying what it returned, which ends the transfer with
/// <c>CURLE_WRITE_ERROR</c> and the runner's exit 23 text,
/// <c>Failure writing output to destination, passed N returned M</c>, as libcurl does. A callback that
/// returns <see cref="WriteCallbackResult.Pause" /> pauses receiving on <paramref name="pauseGate" />, and
/// the write waits until the handle is unpaused and hands the same chunk to the callback again, as
/// libcurl keeps the paused data for later; with no gate, or on the synchronous path, which cannot wait,
/// it fails with libcurl's <c>Write callback asked for PAUSE when not supported</c> (ADR-0573).
/// </summary>
/// <param name="callback">Receives each chunk and returns how many bytes it took.</param>
/// <param name="pauseGate">The handle's pause state, or <see langword="null" /> when the transfer cannot pause.</param>
internal sealed class CallbackWriteStream(Func<ReadOnlySpan<byte>, long> callback, EasyPauseGate? pauseGate = null) : Stream
{
    /// <summary>libcurl's <c>CURLE_WRITE_ERROR</c> text for a pause the transfer cannot honour.</summary>
    internal const string PauseNotSupportedText = "Write callback asked for PAUSE when not supported";

    /// <summary>Creates the stream over a write callback.</summary>
    /// <param name="callback">The <c>CURLOPT_WRITEFUNCTION</c>.</param>
    /// <param name="pauseGate">The handle's pause state, or <see langword="null" /> when the transfer cannot pause.</param>
    /// <returns>The stream.</returns>
    internal static CallbackWriteStream Of(WriteCallback callback, EasyPauseGate? pauseGate = null) => new(data => callback(data), pauseGate);

    /// <summary>Creates the stream over a header callback.</summary>
    /// <param name="callback">The <c>CURLOPT_HEADERFUNCTION</c>.</param>
    /// <param name="pauseGate">The handle's pause state, or <see langword="null" /> when the transfer cannot pause.</param>
    /// <returns>The stream.</returns>
    internal static CallbackWriteStream Of(HeaderCallback callback, EasyPauseGate? pauseGate = null) => new(header => callback(header), pauseGate);

    /// <inheritdoc />
    public override bool CanRead => false;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => true;

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
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

    /// <inheritdoc />
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        long returned = callback(buffer);
        if (returned == WriteCallbackResult.Pause)
        {
            throw PauseNotSupported();
        }

        CheckTook(buffer.Length, returned);
    }

    /// <inheritdoc />
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        int unpauses = pauseGate?.Unpauses ?? 0;
        long returned = callback(buffer.Span);
        while (returned == WriteCallbackResult.Pause)
        {
            EasyPauseGate gate = pauseGate ?? throw PauseNotSupported();
            gate.Pause(CurlPause.Receive, unpauses);
            await gate.WhenResumedAsync(CurlPause.Receive, cancellationToken).ConfigureAwait(false);
            unpauses = gate.Unpauses;
            returned = callback(buffer.Span);
        }

        CheckTook(buffer.Length, returned);
    }

    /// <inheritdoc />
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <summary>Fails the write unless the callback took all <paramref name="length" /> bytes.</summary>
    /// <param name="length">The chunk's length.</param>
    /// <param name="returned">What the callback returned.</param>
    private static void CheckTook(int length, long returned)
    {
        if (returned != length)
        {
            throw new OutputWriteFailedException(
                (int)Math.Clamp(returned, 0, int.MaxValue),
                string.Create(CultureInfo.InvariantCulture, $"Failure writing output to destination, passed {length} returned {returned}"));
        }
    }

    private static OutputWriteFailedException PauseNotSupported() => new(0, PauseNotSupportedText);
}
