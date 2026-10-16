using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.Errors;

namespace Curl.Api;

/// <summary>
/// The bounded buffer between one transfer's <c>CURLOPT_WRITEFUNCTION</c> and the reader of its
/// response body (ADR-0635). <see cref="Write"/> keeps what the engine delivers until the buffer
/// holds <c>capacity</c> bytes, then answers <see cref="WriteCallbackResult.Pause"/> so the engine
/// holds the data and pauses receiving; <see cref="ReadAsync"/> hands the bytes out in order and
/// calls <c>resume</c> once a paused buffer is down to half its capacity, and the engine delivers
/// the held data again. After <see cref="Abort"/>, every write answers <see cref="WriteCallbackResult.Error"/>.
/// </summary>
/// <param name="capacity">The bytes held before the transfer is paused; one delivery larger than that is still taken whole into an empty buffer.</param>
/// <param name="resume">Unpauses the transfer (<c>curl_easy_pause(CURLPAUSE_CONT)</c>).</param>
/// <param name="cancellation">The caller's token: a transfer that fails once it is cancelled is thrown as <see cref="OperationCanceledException"/>, not <see cref="CurlException"/> (ADR-0639).</param>
/// <param name="closing">The client's own token, cancelled when the client is disposed (ADR-0644): a transfer that fails once it is cancelled, and the caller's is not, is thrown as <see cref="OperationCanceledException"/> carrying it.</param>
internal sealed class ResponseBodyBuffer(int capacity, Action resume, CancellationToken cancellation = default, CancellationToken closing = default)
{
    private readonly Lock _gate = new();
    private readonly Queue<byte[]> _chunks = new();
    private readonly TaskCompletionSource _started = NewSignal();
    private TaskCompletionSource _arrived = NewSignal();
    private int _offset;
    private long _buffered;
    private bool _paused;
    private bool _aborted;
    private bool _ended;
    private CurlCode _code;
    private string _errorText = "";
    private CancellationToken _cancelledBy;

    /// <summary>Completes on the first delivery of body bytes or the end of the transfer, whichever comes first.</summary>
    public Task Started => _started.Task;

    /// <summary>Whether <see cref="Complete"/> has been called: the transfer is over.</summary>
    public bool Ended
    {
        get
        {
            lock (_gate)
            {
                return _ended;
            }
        }
    }

    /// <summary>Takes one delivery of body bytes; a <see cref="WriteCallback"/>.</summary>
    /// <param name="data">The bytes received, copied before the call returns.</param>
    /// <returns>
    /// <paramref name="data"/>'s length when taken, <see cref="WriteCallbackResult.Pause"/> when the
    /// buffer is full, or <see cref="WriteCallbackResult.Error"/> after <see cref="Abort"/>.
    /// </returns>
    public long Write(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            return 0;
        }

        TaskCompletionSource arrived;
        lock (_gate)
        {
            if (_aborted)
            {
                return WriteCallbackResult.Error;
            }

            if (_buffered > 0 && _buffered + data.Length > capacity)
            {
                _paused = true;
                return WriteCallbackResult.Pause;
            }

            _chunks.Enqueue(data.ToArray());
            _buffered += data.Length;
            arrived = SwapArrived();
        }

        _started.TrySetResult();
        arrived.TrySetResult();
        return data.Length;
    }

    /// <summary>Records the end of the transfer; readers get the bytes still buffered, then its result.</summary>
    /// <param name="code">The transfer's result.</param>
    /// <param name="errorText">The handle's error text for <paramref name="code"/>.</param>
    public void Complete(CurlCode code, string errorText)
    {
        TaskCompletionSource arrived;
        lock (_gate)
        {
            _ended = true;
            _code = code;
            _errorText = errorText;
            _cancelledBy = CancelledBy(cancellation, closing);
            arrived = SwapArrived();
        }

        _started.TrySetResult();
        arrived.TrySetResult();
    }

    /// <summary>Makes every later <see cref="Write"/> fail the transfer with <c>CURLE_WRITE_ERROR</c>.</summary>
    public void Abort()
    {
        lock (_gate)
        {
            _aborted = true;
        }
    }

    /// <summary>Throws the transfer's failure once it has ended with one; otherwise returns.</summary>
    /// <exception cref="CurlException">The transfer ended with an error code.</exception>
    /// <exception cref="OperationCanceledException">The transfer ended with an error code after the caller cancelled it.</exception>
    public void ThrowIfFailed()
    {
        lock (_gate)
        {
            TransferCancellation.ThrowIfFailed(_code, _errorText, _cancelledBy);
        }
    }

    /// <summary>Copies buffered bytes into <paramref name="destination"/>, waiting for some when none are buffered.</summary>
    /// <param name="destination">Where the bytes go.</param>
    /// <param name="cancellationToken">Stops the wait; the transfer goes on.</param>
    /// <returns>The bytes copied; 0 at the end of a successful transfer.</returns>
    /// <exception cref="CurlException">Nothing is left to read and the transfer failed.</exception>
    /// <exception cref="OperationCanceledException">Nothing is left to read and the caller cancelled the transfer, or <paramref name="cancellationToken"/> stopped the wait.</exception>
    public async ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken cancellationToken)
    {
        int read;
        Task? arrived;
        while ((arrived = TryRead(destination.Span, out read)) is not null)
        {
            await arrived.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        return read;
    }

    /// <summary>Copies what is buffered, or says what to wait for when nothing is and the transfer goes on.</summary>
    /// <param name="destination">Where the bytes go.</param>
    /// <param name="read">The bytes copied.</param>
    /// <returns>The task the next delivery completes, or <see langword="null"/> when <paramref name="read"/> is the answer.</returns>
    private Task? TryRead(Span<byte> destination, out int read)
    {
        bool resumeNow;
        lock (_gate)
        {
            read = 0;
            if (_buffered == 0)
            {
                TransferCancellation.ThrowIfFailed(_code, _errorText, _cancelledBy);
                return _ended ? null : _arrived.Task;
            }

            read = Copy(destination);
            resumeNow = _paused && _buffered <= capacity / 2;
            _paused &= !resumeNow;
        }

        if (resumeNow)
        {
            resume();
        }

        return null;
    }

    private int Copy(Span<byte> destination)
    {
        int copied = 0;
        while (copied < destination.Length && _chunks.TryPeek(out byte[]? chunk))
        {
            int count = Math.Min(chunk.Length - _offset, destination.Length - copied);
            chunk.AsSpan(_offset, count).CopyTo(destination[copied..]);
            copied += count;
            _offset += count;
            if (_offset == chunk.Length)
            {
                _chunks.Dequeue();
                _offset = 0;
            }
        }

        _buffered -= copied;
        return copied;
    }

    /// <summary>The token a failure is thrown as cancelled by: the caller's when cancelled, else the client's when it is closing, else none.</summary>
    private static CancellationToken CancelledBy(CancellationToken cancellation, CancellationToken closing) =>
        cancellation.IsCancellationRequested ? cancellation
        : closing.IsCancellationRequested ? closing
        : CancellationToken.None;

    private TaskCompletionSource SwapArrived()
    {
        TaskCompletionSource arrived = _arrived;
        _arrived = NewSignal();
        return arrived;
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
