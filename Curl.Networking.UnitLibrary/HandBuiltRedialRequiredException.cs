namespace Curl.Networking;

/// <summary>
/// Unwinds a <see cref="TcpConnector" /> connect whose TLS handshake asked to be run again by the
/// hand-built client (<see cref="ITlsProviderWithHandBuiltRedial" />), so the connector dials once
/// more. The connector catches it; it never reaches the connector's caller.
/// </summary>
internal sealed class HandBuiltRedialRequiredException : Exception
{
    /// <summary>Creates the request to dial again.</summary>
    public HandBuiltRedialRequiredException()
        : base("The TLS handshake must be run again, over a new connection, by the hand-built client.")
    {
    }
}
