using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ftp;

/// <summary>
/// What an FTP control connection the run's connection cache keeps after a transfer remembers
/// for the next URL on it, and the <c>QUIT</c> it sends when the cache closes it at exit, as
/// curl 8.21.0 keeps the control connection and sends <c>QUIT</c> only from
/// <c>ftp_disconnect</c> (BL-1981, ADR-0050).
/// </summary>
/// <param name="connection">The pooled control connection the session is held on.</param>
/// <param name="loginKey">The login the connection was made for (<see cref="LoginKeyOf" />).</param>
/// <param name="timeProvider">Times the wait for <c>QUIT</c>'s reply.</param>
/// <remarks>
/// Neither the <c>QUIT</c> nor its reply is reported to <c>-v</c>, <c>--trace</c> or <c>-D</c>,
/// as curl reports neither (BL-930).
/// </remarks>
internal sealed class FtpKeptConnection(IConnection connection, string loginKey, TimeProvider timeProvider) : IConnectionSession
{
    /// <summary>How long curl's <c>ftp_quit</c> waits for <c>QUIT</c>'s reply: its 120-second response timeout.</summary>
    private static readonly TimeSpan QuitReplyTimeout = TimeSpan.FromSeconds(120);

    private static readonly byte[] QuitCommand = Encoding.ASCII.GetBytes("QUIT\r\n");

    /// <summary>Gets the directory the <c>PWD</c> after login named, which a reused connection changes back to.</summary>
    public string? EntryPath { get; private set; }

    /// <summary>Gets the bytes the last transfer read past its last reply.</summary>
    public byte[] UnreadBytes { get; private set; } = [];

    /// <summary>Gets whether an accepted <c>PROT P</c> secures every data connection.</summary>
    public bool ProtectsData { get; private set; }

    /// <summary>Gets the last <c>TYPE</c> the server accepted, <c>A</c> or <c>I</c>, or <see langword="null" /> before any.</summary>
    public char? TransferType { get; private set; }

    /// <summary>
    /// Gets curl's <c>prevpath</c>: the URL path's directory part the last transfer left the
    /// connection in, or <see langword="null" /> when it remembered none.
    /// </summary>
    public string? PreviousPath { get; private set; }

    /// <summary>
    /// Gets or sets whether closing the connection sends <c>QUIT</c>: set when a transfer leaves
    /// it intact, cleared while a transfer runs on it, so a failure that ends with no
    /// <c>QUIT</c> still sends none.
    /// </summary>
    public bool QuitsOnShutDown { get; set; }

    /// <summary>
    /// The login a transfer needs, which a kept connection must have been made for, as curl
    /// 8.21.0 reuses an FTP connection only for the same user, password, account,
    /// alternative-to-user command and TLS level.
    /// </summary>
    /// <param name="context">The transfer.</param>
    /// <returns>The key.</returns>
    public static string LoginKeyOf(ITransferContext context) =>
        string.Join('\0', context.Credentials?.UserName, context.Credentials?.Password, context.FtpAccount, context.FtpAlternativeToUser, FtpTlsRequirements.Of(context), context.FtpCommandChannelClearing);

    /// <summary>Tells whether the connection was made for the login <paramref name="key" /> names.</summary>
    /// <param name="key">A transfer's <see cref="LoginKeyOf" />.</param>
    /// <returns><see langword="true" /> when the logins match.</returns>
    public bool Serves(string key) => key == loginKey;

    /// <summary>Records what the transfer that leaves the connection intact knows of it.</summary>
    /// <param name="entryPath">The entry path.</param>
    /// <param name="protectsData">Whether data connections are TLS.</param>
    /// <param name="transferType">The type last accepted.</param>
    /// <param name="previousPath">The directory part remembered, or <see langword="null" />.</param>
    /// <param name="unreadBytes">The bytes read past the last reply, which the next transfer reads first.</param>
    public void Remember(string? entryPath, bool protectsData, char? transferType, string? previousPath, byte[] unreadBytes)
    {
        EntryPath = entryPath;
        ProtectsData = protectsData;
        TransferType = transferType;
        PreviousPath = previousPath;
        UnreadBytes = unreadBytes;
        QuitsOnShutDown = true;
    }

    /// <summary>
    /// Sends <c>QUIT</c> and waits up to 120 seconds for one reply line, ignoring whatever goes
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
            await connection.WriteAsync(QuitCommand, linked.Token).ConfigureAwait(false);
            await connection.FlushAsync(linked.Token).ConfigureAwait(false);
            await ReadReplyLineAsync(linked.Token).ConfigureAwait(false);
        }
        catch (IOException)
        {
            // The connection is closing anyway, as curl's ftp_quit ends it on any failure.
        }
        catch (OperationCanceledException)
        {
            // Nor does a reply that never comes keep it open.
        }
    }

    private async ValueTask ReadReplyLineAsync(CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[256];
        int read;
        do
        {
            read = await connection.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        }
        while (read > 0 && Array.IndexOf(buffer, (byte)'\n', 0, read) < 0);
    }
}
