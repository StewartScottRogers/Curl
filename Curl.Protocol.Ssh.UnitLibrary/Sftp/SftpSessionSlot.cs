using Curl.Ssh;
using Curl.Ssh.Transport;

namespace Curl.Protocol.Ssh.Sftp;

/// <summary>
/// The SFTP session one SSH connection carries and the home directory its <c>REALPATH .</c>
/// found, kept from one transfer to the next as curl 8.22.0 keeps both on a reused
/// connection (CGR-130, ADR-0865): the first transfer starts the session and asks for the
/// home directory, and every later one uses them as they are.
/// </summary>
/// <remarks>
/// A slot that keeps the session for the connection leaves its channel open at each
/// transfer's end (<see cref="SftpSession.DefersChannelClose" />), and
/// <see cref="CloseIgnoringFailureAsync" /> closes it before the connection's
/// <c>DISCONNECT</c>; any other slot lets each transfer close it as before.
/// </remarks>
/// <param name="keepsSession">Whether the session outlives the transfer that started it.</param>
internal sealed class SftpSessionSlot(bool keepsSession)
{
    private static readonly byte[] HomeDirectoryPath = "."u8.ToArray();

    private SftpSession? session;

    private byte[]? homeDirectory;

    /// <summary>Initializes a new instance of the <see cref="SftpSessionSlot" /> class whose session ends with its transfer.</summary>
    internal SftpSessionSlot()
        : this(keepsSession: false)
    {
    }

    /// <summary>
    /// Gets the session, starting it - curl's <c>SSH_SFTP_INIT</c> - when the slot has none yet.
    /// </summary>
    /// <param name="transport">The transport, after the user is authenticated.</param>
    /// <param name="trace">Where the state change goes.</param>
    /// <param name="cancellationToken">Cancels the start.</param>
    /// <returns>The session.</returns>
    /// <exception cref="SshTransferException">The session could not start (<see cref="SftpSession.StartAsync" />).</exception>
    internal async ValueTask<SftpSession> StartAsync(SshTransport transport, SshStateTrace trace, CancellationToken cancellationToken)
    {
        if (session is { } started)
        {
            return started;
        }

        trace.Enter("SSH_SFTP_INIT");
        session = await SftpSession.StartAsync(transport, cancellationToken).ConfigureAwait(false);
        session.DefersChannelClose = keepsSession;
        session.HasDeferredChannelClose = keepsSession;
        return session;
    }

    /// <summary>
    /// Gets the home directory, asking with <c>REALPATH .</c> - curl's <c>SSH_SFTP_REALPATH</c> -
    /// when the slot has not asked yet.
    /// </summary>
    /// <param name="started">The session <see cref="StartAsync" /> gave.</param>
    /// <param name="trace">Where the state change goes.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The home directory, as the server sent its bytes.</returns>
    /// <exception cref="SshTransferException">The server answered with a status, or the connection broke (exit 79).</exception>
    internal async ValueTask<byte[]> HomeDirectoryAsync(SftpSession started, SshStateTrace trace, CancellationToken cancellationToken)
    {
        if (homeDirectory is { } known)
        {
            return known;
        }

        trace.Enter("SSH_SFTP_REALPATH");
        homeDirectory = await SshConnectionFailure.ReportAsSshLayerErrorAsync(
            () => started.RealPathAsync(HomeDirectoryPath, cancellationToken)).ConfigureAwait(false);
        return homeDirectory;
    }

    /// <summary>
    /// Closes the channel the last transfer left open (<see cref="SftpSession.HasDeferredChannelClose" />),
    /// as curl shuts the SFTP session down before its <c>DISCONNECT</c>; a broken connection is
    /// left as it is.
    /// </summary>
    /// <param name="cancellationToken">Cancels the close.</param>
    /// <returns>A task that completes once the channel is closed, or when there was none to close.</returns>
    internal async ValueTask CloseIgnoringFailureAsync(CancellationToken cancellationToken)
    {
        if (session is not { HasDeferredChannelClose: true } open)
        {
            return;
        }

        open.HasDeferredChannelClose = false;
        try
        {
            await open.ShutdownAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (SshConnectionFailure.Is(exception))
        {
        }
    }
}
