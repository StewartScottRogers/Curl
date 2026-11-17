using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Smb;

/// <summary>
/// Passes every call to <paramref name="inner" /> and counts the bytes written, which is
/// the transfer's <c>%{size_request}</c>, and stamps the time of the first read, which is
/// the transfer's start-transfer time (BL-5328).
/// </summary>
/// <param name="inner">The connection the transfer talks over.</param>
/// <param name="timeProvider">The clock the first read is stamped from.</param>
internal sealed class SmbCountingConnection(IConnection inner, TimeProvider timeProvider) : IConnection
{
    /// <summary>Gets the bytes written so far.</summary>
    public long BytesWritten { get; private set; }

    /// <summary>Gets the timestamp of the first read to return, or <see langword="null" /> when none has.</summary>
    public long? FirstReadAt { get; private set; }

    /// <inheritdoc />
    public bool IsSecure => inner.IsSecure;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => inner.RemoteEndPoint;

    /// <inheritdoc />
    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        int read = await inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        FirstReadAt ??= timeProvider.GetTimestamp();
        return read;
    }

    /// <inheritdoc />
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        BytesWritten += buffer.Length;
        return inner.WriteAsync(buffer, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);

    /// <inheritdoc />
    public ValueTask DisposeAsync() => inner.DisposeAsync();
}
