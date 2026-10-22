using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.Errors;

namespace Curl.Api;

/// <summary>
/// <see cref="CurlResponse.Body"/> while its transfer runs (ADR-0635): a read-only, forward-only
/// <see cref="Stream"/> over a <see cref="ResponseBodyBuffer"/> that the transfer's write callback
/// (<see cref="TakeBody"/>) fills. It owns the easy handle and the handles in <see cref="Owned"/>: they
/// are disposed when the transfer ends, and disposing the stream before then aborts the transfer. Reading is asynchronous only.
/// </summary>
internal sealed class ResponseBodyStream : Stream
{
    /// <summary>The bytes buffered before the transfer is paused for a reader that has fallen behind.</summary>
    public const int DefaultCapacity = 64 * 1024;

    private readonly IEasyTransfer _easy;
    private readonly ResponseBodyBuffer _buffer;
    private readonly Func<IEasyTransfer, CancellationToken, Task<CurlCode>> _perform;
    private readonly CancellationTokenSource _abort;
    private readonly List<IDisposable> _owned = [];
    private readonly TransferCancellation _cancellation;
    private readonly TransferProgressReporter? _progress;
    private Task _transfer = Task.CompletedTask;
    private Task? _closing;

    /// <summary>A body for the transfer <paramref name="easy"/> is about to perform.</summary>
    /// <param name="easy">The handle; the stream disposes it once its transfer ends.</param>
    /// <param name="capacity">The bytes buffered before the transfer is paused.</param>
    /// <param name="cancellationToken">
    /// Aborts the transfer, as disposing the stream does, through the transfer-info callback this
    /// sets on <paramref name="easy"/> (ADR-0639); the failure is then thrown as <see cref="OperationCanceledException"/>.
    /// </param>
    /// <param name="progress">
    /// Takes a report each time the engine calls the transfer-info callback, until the transfer ends (ADR-0643);
    /// <see langword="null"/> for none.
    /// </param>
    public ResponseBodyStream(IEasyTransfer easy, int capacity, CancellationToken cancellationToken, IProgress<CurlTransferProgress>? progress = null)
        : this(easy, capacity, static (transfer, token) => transfer.PerformAsync(token).AsTask(), cancellationToken, CancellationToken.None, progress)
    {
    }

