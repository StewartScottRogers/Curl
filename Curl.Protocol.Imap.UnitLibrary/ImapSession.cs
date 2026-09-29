using System.Globalization;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Imap;

/// <summary>
/// One IMAP conversation on an open connection: the greeting, <c>CAPABILITY</c>, the
/// <c>STARTTLS</c> upgrade <c>--ssl</c> and <c>--ssl-reqd</c> ask for, the <c>SELECT</c> and
/// <c>FETCH</c> of the message the URL names, and <c>LOGOUT</c>, each step and each
/// failure's exit code measured on curl 8.21.0 with <c>Record-CurlExchange.ps1 -Imap</c> and
/// read in its <c>lib/imap.c</c> (BL-553, BL-555, BL-556, BL-557).
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
/// <item>A <c>-X</c> command is percent-decoded; one decoding to a control byte is exit 3.
/// A mailbox with a <c>-X</c> command or a non-empty query (and no <c>UID</c> or
/// <c>MAILINDEX</c>) is selected as above, then the command, or <c>SEARCH</c> and the decoded
/// query, is sent; with no mailbox the command is sent at once, and with neither
/// <c>LIST "mailbox" *</c>. The untagged responses <see cref="ImapListedResponses" /> names
/// are written to the output as they arrive; completion other than <c>OK</c> is exit 21 after
/// <c>LOGOUT</c> (BL-556).</item>
/// <item>A <c>-T</c> upload, whatever the path's parameters or <c>-X</c> say, is appended to
/// the mailbox as <see cref="ImapAppend" /> describes, then <c>LOGOUT</c> is sent whatever
/// became of it (BL-557).</item>
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
    /// Gets how far the session got, which decides the line <c>-v</c> ends a failed transfer
    /// with (BL-559).
    /// </summary>
    public ImapSessionPhase Phase { get; private set; }

    /// <summary>
    /// Opens the session, logs in, fetches, lists, searches or sends the <c>-X</c> command as
    /// the URL asks, and closes the session again with <c>LOGOUT</c>. Login options curl rejects are exit 3
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
            if ((await OpenAsync().ConfigureAwait(false) ?? await LoginAsync(loginOptions).ConfigureAwait(false)) is { } failure)
            {
                return failure;
            }

            Phase = ImapSessionPhase.Performing;
            return await PerformAsync().ConfigureAwait(false);
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
        catch (SaslAuthenticationFailedException failure)
        {
            // Nothing more is sent, not even LOGOUT, as curl's Schannel build does (BL-781).
            return TransferResult.Failure(failure.ExitCode, failure.Message);
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
    /// <param name="line">The line, without its LF.</param>
    /// <param name="name">The response name, such as <c>FETCH</c>.</param>
    /// <returns>Whether the line is that response.</returns>
    internal static bool IsUntaggedResponse(string line, string name)
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
    /// Whether <paramref name="path" /> names a numeric <c>UIDVALIDITY</c> other than the one
    /// the <paramref name="selected" /> response reported; never when either is missing.
    /// </summary>
    private static bool IsUidValidityChanged(ImapResponse selected, ImapUrlPath path) =>
        UidValidityOf(selected.Untagged) is { } actual
        && path.UidValidity is { } requested
        && LeadingNumberOf(requested) is { } expected
        && actual != expected;

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

        ImapDiagnosticLogLines.GreetingReceived(context.DiagnosticLog, greeting.Status);
        preauthenticated = greeting.Status == ImapResponseStatus.Preauth;
        if (preauthenticated)
        {
            context.Events.ReportInfo(ImapInfoLines.Preauthenticated);
        }

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
        ImapDiagnosticLogLines.TlsUpgraded(context.DiagnosticLog);
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
    /// Once the session is open, does what curl does for the URL and <c>-X</c>: a path or
    /// command that is malformed or decodes to a control byte is exit 3 after <c>LOGOUT</c>;
    /// an upload is appended to the URL's mailbox (BL-557); a mailbox with a
    /// command, a <c>UID</c>, a <c>MAILINDEX</c> or a search query is selected first;
    /// anything else sends the command, or <c>LIST</c>, straight away.
    /// </summary>
    private async ValueTask<TransferResult> PerformAsync()
    {
        if (ImapUrlPath.Parse(context.Url.AbsolutePath) is not { } path
            || !TryDecodeCustomCommand(out string? customCommand))
        {
            return await LogoutAndFailAsync(CurlExitCode.UrlMalformat, ImapSessionMessages.MalformedUrl).ConfigureAwait(false);
        }

        return context.Upload is { } upload
            ? await AppendAsync(path, upload).ConfigureAwait(false)
            : await PerformForAsync(path, customCommand).ConfigureAwait(false);
    }

    /// <summary>
    /// Uploads <paramref name="upload" /> to the mailbox of <paramref name="path" /> as
    /// <see cref="ImapAppend" /> describes, then sends <c>LOGOUT</c> whatever became of it.
    /// </summary>
    private async ValueTask<TransferResult> AppendAsync(ImapUrlPath path, Stream upload)
    {
        var append = new ImapAppend(channel, context);
        TransferResult result;
        try
        {
            result = await append.AppendAsync(path.Mailbox, upload).ConfigureAwait(false);
        }
        finally
        {
            if (append.IsMessageSent)
            {
                Phase = ImapSessionPhase.Completing;
            }
        }

        await LogoutAsync().ConfigureAwait(false);
        return result;
    }

    /// <summary>
    /// Selects the mailbox of <paramref name="path" /> first when it comes with a command, a
    /// <c>UID</c>, a <c>MAILINDEX</c> or a search query; otherwise sends
    /// <paramref name="customCommand" />, or <c>LIST</c> when there is none.
    /// </summary>
    private ValueTask<TransferResult> PerformForAsync(ImapUrlPath path, string? customCommand)
    {
        string? query = SearchQueryOf(path);
        if (path.Mailbox is { } mailbox && AsksForASelectedMailbox(path, customCommand, query))
        {
            return SelectAsync(mailbox, path, customCommand, query);
        }

        return customCommand is null
            ? ListAsync("LIST \"" + EscapedMailboxOf(path) + "\" *", ImapListedResponses.ListWanted)
            : SendCustomCommandAsync(customCommand);
    }

    /// <summary>
    /// Whether what is asked for works on a selected mailbox: a <c>-X</c> command, a
    /// <c>UID</c>, a <c>MAILINDEX</c> or a search query.
    /// </summary>
    private static bool AsksForASelectedMailbox(ImapUrlPath path, string? customCommand, string? query) =>
        (customCommand ?? path.Uid ?? path.MailIndex ?? query) is not null;

    /// <summary>Sends <paramref name="customCommand" /> and writes the responses curl writes for it.</summary>
    private ValueTask<TransferResult> SendCustomCommandAsync(string customCommand) =>
        ListAsync(customCommand, ImapListedResponses.CustomWanted(customCommand));

    /// <summary>
    /// What follows a successful <c>SELECT</c>: <paramref name="customCommand" /> when there
    /// is one, else <c>SEARCH</c> with <paramref name="query" /> when there is one, else the
    /// <c>FETCH</c>.
    /// </summary>
    private ValueTask<TransferResult> AfterSelectAsync(ImapUrlPath path, string? customCommand, string? query) =>
        customCommand is not null ? SendCustomCommandAsync(customCommand)
        : query is not null ? ListAsync("SEARCH " + query, ImapListedResponses.SearchWanted)
        : FetchAsync(path);

    /// <summary>
    /// The mailbox of <paramref name="path" /> as curl puts it between <c>LIST</c>'s quotes:
    /// each <c>\</c> and <c>"</c> escaped by a <c>\</c>, and empty when there is none.
    /// </summary>
    private static string EscapedMailboxOf(ImapUrlPath path) =>
        (path.Mailbox ?? string.Empty).Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);

    /// <summary>
    /// Percent-decodes the <c>-X</c> command as curl does; <see langword="false" /> when it
    /// decodes to a control byte. No command decodes to <see langword="null" />.
    /// </summary>
    private bool TryDecodeCustomCommand(out string? customCommand)
    {
        string? typed = context.Mail?.CustomCommand;
        customCommand = typed is null ? null : ImapUrlPath.Decode(typed);
        return typed is null || customCommand is not null;
    }

    /// <summary>
    /// The URL's query, percent-decoded, as curl searches with it: only for a mailbox with no
    /// <c>UID</c> or <c>MAILINDEX</c>, and none when it is empty or decodes to a control byte.
    /// </summary>
    private string? SearchQueryOf(ImapUrlPath path) =>
        path.Mailbox is not null && (path.Uid ?? path.MailIndex) is null && context.Url.Query is { Length: > 0 } query
            ? ImapUrlPath.Decode(query)
            : null;

    /// <summary>
    /// Sends <c>SELECT</c> for <paramref name="mailbox" />: other than <c>OK</c> is exit 67,
    /// a UIDVALIDITY other than the URL's is exit 78, each after <c>LOGOUT</c>; otherwise
    /// <paramref name="customCommand" /> is sent when there is one, else <c>SEARCH</c> with
    /// <paramref name="query" /> when there is one, else the message is fetched.
    /// </summary>
    private async ValueTask<TransferResult> SelectAsync(string mailbox, ImapUrlPath path, string? customCommand, string? query)
    {
        ImapResponse selected = await ExchangeAsync("SELECT " + ImapQuoting.AtomOrQuoted(mailbox), AnyUntagged).ConfigureAwait(false);
        if (selected.Status != ImapResponseStatus.Ok)
        {
            return await LogoutAndFailAsync(CurlExitCode.LoginDenied, ImapSessionMessages.SelectFailed).ConfigureAwait(false);
        }

        if (IsUidValidityChanged(selected, path))
        {
            return await LogoutAndFailAsync(CurlExitCode.RemoteFileNotFound, ImapSessionMessages.UidValidityChanged).ConfigureAwait(false);
        }

        return await AfterSelectAsync(path, customCommand, query).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends <paramref name="command" /> (a <c>LIST</c>, a <c>SEARCH</c> or the <c>-X</c>
    /// command) and writes each untagged response <paramref name="isWanted" /> accepts to the
    /// output, line end included, until the command completes: <c>OK</c> is a success and
    /// anything else exit 21, each after <c>LOGOUT</c>. A written response announcing a literal
    /// ends the listing: the literal's bytes follow it to the output, then <c>LOGOUT</c> with
    /// the rest of the response unread, as curl does.
    /// </summary>
    private async ValueTask<TransferResult> ListAsync(string command, Func<string, bool> isWanted)
    {
        await channel.SendCommandAsync(command).ConfigureAwait(false);
        long written = 0;
        while (true)
        {
            ImapResponse read = await channel.ReadUntaggedAsync(isWanted).ConfigureAwait(false);
            if (read.Status != ImapResponseStatus.Untagged)
            {
                return read.Status == ImapResponseStatus.Ok
                    ? await LogoutAndSucceedAsync(written).ConfigureAwait(false)
                    : await LogoutAndFailAsync(CurlExitCode.QuoteError, ImapSessionMessages.QuoteCommandFailed, written).ConfigureAwait(false);
            }

            long? literalSize = ReportedLiteralSizeOf(read.Untagged[0]);
            byte[] line = Encoding.Latin1.GetBytes(read.Untagged[0] + "\n");
            context.Events.ReportDataReceived(line);
            if (await WriteOutputAsync(line, written).ConfigureAwait(false) is { } failure)
            {
                return failure;
            }

            written += line.Length;
            if (literalSize is { } size)
            {
                return await CopyListedLiteralAsync(size, written).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// The size of the literal the listed <paramref name="response" /> announces, reported as
    /// curl's <c>Found N bytes to download</c>; <see langword="null" /> when it announces none.
    /// </summary>
    private long? ReportedLiteralSizeOf(string response)
    {
        long? size = ImapListedResponses.LiteralSizeOf(response);
        if (size is { } announced)
        {
            context.Events.ReportInfo(ImapInfoLines.Found(announced));
        }

        return size;
    }

    /// <summary>
    /// Copies the literal a listed response announced, <paramref name="written" /> bytes
    /// already written, then sends <c>LOGOUT</c> with the rest of the response unread.
    /// </summary>
    private async ValueTask<TransferResult> CopyListedLiteralAsync(long size, long written)
    {
        Phase = ImapSessionPhase.Transferring;
        return await CopyLiteralAsync(size, written, reportsProgress: false).ConfigureAwait(false)
            ?? await LogoutAndSucceedAsync(written + size).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends the <c>FETCH</c> and waits for its untagged <c>FETCH</c> response: a completion
    /// first is exit 78 and a response with no literal exit 8, each after <c>LOGOUT</c>;
    /// otherwise the literal is written to the output.
    /// </summary>
    private async ValueTask<TransferResult> FetchAsync(ImapUrlPath path)
    {
        await channel.SendCommandAsync(FetchCommandOf(path)).ConfigureAwait(false);
        ImapResponse read = await channel.ReadUntaggedAsync(IsFetchResponse).ConfigureAwait(false);
        if (read.Status != ImapResponseStatus.Untagged)
        {
            return await LogoutAndFailAsync(CurlExitCode.RemoteFileNotFound, ImapSessionMessages.RemoteFileNotFound).ConfigureAwait(false);
        }

        if (LiteralSizeOf(read.Untagged[0]) is not { } size)
        {
            return await LogoutAndFailAsync(CurlExitCode.WeirdServerReply, ImapSessionMessages.FetchResponseUnparsed).ConfigureAwait(false);
        }

        context.Events.ReportInfo(ImapInfoLines.Found(size));
        return await DownloadAsync(size).ConfigureAwait(false);
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
        Phase = ImapSessionPhase.Transferring;
        if (await CopyLiteralAsync(size, 0, reportsProgress: true).ConfigureAwait(false) is { } failure)
        {
            return failure;
        }

        Phase = ImapSessionPhase.Completing;
        ImapResponse completion = await channel.ReadResponseAsync(NoUntagged).ConfigureAwait(false)
            ?? throw new ImapResponseMissingException();
        if (completion.Status != ImapResponseStatus.Ok)
        {
            return await LogoutAndFailAsync(CurlExitCode.WeirdServerReply, ImapSessionMessages.WeirdServerReply, size).ConfigureAwait(false);
        }

        await LogoutAsync().ConfigureAwait(false);
        return TransferResult.Success(size);
    }

    /// <summary>
    /// Copies the literal's <paramref name="size" /> bytes from the connection to the output,
    /// <paramref name="writtenBefore" /> bytes already written. The server closing inside the
    /// literal is exit 18 and the output failing exit 23.
    /// </summary>
    /// <param name="size">The literal's size.</param>
    /// <param name="writtenBefore">The bytes written before the literal.</param>
    /// <param name="reportsProgress">
    /// Whether each piece written is reported, as curl reports a <c>FETCH</c> literal and not
    /// a listed one (BL-559): to the progress meter and, for the piece that arrived with the
    /// response line, as <see cref="ImapInfoLines.Written" />. Every piece, listed or not, is
    /// reported as data received before it is written, the empty piece of a server hanging up
    /// included.
    /// </param>
    /// <returns><see langword="null" /> once every byte is written, else the failure.</returns>
    private async ValueTask<TransferResult?> CopyLiteralAsync(long size, long writtenBefore, bool reportsProgress)
    {
        long copied = 0;
        while (copied < size)
        {
            bool arrivedWithResponse = channel.HasUnreadBytes;
            ReadOnlyMemory<byte> piece = await channel.ReadLiteralPieceAsync(size - copied).ConfigureAwait(false);
            context.Events.ReportDataReceived(piece.Span);
            if (piece.IsEmpty)
            {
                return TransferResult.Failure(CurlExitCode.PartialFile, ImapSessionMessages.LiteralCutShort(size - copied), writtenBefore + copied);
            }

            if (await WriteOutputAsync(piece, writtenBefore + copied).ConfigureAwait(false) is { } failure)
            {
                return failure;
            }

            copied += piece.Length;
            if (reportsProgress)
            {
                ReportFetched(copied, size, arrivedWithResponse);
            }
        }

        return null;
    }

    /// <summary>
    /// Reports <paramref name="copied" /> of the <c>FETCH</c> literal's <paramref name="size" />
    /// bytes written to the progress meter and, when the piece just written arrived with the
    /// response line, as curl's <c>Written N bytes, M bytes are left for transfer</c>.
    /// </summary>
    private void ReportFetched(long copied, long size, bool arrivedWithResponse)
    {
        context.Progress.ReportDownloaded(copied, size);
        if (arrivedWithResponse)
        {
            context.Events.ReportInfo(ImapInfoLines.Written(copied, size - copied));
        }
    }

    private async ValueTask<TransferResult> LogoutAndSucceedAsync(long bytesTransferred)
    {
        await LogoutAsync().ConfigureAwait(false);
        return TransferResult.Success(bytesTransferred);
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
        channel.StopReporting();
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
