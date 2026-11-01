namespace Curl.Extended.Protocol.Coap.Dtls.Handshake;

/// <summary>
/// A clock whose every timer fires only once <paramref name="channel" /> has nothing left to hand the client, so a
/// retransmission happens after the flight it answers was lost, never while the client is still reading the server's reply.
/// </summary>
internal sealed class StarvedChannelTimeProvider(DtlsTestServerChannel channel) : TimeProvider
{
    /// <inheritdoc />
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        _ = Task.Run(async () =>
        {
            await channel.WaitUntilStarvedAsync();
            callback(state);
        });
        return new NoTimer();
    }

    private sealed class NoTimer : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
