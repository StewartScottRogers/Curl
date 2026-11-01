using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Pop3;

/// <summary>
/// A POP3 connection the run's connection cache keeps, logged in, after a successful transfer,
/// so the next URL for the same login carries on with its command, and the <c>QUIT</c> it sends
/// when the cache closes it at exit, as curl 8.22.0 sends <c>CAPA</c>, its login, <c>RETR 1</c>,
/// <c>RETR 2</c> and one <c>QUIT</c> for <c>pop3://h/1 pop3://h/2</c> (BL-3167).
/// </summary>
/// <param name="connection">The lease of the pooled connection the session is first held on, which <see cref="ResumedOn" /> moves on to each later lease.</param>
/// <param name="loginKey">The login the connection was made for (<see cref="LoginKeyOf" />).</param>
/// <param name="timeProvider">Times the wait for <c>QUIT</c>'s reply.</param>
/// <remarks>
/// Neither the <c>QUIT</c> nor its reply is reported to <c>-v</c>, <c>--trace</c> or <c>-D</c>,
/// as for IMAP's <c>LOGOUT</c> (<c>ImapKeptConnection</c>).
/// </remarks>
internal sealed class Pop3KeptConnection(IConnection connection, string loginKey, TimeProvider timeProvider) : IConnectionSession
{
    /// <summary>How long the wait for <c>QUIT</c>'s reply lasts: curl's 120-second pingpong response timeout.</summary>
    private static readonly TimeSpan QuitReplyTimeout = TimeSpan.FromSeconds(120);

    /// <summary>The most bytes read while waiting for <c>QUIT</c>'s reply line.</summary>
    private const int MaxQuitReplyBytes = 64 * 1024;

    /// <summary>
    /// Gets or sets whether closing the connection sends <c>QUIT</c>: set when a transfer leaves
    /// it intact, cleared while a transfer runs on it, so a failure that ends with no <c>QUIT</c>
    /// still sends none.
    /// </summary>
    public bool QuitsOnShutDown { get; set; }

    /// <summary>
    /// The login a transfer needs, which a kept connection must have been made for, as curl
    /// reuses a POP3 connection only for the same user, password and TLS level.
    /// </summary>
    /// <param name="context">The transfer.</param>
    /// <returns>The key.</returns>
    public static string LoginKeyOf(ITransferContext context) =>
        string.Join('\0', context.Credentials?.UserName, context.Credentials?.Password, context.SslLevel);

    /// <summary>Tells whether the connection was made for the login <paramref name="key" /> names.</summary>
    /// <param name="key">A transfer's <see cref="LoginKeyOf" />.</param>
    /// <returns><see langword="true" /> when the logins match.</returns>
    public bool Serves(string key) => key == loginKey;

    /// <summary>
    /// Moves the session onto the lease of the pooled connection a later transfer reuses it
    /// through, so keeping it after that transfer marks that lease reusable, as curl keeps one
    /// connection for every URL of the same login, not only the first two (CGR-102, BL-3631).
    /// </summary>
    /// <param name="lease">The connection the reusing transfer was handed.</param>
    /// <returns>This session.</returns>
    public Pop3KeptConnection ResumedOn(IConnection lease)
    {
        connection = lease;
        return this;
    }

    /// <summary>
    /// Hands the connection back to the run's connection cache, holding this session, so it
    /// sends <c>QUIT</c> only when the cache closes it.
    /// </summary>
    /// <returns><see langword="true" /> when the connection is kept; <see langword="false" /> when it cannot hold a session.</returns>
    public bool TryKeep()
    {
        if (!connection.TryHoldSession(this))
        {
            return false;
        }

        QuitsOnShutDown = true;
        connection.MarkReusable();
        return true;
    }

    /// <summary>
    /// Sends <c>QUIT</c> and waits up to 120 seconds for its reply line, ignoring whatever goes
    /// wrong, when <see cref="QuitsOnShutDown" /> is set; otherwise does nothing.
    /// </summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>A task that completes when the reply is read or the wait ends.</returns>
    public async ValueTask ShutDownAsync(CancellationToken cancellationToken)
    {
        if (!QuitsOnShutDown)
        {
            return;
        }

        using var limit = new CancellationTokenSource(QuitReplyTimeout, timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, limit.Token);
        try
        {
            await connection.WriteAsync(Encoding.ASCII.GetBytes("QUIT\r\n"), linked.Token).ConfigureAwait(false);
            await connection.FlushAsync(linked.Token).ConfigureAwait(false);
            await ReadReplyLineAsync(linked.Token).ConfigureAwait(false);
        }
        catch (IOException)
        {
            // The connection is closing anyway, as curl's pop3_disconnect ends it on any failure.
        }
        catch (OperationCanceledException)
        {
            // Nor does a reply that never comes keep it open.
        }
    }

    /// <summary>Reads until a line ends, the server hangs up, or <see cref="MaxQuitReplyBytes" /> are read.</summary>
    private async ValueTask ReadReplyLineAsync(CancellationToken cancellationToken)
    {
        byte[] reply = new byte[MaxQuitReplyBytes];
        int length = 0;
        int read;
        do
        {
            read = await connection.ReadAsync(reply.AsMemory(length), cancellationToken).ConfigureAwait(false);
            length += read;
        }
        while (read > 0 && length < reply.Length && reply[length - 1] != (byte)'\n');
    }
}
