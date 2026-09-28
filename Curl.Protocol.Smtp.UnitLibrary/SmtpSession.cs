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
/// bytes is exit 100.</item>
/// <item>No failure above sends <c>QUIT</c>. Once the session is open, <c>QUIT</c>'s reply is
/// read and whatever it says is ignored, as curl ignores it.</item>
/// </list>
/// </remarks>
internal sealed class SmtpSession(
    SmtpControlChannel channel,
    ITlsProvider tlsProvider,
    ITransferContext context,
    string domain,
    bool implicitTls) : IAsyncDisposable
{
    private const int StartTlsAccepted = 220;

    private const string StartTlsKeyword = "STARTTLS";

    private bool secure = implicitTls;

    private IConnection? securedConnection;

    /// <summary>
    /// Opens the session and closes it again with <c>QUIT</c>.
    /// </summary>
    /// <returns>A success, or the failure that stopped the session opening.</returns>
    public async ValueTask<TransferResult> RunAsync()
    {
        try
        {
            if (await OpenAsync().ConfigureAwait(false) is { } failure)
            {
                return failure;
            }
        }
        catch (SmtpReplyMissingException)
        {
            return TransferResult.Failure(CurlExitCode.RecvError, SmtpSessionMessages.ResponseReadingFailed);
        }
        catch (InvalidDataException)
        {
            return TransferResult.Failure(CurlExitCode.TooLarge, SmtpSessionMessages.ReplyLineTooLarge);
        }

        await QuitAsync().ConfigureAwait(false);
        return TransferResult.Success(0);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (securedConnection is not null)
        {
            await securedConnection.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static bool AdvertisesStartTls(SmtpReply ehlo) =>
        ehlo.Lines.Any(line => line.Length >= 4 + StartTlsKeyword.Length
            && line.AsSpan(4, StartTlsKeyword.Length).Equals(StartTlsKeyword, StringComparison.OrdinalIgnoreCase));

    private async ValueTask<TransferResult?> OpenAsync()
    {
        SmtpReply greeting = await ReadReplyAsync().ConfigureAwait(false);
        return greeting.IsCompletion
            ? await GreetAsync().ConfigureAwait(false)
            : TransferResult.Failure(CurlExitCode.WeirdServerReply, SmtpSessionMessages.UnexpectedResponse(greeting.Code));
    }

    private async ValueTask<TransferResult?> GreetAsync()
    {
        SmtpReply ehlo = await ExchangeAsync("EHLO " + domain).ConfigureAwait(false);
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
        SmtpReply helo = await ExchangeAsync("HELO " + domain).ConfigureAwait(false);
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

        if (!AdvertisesStartTls(ehlo))
        {
            return context.SslLevel == TransportSecurityLevel.Try
                ? null
                : TransferResult.Failure(CurlExitCode.UseSslFailed, SmtpSessionMessages.StartTlsNotSupported);
        }

        return await StartTlsAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Sends <c>STARTTLS</c> and upgrades on its 220; any other answer carries on in
    /// plaintext under <c>--ssl</c> and is exit 64 under <c>--ssl-reqd</c>.
    /// </summary>
    private async ValueTask<TransferResult?> StartTlsAsync()
    {
        SmtpReply startTls = await ExchangeAsync(StartTlsKeyword).ConfigureAwait(false);
        if (startTls.Code == StartTlsAccepted)
        {
            return await UpgradeAsync().ConfigureAwait(false);
        }

        return context.SslLevel == TransportSecurityLevel.Try
            ? null
            : TransferResult.Failure(CurlExitCode.UseSslFailed, SmtpSessionMessages.StartTlsDenied(startTls.Code));
    }

    /// <summary>
    /// Runs the TLS handshake over the connection and greets again over the secured one; a
    /// failed handshake ends the session with its exit code and no <c>QUIT</c>.
    /// </summary>
    private async ValueTask<TransferResult?> UpgradeAsync()
    {
        ConnectResult secured = await tlsProvider
            .AuthenticateAsClientAsync(channel.Connection, context.Url.IdnHost, context.CancellationToken)
            .ConfigureAwait(false);
        if (secured.Connection is not { } connection)
        {
            return TransferResult.Failure(secured.ExitCode, secured.ErrorMessage!);
        }

        securedConnection = connection;
        channel.SwitchTo(connection);
        secure = true;
        return await GreetAsync().ConfigureAwait(false);
    }

    private async ValueTask QuitAsync()
    {
        await channel.SendAsync("QUIT").ConfigureAwait(false);
        try
        {
            await channel.ReadReplyAsync().ConfigureAwait(false);
        }
        catch (InvalidDataException)
        {
        }
    }

    private async ValueTask<SmtpReply> ExchangeAsync(string command)
    {
        await channel.SendAsync(command).ConfigureAwait(false);
        return await ReadReplyAsync().ConfigureAwait(false);
    }

    private async ValueTask<SmtpReply> ReadReplyAsync() =>
        await channel.ReadReplyAsync().ConfigureAwait(false) ?? throw new SmtpReplyMissingException();
}
