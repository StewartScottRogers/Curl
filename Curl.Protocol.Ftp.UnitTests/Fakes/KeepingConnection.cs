using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ftp.Fakes;

/// <summary>
/// A control connection a connection cache would keep: it holds the session a transfer hands
/// it, records <see cref="MarkReusable" />, and closes, letting the held session send what it
/// sends first, only when <see cref="CloseAsync" /> is called, as the run's cache does at exit.
/// </summary>
/// <param name="script">The scripted connection every read and write goes to.</param>
public sealed class KeepingConnection(ScriptedConnection script) : IConnection
{
    /// <summary>Gets the scripted connection every read and write goes to.</summary>
    public ScriptedConnection Script => script;

    /// <summary>Gets how many times a transfer marked the connection reusable.</summary>
    public int ReusableMarks { get; private set; }

    /// <summary>Gets how many leases ended, each a <see cref="DisposeAsync" />.</summary>
    public int LeasesEnded { get; private set; }

    /// <inheritdoc />
    public IConnectionSession? Session { get; private set; }

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => script.RemoteEndPoint;

    /// <inheritdoc />
    public EndPoint? LocalEndPoint => script.LocalEndPoint;

    /// <inheritdoc />
    public bool IsSecure => false;

    /// <inheritdoc />
    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => script.ReadAsync(buffer, cancellationToken);

    /// <inheritdoc />
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => script.WriteAsync(buffer, cancellationToken);

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken cancellationToken) => script.FlushAsync(cancellationToken);

    /// <inheritdoc />
    public void MarkReusable() => ReusableMarks++;

    /// <inheritdoc />
    public bool TryHoldSession(IConnectionSession session)
    {
        Session = session;
        return true;
    }

    /// <summary>Ends a transfer's lease of the connection without closing it.</summary>
    /// <returns>A completed task.</returns>
    public ValueTask DisposeAsync()
    {
        LeasesEnded++;
        return ValueTask.CompletedTask;
    }

    /// <summary>Closes the connection as the cache does at exit: the held session's shutdown first.</summary>
    /// <returns>A task that completes when the session has shut down.</returns>
    public async Task CloseAsync()
    {
        if (Session is not null)
        {
            await Session.ShutDownAsync(CancellationToken.None);
        }

        await script.DisposeAsync();
    }
}
