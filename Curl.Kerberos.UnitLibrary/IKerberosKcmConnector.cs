namespace Curl.Kerberos;

/// <summary>
/// Connects to the KCM credential cache daemon's Unix socket, so this library never opens a
/// socket and its tests need no daemon. The Unix-socket implementation lives in
/// <c>Curl.Networking.UnitLibrary</c>.
/// </summary>
public interface IKerberosKcmConnector
{
    /// <summary>Connects to the KCM daemon listening on the Unix socket at <paramref name="socketPath" />.</summary>
    /// <param name="socketPath">The socket's path, e.g. <see cref="CredentialCacheStore.DefaultKcmSocketPath" />.</param>
    /// <returns>The connection's stream, which the caller disposes, or <see langword="null" /> when nothing listens there.</returns>
    Stream? Connect(string socketPath);
}
