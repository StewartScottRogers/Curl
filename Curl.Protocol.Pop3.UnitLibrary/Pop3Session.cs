using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Pop3;

/// <summary>
/// One POP3 conversation on an open connection: the greeting, <c>CAPA</c>, the <c>STLS</c>
/// upgrade <c>--ssl</c> and <c>--ssl-reqd</c> ask for, the login, <c>LIST</c>, <c>RETR</c> or
/// <c>-X</c>'s command, and <c>QUIT</c>, each step and each failure's exit code measured on curl 8.21.0 with
/// <c>Record-CurlExchange.ps1 -Pop3</c> (BL-547, BL-548, BL-549, BL-550).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Login options (<c>--login-options</c>, else the URL's) that curl refuses are exit 3
/// before a byte is read or sent (<see cref="Pop3LoginOptions" />).</item>
/// <item>After <c>CAPA</c> and any upgrade, the session logs in as <see cref="Pop3Login" />
/// describes; a login failure sends no <c>QUIT</c>.</item>
/// <item>A URL naming no message sends <c>LIST</c>, one naming <c>&lt;id&gt;</c> sends
/// <c>RETR &lt;id&gt;</c> (<see cref="Pop3MessageId" />), and <c>-X</c> and <c>-l</c> change
/// the command and whether its answer has a body as <see cref="Pop3Command" /> describes; an
/// id or <c>-X</c> command with a control character is exit 3 after the login, and
/// <c>QUIT</c> is still sent. An answer other than <c>+OK</c> is exit 8,
/// <c>Weird server reply</c>, and <c>QUIT</c> is still sent. A body is written as
/// <see cref="Pop3BodyDecoder" /> decodes it; the server closing before its terminator is a
/// success with no <c>QUIT</c>.</item>
/// <item>A greeting that does not start <c>+OK</c> is exit 8,
/// <c>Got unexpected pop3-server response</c>. The greeting's APOP timestamp is kept in
/// <see cref="ApopTimestamp" />.</item>
/// <item><c>CAPA</c> is always sent, and its answer kept in <see cref="Capabilities" />; a
/// refusal leaves no capabilities and the session carries on.</item>
/// <item>Under <c>--ssl</c> or <c>--ssl-reqd</c> on a plaintext connection, <c>STLS</c> is
/// sent when <c>CAPA</c> advertised it. Not advertised, or <c>CAPA</c> refused:
/// <c>--ssl</c> carries on in plaintext, <c>--ssl-reqd</c> is exit 64
/// <c>STLS not supported.</c>. Answered other than <c>+OK</c>: <c>--ssl</c> carries on in
/// plaintext, <c>--ssl-reqd</c> is exit 64 <c>STARTTLS denied</c>. Answered <c>+OK</c>: the
/// TLS handshake through the injected <see cref="ITlsProvider" />, whose failure is returned
/// as it reported it, then <c>CAPA</c> again.</item>
/// <item>The server closing before a response is complete is exit 56; a line of 65536 bytes
/// is exit 100; a line holding a NUL byte is exit 8 <c>Nul byte in server response line</c>,
/// the line itself not reported (BL-1120).</item>
/// <item>No failure above sends <c>QUIT</c>. Once the session is open, the response to
/// <c>QUIT</c> is read and whatever it says is ignored, as curl ignores it.</item>
/// </list>
/// </remarks>
internal sealed class Pop3Session(
    Pop3ControlChannel channel,
    ITlsProvider tlsProvider,
    ITransferContext context,
    bool implicitTls,
    ISaslAuthenticator? saslAuthenticator = null,
    ConnectionOpenedEvent? opened = null) : IAsyncDisposable
{
    private bool secure = implicitTls;

    private IConnection? securedConnection;

    /// <summary>
    /// Whether the server closed the connection before the body's terminator, after which
    /// curl sends no <c>QUIT</c>.
    /// </summary>
    private bool bodyCutOff;

    /// <summary>
    /// Gets the APOP timestamp the greeting carried, or <see langword="null" /> when it
    /// carried none (<see cref="Pop3ApopTimestamp" />).
    /// </summary>
    public string? ApopTimestamp { get; private set; }

    /// <summary>
    /// Gets the answer to the last <c>CAPA</c> sent, or <see langword="null" /> when it was
    /// refused or has not been sent.
    /// </summary>
    public Pop3Capabilities? Capabilities { get; private set; }

    /// <summary>
    /// Gets whether the session got as far as logging in, after which curl 8.21.0 ends a failed
    /// transfer with <c>shutting down connection</c> rather than <c>closing connection</c>
    /// (BL-552).
    /// </summary>
    public bool IsOpen { get; private set; }

    /// <summary>
    /// Opens the session, lists or retrieves, and closes the session again with <c>QUIT</c>.
    /// </summary>
    /// <returns>
    /// A success carrying the body bytes written, or the failure that stopped the session.
    /// </returns>
    public async ValueTask<TransferResult> RunAsync()
    {
        if (Pop3LoginOptions.Read(context.Mail?.LoginOptions ?? context.Url.Options) is not { } loginOptions)
        {
            return TransferResult.Failure(CurlExitCode.UrlMalformat, Pop3SessionMessages.UrlMalformed);
        }

        TransferResult result;
        try
        {
            if (await OpenAsync(loginOptions).ConfigureAwait(false) is { } failure)
            {
                return failure;
            }

            IsOpen = true;
            result = await TransferAsync().ConfigureAwait(false);
        }
        catch (Pop3ReplyMissingException)
        {
            return TransferResult.Failure(CurlExitCode.RecvError, Pop3SessionMessages.ResponseReadingFailed);
        }
        catch (InvalidDataException)
        {
            return TransferResult.Failure(CurlExitCode.TooLarge, Pop3SessionMessages.ResponseLineTooLarge);
        }
        catch (Pop3NulByteInLineException)
        {
            return TransferResult.Failure(CurlExitCode.WeirdServerReply, Pop3SessionMessages.NulByteInLine);
        }
        catch (SaslAuthenticationFailedException failure)
        {
            // Nothing more is sent, not even QUIT, as curl's Schannel build does (BL-781).
            return TransferResult.Failure(failure.ExitCode, failure.Message);
        }

        if (!bodyCutOff)
        {
            await QuitAsync().ConfigureAwait(false);
        }

        return result;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (securedConnection is not null)
        {
            await securedConnection.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Reads the greeting, asks for the capabilities, upgrades as <c>--ssl</c> asks, and logs
    /// in (<see cref="Pop3Login" />).
    /// </summary>
    private async ValueTask<TransferResult?> OpenAsync(Pop3LoginOptions loginOptions)
    {
        Pop3Response greeting = await channel.ReadResponseAsync().ConfigureAwait(false);
        if (!greeting.IsOk)
        {
            return TransferResult.Failure(CurlExitCode.WeirdServerReply, Pop3SessionMessages.UnexpectedResponse);
        }

        Pop3DiagnosticLogLines.GreetingReceived(context.DiagnosticLog);
        ApopTimestamp = Pop3ApopTimestamp.Read(greeting.Line);
        return await ReadCapabilitiesAsync().ConfigureAwait(false)
            ?? await new Pop3Login(channel, saslAuthenticator, context)
                .LogInAsync(loginOptions, Capabilities, ApopTimestamp)
                .ConfigureAwait(false);
    }

    /// <summary>
    /// Sends <c>CAPA</c> and, on a plaintext connection under <c>--ssl</c> or
    /// <c>--ssl-reqd</c>, upgrades as its answer allows.
    /// </summary>
    private async ValueTask<TransferResult?> ReadCapabilitiesAsync()
    {
        await channel.SendAsync("CAPA").ConfigureAwait(false);
        Capabilities = await channel.ReadCapabilitiesAsync().ConfigureAwait(false);
        Pop3DiagnosticLogLines.CapabilitiesRead(context.DiagnosticLog, Capabilities);
        if (secure || context.SslLevel == TransportSecurityLevel.None)
        {
            return null;
        }

        if (Capabilities is not { AdvertisesStls: true })
        {
            return context.SslLevel == TransportSecurityLevel.Try
                ? null
                : TransferResult.Failure(CurlExitCode.UseSslFailed, Pop3SessionMessages.StlsNotSupported);
        }

        return await StartTlsAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Sends <c>STLS</c> and upgrades on its <c>+OK</c>; any other answer carries on in
    /// plaintext under <c>--ssl</c> and is exit 64 under <c>--ssl-reqd</c>.
    /// </summary>
    private async ValueTask<TransferResult?> StartTlsAsync()
    {
        await channel.SendAsync("STLS").ConfigureAwait(false);
        Pop3Response stls = await channel.ReadResponseAsync().ConfigureAwait(false);
        if (stls.IsOk)
        {
            return await UpgradeAsync().ConfigureAwait(false);
        }

        return context.SslLevel == TransportSecurityLevel.Try
            ? null
            : TransferResult.Failure(CurlExitCode.UseSslFailed, Pop3SessionMessages.StartTlsDenied);
    }

    /// <summary>
    /// Runs the TLS handshake over the connection, reporting it to the transfer's events, and
    /// asks for the capabilities again over the secured one; a failed handshake ends the
    /// session with its exit code and no <c>QUIT</c>. A completed one reports the connect's
    /// <c>Established connection</c> line again, as curl 8.21.0 writes it a second time
    /// between <c>+OK Begin TLS negotiation</c> and the second <c>CAPA</c> (measured, BL-1084).
    /// </summary>
    private async ValueTask<TransferResult?> UpgradeAsync()
    {
        ConnectResult secured = await tlsProvider
            .AuthenticateAsClientAsync(channel.Connection, context.Url.IdnHost, context.Events, context.CancellationToken)
            .ConfigureAwait(false);
        if (secured.Connection is not { } connection)
        {
            return TransferResult.Failure(secured.ExitCode, secured.ErrorMessage!);
        }

        if (opened is not null)
        {
            context.Events.ReportConnectionOpened(opened);
        }

        securedConnection = connection;
        channel.SwitchTo(connection);
        secure = true;
        Pop3DiagnosticLogLines.TlsUpgraded(context.DiagnosticLog);
        return await ReadCapabilitiesAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Sends the command <see cref="Pop3Command" /> chooses and, when its answer has a body,
    /// writes the body to <see cref="ITransferContext.Output" />.
    /// </summary>
    private async ValueTask<TransferResult> TransferAsync()
    {
        if (Pop3Command.Choose(context) is not { } command)
        {
            return TransferResult.Failure(CurlExitCode.UrlMalformat, Pop3SessionMessages.UrlMalformed);
        }

        await channel.SendAsync(command.Line).ConfigureAwait(false);
        Pop3Response response = await channel.ReadResponseAsync().ConfigureAwait(false);
        if (!response.IsOk)
        {
            return TransferResult.Failure(CurlExitCode.WeirdServerReply, Pop3SessionMessages.WeirdServerReply);
        }

        return TransferResult.Success(command.WritesBody ? await ReceiveBodyAsync().ConfigureAwait(false) : 0);
    }

    /// <summary>
    /// Writes the body chunk by chunk through a <see cref="Pop3BodyDecoder" /> until a chunk
    /// ends with the terminator, or the server closes the connection, which curl also counts
    /// as success, writing what it had and sending no <c>QUIT</c>. Each piece the decoder lets
    /// go of is reported as data received just before it is written, as curl's
    /// <c>--trace</c> shows it (BL-552).
    /// </summary>
    /// <returns>How many body bytes were written.</returns>
    private async ValueTask<long> ReceiveBodyAsync()
    {
        context.Progress.ReportTransferStarted();
        var decoder = new Pop3BodyDecoder();
        var pieces = new Pop3BodyPieces();
        long written = 0;
        bool ended = false;
        while (!ended)
        {
            ReadOnlyMemory<byte> chunk = await channel.ReadChunkAsync().ConfigureAwait(false);
            if (chunk.IsEmpty)
            {
                bodyCutOff = true;
                return written;
            }

            ended = decoder.Decode(chunk.Span, pieces);
            await WritePiecesAsync(pieces).ConfigureAwait(false);
            written += pieces.Length;
            pieces.Clear();
            context.Progress.ReportDownloaded(written, null);
        }

        return written;
    }

    private async ValueTask WritePiecesAsync(Pop3BodyPieces pieces)
    {
        for (int index = 0; index < pieces.Count; index++)
        {
            ReadOnlyMemory<byte> piece = pieces[index];
            context.Events.ReportDataReceived(piece.Span);
            await context.Output.WriteAsync(piece, context.CancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask QuitAsync()
    {
        channel.StopReporting();
        await channel.SendAsync("QUIT").ConfigureAwait(false);
        try
        {
            await channel.ReadResponseAsync().ConfigureAwait(false);
        }
        catch (Pop3ReplyMissingException)
        {
        }
        catch (InvalidDataException)
        {
        }
        catch (Pop3NulByteInLineException)
        {
        }
    }
}
