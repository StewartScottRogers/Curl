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
/// prints from its response code. Otherwise exit 67 <c>Login denied</c>, nothing sent, after
/// the <c>-v</c> <c>SASL:</c> lines curl writes for it (BL-1060, BL-1219).</item>
/// </list>
/// </remarks>
internal sealed class ImapAuthentication(ImapControlChannel channel, ISaslAuthenticator? authenticator, ITransferContext context)
{
    private const string External = "EXTERNAL";

    private const string DefaultServiceName = "imap";

    private static readonly Func<string, bool> NoUntagged = static _ => false;

    private const string BearerOption = "CURLOPT_XOAUTH2_BEARER";

    /// <summary>
    /// The mechanisms curl 8.21.0's <c>Curl_sasl_is_blocked</c> explains, in the order its
    /// <c>-v</c> names them (BL-1219).
    /// </summary>
    private static readonly string[] UnchosenMechanisms =
        ["GSSAPI", "SCRAM-SHA-256", "SCRAM-SHA-1", "DIGEST-MD5", "CRAM-MD5", "NTLM", "OAUTHBEARER", "XOAUTH2"];

    /// <summary>
    /// The mechanisms <c>--oauth2-bearer</c> makes curl prefer when no <c>AUTH=</c> option
    /// names any (BL-1060's measured <c>no overlap</c> lines).
    /// </summary>
    private static readonly string[] BearerMechanisms = ["OAUTHBEARER", "XOAUTH2"];

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

