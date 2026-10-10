using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Smtp;

/// <summary>
/// One SMTP conversation on an open connection: the greeting, <c>EHLO</c> (or <c>HELO</c>),
/// the <c>STARTTLS</c> upgrade <c>--ssl</c> and <c>--ssl-reqd</c> ask for, and <c>QUIT</c>,
/// each step and each failure's exit code measured on curl 8.21.0 with
/// <c>Record-CurlExchange.ps1 -Smtp</c> (BL-540).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>A greeting that is not 2xx is exit 8, <c>Got unexpected smtp-server response: 554</c>.</item>
/// <item>A refused <c>EHLO</c> falls back to <c>HELO</c> with the same domain, except under
/// <c>--ssl-reqd</c> before TLS, which is exit 9, <c>Remote access denied: 502</c>. A refused
/// <c>HELO</c> is exit 9 too. A session greeted with <c>HELO</c> never upgrades.</item>
/// <item>Under <c>--ssl</c> or <c>--ssl-reqd</c> on a plaintext connection, <c>STARTTLS</c>
/// is sent when a line of the <c>EHLO</c> reply starts, after its code, with <c>STARTTLS</c>
/// in any case. Not advertised: <c>--ssl</c> carries on in plaintext, <c>--ssl-reqd</c> is
/// exit 64 <c>STARTTLS not supported.</c>. Answered other than 220: <c>--ssl</c> carries on
/// in plaintext without a second <c>EHLO</c>, <c>--ssl-reqd</c> is exit 64
/// <c>STARTTLS denied, code 454</c>. Answered 220: the TLS handshake through the injected
/// <see cref="ITlsProvider" />, whose failure is returned as it reported it, then
/// <c>EHLO</c> again.</item>
/// <item>The server closing before a reply is complete is exit 56; a reply line of 65536
/// bytes is exit 100; a reply line holding a NUL byte is exit 8
/// <c>Nul byte in server response line</c>, the line unreported (BL-1121); a command that
/// cannot be written is exit 55 (<see cref="SmtpSendFailedException" />, BL-1243).</item>
/// <item>No failure above sends <c>QUIT</c>. Once the session is open, <c>QUIT</c>'s reply is
/// read and whatever it says is ignored, as curl ignores it.</item>
/// <item>Given an <see cref="ISaslAuthenticator" />, a session opened with <c>EHLO</c>
/// authenticates through <see cref="SmtpSaslAuthentication" /> before anything else
/// (BL-541); one greeted with <c>HELO</c> never does.</item>
/// <item>With <c>-T</c> and at least one <c>--mail-rcpt</c>, the open session sends the
/// message through <see cref="SmtpMailTransaction" /> (BL-542). Otherwise it sends
/// <c>VRFY</c>, <c>EXPN</c>, <c>HELP</c> or the <c>-X</c> command through
/// <see cref="SmtpCommandTransfer" /> (BL-543).</item>
/// </list>
/// </remarks>
internal sealed class SmtpSession(
    SmtpControlChannel channel,
    ITlsProvider tlsProvider,
    ISaslAuthenticator? saslAuthenticator,
    ITransferContext context,
    string domain,
    bool implicitTls,
    SmtpCommandLineText commandLineText,
    ConnectionOpenedEvent? opened) : IAsyncDisposable
{
    private const int StartTlsAccepted = 220;

    private const string StartTlsKeyword = "STARTTLS";

    private const string SmtpUtf8Keyword = "SMTPUTF8";

    private const string SizeKeyword = "SIZE";

    private bool secure = implicitTls;

    /// <summary>Whether an <c>AUTH</c> exchange succeeded, so <c>--mail-auth</c> is sent.</summary>
    private bool authenticated;

    private IConnection? securedConnection;

    /// <summary>The reply to the last accepted <c>EHLO</c>; <see langword="null" /> once greeted with <c>HELO</c>.</summary>
    private SmtpReply? capabilities;

    /// <summary>
    /// Opens the session, authenticates, sends the message when there is one or the commands
    /// when there is not, and closes it with <c>QUIT</c>.
    /// </summary>
    /// <returns>A success, or the failure that stopped the session or its message.</returns>
    public async ValueTask<TransferResult> RunAsync()
    {
        try
        {
            if ((await OpenAsync().ConfigureAwait(false) ?? await AuthenticateAsync().ConfigureAwait(false)) is { } failure)
            {
                return failure;
            }
        }
        catch (SmtpReplyMissingException)
        {
            return TransferResult.Failure(CurlExitCode.RecvError, SmtpSessionMessages.ResponseReadingFailed);
        }
        catch (SmtpNulByteInReplyException)
        {
            return TransferResult.Failure(CurlExitCode.WeirdServerReply, SmtpSessionMessages.NulByteInResponseLine);
        }
        catch (InvalidDataException)
        {
            return TransferResult.Failure(CurlExitCode.TooLarge, SmtpSessionMessages.ReplyLineTooLarge);
        }
        catch (SmtpSendFailedException failure)
        {
            return TransferResult.Failure(CurlExitCode.SendError, failure.Message);
        }
        catch (SaslAuthenticationFailedException failure)
        {
            // Nothing more is sent, not even QUIT, as curl's Schannel build does (BL-781).
            return TransferResult.Failure(failure.ExitCode, failure.Message);
        }

        channel.Trace.PerformStarts();
        return await SendMailOrCommandsAsync().ConfigureAwait(false);
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
    /// Sends the message when there is an upload and a recipient, and the commands
    /// otherwise, as curl chooses between them.
    /// </summary>
    private ValueTask<TransferResult> SendMailOrCommandsAsync()
    {
        if (context.Upload is { } upload && context.Mail is { Recipients.Count: > 0 } mail)
        {
            var extensions = new SmtpMailExtensions(authenticated, Advertises(SizeKeyword), Advertises(SmtpUtf8Keyword));
            return new SmtpMailTransaction(channel, context, extensions, commandLineText).SendAsync(upload, mail);
        }

        return new SmtpCommandTransfer(channel, context, Advertises(SmtpUtf8Keyword), commandLineText).SendAsync(context.Mail ?? new MailRequestOptions());
    }

    /// <summary>Whether the last accepted <c>EHLO</c> advertised <paramref name="keyword" />; never after <c>HELO</c>.</summary>
    private bool Advertises(string keyword) => capabilities?.Advertises(keyword) == true;

    private async ValueTask<TransferResult?> AuthenticateAsync()
    {
        if (saslAuthenticator is null || capabilities is not { } ehlo)
        {
            return null;
        }

        var authentication = new SmtpSaslAuthentication(channel, saslAuthenticator, context);
        TransferResult? failure = await authentication.AuthenticateAsync(ehlo).ConfigureAwait(false);
        authenticated = authentication.IsAuthenticated;
        return failure;
    }

    private async ValueTask<TransferResult?> OpenAsync()
    {
        channel.Trace.Enter("SERVERGREET");
        SmtpReply greeting = await ReadReplyAsync().ConfigureAwait(false);
        if (!greeting.IsCompletion)
        {
            return TransferResult.Failure(CurlExitCode.WeirdServerReply, SmtpSessionMessages.UnexpectedResponse(greeting.Code));
        }

        SmtpDiagnosticLogLines.GreetingReceived(context.DiagnosticLog, greeting.Code);
        return await GreetAsync().ConfigureAwait(false);
    }

    private async ValueTask<TransferResult?> GreetAsync()
    {
        SmtpReply ehlo = await ExchangeAsync("EHLO " + domain, "EHLO").ConfigureAwait(false);
        capabilities = ehlo.IsCompletion ? ehlo : null;
        if (ehlo.IsCompletion)
        {
            return await SecureAsync(ehlo).ConfigureAwait(false);
        }

        return context.SslLevel == TransportSecurityLevel.Required && !secure
            ? TransferResult.Failure(CurlExitCode.RemoteAccessDenied, SmtpSessionMessages.RemoteAccessDenied(ehlo.Code))
            : await HeloAsync().ConfigureAwait(false);
    }

    private async ValueTask<TransferResult?> HeloAsync()
    {
        SmtpReply helo = await ExchangeAsync("HELO " + domain, "HELO").ConfigureAwait(false);
        return helo.IsCompletion
            ? null
            : TransferResult.Failure(CurlExitCode.RemoteAccessDenied, SmtpSessionMessages.RemoteAccessDenied(helo.Code));
    }

    private async ValueTask<TransferResult?> SecureAsync(SmtpReply ehlo)
    {
        if (secure || context.SslLevel == TransportSecurityLevel.None)
        {
            return null;
        }

        if (!ehlo.Advertises(StartTlsKeyword))
        {
            return context.SslLevel == TransportSecurityLevel.Try
                ? null
                : TransferResult.Failure(CurlExitCode.UseSslFailed, SmtpSessionMessages.StartTlsNotSupported);
        }

        return await StartTlsAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Sends <c>STARTTLS</c> and upgrades on its 220; any other answer carries on in
    /// plaintext under <c>--ssl</c> and is exit 64 under <c>--ssl-reqd</c>. A reply with more
    /// bytes pipelined behind it is exit 8 whatever its code, and nothing more is sent, as
    /// curl 8.21.0 forbids pipelining there (upstream test980, BL-1985).
    /// </summary>
    private async ValueTask<TransferResult?> StartTlsAsync()
    {
        SmtpReply startTls = await ExchangeAsync(StartTlsKeyword, StartTlsKeyword).ConfigureAwait(false);
        if (channel.HasBufferedBytes)
        {
            return TransferResult.Failure(CurlExitCode.WeirdServerReply, SmtpSessionMessages.WeirdServerReply);
        }

        if (startTls.Code == StartTlsAccepted)
        {
            return await UpgradeAsync().ConfigureAwait(false);
        }

        return context.SslLevel == TransportSecurityLevel.Try
            ? null
            : TransferResult.Failure(CurlExitCode.UseSslFailed, SmtpSessionMessages.StartTlsDenied(startTls.Code));
    }

    /// <summary>
    /// Runs the TLS handshake over the connection, reporting it to the transfer's events, and
    /// greets again over the secured one; a failed handshake ends the session with its exit
    /// code and no <c>QUIT</c>. A completed one reports the connect's
    /// <c>Established connection</c> line again, as curl 8.21.0 writes it a second time
    /// between <c>220 Ready to start TLS</c> and the second <c>EHLO</c> (measured, BL-1058).
    /// </summary>
    private async ValueTask<TransferResult?> UpgradeAsync()
    {
        channel.Trace.Enter("UPGRADETLS");
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
        SmtpDiagnosticLogLines.TlsUpgraded(context.DiagnosticLog);
        return await GreetAsync().ConfigureAwait(false);
    }

    /// <summary>Sends <paramref name="command" />, which puts curl's state machine in <paramref name="state" />, and reads its reply.</summary>
    private async ValueTask<SmtpReply> ExchangeAsync(string command, string state)
    {
        await channel.SendAsync(command).ConfigureAwait(false);
        channel.Trace.Enter(state);
        return await ReadReplyAsync().ConfigureAwait(false);
    }

    private async ValueTask<SmtpReply> ReadReplyAsync() =>
        await channel.ReadReplyAsync().ConfigureAwait(false) ?? throw new SmtpReplyMissingException();
}
