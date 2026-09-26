using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http.Fakes;

/// <summary>
/// An <see cref="IConnection" /> that plays an HTTP server from a recorded exchange: it
/// asserts the request bytes written before the first read are exactly the ones expected,
/// then replays the response in reads of at most a chosen size, then reports the peer
/// closed or throws a chosen failure.
/// </summary>
/// <remarks>
/// Replaying the same response with a chunk size of 1 and with a large one proves a reader
/// does not depend on how the peer's bytes are split across reads.
/// </remarks>
public sealed class ScriptedConnection : IConnection
{
    private readonly byte[] response;

    private readonly int chunkSize;

    private readonly byte[]? expectedRequest;

    private readonly Exception? failureAfterResponse;

    private readonly MemoryStream written = new();

    private int responseOffset;

    private bool requestChecked;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScriptedConnection" /> class.
    /// </summary>
    /// <param name="response">The bytes the server sends.</param>
    /// <param name="chunkSize">The most bytes one read returns; at least 1.</param>
    /// <param name="expectedRequest">
    /// The exact bytes the client must have written before its first read, or
    /// <see langword="null" /> to accept any.
    /// </param>
    /// <param name="failureAfterResponse">
    /// The exception the read after the last response byte throws, such as an
    /// <see cref="IOException" /> for a reset connection; <see langword="null" /> to report
    /// the peer closed instead.
    /// </param>
    public ScriptedConnection(
        byte[] response,
        int chunkSize,
        byte[]? expectedRequest = null,
        Exception? failureAfterResponse = null)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentOutOfRangeException.ThrowIfLessThan(chunkSize, 1);

        this.response = response;
        this.chunkSize = chunkSize;
        this.expectedRequest = expectedRequest;
        this.failureAfterResponse = failureAfterResponse;
    }

    /// <inheritdoc />
    public bool IsSecure => false;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => null;

    /// <summary>Gets every byte written so far, in order.</summary>
    public byte[] Written => written.ToArray();

    /// <summary>Gets the number of reads made so far, the one that reported the close included.</summary>
    public int ReadCount { get; private set; }

    /// <summary>Gets a value indicating whether the connection has been disposed.</summary>
    public bool IsDisposed { get; private set; }

    /// <inheritdoc />
    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AssertRequestOnFirstRead();
        ReadCount++;

        int length = Math.Min(Math.Min(chunkSize, buffer.Length), response.Length - responseOffset);
        if (responseOffset == response.Length && failureAfterResponse is not null)
        {
            throw failureAfterResponse;
        }

        response.AsSpan(responseOffset, length).CopyTo(buffer.Span);
        responseOffset += length;
        return ValueTask.FromResult(length);
    }

    /// <inheritdoc />
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        written.Write(buffer.Span);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        written.Dispose();
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }

    private void AssertRequestOnFirstRead()
    {
        if (requestChecked)
        {
            return;
        }

        requestChecked = true;
        if (expectedRequest is not null)
        {
            CollectionAssert.AreEqual(expectedRequest, Written, "The request bytes differ from the recorded exchange.");
        }
    }
}
