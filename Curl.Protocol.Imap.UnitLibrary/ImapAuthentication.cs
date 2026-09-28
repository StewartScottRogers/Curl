using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Imap;

/// <summary>
/// Logs an open IMAP session in with <c>AUTHENTICATE</c> through the injected
/// <see cref="ISaslAuthenticator" /> or with <c>LOGIN</c>, as curl 8.21.0 does, each rule
/// measured with <c>Record-CurlExchange.ps1 -Imap</c> (BL-554), with the framing rules ADR-0133 set for SMTP.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Nothing to authenticate with - no <c>-u</c>, no <c>--oauth2-bearer</c>, and not
/// <c>AUTH=EXTERNAL</c> with <c>EXTERNAL</c> offered: no login.</item>
/// <item>The offered mechanisms are the <c>AUTH=&lt;mech&gt;</c> words, in any case, of the
/// last <c>CAPABILITY</c>; the authenticator chooses among those
/// <see cref="ImapLoginOptions" /> allows. A choice sends <c>AUTHENTICATE &lt;mech&gt;</c>,
/// with the initial response in base64 after it (<c>=</c> when empty) when the server
/// advertised <c>SASL-IR</c> or <c>--sasl-ir</c> was given, else in answer to the first
/// <c>+</c>. Each <c>+</c> is answered in base64; the tagged <c>OK</c> once a message has been
/// sent is success. Anything else, or a challenge the exchange cannot answer, is exit 67
/// <c>Login denied</c> with nothing more sent.</item>
/// <item>No choice: with a user, <c>LOGINDISABLED</c> not advertised and the options
/// allowing it, <c>LOGIN user password</c>, each as <see cref="ImapQuoting" /> writes it;
/// answered other than <c>OK</c> it is exit 67 <c>Access denied. </c> and the byte curl
/// prints from its response code. Otherwise exit 67 <c>Login denied</c>, nothing sent.</item>
/// </list>
/// </remarks>
internal sealed class ImapAuthentication(ImapControlChannel channel, ISaslAuthenticator? authenticator, ITransferContext context)
{
    private const string External = "EXTERNAL";

    private const string DefaultServiceName = "imap";

    private static readonly Func<string, bool> NoUntagged = static _ => false;

    /// <summary>
    /// Logs in when the transfer has something to authenticate with.
    /// </summary>
    /// <param name="options">What the login options allow.</param>
    /// <param name="offered">The mechanisms the last <c>CAPABILITY</c> advertised.</param>
    /// <param name="capabilities">Every capability the server advertised, compared in any case.</param>
    /// <returns><see langword="null" /> to carry on, or exit 67's failure.</returns>
    /// <exception cref="ImapResponseMissingException">The server closed before a response was complete.</exception>
    public async ValueTask<TransferResult?> AuthenticateAsync(ImapLoginOptions options, IReadOnlyList<string> offered, IReadOnlySet<string> capabilities)
    {
        SaslRequest request = CreateRequest();
        if (!CanAuthenticate(request, options, offered))
        {
            return null;
        }

        if (ChooseMechanism(request, options, offered) is { } chosen)
        {
            return await AuthenticateWithSaslAsync(chosen.Mechanism, chosen.Request, capabilities.Contains("SASL-IR")).ConfigureAwait(false);
        }

        return MayLogIn(request, options, capabilities)
            ? await LoginAsync(request.Credential!).ConfigureAwait(false)
            : TransferResult.Failure(CurlExitCode.LoginDenied, ImapSessionMessages.LoginDenied);
    }

    private static bool CanAuthenticate(SaslRequest request, ImapLoginOptions options, IReadOnlyList<string> offered) =>
        request.Credential is not null
        || request.BearerToken is not null
        || (options.NamedMechanisms.Contains(External) && offered.Contains(External, StringComparer.OrdinalIgnoreCase));

    /// <summary>
    /// Whether <c>LOGIN</c> may be sent: there is a user, the options allow it and the server
    /// did not advertise <c>LOGINDISABLED</c>.
    /// </summary>
    private static bool MayLogIn(SaslRequest request, ImapLoginOptions options, IReadOnlySet<string> capabilities) =>
        request.Credential is not null && options.AllowsLogin && !capabilities.Contains("LOGINDISABLED");

    private static string Encode(byte[] message) => message.Length == 0 ? "=" : Convert.ToBase64String(message);

