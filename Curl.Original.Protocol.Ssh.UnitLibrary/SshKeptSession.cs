using System.Security.Cryptography;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Ssh.Sftp;
using Curl.Ssh;
using Curl.Ssh.Transport;

namespace Curl.Original.Protocol.Ssh;

/// <summary>
/// An authenticated SSH session a transfer left on its connection for the run's connection
/// cache, as curl 8.22.0 leaves an <c>scp</c> or <c>sftp</c> connection intact and reuses it
/// for the next URL of the same login (CGR-130, ADR-0865): the transport, its SFTP session
/// and home directory, and the login it serves. Shut down when the cache closes the
/// connection, it closes the SFTP channel and sends <c>DISCONNECT</c> 11 <c>Shutdown</c>.
/// </summary>
/// <param name="transport">The authenticated transport.</param>
/// <param name="connection">The connection the transport reads and writes.</param>
/// <param name="sftp">The connection's SFTP session, when one was started.</param>
/// <param name="loginKey">The login the session authenticated (<see cref="LoginKeyOf" />).</param>
internal sealed class SshKeptSession(SshTransport transport, SshSessionConnection connection, SftpSessionSlot sftp, string loginKey) : IConnectionSession
{
    private bool isShutDown;

    /// <summary>Gets the authenticated transport.</summary>
    internal SshTransport Transport => transport;

    /// <summary>Gets the connection's SFTP session and home directory.</summary>
    internal SftpSessionSlot Sftp => sftp;

    /// <summary>
    /// Gets what a session must have been authenticated with to serve a transfer: the user
    /// name, the password and the key files, which curl's connection match compares for SSH.
    /// </summary>
    /// <param name="context">The transfer.</param>
    /// <returns>The key, equal for transfers one session may serve.</returns>
    internal static string LoginKeyOf(ITransferContext context) =>
        string.Join('\0', context.Credentials?.UserName, context.Credentials?.Password, context.Ssh?.PrivateKeyPath, context.Ssh?.PublicKeyPath, context.Ssh?.PrivateKeyPassphrase);

    /// <summary>Tells whether the session was authenticated with <paramref name="key" />.</summary>
    /// <param name="key">A transfer's <see cref="LoginKeyOf" />.</param>
    /// <returns><see langword="true" /> when the session may serve the transfer.</returns>
    /// <remarks>
    /// The key holds the password and the pass phrase, so it is compared in constant time, as
    /// curl compares reuse credentials with <c>Curl_timestrcmp</c> (AF-0521): the SHA-256
    /// digests of both keys UTF-8 bytes are compared with
    /// <see cref="CryptographicOperations.FixedTimeEquals" />, so the time taken reveals neither
    /// where the keys first differ nor their lengths.
    /// </remarks>
    internal bool Serves(string key) =>
        CryptographicOperations.FixedTimeEquals(DigestOf(key), DigestOf(loginKey));

    private static byte[] DigestOf(string key) => SHA256.HashData(Encoding.UTF8.GetBytes(key));

    /// <summary>Points the transport at the connection of the transfer reusing the session.</summary>
    /// <param name="current">That transfer's connection.</param>
    internal void UseConnection(IConnection current) => connection.Current = current;

    /// <inheritdoc />
    /// <remarks>
    /// Ends the session once: a failed transfer on a reused connection shuts it down and the
    /// cache's close of that connection shuts it down again, yet curl sends one
    /// <c>DISCONNECT</c> (BL-4059).
    /// </remarks>
    public async ValueTask ShutDownAsync(CancellationToken cancellationToken)
    {
        if (isShutDown)
        {
            return;
        }

        isShutDown = true;
        await sftp.CloseIgnoringFailureAsync(cancellationToken).ConfigureAwait(false);
        await SshProtocolHandler.DisconnectIgnoringFailureAsync(transport, cancellationToken).ConfigureAwait(false);
    }
}
