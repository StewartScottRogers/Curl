using System.Globalization;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Imap;

/// <summary>
/// What an IMAP connection the run's connection cache keeps after a successful transfer
/// remembers for the next URL on it - its tag letter, the commands sent so far and the
/// mailbox last selected - and the <c>LOGOUT</c> it sends when the cache closes it at exit, as
/// curl 8.21.0 keeps an IMAP connection and sends <c>LOGOUT</c> only from
/// <c>imap_disconnect</c> (BL-1987, upstream tests 804, 815, 816, 836 and 1982).
/// </summary>
/// <param name="connection">The pooled connection the session is held on.</param>
/// <param name="loginKey">The login the connection was made for (<see cref="LoginKeyOf" />).</param>
/// <param name="tagLetter">The letter every tag on the connection starts with.</param>
/// <param name="timeProvider">Times the wait for <c>LOGOUT</c>'s reply.</param>
/// <remarks>
/// Neither the <c>LOGOUT</c> nor its reply is reported to <c>-v</c>, <c>--trace</c> or <c>-D</c>,
/// as curl reports neither once the transfer is over (measured 2026-10-01).
/// </remarks>
internal sealed class ImapKeptConnection(IConnection connection, string loginKey, char tagLetter, TimeProvider timeProvider) : IConnectionSession
{
    /// <summary>How long the wait for <c>LOGOUT</c>'s reply lasts: curl's 120-second pingpong response timeout.</summary>
    private static readonly TimeSpan LogoutReplyTimeout = TimeSpan.FromSeconds(120);

    /// <summary>The most bytes read while waiting for <c>LOGOUT</c>'s tagged reply.</summary>
    private const int MaxLogoutReplyBytes = 64 * 1024;

    /// <summary>Gets the letter every tag on the connection starts with.</summary>
    public char TagLetter => tagLetter;

    /// <summary>Gets the number of commands sent on the connection, which the next command's tag counts on from.</summary>
    public byte CommandsSent { get; private set; }

    /// <summary>Gets the mailbox the last <c>SELECT</c> selected, or <see langword="null" /> when none was.</summary>
    public string? SelectedMailbox { get; private set; }

    /// <summary>Gets the UIDVALIDITY the last <c>SELECT</c> reported, or <see langword="null" /> when it reported none.</summary>
    public uint? SelectedUidValidity { get; private set; }

    /// <summary>
    /// Gets or sets whether closing the connection sends <c>LOGOUT</c>: set when a transfer
    /// leaves it intact, cleared while a transfer runs on it, so a failure that ends with no
    /// <c>LOGOUT</c> still sends none.
    /// </summary>
    public bool LogsOutOnShutDown { get; set; }

    /// <summary>
    /// The login a transfer needs, which a kept connection must have been made for, as curl
    /// 8.21.0 reuses an IMAP connection only for the same user, password, login options and
    /// TLS level.
    /// </summary>
    /// <param name="context">The transfer.</param>
    /// <returns>The key.</returns>
    public static string LoginKeyOf(ITransferContext context) =>
        string.Join('\0', context.Credentials?.UserName, context.Credentials?.Password, context.Mail?.LoginOptions ?? context.Url.Options, context.SslLevel);

    /// <summary>Tells whether the connection was made for the login <paramref name="key" /> names.</summary>
    /// <param name="key">A transfer's <see cref="LoginKeyOf" />.</param>
    /// <returns><see langword="true" /> when the logins match.</returns>
    public bool Serves(string key) => key == loginKey;

    /// <summary>Records what the transfer that leaves the connection intact knows of it.</summary>
    /// <param name="commandsSent">The commands sent on the connection so far.</param>
    /// <param name="selectedMailbox">The mailbox selected, or <see langword="null" />.</param>
    /// <param name="selectedUidValidity">Its UIDVALIDITY, or <see langword="null" />.</param>
    public void Remember(byte commandsSent, string? selectedMailbox, uint? selectedUidValidity)
    {
        CommandsSent = commandsSent;
        SelectedMailbox = selectedMailbox;
        SelectedUidValidity = selectedUidValidity;
        LogsOutOnShutDown = true;
    }

    /// <summary>
    /// Sends <c>LOGOUT</c> with the next tag and waits up to 120 seconds for its tagged reply,
    /// ignoring whatever goes wrong, when <see cref="LogsOutOnShutDown" /> is set; otherwise
    /// does nothing.
    /// </summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>A task that completes when the reply is read or the wait ends.</returns>
    public async ValueTask ShutDownAsync(CancellationToken cancellationToken)
    {
        if (!LogsOutOnShutDown)
        {
            return;
        }

        string tag = string.Create(CultureInfo.InvariantCulture, $"{tagLetter}{unchecked((byte)(CommandsSent + 1)):D3}");
        using var limit = new CancellationTokenSource(LogoutReplyTimeout, timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, limit.Token);
        try
        {
            await connection.WriteAsync(Encoding.ASCII.GetBytes(tag + " LOGOUT\r\n"), linked.Token).ConfigureAwait(false);
            await connection.FlushAsync(linked.Token).ConfigureAwait(false);
            await ReadTaggedReplyAsync(Encoding.ASCII.GetBytes(tag + " "), linked.Token).ConfigureAwait(false);
        }
        catch (IOException)
        {
            // The connection is closing anyway, as curl's imap_disconnect ends it on any failure.
        }
        catch (OperationCanceledException)
        {
            // Nor does a reply that never comes keep it open.
        }
    }

    /// <summary>
    /// Reads until a line starting with <paramref name="tagAndSpace" /> is complete, the server
    /// hangs up, or <see cref="MaxLogoutReplyBytes" /> are read.
    /// </summary>
    private async ValueTask ReadTaggedReplyAsync(byte[] tagAndSpace, CancellationToken cancellationToken)
    {
        byte[] reply = new byte[MaxLogoutReplyBytes];
        int length = 0;
        int read;
        do
        {
            read = await connection.ReadAsync(reply.AsMemory(length), cancellationToken).ConfigureAwait(false);
            length += read;
        }
        while (read > 0 && length < reply.Length && !EndsTaggedLine(reply.AsSpan(0, length), tagAndSpace));
    }

    /// <summary>Whether <paramref name="received" /> ends with a complete line that starts with <paramref name="tagAndSpace" />.</summary>
    private static bool EndsTaggedLine(ReadOnlySpan<byte> received, ReadOnlySpan<byte> tagAndSpace)
    {
        if (received[^1] != (byte)'\n')
        {
            return false;
        }

        ReadOnlySpan<byte> beforeLast = received[..^1];
        ReadOnlySpan<byte> lastLine = beforeLast[(beforeLast.LastIndexOf((byte)'\n') + 1)..];
        return lastLine.StartsWith(tagAndSpace);
    }
}