    /// <summary>
    /// Decodes a <c>+</c> continuation's text, blanks and line end trimmed; one that is not
    /// base64 is handed over empty, as <c>SmtpSaslAuthentication</c> does (ADR-0133).
    /// </summary>
    private static byte[] DecodeChallenge(string continuation)
    {
        string text = continuation[1..].Trim(' ', '\t', '\r');
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
            RequiredMechanism: null,
            mail.ServiceName ?? DefaultServiceName,
            context.Url.Host);
    }

    /// <summary>
    /// Asks the authenticator for a mechanism among those offered that the options allow:
    /// <c>EXTERNAL</c> first when the options name it, as curl prefers it, then any other.
    /// </summary>
    private (string Mechanism, SaslRequest Request)? ChooseMechanism(SaslRequest request, ImapLoginOptions options, IReadOnlyList<string> offered)
    {
        if (authenticator is null)
        {
            return null;
        }

        List<string> allowed = options.AllowedAmong(offered);
        SaslRequest external = request with { RequiredMechanism = External };
        if (options.NamedMechanisms.Contains(External) && authenticator.ChooseMechanism(external, allowed) is { } externalMechanism)
        {
            return (externalMechanism, external);
        }

        return authenticator.ChooseMechanism(request, allowed) is { } mechanism ? (mechanism, request) : null;
    }

    /// <summary>
    /// Encodes the initial response for the <c>AUTHENTICATE</c> line, when there is one and
    /// the server advertised <c>SASL-IR</c> or <c>--sasl-ir</c> was given; curl sets IMAP no
    /// length limit.
    /// </summary>
    /// <returns>The encoded response, or <see langword="null" /> to send it after the first <c>+</c>.</returns>
    private string? InlineInitialResponse(ISaslExchange exchange, bool serverTakesInitialResponse) =>
        exchange.InitialResponse is { } initialResponse
            && (serverTakesInitialResponse || context.Mail is { SaslInitialResponse: true })
            ? Encode(initialResponse)
            : null;

    /// <summary>
    /// Sends <c>AUTHENTICATE</c> and answers each <c>+</c> until the tagged completion.
    /// </summary>
    /// <returns>Whether the server accepted the exchange.</returns>
    private async ValueTask<bool> ExchangeAsync(ISaslExchange exchange, bool serverTakesInitialResponse)
    {
        string? inline = InlineInitialResponse(exchange, serverTakesInitialResponse);
        byte[]? pending = inline is null ? exchange.InitialResponse : null;
        bool messageSent = inline is not null;
        await channel.SendCommandAsync("AUTHENTICATE " + exchange.Mechanism + (inline is null ? string.Empty : " " + inline))
            .ConfigureAwait(false);
        while (true)
        {
            ImapResponse response = await channel.ReadResponseAsync(NoUntagged, acceptsContinuation: true).ConfigureAwait(false)
                ?? throw new ImapResponseMissingException();
            if (response.Status != ImapResponseStatus.Continuation)
            {
                return response.Status == ImapResponseStatus.Ok && messageSent;
            }

            byte[]? answer = pending ?? exchange.Respond(DecodeChallenge(response.Untagged[0]));
            pending = null;
            if (answer is null)
            {
                return false;
            }

            await channel.SendLineAsync(Encode(answer)).ConfigureAwait(false);
            messageSent = true;
        }
    }

    /// <summary>Runs the exchange for <paramref name="mechanism" />; refused is exit 67 <c>Login denied</c>.</summary>
    private async ValueTask<TransferResult?> AuthenticateWithSaslAsync(string mechanism, SaslRequest request, bool serverTakesInitialResponse) =>
        await ExchangeAsync(authenticator!.Begin(mechanism, request), serverTakesInitialResponse).ConfigureAwait(false)
            ? null
            : TransferResult.Failure(CurlExitCode.LoginDenied, ImapSessionMessages.LoginDenied);

    /// <summary>Sends <c>LOGIN</c>; other than <c>OK</c> is exit 67 <c>Access denied. </c>.</summary>
    private async ValueTask<TransferResult?> LoginAsync(System.Net.NetworkCredential credential)
    {
        await channel.SendCommandAsync("LOGIN " + ImapQuoting.AtomOrQuoted(credential.UserName) + " " + ImapQuoting.AtomOrQuoted(credential.Password))
            .ConfigureAwait(false);
        ImapResponse response = await channel.ReadResponseAsync(NoUntagged).ConfigureAwait(false)
            ?? throw new ImapResponseMissingException();
        return response.Status == ImapResponseStatus.Ok
            ? null
            : TransferResult.Failure(CurlExitCode.LoginDenied, ImapSessionMessages.AccessDenied(response.Status));
    }
}