    /// <summary>A body for the transfer <paramref name="easy"/> is about to run through <paramref name="perform"/>.</summary>
    /// <param name="easy">The handle; the stream disposes it once its transfer ends.</param>
    /// <param name="capacity">The bytes buffered before the transfer is paused.</param>
    /// <param name="perform">Runs the transfer and gives its result: a client's multi handle driver (ADR-0644).</param>
    /// <param name="cancellationToken">The caller's token, which aborts the transfer as above.</param>
    /// <param name="clientClosing">The client's own token, cancelled when the client is disposed; it aborts the transfer as <paramref name="cancellationToken"/> does.</param>
    /// <param name="progress">Takes a report each time the engine calls the transfer-info callback, as above; <see langword="null"/> for none.</param>
    public ResponseBodyStream(IEasyTransfer easy, int capacity, Func<IEasyTransfer, CancellationToken, Task<CurlCode>> perform, CancellationToken cancellationToken, CancellationToken clientClosing, IProgress<CurlTransferProgress>? progress = null)
    {
        _easy = easy;
        _perform = perform;

        _abort = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, clientClosing);
        _buffer = new ResponseBodyBuffer(capacity, () => easy.Pause(CurlPause.Continue), cancellationToken, clientClosing);
        _cancellation = new TransferCancellation(easy, _abort.Token, _buffer.Abort);
        easy.Options = _cancellation.ApplyTo(easy.Options);
        if (progress is not null)
        {
            _progress = new TransferProgressReporter(easy, progress, _cancellation.Progress);
            easy.Options = easy.Options with { Callbacks = easy.Options.Callbacks with { TransferInfo = _progress.Report } };
        }
    }

    /// <summary>The <c>CURLOPT_WRITEFUNCTION</c> that fills the stream.</summary>
    public WriteCallback TakeBody => _buffer.Write;

    /// <summary>
    /// Takes every handle the request body creates (a multipart body's MIME builder); each is
    /// disposed after the easy handle, once the transfer ends.
    /// </summary>
    public ICollection<IDisposable> Owned => _owned;

    /// <inheritdoc />
    public override bool CanRead => _closing is null;

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

    /// <summary>
    /// Starts the transfer and waits until its body starts or it ends. A transfer that has ended by
    /// then with an error throws here, with the handle already disposed; a later failure is thrown by
    /// <see cref="ReadAsync(Memory{byte}, CancellationToken)"/>.
    /// </summary>
    /// <returns>A task that completes once the body has started or the transfer has ended.</returns>
    /// <exception cref="CurlException">The transfer ended with an error before the body could be read.</exception>
    /// <exception cref="OperationCanceledException">The caller cancelled the transfer before the body could be read.</exception>
    public async Task StartAsync()
    {
        _transfer = PerformAsync();
        await Task.WhenAny(_buffer.Started, _transfer).ConfigureAwait(false);
        if (_buffer.Ended || _transfer.IsCompleted)
        {
            await _transfer.ConfigureAwait(false);
            _buffer.ThrowIfFailed();
        }
    }

    /// <summary>
    /// Disposes the easy handle and every handle in <see cref="Owned"/> for a transfer that will never
    /// start, as when setting up the request failed.
    /// </summary>
    /// <returns>A task that completes once every handle is disposed.</returns>
    public async Task DisposeUnstartedAsync()
    {
        _cancellation.Dispose();
        await _easy.DisposeAsync().ConfigureAwait(false);
        _owned.ForEach(handle => handle.Dispose());
        _abort.Dispose();
    }

    /// <summary>Reads the next bytes of the body, waiting for the transfer to deliver some.</summary>
    /// <param name="buffer">Where the bytes go.</param>
    /// <param name="cancellationToken">Stops this read only; the transfer goes on.</param>
    /// <returns>The bytes read; 0 once the whole body has been read.</returns>
    /// <exception cref="ObjectDisposedException">The stream has been disposed.</exception>
    /// <exception cref="CurlException">The transfer failed before the rest of the body arrived.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled while waiting.</exception>
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_closing is not null, this);
        return _buffer.ReadAsync(buffer, cancellationToken);
    }

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <summary>Not supported: the body arrives asynchronously, so read it with <c>ReadAsync</c> or <c>CopyToAsync</c>.</summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    public override int Read(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException("A streamed response body is read asynchronously; use ReadAsync or CopyToAsync.");

    /// <inheritdoc />
    public override void Flush()
    {
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <summary>Aborts the transfer if it is still running and disposes the handle once it ends.</summary>
    /// <returns>A task that completes once the handle is disposed.</returns>
    public override async ValueTask DisposeAsync()
    {
        Dispose();
        await _closing!.ConfigureAwait(false);
    }

    /// <summary>Aborts the transfer if it is still running; the handle is disposed once it ends.</summary>
    /// <param name="disposing">Ignored: the stream has no finalizer, so it is always <see langword="true"/>.</param>
    protected override void Dispose(bool disposing)
    {
        _closing ??= CloseAsync();
        base.Dispose(disposing);
    }

    /// <summary>Runs the transfer to its end, then completes the buffer with its result; always ends progress reports and disposes the owned handles, whatever the outcome.</summary>
    private async Task PerformAsync()
    {
        try
        {
            await using (_easy.ConfigureAwait(false))
            {
                using (_cancellation)
                {
                    _buffer.Complete(await _perform(_easy, _abort.Token).ConfigureAwait(false), _easy.ErrorText);
                }
            }
        }
        finally
        {
            _progress?.End();
            _owned.ForEach(handle => handle.Dispose());
        }
    }

    /// <summary>Aborts the buffer and the perform token, then waits for the transfer to finish so the handle is never freed while the engine is still using it.</summary>
    private async Task CloseAsync()
    {
        _buffer.Abort();
        await _abort.CancelAsync().ConfigureAwait(false);
        await _transfer.ConfigureAwait(false);
        _abort.Dispose();
    }
}
