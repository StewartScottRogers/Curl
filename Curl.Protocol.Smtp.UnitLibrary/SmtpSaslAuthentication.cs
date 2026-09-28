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

        IsAuthenticated = authenticator.ChooseMechanism(request, offered) is { } mechanism
            && await ExchangeAsync(authenticator.Begin(mechanism, request)).ConfigureAwait(false);
        return IsAuthenticated ? null : TransferResult.Failure(CurlExitCode.LoginDenied, SmtpSessionMessages.LoginDenied);
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
    /// Decodes a <c>334</c> challenge; one that is not base64 is handed over empty, since no
    /// mechanism built yet reads its challenge (ADR-0133).
    /// </summary>
    private static byte[] DecodeChallenge(SmtpReply reply)
    {
        string line = reply.Lines[^1];
        string text = line.Length > 4 ? line[4..] : string.Empty;
        var buffer = new byte[text.Length];
        return Convert.TryFromBase64String(text, buffer, out int written) ? buffer[..written] : [];
    }

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
    private string? InlineInitialResponse(ISaslExchange exchange)
    {
        if (exchange.InitialResponse is not { } initialResponse || context.Mail is not { SaslInitialResponse: true })
        {
            return null;
        }

        string encoded = Encode(initialResponse);
        return exchange.Mechanism.Length + encoded.Length <= MaxInitialResponseLength ? encoded : null;
    }

    /// <summary>
    /// Sends <c>AUTH</c> and answers each <c>334</c> until the server says <c>235</c>.
    /// </summary>
    /// <returns>Whether the server accepted the exchange.</returns>
    private async ValueTask<bool> ExchangeAsync(ISaslExchange exchange)
    {
        string? inline = InlineInitialResponse(exchange);
        byte[]? pending = inline is null ? exchange.InitialResponse : null;
        bool messageSent = inline is not null;
        await channel.SendAsync(AuthKeyword + exchange.Mechanism + (inline is null ? string.Empty : " " + inline)).ConfigureAwait(false);
        while (true)
        {
            SmtpReply reply = await channel.ReadReplyAsync().ConfigureAwait(false) ?? throw new SmtpReplyMissingException();
            if (reply.Code != Continuation)
            {
                return reply.Code == Authenticated && messageSent;
            }

            byte[]? response = pending ?? exchange.Respond(DecodeChallenge(reply));
            pending = null;
            if (response is null)
            {
                return false;
            }

            await channel.SendAsync(Encode(response)).ConfigureAwait(false);
            messageSent = true;
        }
    }
}
