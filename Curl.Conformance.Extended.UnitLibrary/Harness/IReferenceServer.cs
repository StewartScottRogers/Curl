namespace Curl.Conformance.Extended.Harness;

/// <summary>A reference server that serves one TCP (or TLS) connection curl opened.</summary>
public interface IReferenceServer
{
    /// <summary>Serves <paramref name="connection" />; the harness closes the connection when the task ends.</summary>
    /// <param name="connection">The server's end of the connection, recording what each side sends.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>A task that ends when the server is done with the connection.</returns>
    Task ServeAsync(ReferenceConnection connection, CancellationToken cancellationToken);
}
