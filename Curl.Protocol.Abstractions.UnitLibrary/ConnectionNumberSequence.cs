namespace Curl.Protocol.Abstractions;

/// <summary>
/// An <see cref="IConnectionNumbers" /> with a count of its own, from <c>0</c>, for a handler
/// built without the run's shared count.
/// </summary>
public sealed class ConnectionNumberSequence : IConnectionNumbers
{
    private long _nextConnectionNumber;

    /// <inheritdoc />
    public long NumberNextConnection() => Interlocked.Increment(ref _nextConnectionNumber) - 1;
}
