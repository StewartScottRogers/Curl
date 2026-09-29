namespace Curl.Tls;

/// <summary>
/// A TLS connection ended with a fatal alert after its handshake: the client sent
/// <see cref="Alert" /> because of what the server sent, or received it from the server.
/// <see cref="Tls13ClientStream" /> throws it from a read or write, as <c>SslStream</c>
/// throws an <see cref="IOException" />.
/// </summary>
public sealed class TlsAlertException : IOException
{
    /// <summary>Creates the exception for <paramref name="alert" />.</summary>
    /// <param name="alert">The alert.</param>
    /// <param name="isFromServer">Whether the server sent the alert, rather than the client.</param>
    public TlsAlertException(TlsAlertDescription alert, bool isFromServer)
        : base(isFromServer ? $"The server sent the TLS alert {alert}." : $"The client sent the TLS alert {alert}.")
    {
        Alert = alert;
        IsFromServer = isFromServer;
    }

    /// <summary>Gets the alert that ended the connection.</summary>
    public TlsAlertDescription Alert { get; }

    /// <summary>Gets a value indicating whether the server sent the alert; otherwise the client sent it.</summary>
    public bool IsFromServer { get; }
}
