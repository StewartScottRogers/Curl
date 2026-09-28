using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Pop3;

/// <summary>
/// One POP3 conversation on an open connection: the greeting, <c>CAPA</c>, the <c>STLS</c>
/// upgrade <c>--ssl</c> and <c>--ssl-reqd</c> ask for, and <c>QUIT</c>, each step and each
/// failure's exit code measured on curl 8.21.0 with <c>Record-CurlExchange.ps1 -Pop3</c>
/// (BL-547).
/// </summary>
/// <remarks>
/// <list type="bullet">
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
/// is exit 100.</item>
/// <item>No failure above sends <c>QUIT</c>. Once the session is open, the response to
/// <c>QUIT</c> is read and whatever it says is ignored, as curl ignores it.</item>
/// </list>
/// </remarks>
internal sealed class Pop3Session(
    Pop3ControlChannel channel,
    ITlsProvider tlsProvider,
    ITransferContext context,
    bool implicitTls) : IAsyncDisposable
{
    private bool secure = implicitTls;

    private IConnection? securedConnection;

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
        catch (Pop3ReplyMissingException)
        {
            return TransferResult.Failure(CurlExitCode.RecvError, Pop3SessionMessages.ResponseReadingFailed);
        }
        catch (InvalidDataException)
        {
            return TransferResult.Failure(CurlExitCode.TooLarge, Pop3SessionMessages.ResponseLineTooLarge);
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

    private async ValueTask<TransferResult?> OpenAsync()
    {
        Pop3Response greeting = await channel.ReadResponseAsync().ConfigureAwait(false);
        if (!greeting.IsOk)
        {
            return TransferResult.Failure(CurlExitCode.WeirdServerReply, Pop3SessionMessages.UnexpectedResponse);
        }

        ApopTimestamp = Pop3ApopTimestamp.Read(greeting.Line);
        return await ReadCapabilitiesAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Sends <c>CAPA</c> and, on a plaintext connection under <c>--ssl</c> or
    /// <c>--ssl-reqd</c>, upgrades as its answer allows.
    /// </summary>
    private async ValueTask<TransferResult?> ReadCapabilitiesAsync()
    {
        await channel.SendAsync("CAPA").ConfigureAwait(false);
        Capabilities = await channel.ReadCapabilitiesAsync().ConfigureAwait(false);
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
    /// Runs the TLS handshake over the connection and asks for the capabilities again over
    /// the secured one; a failed handshake ends the session with its exit code and no
    /// <c>QUIT</c>.
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
        return await ReadCapabilitiesAsync().ConfigureAwait(false);
    }

    private async ValueTask QuitAsync()
    {
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
    }
}
