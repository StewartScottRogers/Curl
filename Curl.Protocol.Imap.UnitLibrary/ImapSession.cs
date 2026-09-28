using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Imap;

/// <summary>
/// One IMAP conversation on an open connection: the greeting, <c>CAPABILITY</c>, the
/// <c>STARTTLS</c> upgrade <c>--ssl</c> and <c>--ssl-reqd</c> ask for, the <c>SELECT</c> and
/// <c>FETCH</c> of the message the URL names, and <c>LOGOUT</c>, each step and each
/// failure's exit code measured on curl 8.21.0 with <c>Record-CurlExchange.ps1 -Imap</c> and
/// read in its <c>lib/imap.c</c> (BL-553, BL-555).
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
/// <item>Then, unless the greeting was <c>PREAUTH</c>, the login
/// <see cref="ImapAuthentication" /> describes, reading the capabilities <c>SASL-IR</c>,
/// <c>LOGINDISABLED</c> and <c>AUTH=&lt;mech&gt;</c> in any case; login options curl rejects
/// (<see cref="ImapLoginOptions" />) are exit 3 before the greeting is read.</item>
/// <item>No failure above sends <c>LOGOUT</c>. Once the session is open, <c>LOGOUT</c>'s
/// response is read and whatever it says, or the server hanging up, is ignored.</item>
/// <item>Once the session is open, the URL's path is read as <see cref="ImapUrlPath" />
/// describes; a malformed one is exit 3. A mailbox with a <c>UID</c> or <c>MAILINDEX</c>
/// sends <c>SELECT</c> (the name as <see cref="ImapQuoting" /> writes it), then
/// <c>UID FETCH uid BODY[section]&lt;partial&gt;</c> or <c>FETCH index ...</c>. <c>SELECT</c>
/// answered other than <c>OK</c> is exit 67 <c>Select failed</c>; a numeric URL
/// <c>UIDVALIDITY</c> other than the one <c>SELECT</c> reported is exit 78 <c>Mailbox
/// UIDVALIDITY has changed</c>; <c>FETCH</c> completing before an untagged <c>FETCH</c> is
/// exit 78, and an untagged <c>FETCH</c> with no <c>{n}</c> exit 8. The literal's n bytes are
/// written to the output as they arrive; its completion other than <c>OK</c> is then exit
/// 8. Each of these sends <c>LOGOUT</c> first. The server closing inside the literal (exit
/// 18) or the output failing (exit 23) ends the transfer with no <c>LOGOUT</c>.</item>
/// </list>
/// </remarks>
internal sealed class ImapSession(
    ImapControlChannel channel,
    ITlsProvider tlsProvider,
    ISaslAuthenticator? saslAuthenticator,
    ITransferContext context,
    bool implicitTls) : IAsyncDisposable
{
    private const string CapabilityCommand = "CAPABILITY";

    private const string StartTlsCommand = "STARTTLS";

    private const string AuthCapabilityPrefix = "AUTH=";

    private static readonly char[] WordSeparators = [' ', '\t', '\r', '\n'];

    private readonly HashSet<string> capabilities = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The SASL mechanisms the last <c>CAPABILITY</c> advertised, as curl resets them each time.</summary>
    private readonly List<string> offeredMechanisms = [];

    private bool secure = implicitTls;

    private bool preauthenticated;

    private IConnection? securedConnection;

    /// <summary>
    /// Opens the session, logs in, fetches the message the URL names when it names one, and
    /// closes the session again with <c>LOGOUT</c>. Login options curl rejects are exit 3
    /// before anything is read or sent.
    /// </summary>
    /// <returns>A success, or the failure that stopped the session.</returns>
    public async ValueTask<TransferResult> RunAsync()
    {
        if (ImapLoginOptions.Parse(context.Mail?.LoginOptions ?? context.Url.Options) is not { } loginOptions)
        {
            return TransferResult.Failure(CurlExitCode.UrlMalformat, ImapSessionMessages.MalformedUrl);
        }

        try
        {
            return await OpenAsync().ConfigureAwait(false)
                ?? await LoginAsync(loginOptions).ConfigureAwait(false)
                ?? await PerformAsync().ConfigureAwait(false);
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

    /// <summary>Keeps every untagged response, as curl does while <c>SELECT</c> waits.</summary>
    private static readonly Func<string, bool> AnyUntagged = static _ => true;

    private static readonly Func<string, bool> IsCapabilityResponse = static line => IsUntaggedResponse(line, CapabilityCommand);

    private static readonly Func<string, bool> IsFetchResponse = static line => IsUntaggedResponse(line, "FETCH");

    /// <summary>
    /// Whether <paramref name="line" /> is an untagged <paramref name="name" /> response as
    /// curl's <c>imap_matchresp</c> judges one: <c>* </c>, an optional number and a space,
    /// then the name in any case followed by a space or by exactly one character before the LF.
    /// </summary>
    private static bool IsUntaggedResponse(string line, string name)
    {
        int start = ResponseNameStartOf(line);
        int after = start + name.Length;
        return start > 0
            && after < line.Length
            && line.AsSpan(start, name.Length).Equals(name, StringComparison.OrdinalIgnoreCase)
            && (line[after] == ' ' || after + 1 == line.Length);
    }

    /// <summary>
    /// The mailbox's UIDVALIDITY as the last untagged <c>OK [UIDVALIDITY n]</c> (in any case)
    /// of a <c>SELECT</c> response that carries a number reports it, or
    /// <see langword="null" /> when none does.
    /// </summary>
    private static uint? UidValidityOf(IReadOnlyList<string> selectUntagged)
    {
        const string Prefix = "* OK [UIDVALIDITY ";
        uint? validity = null;
        foreach (string line in selectUntagged)
        {
            if (line.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            {
                validity = LeadingNumberOf(line.AsSpan(Prefix.Length)) ?? validity;
            }
        }

        return validity;
    }

    /// <summary>
    /// The number the digits starting <paramref name="text" /> spell, as curl's
    /// <c>curlx_str_number</c> reads one: <see langword="null" /> when there are none or it
    /// is more than <see cref="uint.MaxValue" />; whatever follows them is ignored.
    /// </summary>
    private static uint? LeadingNumberOf(ReadOnlySpan<char> text)
    {
        int digits = text.IndexOfAnyExceptInRange('0', '9');
        return uint.TryParse(digits < 0 ? text : text[..digits], NumberStyles.None, CultureInfo.InvariantCulture, out uint number)
            ? number
            : null;
    }

    /// <summary>
    /// The size of the literal an untagged <c>FETCH</c> response announces, read as curl
    /// does: digits right after the line's first <c>{</c>, then <c>}</c>; <see langword="null" />
    /// when the line has no such literal.
    /// </summary>
    private static long? LiteralSizeOf(string fetchLine)
    {
        int open = fetchLine.IndexOf('{', StringComparison.Ordinal);
        if (open < 0)
        {
            return null;
        }

        ReadOnlySpan<char> afterBrace = fetchLine.AsSpan(open + 1);
        int digits = afterBrace.IndexOfAnyExceptInRange('0', '9');
        return digits >= 0
            && afterBrace[digits] == '}'
            && long.TryParse(afterBrace[..digits], NumberStyles.None, CultureInfo.InvariantCulture, out long size)
                ? size
                : null;
    }

    /// <summary>
    /// The <c>FETCH</c> curl sends for <paramref name="path" />: <c>UID FETCH</c> for a
    /// <c>UID</c>, else <c>FETCH</c> for a <c>MAILINDEX</c>, then <c>BODY[section]</c> and
    /// <c>&lt;partial&gt;</c> when the URL gives one.
    /// </summary>
    private static string FetchCommandOf(ImapUrlPath path)
    {
        string command = path.Uid is { } uid ? "UID FETCH " + uid : "FETCH " + path.MailIndex;
        command += " BODY[" + path.Section + "]";
        return path.Partial is { } partial ? command + "<" + partial + ">" : command;
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

    /// <summary>
    /// Adds every word of <paramref name="capabilityResponse" />'s untagged lines to the
    /// capabilities, and replaces the offered mechanisms with its <c>AUTH=&lt;mech&gt;</c> words.
    /// </summary>
    private void RecordCapabilities(ImapResponse capabilityResponse)
    {
        offeredMechanisms.Clear();
        foreach (string untagged in capabilityResponse.Untagged)
        {
            string[] words = untagged[2..].Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries);
            capabilities.UnionWith(words);
            offeredMechanisms.AddRange(
                words.Where(word => word.Length > AuthCapabilityPrefix.Length && word.StartsWith(AuthCapabilityPrefix, StringComparison.OrdinalIgnoreCase))
                    .Select(word => word[AuthCapabilityPrefix.Length..]));
        }
    }

    private async ValueTask<TransferResult?> CapabilityAsync()
    {
        ImapResponse response = await ExchangeAsync(CapabilityCommand, IsCapabilityResponse).ConfigureAwait(false);
        RecordCapabilities(response);
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

    /// <summary>
    /// Logs in as <see cref="ImapAuthentication" /> describes, unless the greeting was
    /// <c>PREAUTH</c>.
    /// </summary>
    private async ValueTask<TransferResult?> LoginAsync(ImapLoginOptions loginOptions) =>
        preauthenticated
            ? null
            : await new ImapAuthentication(channel, saslAuthenticator, context)
                .AuthenticateAsync(loginOptions, offeredMechanisms, capabilities).ConfigureAwait(false);

    /// <summary>
    /// Once the session is open: reads the URL's path, fetches the message it names when it
    /// names a mailbox and a <c>UID</c> or <c>MAILINDEX</c>, and closes with <c>LOGOUT</c>. A
    /// malformed path is exit 3 after <c>LOGOUT</c>. Any other URL (a listing, a search, a
    /// custom command or an upload) is not implemented yet and closes with a success.
    /// </summary>
    private async ValueTask<TransferResult> PerformAsync()
    {
        if (ImapUrlPath.Parse(context.Url.AbsolutePath) is not { } path)
        {
            return await LogoutAndFailAsync(CurlExitCode.UrlMalformat, ImapSessionMessages.MalformedUrl).ConfigureAwait(false);
        }

        if (NamesAMessageToFetch(path))
        {
            return await SelectAsync(path.Mailbox!, path).ConfigureAwait(false);
        }

        await LogoutAsync().ConfigureAwait(false);
        return TransferResult.Success(0);
    }

    /// <summary>
    /// Whether curl fetches for <paramref name="path" />: it names a mailbox and a
    /// <c>UID</c> or <c>MAILINDEX</c>, with no custom command and no upload.
    /// </summary>
    private bool NamesAMessageToFetch(ImapUrlPath path) =>
        path.Mailbox is not null
        && (path.Uid ?? path.MailIndex) is not null
        && context.Upload is null
        && context.Mail?.CustomCommand is null;

    /// <summary>
    /// Sends <c>SELECT</c> for <paramref name="mailbox" />: other than <c>OK</c> is exit 67,
    /// a UIDVALIDITY other than the URL's is exit 78, each after <c>LOGOUT</c>; otherwise the
    /// message is fetched.
    /// </summary>
    private async ValueTask<TransferResult> SelectAsync(string mailbox, ImapUrlPath path)
    {
        ImapResponse selected = await ExchangeAsync("SELECT " + ImapQuoting.AtomOrQuoted(mailbox), AnyUntagged).ConfigureAwait(false);
        if (selected.Status != ImapResponseStatus.Ok)
        {
            return await LogoutAndFailAsync(CurlExitCode.LoginDenied, ImapSessionMessages.SelectFailed).ConfigureAwait(false);
        }

        if (UidValidityOf(selected.Untagged) is { } actual
            && path.UidValidity is { } requested
            && LeadingNumberOf(requested) is { } expected
            && actual != expected)
        {
            return await LogoutAndFailAsync(CurlExitCode.RemoteFileNotFound, ImapSessionMessages.UidValidityChanged).ConfigureAwait(false);
        }

        return await FetchAsync(path).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends the <c>FETCH</c> and waits for its untagged <c>FETCH</c> response: a completion
    /// first is exit 78 and a response with no literal exit 8, each after <c>LOGOUT</c>;
    /// otherwise the literal is written to the output.
    /// </summary>
    private async ValueTask<TransferResult> FetchAsync(ImapUrlPath path)
    {
        await channel.SendCommandAsync(FetchCommandOf(path)).ConfigureAwait(false);
        if (await channel.ReadUntaggedAsync(IsFetchResponse).ConfigureAwait(false) is not { } fetchLine)
        {
            return await LogoutAndFailAsync(CurlExitCode.RemoteFileNotFound, ImapSessionMessages.RemoteFileNotFound).ConfigureAwait(false);
        }

        return LiteralSizeOf(fetchLine) is { } size
            ? await DownloadAsync(size).ConfigureAwait(false)
            : await LogoutAndFailAsync(CurlExitCode.WeirdServerReply, ImapSessionMessages.FetchResponseUnparsed).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes the literal's <paramref name="size" /> bytes to the output, then reads the rest
    /// of the <c>FETCH</c> response: its completion other than <c>OK</c> is exit 8 after
    /// <c>LOGOUT</c>. The server closing inside the literal is exit 18 and the output failing
    /// exit 23, both without <c>LOGOUT</c>, as curl drops a connection a transfer failed on.
    /// </summary>
    private async ValueTask<TransferResult> DownloadAsync(long size)
    {
        context.Progress.ReportTransferStarted();
        long written = 0;
        while (written < size)
        {
            ReadOnlyMemory<byte> piece = await channel.ReadLiteralPieceAsync(size - written).ConfigureAwait(false);
            if (piece.IsEmpty)
            {
                return TransferResult.Failure(CurlExitCode.PartialFile, ImapSessionMessages.LiteralCutShort(size - written), written);
            }

            if (await WriteOutputAsync(piece, written).ConfigureAwait(false) is { } failure)
            {
                return failure;
            }

            written += piece.Length;
            context.Progress.ReportDownloaded(written, size);
        }

        ImapResponse completion = await channel.ReadResponseAsync(NoUntagged).ConfigureAwait(false)
            ?? throw new ImapResponseMissingException();
        if (completion.Status != ImapResponseStatus.Ok)
        {
            return await LogoutAndFailAsync(CurlExitCode.WeirdServerReply, ImapSessionMessages.WeirdServerReply, size).ConfigureAwait(false);
        }

        await LogoutAsync().ConfigureAwait(false);
        return TransferResult.Success(size);
    }

    /// <summary>Writes <paramref name="piece" /> to the output; exit 23 when the output fails.</summary>
    private async ValueTask<TransferResult?> WriteOutputAsync(ReadOnlyMemory<byte> piece, long written)
    {
        try
        {
            await context.Output.WriteAsync(piece, context.CancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (IOException exception)
        {
            int accepted = exception is OutputWriteFailedException failed ? failed.BytesAccepted : 0;
            return TransferResult.Failure(
                CurlExitCode.WriteError, ImapSessionMessages.OutputWriteFailed(piece.Length, accepted), written);
        }
    }

    private async ValueTask<TransferResult> LogoutAndFailAsync(CurlExitCode exitCode, string message, long bytesTransferred = 0)
    {
        await LogoutAsync().ConfigureAwait(false);
        return TransferResult.Failure(exitCode, message, bytesTransferred);
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
