using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Imap;

/// <summary>
/// One IMAP conversation on an open connection: the greeting, <c>CAPABILITY</c>, the
/// <c>STARTTLS</c> upgrade <c>--ssl</c> and <c>--ssl-reqd</c> ask for, and <c>LOGOUT</c>,
/// each step and each failure's exit code measured on curl 8.21.0 with
/// <c>Record-CurlExchange.ps1 -Imap</c> and read in its <c>lib/imap.c</c> (BL-553).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>A greeting that is neither <c>* OK</c> nor <c>* PREAUTH</c> (case-sensitive) is exit
/// 8, <c>Got unexpected imap-server response</c>. Either way <c>CAPABILITY</c> follows; the
/// capabilities a greeting carries are not read.</item>
/// <item>A capability is a word, in any case, of an untagged <c>CAPABILITY</c> response,
/// literals included. Under <c>--ssl</c> or <c>--ssl-reqd</c> on a plaintext connection,
/// <c>STARTTLS</c> is sent when <c>CAPABILITY</c> was answered <c>OK</c>, advertised
/// <c>STARTTLS</c> and the greeting was not <c>PREAUTH</c>. Otherwise <c>--ssl</c> carries on
/// in plaintext and <c>--ssl-reqd</c> is exit 64 <c>STARTTLS not available.</c>.</item>
/// <item><c>STARTTLS</c>'s tagged answer followed in the same read by more bytes is exit 8,
/// <c>Weird server reply</c>. Answered other than <c>OK</c>: <c>--ssl</c> carries on in
/// plaintext, <c>--ssl-reqd</c> is exit 64 <c>STARTTLS denied</c>. Answered <c>OK</c>: the
/// TLS handshake through the injected <see cref="ITlsProvider" />, whose failure is returned
/// as it reported it, then <c>CAPABILITY</c> again.</item>
/// <item>A continuation, or a NUL byte in a line, is exit 8; the server closing before a
/// response is complete is exit 56; a response line of 65536 bytes is exit 100.</item>
/// <item>No failure above sends <c>LOGOUT</c>. Once the session is open, <c>LOGOUT</c>'s
/// response is read and whatever it says, or the server hanging up, is ignored.</item>
/// </list>
/// </remarks>
internal sealed class ImapSession(
    ImapControlChannel channel,
    ITlsProvider tlsProvider,
    ITransferContext context,
    bool implicitTls) : IAsyncDisposable
{
    private const string CapabilityCommand = "CAPABILITY";

    private const string StartTlsCommand = "STARTTLS";

    private static readonly char[] WordSeparators = [' ', '\t', '\r', '\n'];

    private readonly HashSet<string> capabilities = new(StringComparer.OrdinalIgnoreCase);

    private bool secure = implicitTls;

    private bool preauthenticated;

    private IConnection? securedConnection;

    /// <summary>
    /// Opens the session and closes it again with <c>LOGOUT</c>.
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
        catch (ImapResponseMissingException)
        {
            return TransferResult.Failure(CurlExitCode.RecvError, ImapSessionMessages.ResponseReadingFailed);
        }
        catch (InvalidDataException)
        {
            return TransferResult.Failure(CurlExitCode.TooLarge, ImapSessionMessages.ResponseLineTooLarge);
        }
        catch (ImapWeirdResponseException weird)
        {
            return TransferResult.Failure(CurlExitCode.WeirdServerReply, weird.Message);
        }

        await LogoutAsync().ConfigureAwait(false);
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

    /// <summary>Keeps no untagged response: the greeting, <c>STARTTLS</c> and <c>LOGOUT</c> want none.</summary>
    private static readonly Func<string, bool> NoUntagged = static _ => false;

    /// <summary>
    /// Whether <paramref name="line" /> is an untagged <c>CAPABILITY</c> response as curl's
    /// <c>imap_matchresp</c> judges one: <c>* </c>, an optional number and a space, then the
    /// name in any case followed by a space or by exactly one character before the LF.
    /// </summary>
    private static bool IsCapabilityResponse(string line)
    {
        int name = ResponseNameStartOf(line);
        int after = name + CapabilityCommand.Length;
        return name > 0
            && after < line.Length
            && line.AsSpan(name, CapabilityCommand.Length).Equals(CapabilityCommand, StringComparison.OrdinalIgnoreCase)
            && (line[after] == ' ' || after + 1 == line.Length);
    }

    /// <summary>
    /// Where the response name starts in the untagged <paramref name="line" />: after the
    /// <c>* </c>, and after a number and its space when one follows it; -1 when a number is
    /// not followed by a space.
    /// </summary>
    private static int ResponseNameStartOf(string line)
    {
        const int afterStar = 2;
        int digitsEnd = afterStar;
        while (digitsEnd < line.Length && char.IsAsciiDigit(line[digitsEnd]))
        {
            digitsEnd++;
        }

        if (digitsEnd == afterStar)
        {
            return afterStar;
        }

        return digitsEnd < line.Length && line[digitsEnd] == ' ' ? digitsEnd + 1 : -1;
    }

    private async ValueTask<TransferResult?> OpenAsync()
    {
        ImapResponse greeting = await channel.ReadResponseAsync(NoUntagged).ConfigureAwait(false)
            ?? throw new ImapResponseMissingException();
        if (greeting.Status == ImapResponseStatus.NotOk)
        {
            return TransferResult.Failure(CurlExitCode.WeirdServerReply, ImapSessionMessages.UnexpectedGreeting);
        }

        preauthenticated = greeting.Status == ImapResponseStatus.Preauth;
        return await CapabilityAsync().ConfigureAwait(false);
    }

    private async ValueTask<TransferResult?> CapabilityAsync()
    {
        ImapResponse response = await ExchangeAsync(CapabilityCommand, IsCapabilityResponse).ConfigureAwait(false);
        foreach (string untagged in response.Untagged)
        {
            capabilities.UnionWith(untagged[2..].Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries));
        }

        if (secure || context.SslLevel == TransportSecurityLevel.None)
        {
            return null;
        }

        return IsStartTlsOffered(response)
            ? await StartTlsAsync().ConfigureAwait(false)
            : CarryOnInPlaintextOrFail(ImapSessionMessages.StartTlsNotAvailable);
    }

    /// <summary>
    /// Whether <c>STARTTLS</c> may be sent: <c>CAPABILITY</c> was answered <c>OK</c>,
    /// advertised it, and the greeting was not <c>PREAUTH</c>.
    /// </summary>
    private bool IsStartTlsOffered(ImapResponse capabilityResponse) =>
        capabilityResponse.Status == ImapResponseStatus.Ok
        && capabilities.Contains(StartTlsCommand)
        && !preauthenticated;

    /// <summary>
    /// <see langword="null" />, carrying on in plaintext, under <c>--ssl</c>; exit 64 with
    /// <paramref name="message" /> under <c>--ssl-reqd</c>.
    /// </summary>
    private TransferResult? CarryOnInPlaintextOrFail(string message) =>
        context.SslLevel == TransportSecurityLevel.Try
            ? null
            : TransferResult.Failure(CurlExitCode.UseSslFailed, message);

    /// <summary>
    /// Sends <c>STARTTLS</c> and upgrades on its <c>OK</c>; any other answer carries on in
    /// plaintext under <c>--ssl</c> and is exit 64 under <c>--ssl-reqd</c>.
    /// </summary>
    private async ValueTask<TransferResult?> StartTlsAsync()
    {
        ImapResponse response = await ExchangeAsync(StartTlsCommand, NoUntagged).ConfigureAwait(false);
        if (channel.HasUnreadBytes)
        {
            return TransferResult.Failure(CurlExitCode.WeirdServerReply, ImapSessionMessages.WeirdServerReply);
        }

        return response.Status == ImapResponseStatus.Ok
            ? await UpgradeAsync().ConfigureAwait(false)
            : CarryOnInPlaintextOrFail(ImapSessionMessages.StartTlsDenied);
    }

    /// <summary>
    /// Runs the TLS handshake over the connection and asks for the capabilities again over
    /// the secured one; a failed handshake ends the session with its exit code and no
    /// <c>LOGOUT</c>.
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
        return await CapabilityAsync().ConfigureAwait(false);
    }

    private async ValueTask LogoutAsync()
    {
        await channel.SendCommandAsync("LOGOUT").ConfigureAwait(false);
        try
        {
            await channel.ReadResponseAsync(NoUntagged).ConfigureAwait(false);
        }
        catch (InvalidDataException)
        {
        }
        catch (ImapWeirdResponseException)
        {
        }
    }

    private async ValueTask<ImapResponse> ExchangeAsync(string command, Func<string, bool> isWanted)
    {
        await channel.SendCommandAsync(command).ConfigureAwait(false);
        return await channel.ReadResponseAsync(isWanted).ConfigureAwait(false) ?? throw new ImapResponseMissingException();
    }
}
