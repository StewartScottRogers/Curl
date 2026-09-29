using Curl.Protocol.Abstractions;

namespace Curl.Networking.Fakes;

/// <summary>
/// An <see cref="IConnectionSession" /> that counts its shutdowns and notes whether the
/// connection it runs on was still open at the first one.
/// </summary>
/// <param name="connection">The connection the session runs on.</param>
public sealed class RecordingConnectionSession(ScriptedConnection connection) : IConnectionSession
{
    /// <summary>Gets how many times <see cref="ShutDownAsync" /> was called.</summary>
    public int ShutDownCount { get; private set; }

    /// <summary>Gets a value indicating whether the connection was not yet disposed when the session was shut down.</summary>
    public bool WasConnectionOpenAtShutDown { get; private set; }

    /// <inheritdoc />
    public ValueTask ShutDownAsync(CancellationToken cancellationToken)
    {
        WasConnectionOpenAtShutDown = ShutDownCount == 0 && !connection.IsDisposed;
        ShutDownCount++;
        return ValueTask.CompletedTask;
    }
}
