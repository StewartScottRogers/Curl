namespace Curl.Networking;

/// <summary>
/// Unwinds a <see cref="TcpConnector" /> connect whose TLS handshake asked to be run again by the
/// hand-built client (<see cref="ITlsProviderWithHandBuiltRedial" />), so the connector dials once
/// more. The connector catches it; it never reaches the connector's caller.
/// </summary>
internal sealed class HandBuiltRedialRequiredException : Exception
{
    /// <summary>Creates the request to dial again.</summary>
    /// <param name="isProxy">Whether the handshake that asked for it is a proxy's.</param>
    public HandBuiltRedialRequiredException(bool isProxy)
        : base("The TLS handshake must be run again, over a new connection, by the hand-built client.") =>
        IsProxy = isProxy;

    /// <summary>Gets whether the handshake that asked to be run again is a proxy's.</summary>
    public bool IsProxy { get; }
}
