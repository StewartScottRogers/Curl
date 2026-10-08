using System.Collections.Concurrent;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Tftp.Fakes;

/// <summary>
/// An <see cref="IDatagramConnector" /> that hands out a different channel on each open, in
/// the order given, safely from many tasks at once, so one handler can run several
/// transfers side by side.
/// </summary>
/// <param name="channels">The channels to open, one per open.</param>
/// <remarks>
/// An open after the channels run out throws <see cref="InvalidOperationException" />.
/// </remarks>
public sealed class QueuedDatagramConnector(IEnumerable<IDatagramChannel> channels) : IDatagramConnector
{
    private readonly ConcurrentQueue<IDatagramChannel> pending = new(channels);

    /// <inheritdoc />
    public ValueTask<DatagramOpenResult> OpenAsync(string host, int port, CancellationToken cancellationToken) =>
        pending.TryDequeue(out var channel)
            ? ValueTask.FromResult(DatagramOpenResult.Opened(channel))
            : throw new InvalidOperationException("The handler opened more channels than the test queued.");
}
