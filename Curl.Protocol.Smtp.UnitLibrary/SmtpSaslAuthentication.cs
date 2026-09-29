using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Smtp;

/// <summary>
/// Authenticates an open SMTP session with <c>AUTH</c> through the injected
/// <see cref="ISaslAuthenticator" />, as curl 8.21.0 does, each rule measured with
/// <c>Record-CurlExchange.ps1 -Smtp</c> (BL-541, ADR-0133).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>The offered mechanisms are the words, split on spaces and tabs, after <c>AUTH </c>
/// (in any case) on every line of the <c>EHLO</c> reply. No such line: no authentication.</item>
/// <item>Nothing to authenticate with - no <c>-u</c>, no <c>--oauth2-bearer</c>, and not
/// <c>AUTH=EXTERNAL</c> with <c>EXTERNAL</c> offered: no authentication.</item>
/// <item>No usable mechanism among those offered: exit 67 <c>Login denied</c>, nothing sent.</item>
/// <item>Under <c>--sasl-ir</c> the initial response goes on the <c>AUTH</c> line while the
/// mechanism's name and the base64 fit in 504 characters; otherwise it answers the first
/// <c>334</c>. An empty message is sent as <c>=</c>.</item>
/// <item>Each <c>334</c> is answered; <c>235</c> once a message has been sent is success.
/// Anything else, <c>235</c> before any message, or a challenge the exchange cannot answer
/// is exit 67 <c>Login denied</c> with nothing more sent.</item>
/// <item>A <c>334</c> whose text is not base64 (empty text or text starting <c>=</c> is an
/// empty challenge) is handed over empty to a mechanism that ignores it. A mechanism that
/// reads it - every GSSAPI challenge, the first one a CRAM-MD5, DIGEST-MD5 or NTLM exchange
/// answers - is cancelled with <c>*</c>: the reply is read whatever it is, the mechanism is
/// dropped from the offered ones and the authenticator chooses again. None left is exit 67
/// <c>Authentication cancelled</c> (BL-774).</item>
/// </list>
/// </remarks>
internal sealed class SmtpSaslAuthentication(SmtpControlChannel channel, ISaslAuthenticator authenticator, ITransferContext context)
{
    private const string AuthKeyword = "AUTH ";

    private const string AuthOption = "AUTH=";

    private const string External = "EXTERNAL";

    private const string DefaultServiceName = "smtp";

    private const int Continuation = 334;

    private const int Authenticated = 235;

    /// <summary>curl's longest <c>AUTH</c> argument with an initial response: 512 less <c>AUTH </c>, a space and CRLF.</summary>
    private const int MaxInitialResponseLength = 504;

    private static readonly char[] MechanismSeparators = [' ', '\t'];

    /// <summary>How many challenges the current exchange has been handed.</summary>
    private int challengesHanded;

    /// <summary>Whether the current exchange has been asked for its initial response.</summary>
    private bool initialResponseAsked;

    /// <summary>
    /// Gets a value indicating whether <see cref="AuthenticateAsync" /> ran an <c>AUTH</c>
    /// exchange the server accepted, which is when curl adds <c>--mail-auth</c>'s
    /// <c>AUTH=</c> to <c>MAIL FROM</c> (BL-544).
    /// </summary>
    public bool IsAuthenticated { get; private set; }

