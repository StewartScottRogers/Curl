using System.Net;
using System.Text;

namespace Curl.Extension.OpenTelemetry.UnitTests;

/// <summary>A fake <see cref="IConnector" /> that hands out one <see cref="RecordedConnection" />, or fails to connect, and records the target.</summary>
internal sealed class RecordedConnector(ConnectResult result) : IConnector, IConnectionListener
{
    public RecordedConnector(RecordedConnection connection)
        : this(ConnectResult.Connected(connection))
    {
    }

    public ConnectTarget? Target { get; private set; }

    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        Target = target;
        return ValueTask.FromResult(result);
    }

    public ValueTask<ListenResult> ListenAsync(ListenTarget target, CancellationToken cancellationToken) =>
        throw new NotSupportedException("The OpenTelemetry hook never listens.");
}

/// <summary>
/// A fake <see cref="IConnection" /> that replays recorded response bytes, records what is written, and throws an
/// <see cref="IOException" /> on read when given no response.
/// </summary>
internal sealed class RecordedConnection(string? response) : IConnection
{
    private readonly byte[] _responseBytes = Encoding.Latin1.GetBytes(response ?? string.Empty);
    private readonly MemoryStream _written = new();
    private int _position;

    public bool IsSecure => false;

    public EndPoint? RemoteEndPoint => null;

    public bool IsDisposed { get; private set; }

    public string Written => Encoding.UTF8.GetString(_written.ToArray());

    public string WrittenBody => Written[(Written.IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4)..];

    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (response is null)
        {
            throw new IOException("An existing connection was forcibly closed by the remote host.");
        }

        var count = Math.Min(buffer.Length, _responseBytes.Length - _position);
        _responseBytes.AsMemory(_position, count).CopyTo(buffer);
        _position += count;
        return ValueTask.FromResult(count);
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        _written.Write(buffer.Span);
        return ValueTask.CompletedTask;
    }

    public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}

/// <summary>A fake <see cref="IDiagnosticLog" /> that records every line written.</summary>
internal sealed class RecordedDiagnosticLog : IDiagnosticLog
{
    public List<string> Lines { get; } = [];

    public bool IsEnabled(DiagnosticLogLevel level) => true;

    public void Write(DiagnosticLogLevel level, string component, string message) => Lines.Add($"{level} {component}: {message}");
}
