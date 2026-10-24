namespace Curl.Conformance.Differential.Protocols.Core;

/// <summary>One passive data listener: it accepts a single data connection, then is closed.</summary>
public interface IPassiveDataListener : IDisposable
{
    /// <summary>Gets the port the client is told to connect to.</summary>
    int Port { get; }

    /// <summary>Waits for the client's data connection and closes the listener.</summary>
    Task<Stream> AcceptAsync(CancellationToken cancellationToken);
}