    /// <summary>
    /// Authenticates when the <c>EHLO</c> reply offers <c>AUTH</c> and the transfer has
    /// something to authenticate with.
    /// </summary>
    /// <param name="ehlo">The reply to the session's last <c>EHLO</c>.</param>
    /// <returns><see langword="null" /> to carry on, or exit 67's failure.</returns>
    /// <exception cref="SmtpReplyMissingException">The server closed before a reply was complete.</exception>
    public async ValueTask<TransferResult?> AuthenticateAsync(SmtpReply ehlo)
    {
        if (OfferedMechanisms(ehlo) is not { } offered)
        {
            return null;
        }

        SaslRequest request = CreateRequest();
        if (!CanAuthenticate(request, offered))
        {
            return null;
        }

        return await TryMechanismsAsync(request, offered).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads the mechanism named by <c>AUTH=</c> in the login options: <c>--login-options</c>
    /// when given, otherwise the URL's <c>;</c> options.
    /// </summary>
    /// <param name="loginOptions">The login options, or <see langword="null" />.</param>
    /// <returns>The first <c>AUTH=</c> value, or <see langword="null" /> when there is none.</returns>
    internal static string? RequiredMechanism(string? loginOptions) =>
        loginOptions?.Split(';')
            .FirstOrDefault(option => option.StartsWith(AuthOption, StringComparison.OrdinalIgnoreCase))?[AuthOption.Length..];

    /// <summary>
    /// Collects the mechanisms every <c>AUTH</c> line of <paramref name="ehlo" /> offers.
    /// </summary>
    /// <returns>The mechanisms, empty for a bare <c>AUTH </c>; <see langword="null" /> without an <c>AUTH</c> line.</returns>
    internal static List<string>? OfferedMechanisms(SmtpReply ehlo)
    {
        List<string>? offered = null;
        foreach (string line in ehlo.Lines)
        {
            if (line.Length >= 4 + AuthKeyword.Length
                && line.AsSpan(4, AuthKeyword.Length).Equals(AuthKeyword, StringComparison.OrdinalIgnoreCase))
            {
                offered ??= [];
                offered.AddRange(line[(4 + AuthKeyword.Length)..].Split(MechanismSeparators, StringSplitOptions.RemoveEmptyEntries));
            }
        }

        return offered;
    }

    private static bool CanAuthenticate(SaslRequest request, List<string> offered) =>
        request.Credential is not null
        || request.BearerToken is not null
        || (External.Equals(request.RequiredMechanism, StringComparison.OrdinalIgnoreCase)
            && offered.Contains(External, StringComparer.OrdinalIgnoreCase));

    private static string Encode(byte[] message) => message.Length == 0 ? "=" : Convert.ToBase64String(message);

    /// <summary>
    /// Decodes a <c>334</c> challenge. One that is not base64 is empty when its text starts
    /// with <c>=</c> or <paramref name="mechanism" /> ignores it (ADR-0133), and cancels the
    /// exchange otherwise (BL-774).
    /// </summary>
    /// <param name="reply">The <c>334</c> reply.</param>
    /// <param name="mechanism">The exchange's mechanism.</param>
    /// <param name="index">How many challenges the exchange has been handed before this one.</param>
    /// <returns>The challenge, or <see langword="null" /> to cancel with <c>*</c>.</returns>
    private static byte[]? DecodeChallenge(SmtpReply reply, string mechanism, int index)
    {
        string line = reply.Lines[^1];
        string text = line.Length > 4 ? line[4..] : string.Empty;
        var buffer = new byte[text.Length];
        if (Convert.TryFromBase64String(text, buffer, out int written))
        {
            return buffer[..written];
        }

        return text.StartsWith('=') || !ReadsChallenge(mechanism, index) ? [] : null;
    }

    /// <summary>
    /// Whether curl decodes challenge <paramref name="index" /> of <paramref name="mechanism" />
    /// (<c>get_server_message</c> in <c>lib/sasl.c</c>): every GSSAPI challenge, and the first
    /// one a CRAM-MD5, DIGEST-MD5 or NTLM exchange answers (NTLM's Type 2).
    /// </summary>
    private static bool ReadsChallenge(string mechanism, int index) =>
        mechanism.ToUpperInvariant() switch
        {
            "GSSAPI" => true,
            "CRAM-MD5" or "DIGEST-MD5" or "NTLM" => index == 0,
            _ => false,
        };

    private SaslRequest CreateRequest()
    {
        MailRequestOptions mail = context.Mail ?? new MailRequestOptions();
        return new SaslRequest(
            context.Credentials,
            mail.SaslAuthorizationIdentity,
            mail.BearerToken,
            RequiredMechanism(mail.LoginOptions ?? context.Url.Options),
            mail.ServiceName ?? DefaultServiceName,
            context.Url.Host);
    }

    /// <summary>
    /// Encodes the initial response for the <c>AUTH</c> line: only under <c>--sasl-ir</c>, and
    /// only while the mechanism's name and the base64 fit in 504 characters.
    /// </summary>
    /// <returns>The encoded response, or <see langword="null" /> to send it after the first <c>334</c>.</returns>
    private static string? InlineInitialResponse(ISaslExchange exchange, byte[]? initialResponse)
    {
        if (initialResponse is null)
        {
            return null;
        }

        string encoded = Encode(initialResponse);
        return exchange.Mechanism.Length + encoded.Length <= MaxInitialResponseLength ? encoded : null;
    }

    /// <summary>
    /// Sends <c>AUTH</c> and answers each <c>334</c> until the server says <c>235</c>.
    /// </summary>
    /// <returns>Whether the server accepted or refused the exchange, or it was cancelled with <c>*</c>.</returns>
    private async ValueTask<ExchangeOutcome> ExchangeAsync(ISaslExchange exchange)
    {
        initialResponseAsked = false;
        (byte[]? pending, bool messageSent) = await SendAuthAsync(exchange).ConfigureAwait(false);
        challengesHanded = 0;
        while (true)
        {
            SmtpReply reply = await ReadReplyAsync().ConfigureAwait(false);
            if (reply.Code != Continuation)
            {
                return Outcome(reply, messageSent);
            }

            if (await AnswerAsync(exchange, reply, pending).ConfigureAwait(false) is { } end)
            {
                return end;
            }

            pending = null;
            messageSent = true;
        }
    }

    /// <summary>
    /// Tries the mechanism the authenticator chooses, and after each one cancelled the next,
    /// until an exchange is accepted or refused or none is left.
    /// </summary>
    /// <returns><see langword="null" /> to carry on, or exit 67's failure.</returns>
    private async ValueTask<TransferResult?> TryMechanismsAsync(SaslRequest request, List<string> offered)
    {
        string denial = SmtpSessionMessages.LoginDenied;
        while (authenticator.ChooseMechanism(request, offered) is { } mechanism)
        {
            ExchangeOutcome outcome = await ExchangeAsync(authenticator.Begin(mechanism, request)).ConfigureAwait(false);
            if (outcome != ExchangeOutcome.Cancelled)
            {
                return Finish(outcome);
            }

            denial = SmtpSessionMessages.AuthenticationCancelled;
            if (offered.RemoveAll(offer => offer.Equals(mechanism, StringComparison.OrdinalIgnoreCase)) == 0)
            {
                break;
            }
        }

        return TransferResult.Failure(CurlExitCode.LoginDenied, denial);
    }

    /// <summary>Records whether the exchange was accepted, and fails a refused one with <c>Login denied</c>.</summary>
    private TransferResult? Finish(ExchangeOutcome outcome)
    {
        IsAuthenticated = outcome == ExchangeOutcome.Accepted;
        return IsAuthenticated ? null : TransferResult.Failure(CurlExitCode.LoginDenied, SmtpSessionMessages.LoginDenied);
    }

    private static ExchangeOutcome Outcome(SmtpReply reply, bool messageSent) =>
        reply.Code == Authenticated && messageSent ? ExchangeOutcome.Accepted : ExchangeOutcome.Refused;

    /// <summary>
    /// Sends <c>AUTH</c>, with the initial response on the line when it goes there. curl makes
    /// the initial response before <c>AUTH</c> only under <c>--sasl-ir</c>, and otherwise at
    /// the first <c>334</c>, so a mechanism that cannot make one fails before <c>AUTH</c> only
    /// under <c>--sasl-ir</c> (BL-856).
    /// </summary>
    /// <returns>The initial response still to send after the first <c>334</c>, and whether a message was sent.</returns>
    private async ValueTask<(byte[]? Pending, bool MessageSent)> SendAuthAsync(ISaslExchange exchange)
    {
        byte[]? initialResponse = context.Mail is { SaslInitialResponse: true } ? await TakeInitialResponseAsync(exchange).ConfigureAwait(false) : null;
        string? inline = InlineInitialResponse(exchange, initialResponse);
        await channel.SendAsync(AuthKeyword + exchange.Mechanism + (inline is null ? string.Empty : " " + inline)).ConfigureAwait(false);
        return inline is null ? (initialResponse, false) : (null, true);
    }

    /// <summary>
    /// Answers a <c>334</c> with the pending initial response, or with the exchange's answer to
    /// the decoded challenge.
    /// </summary>
    /// <returns><see langword="null" /> once the answer is sent, or how the exchange ended.</returns>
    private async ValueTask<ExchangeOutcome?> AnswerAsync(ISaslExchange exchange, SmtpReply reply, byte[]? pending)
    {
        byte[]? response = pending ?? await TakeInitialResponseAsync(exchange).ConfigureAwait(false);
        if (response is null)
        {
            if (DecodeChallenge(reply, exchange.Mechanism, challengesHanded++) is not { } challenge)
            {
                return await CancelAsync().ConfigureAwait(false);
            }

            response = await exchange.RespondAsync(challenge, context.CancellationToken).ConfigureAwait(false);
        }

        if (response is null)
        {
            return ExchangeOutcome.Refused;
        }

        await channel.SendAsync(Encode(response)).ConfigureAwait(false);
        return null;
    }

    /// <summary>Asks the exchange for its initial response the first time only.</summary>
    /// <returns>The initial response; <see langword="null" /> when it has none or was already asked.</returns>
    private async ValueTask<byte[]?> TakeInitialResponseAsync(ISaslExchange exchange)
    {
        if (initialResponseAsked)
        {
            return null;
        }

        initialResponseAsked = true;
        return await exchange.GetInitialResponseAsync(context.CancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Cancels the exchange with <c>*</c> and reads the server's reply, whatever it is, as
    /// curl's <c>SASL_CANCEL</c> state does.
    /// </summary>
    private async ValueTask<ExchangeOutcome> CancelAsync()
    {
        await channel.SendAsync("*").ConfigureAwait(false);
        await ReadReplyAsync().ConfigureAwait(false);
        return ExchangeOutcome.Cancelled;
    }

    private async ValueTask<SmtpReply> ReadReplyAsync() =>
        await channel.ReadReplyAsync().ConfigureAwait(false) ?? throw new SmtpReplyMissingException();

    /// <summary>How one mechanism's exchange ended.</summary>
    private enum ExchangeOutcome
    {
        /// <summary>The server said <c>235</c> after a message was sent.</summary>
        Accepted,

        /// <summary>The server refused, or the exchange could not answer a challenge.</summary>
        Refused,

        /// <summary>The exchange was cancelled with <c>*</c> over a challenge that was not base64.</summary>
        Cancelled,
    }
}