        WarnOfNoUsableMechanism(offered);
        return MayLogIn(request, options, capabilities)
            ? await LoginAsync(request.Credential!).ConfigureAwait(false)
            : NoWayToLogIn(request, options, offered);
    }

    /// <summary>
    /// Writes the <c>-v</c> lines curl 8.21.0's <c>Curl_sasl_is_blocked</c> writes when no way
    /// of logging in is possible, and fails with exit 67 <c>Login denied</c> (BL-1060, BL-1219):
    /// <c>no auth mechanism was offered or recognized</c> when no mechanism curl knows was
    /// offered; <c>no overlap</c> when none offered is one curl prefers; otherwise that none
    /// could be selected, then why <c>EXTERNAL</c> and each other offered, preferred mechanism
    /// was not chosen.
    /// </summary>
    private TransferResult NoWayToLogIn(SaslRequest request, ImapLoginOptions options, IReadOnlyList<string> offered)
    {
        string[] known = [.. offered.Where(ImapLoginOptions.IsKnownMechanism)];
        string[] enabled = [.. known.Where(mechanism => IsPreferred(mechanism, request, options))];
        if (enabled.Length > 0)
        {
            context.Events.ReportInfo(ImapInfoLines.NoSaslMechanismSelectable);
            ReportUnchosen(request, enabled);
        }
        else
        {
            context.Events.ReportInfo(known.Length > 0 ? ImapInfoLines.NoSaslMechanismOverlap : ImapInfoLines.NoSaslMechanismOffered);
        }

        return TransferResult.Failure(CurlExitCode.LoginDenied, ImapSessionMessages.LoginDenied);
    }

    /// <summary>
    /// Whether curl prefers <paramref name="mechanism" />: as the <c>AUTH=</c> options say when
    /// given, else only the bearer mechanisms with <c>--oauth2-bearer</c>, else any but
    /// <c>EXTERNAL</c>.
    /// </summary>
    private static bool IsPreferred(string mechanism, SaslRequest request, ImapLoginOptions options) =>
        options.NamesMechanisms || request.BearerToken is null
            ? options.Prefers(mechanism)
            : BearerMechanisms.Contains(mechanism, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Writes curl's line for each enabled mechanism it did not choose, as its Schannel build
    /// writes them (<c>sasl_unchosen</c>): <c>EXTERNAL</c> with a password; SCRAM, DIGEST-MD5,
    /// CRAM-MD5 and NTLM <c>not builtin</c>, its build macros reading so; GSSAPI and the bearer
    /// mechanisms what they miss.
    /// </summary>
    private void ReportUnchosen(SaslRequest request, string[] enabled)
    {
        foreach (string line in UnchosenLines(request, enabled))
        {
            context.Events.ReportInfo(line);
        }
    }

    private static IEnumerable<string> UnchosenLines(SaslRequest request, string[] enabled)
    {
        bool hasUser = request.Credential is { UserName.Length: > 0 };
        bool hasBearerToken = request.BearerToken is not null;
        IEnumerable<string> external = IsExternalRefusedForPassword(request, enabled) ? [ImapInfoLines.SaslExternalNotChosenWithPassword] : [];
        return external.Concat(UnchosenMechanisms
            .Where(mechanism => enabled.Contains(mechanism, StringComparer.OrdinalIgnoreCase))
            .SelectMany(mechanism => UnchosenLines(mechanism, hasUser, hasBearerToken)));
    }

    /// <summary>Whether <c>EXTERNAL</c> was enabled and a password given, so curl did not choose it.</summary>
    private static bool IsExternalRefusedForPassword(SaslRequest request, string[] enabled) =>
        enabled.Contains(External, StringComparer.OrdinalIgnoreCase) && request.Credential is { Password.Length: > 0 };

    private static IEnumerable<string> UnchosenLines(string mechanism, bool hasUser, bool hasBearerToken) => mechanism switch
    {
        "GSSAPI" => MissingLines(mechanism, null, hasUser),
        "OAUTHBEARER" or "XOAUTH2" => MissingLines(mechanism, hasBearerToken ? null : BearerOption, hasUser),
        _ => [ImapInfoLines.SaslMechanismNotBuiltIn(mechanism)],
    };

    private static IEnumerable<string> MissingLines(string mechanism, string? missingParameter, bool hasUser)
    {
        if (missingParameter is not null)
        {
            yield return ImapInfoLines.SaslMechanismMissing(mechanism, missingParameter);
        }

        if (!hasUser)
        {
            yield return ImapInfoLines.SaslMechanismMissing(mechanism, "username");
        }
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
            context.Url.Host,
            context.Url.Port);
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
    /// Sends <c>AUTHENTICATE</c> and answers each <c>+</c> until the tagged completion.
    /// </summary>
    /// <remarks>
    /// When the server advertised <c>SASL-IR</c> or <c>--sasl-ir</c> was given, the initial
    /// response is made first and goes on the <c>AUTHENTICATE</c> line, curl setting IMAP no
    /// length limit; otherwise curl makes it at the first <c>+</c>. So a mechanism that cannot
    /// make one fails before <c>AUTHENTICATE</c> only in the first case (BL-856).
    /// </remarks>
    /// <returns>Whether the server accepted the exchange.</returns>
    private async ValueTask<bool> ExchangeAsync(ISaslExchange exchange, bool serverTakesInitialResponse)
    {
        bool initialResponseFirst = serverTakesInitialResponse || context.Mail is { SaslInitialResponse: true };
        bool messageSent = await SendAuthenticateAsync(exchange, initialResponseFirst).ConfigureAwait(false);
        bool initialResponseDue = !initialResponseFirst;
        while (true)
        {
            ImapResponse response = await channel.ReadResponseAsync(NoUntagged, acceptsContinuation: true).ConfigureAwait(false)
                ?? throw new ImapResponseMissingException();
            if (response.Status != ImapResponseStatus.Continuation)
            {
                return response.Status == ImapResponseStatus.Ok && messageSent;
            }

            byte[]? answer = await AnswerAsync(exchange, response, initialResponseDue).ConfigureAwait(false);
            initialResponseDue = false;
            if (answer is null)
            {
                return false;
            }

            await channel.SendLineAsync(Encode(answer), ImapDiagnosticLogLines.SaslResponseNotLogged).ConfigureAwait(false);
            messageSent = true;
        }
    }

    /// <summary>
    /// Sends <c>AUTHENTICATE</c>; with <paramref name="initialResponseFirst" />, makes the initial
    /// response first and puts it on the line when the mechanism has one.
    /// </summary>
    /// <returns>Whether the initial response was sent.</returns>
    private async ValueTask<bool> SendAuthenticateAsync(ISaslExchange exchange, bool initialResponseFirst)
    {
        byte[]? initialResponse = initialResponseFirst ? await exchange.GetInitialResponseAsync(context.CancellationToken).ConfigureAwait(false) : null;
        string command = "AUTHENTICATE " + exchange.Mechanism;
        await (initialResponse is null
            ? channel.SendCommandAsync(command)
            : channel.SendCommandAsync(command + " " + Encode(initialResponse), command + " " + ImapDiagnosticLogLines.SaslResponseNotLogged))
            .ConfigureAwait(false);
        return initialResponse is not null;
    }

    /// <summary>
    /// Answers a <c>+</c> with the initial response when it is due and the mechanism has one,
    /// and otherwise with the exchange's answer to the decoded challenge.
    /// </summary>
    /// <returns>The answer, or <see langword="null" /> when the exchange cannot answer.</returns>
    private async ValueTask<byte[]?> AnswerAsync(ISaslExchange exchange, ImapResponse continuation, bool initialResponseDue) =>
        (initialResponseDue ? await exchange.GetInitialResponseAsync(context.CancellationToken).ConfigureAwait(false) : null)
            ?? await exchange.RespondAsync(DecodeChallenge(continuation.Untagged[0]), context.CancellationToken).ConfigureAwait(false);

    /// <summary>Runs the exchange for <paramref name="mechanism" />; refused is exit 67 <c>Login denied</c>.</summary>
    private async ValueTask<TransferResult?> AuthenticateWithSaslAsync(string mechanism, SaslRequest request, bool serverTakesInitialResponse) =>
        await ExchangeAsync(authenticator!.Begin(mechanism, request), serverTakesInitialResponse).ConfigureAwait(false)
            ? LoggedIn("SASL " + mechanism)
            : TransferResult.Failure(CurlExitCode.LoginDenied, ImapSessionMessages.LoginDenied);

    /// <summary>Sends <c>LOGIN</c>; other than <c>OK</c> is exit 67 <c>Access denied. </c>.</summary>
    private async ValueTask<TransferResult?> LoginAsync(System.Net.NetworkCredential credential)
    {
        string user = "LOGIN " + ImapQuoting.AtomOrQuoted(credential.UserName) + " ";
        await channel.SendCommandAsync(user + ImapQuoting.AtomOrQuoted(credential.Password), user + ImapDiagnosticLogLines.PasswordNotLogged)
            .ConfigureAwait(false);
        ImapResponse response = await channel.ReadResponseAsync(NoUntagged).ConfigureAwait(false)
            ?? throw new ImapResponseMissingException();
        return response.Status == ImapResponseStatus.Ok
            ? LoggedIn("LOGIN")
            : TransferResult.Failure(CurlExitCode.LoginDenied, ImapSessionMessages.AccessDenied(response.Status));
    }

    /// <summary>
    /// Logs a warning when the server offered SASL mechanisms and the authenticator could use
    /// none of them.
    /// </summary>
    private void WarnOfNoUsableMechanism(IReadOnlyList<string> offered)
    {
        if (authenticator is not null && offered.Count > 0)
        {
            ImapDiagnosticLogLines.NoUsableMechanism(context.DiagnosticLog, offered);
        }
    }

    /// <summary>Logs the login that succeeded by <paramref name="method" />.</summary>
    /// <returns><see langword="null" />, to carry on.</returns>
    private TransferResult? LoggedIn(string method)
    {
        ImapDiagnosticLogLines.LoggedIn(context.DiagnosticLog, method);
        return null;
    }
}
