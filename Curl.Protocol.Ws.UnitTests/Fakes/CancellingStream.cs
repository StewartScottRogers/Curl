namespace Curl.Protocol.Ws.Fakes;

/// <summary>
/// A <see cref="MemoryStream" /> that cancels <paramref name="cancellation" /> once written to,
/// as <c>-m</c> running out while the server holds the connection open after a frame.
/// </summary>
/// <param name="cancellation">Cancelled after the first write.</param>
public sealed class CancellingStream(CancellationTokenSource cancellation) : MemoryStream
{
    /// <inheritdoc />
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await base.WriteAsync(buffer, cancellationToken);
        await cancellation.CancelAsync();
    }
}
