namespace Curl.Protocol.Abstractions;

/// <summary>
/// Upgrades a plaintext connection to TLS.
/// </summary>
/// <remarks>
/// TLS lives behind this one interface because nine of curl's URL schemes differ from
/// another only by transport security. Implementing the handshake per protocol would
/// mean nine copies of the most security-sensitive code in the solution; instead every
/// protocol receives an already-secured <see cref="IConnection" /> and never knows the
/// difference. The production implementation delegates to the .NET
/// <c>SslStream</c> rather than implementing TLS.
/// </remarks>
public interface ITlsProvider
{
    /// <summary>
    /// Performs a client-side TLS handshake over an existing plaintext connection.
    /// </summary>
    /// <param name="plaintext">The connection to upgrade. Ownership transfers to the result.</param>
    /// <param name="targetHost">The host name to validate the server certificate against.</param>
    /// <param name="cancellationToken">Cancels the handshake.</param>
    /// <returns>A connection whose <see cref="IConnection.IsSecure" /> is <see langword="true" />.</returns>
    ValueTask<IConnection> AuthenticateAsClientAsync(
        IConnection plaintext,
        string targetHost,
        CancellationToken cancellationToken);
}
