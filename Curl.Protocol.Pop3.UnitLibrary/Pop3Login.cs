using System.Globalization;
using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Pop3;

/// <summary>
/// Logs a POP3 session in as curl 8.21.0 does, each step measured with
/// <c>Record-CurlExchange.ps1 -Pop3</c> (BL-548).
/// </summary>
/// <param name="channel">The session's control channel.</param>
/// <param name="saslAuthenticator">
/// Chooses and runs the SASL mechanism, or <see langword="null" /> to log in only with
/// <c>APOP</c> or <c>USER</c>/<c>PASS</c> (ADR-0121).
/// </param>
/// <param name="context">The transfer, for its credentials, mail options and host.</param>
/// <remarks>
/// <list type="bullet">
/// <item>Without credentials and without a bearer token, nothing is sent. With a bearer
/// token alone, only SASL is tried; no mechanism chosen is exit 67 <c>Login denied</c>.</item>
/// <item>SASL <c>AUTH</c> first, when <c>CAPA</c> lists <c>SASL</c> mechanisms and the
/// authenticator chooses one. Under <c>--sasl-ir</c> the initial response goes on the
/// command line (<c>=</c> when empty) unless the mechanism's name and the base64 are longer
/// than 247 characters together; otherwise it answers the first <c>+</c> continuation. Every
/// later continuation is answered by the exchange; a continuation it cannot answer, a
/// <c>-ERR</c>, or <c>+OK</c> before the initial response was sent is exit 67
/// <c>Login denied</c> with nothing more sent. No fallback follows a failed exchange.</item>
/// <item>A continuation whose text is not base64 (empty text or text starting <c>=</c> is an
/// empty challenge) is handed over empty to a mechanism that ignores it. A mechanism that
/// reads it - every GSSAPI challenge, the first one a CRAM-MD5, DIGEST-MD5 or NTLM exchange
/// answers - is cancelled with <c>*</c>: the response is read whatever it is, the mechanism
/// is dropped and the authenticator chooses again. None left: <c>APOP</c> or
/// <c>USER</c>/<c>PASS</c> as below when possible, else exit 67 <c>Authentication
/// cancelled</c> (BL-1222).</item>
/// <item>No SASL mechanism chosen: <c>APOP &lt;user&gt; &lt;digest&gt;</c>
/// (<see cref="Pop3ApopDigest" />) when the greeting carried a timestamp, whatever
/// <c>CAPA</c> said; refused, exit 67 <c>Authentication failed: &lt;n&gt;</c>, n being 45
/// for <c>-ERR</c> and 42 for another <c>+</c> line.</item>
/// <item>No timestamp: <c>USER</c> then <c>PASS</c> when <c>CAPA</c> listed <c>USER</c> or
/// was refused; either refused, exit 67 <c>Access denied. &lt;c&gt;</c>, c being <c>-</c>
/// or <c>*</c> as above.</item>
/// <item>None of these possible, or <c>AUTH=</c> ruling out the one that is: exit 67
/// <c>Login denied</c>, after the <c>-v</c> line <c>SASL: no auth mechanism was offered or
/// recognized</c> when <c>CAPA</c> listed no SASL mechanism curl knows, <c>SASL: no overlap
/// between offered and configured auth mechanisms</c> when it listed none the credentials and
/// options allow, and otherwise <c>SASL: no auth mechanism offered could be selected</c>
/// followed by curl's reason for each allowed one, such as <c>SASL: SCRAM-SHA-1 not
/// builtin</c> or <c>SASL: XOAUTH2 is missing CURLOPT_XOAUTH2_BEARER</c> (BL-810,
/// BL-1221).</item>
/// </list>
/// A login failure sends no <c>QUIT</c>.
/// </remarks>
internal sealed class Pop3Login(Pop3ControlChannel channel, ISaslAuthenticator? saslAuthenticator, ITransferContext context)
{
    /// <summary>The service name SASL uses for POP3 unless <c>--service-name</c> overrides it.</summary>
    private const string DefaultServiceName = "pop";

    /// <summary>
    /// The longest mechanism name and base64 initial response, together, that curl 8.21.0
    /// sends on the <c>AUTH</c> line (255 - 8; 245 measured inline, 249 not).
    /// </summary>
    private const int MaxInitialResponseLength = 247;

    /// <summary>What curl sends for an empty SASL message (RFC 5034 section 4).</summary>
    private const string EmptyMessage = "=";

    /// <summary>The SASL mechanism curl enables only when <c>AUTH=EXTERNAL</c> names it.</summary>
    private const string ExternalMechanism = "EXTERNAL";

    /// <summary>
    /// The mechanisms curl 8.21.0's <c>Curl_sasl_is_blocked</c> explains when none was chosen,
    /// in the order its <c>-v</c> names them (BL-1221).
    /// </summary>
    private static readonly string[] UnchosenMechanisms =
        ["GSSAPI", "SCRAM-SHA-256", "SCRAM-SHA-1", "DIGEST-MD5", "CRAM-MD5", "NTLM", "OAUTHBEARER", "XOAUTH2"];

    /// <summary>
    /// The mechanisms curl 8.21.0's Schannel build calls <c>not builtin</c>: SCRAM, which it
    /// lacks (measured, BL-810), and DIGEST-MD5, CRAM-MD5 and NTLM, which its
    /// <c>Curl_sasl_is_blocked</c> reports so although it would have chosen them (read from
    /// <c>lib/curl_sasl.c</c>, BL-1221).
    /// </summary>
    private static readonly string[] NotBuiltInMechanisms = ["SCRAM-SHA-256", "SCRAM-SHA-1", "DIGEST-MD5", "CRAM-MD5", "NTLM"];

    /// <summary>The mechanisms that need <c>--oauth2-bearer</c>.</summary>
    private static readonly string[] BearerMechanisms = ["OAUTHBEARER", "XOAUTH2"];

    /// <summary>What curl sends to cancel a SASL exchange (RFC 5034 section 4).</summary>
    private const string CancelLine = "*";

    private readonly MailRequestOptions mail = context.Mail ?? new MailRequestOptions();

    /// <summary>How many challenges the current exchange has been handed.</summary>
    private int challengesHanded;

    /// <summary>
    /// Logs in.
    /// </summary>
    /// <param name="options">The ways the login options allow.</param>
    /// <param name="capabilities">The answer to the last <c>CAPA</c>, or <see langword="null" /> when it was refused.</param>
    /// <param name="apopTimestamp">The greeting's APOP timestamp, or <see langword="null" />.</param>
    /// <returns><see langword="null" /> once logged in or when there is nothing to log in with, else the failure.</returns>
    /// <exception cref="Pop3ReplyMissingException">The server closed the connection first.</exception>
    /// <exception cref="InvalidDataException">A response line reached 65536 bytes.</exception>
    public async ValueTask<TransferResult?> LogInAsync(Pop3LoginOptions options, Pop3Capabilities? capabilities, string? apopTimestamp)
    {
        if (context.Credentials is null && mail.BearerToken is null)
        {
            return null;
        }

        (SaslOutcome outcome, TransferResult? result) = options.Method == Pop3LoginMethod.Apop
            ? (SaslOutcome.NoneChosen, null)
            : await TrySaslAsync(options, capabilities).ConfigureAwait(false);
        return outcome == SaslOutcome.Finished
            ? result
            : await LogInWithoutSaslAsync(options, capabilities, apopTimestamp, outcome == SaslOutcome.Cancelled).ConfigureAwait(false);
    }

    private static TransferResult LoginDenied() =>
        TransferResult.Failure(CurlExitCode.LoginDenied, Pop3SessionMessages.LoginDenied);

    private static TransferResult AuthenticationCancelled() =>
        TransferResult.Failure(CurlExitCode.LoginDenied, Pop3SessionMessages.AuthenticationCancelled);

    /// <summary>
    /// The character curl 8.21.0 reports a refusal by: <c>-</c> for <c>-ERR</c>, <c>*</c> for
    /// any other <c>+</c> line, which it takes as a continuation.
    /// </summary>
    private static char RefusalCode(Pop3Response response) => response.Line[0] == '-' ? '-' : '*';

    private static string Encode(byte[] message) =>
        message.Length == 0 ? EmptyMessage : Convert.ToBase64String(message);

    /// <summary>
    /// The challenge a continuation carries: its text after the <c>+</c> and any spaces,
    /// decoded from base64. Text that is not base64 is an empty challenge when it starts with
    /// <c>=</c> or <paramref name="mechanism" /> ignores it, so LOGIN answers <c>+ !!!</c> as
    /// measured, and cancels the exchange otherwise (BL-1222).
    /// </summary>
    /// <param name="continuation">The <c>+</c> line.</param>
    /// <param name="mechanism">The exchange's mechanism.</param>
    /// <param name="index">How many challenges the exchange has been handed before this one.</param>
    /// <returns>The challenge, or <see langword="null" /> to cancel with <c>*</c>.</returns>
    private static byte[]? DecodeChallenge(Pop3Response continuation, string mechanism, int index)
    {
        string text = continuation.Line[1..].TrimStart(' ');
        var challenge = new byte[text.Length];
        if (Convert.TryFromBase64String(text, challenge, out int length))
        {
            return challenge[..length];
        }

        return text.StartsWith('=') || !ReadsChallenge(mechanism, index) ? [] : null;
    }

    /// <summary>
    /// Whether curl 8.21.0 decodes challenge <paramref name="index" /> of
    /// <paramref name="mechanism" /> (<c>get_server_message</c> in <c>lib/curl_sasl.c</c>):
    /// every GSSAPI challenge, and the first one a CRAM-MD5, DIGEST-MD5 or NTLM exchange
    /// answers (NTLM's Type 2).
    /// </summary>
    private static bool ReadsChallenge(string mechanism, int index) =>
        mechanism.ToUpperInvariant() switch
        {
            "GSSAPI" => true,
            "CRAM-MD5" or "DIGEST-MD5" or "NTLM" => index == 0,
            _ => false,
        };

    /// <summary>
    /// Tries the SASL mechanism the authenticator chooses among those <c>CAPA</c> listed, and
    /// after each one cancelled with <c>*</c> the next, as curl 8.21.0 removes a cancelled
    /// mechanism and starts again (BL-1222). A chosen mechanism that was never offered cannot
    /// be removed, so the loop stops rather than choosing it forever.
    /// </summary>
    /// <returns>How SASL ended, and the exchange's result when one was accepted or refused.</returns>
    private async ValueTask<(SaslOutcome Outcome, TransferResult? Result)> TrySaslAsync(Pop3LoginOptions options, Pop3Capabilities? capabilities)
    {
        if (saslAuthenticator is null || capabilities is null)
        {
            return (SaslOutcome.NoneChosen, null);
        }

        List<string> offered = [.. capabilities.SaslMechanisms];
        (SaslOutcome outcome, TransferResult? result) = await TryMechanismsAsync(saslAuthenticator, CreateSaslRequest(options), offered).ConfigureAwait(false);
        if (outcome == SaslOutcome.NoneChosen && offered.Count > 0)
        {
            Pop3DiagnosticLogLines.NoUsableMechanism(context.DiagnosticLog, offered);
        }

        return (outcome, result);
    }

    /// <summary>
    /// Runs the exchange of each mechanism <paramref name="authenticator" /> chooses among
    /// <paramref name="offered" />, removing each one cancelled, until one is accepted or
    /// refused, none is chosen, or a cancelled one was not among <paramref name="offered" />.
    /// </summary>
    private async ValueTask<(SaslOutcome Outcome, TransferResult? Result)> TryMechanismsAsync(
        ISaslAuthenticator authenticator, SaslRequest request, List<string> offered)
    {
        SaslOutcome outcome = SaslOutcome.NoneChosen;
        while (authenticator.ChooseMechanism(request, offered) is { } mechanism)
        {
            (bool cancelled, TransferResult? result) = await AuthenticateAsync(authenticator.Begin(mechanism, request)).ConfigureAwait(false);
            if (!cancelled)
            {
                return (SaslOutcome.Finished, result);
            }

            Pop3DiagnosticLogLines.MechanismCancelled(context.DiagnosticLog, mechanism);
            outcome = SaslOutcome.Cancelled;
            if (offered.RemoveAll(offer => offer.Equals(mechanism, StringComparison.OrdinalIgnoreCase)) == 0)
            {
                break;
            }
        }

        return (outcome, null);
    }

    private SaslRequest CreateSaslRequest(Pop3LoginOptions options) =>
        new(
            context.Credentials,
            mail.SaslAuthorizationIdentity,
            mail.BearerToken,
            options.RequiredMechanism,
            mail.ServiceName ?? DefaultServiceName,
            context.Url.IdnHost,
            context.Url.Port);

    /// <summary>Logs the login that succeeded by <paramref name="method" />.</summary>
    /// <returns><see langword="null" />, to carry on.</returns>
    private TransferResult? LoggedIn(string method)
    {
        Pop3DiagnosticLogLines.LoggedIn(context.DiagnosticLog, method);
        return null;
    }

    /// <summary>
    /// Runs one SASL exchange (RFC 5034): <c>AUTH &lt;mechanism&gt;</c>, with the initial
    /// response on the line under <c>--sasl-ir</c>, then one answer per continuation until
    /// <c>+OK</c>.
    /// </summary>
    /// <remarks>
    /// curl makes the initial response before <c>AUTH</c> only under <c>--sasl-ir</c>, and
    /// otherwise at the first continuation, so a mechanism that cannot make one fails before
    /// <c>AUTH</c> only under <c>--sasl-ir</c> (BL-856). <c>+OK</c> before the initial response
    /// was made or sent is <c>Login denied</c>, as it is anywhere before curl's final state.
    /// </remarks>
    /// <returns>
    /// Whether the exchange was cancelled with <c>*</c> over a challenge that was not base64,
    /// and otherwise <see langword="null" /> once logged in or the failure.
    /// </returns>
    private async ValueTask<(bool Cancelled, TransferResult? Result)> AuthenticateAsync(ISaslExchange exchange)
    {
        challengesHanded = 0;
        bool initialResponseDue = !mail.SaslInitialResponse;
        byte[]? unsent = await SendAuthAsync(exchange).ConfigureAwait(false);
        while (true)
        {
            Pop3Response response = await channel.ReadResponseAsync().ConfigureAwait(false);
            if (response.IsOk)
            {
                return (false, unsent is null && !initialResponseDue ? LoggedIn("SASL " + exchange.Mechanism) : LoginDenied());
            }

            if (await AnswerToAsync(response, unsent, initialResponseDue, exchange).ConfigureAwait(false) is not { } answer)
            {
                return (false, LoginDenied());
            }

            if (answer == CancelLine)
            {
                await CancelAsync().ConfigureAwait(false);
                return (true, null);
            }

            unsent = null;
            initialResponseDue = false;
            await channel.SendAsync(answer, Pop3DiagnosticLogLines.SaslResponseNotLogged).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Sends <c>AUTH &lt;mechanism&gt;</c>, with the initial response on the line when it goes
    /// there; under <c>--sasl-ir</c> it is made now.
    /// </summary>
    /// <returns>The initial response made now and still to send at the first continuation, if any.</returns>
    private async ValueTask<byte[]?> SendAuthAsync(ISaslExchange exchange)
    {
        byte[]? initialResponse = mail.SaslInitialResponse ? await exchange.GetInitialResponseAsync(context.CancellationToken).ConfigureAwait(false) : null;
        string command = "AUTH " + exchange.Mechanism;
        if (!SendsInitialResponseInline(exchange, initialResponse))
        {
            await channel.SendAsync(command).ConfigureAwait(false);
            return initialResponse;
        }

        await channel.SendAsync(command + " " + Encode(initialResponse!), command + " " + Pop3DiagnosticLogLines.SaslResponseNotLogged).ConfigureAwait(false);
        return null;
    }

    /// <summary>
    /// Cancels the exchange with <c>*</c> and reads the server's response, whatever it is, as
    /// curl's <c>SASL_CANCEL</c> state does.
    /// </summary>
    private async ValueTask CancelAsync()
    {
        await channel.SendAsync(CancelLine).ConfigureAwait(false);
        await channel.ReadResponseAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Whether the initial response goes on the <c>AUTH</c> line: only under <c>--sasl-ir</c>,
    /// and only while the mechanism's name and the base64 fit in 247 characters.
    /// </summary>
    private static bool SendsInitialResponseInline(ISaslExchange exchange, byte[]? initialResponse) =>
        initialResponse is not null
        && exchange.Mechanism.Length + Encode(initialResponse).Length <= MaxInitialResponseLength;

    /// <summary>
    /// The line that answers a response other than <c>+OK</c>, for a continuation: the unsent
    /// initial response, the one made now when it is due, or the exchange's answer to the
    /// challenge, each encoded; <see cref="CancelLine" /> for a challenge that is not base64
    /// and that the mechanism reads; <see langword="null" /> for <c>-ERR</c> or a challenge the
    /// exchange cannot answer.
    /// </summary>
    private async ValueTask<string?> AnswerToAsync(Pop3Response response, byte[]? unsentInitialResponse, bool initialResponseDue, ISaslExchange exchange)
    {
        if (response.Line[0] != '+')
        {
            return null;
        }

        byte[]? initialResponse = initialResponseDue
            ? await exchange.GetInitialResponseAsync(context.CancellationToken).ConfigureAwait(false)
            : unsentInitialResponse;
        if (initialResponse is not null)
        {
            return Encode(initialResponse);
        }

        if (DecodeChallenge(response, exchange.Mechanism, challengesHanded++) is not { } challenge)
        {
            return CancelLine;
        }

        return await exchange.RespondAsync(challenge, context.CancellationToken).ConfigureAwait(false) is { } answer ? Encode(answer) : null;
    }

    /// <summary>
    /// Logs in with <c>APOP</c> or <c>USER</c>/<c>PASS</c>, which need credentials and a login
    /// option that does not name a SASL mechanism. With neither possible, a SASL exchange
    /// cancelled before is <c>Authentication cancelled</c>, as curl's <c>SASL_IDLE</c> answer
    /// in <c>lib/pop3.c</c> is, with no <c>SASL:</c> line (BL-1222).
    /// </summary>
    private ValueTask<TransferResult?> LogInWithoutSaslAsync(
        Pop3LoginOptions options, Pop3Capabilities? capabilities, string? apopTimestamp, bool saslCancelled)
    {
        if (options.Method == Pop3LoginMethod.Sasl || context.Credentials is not { } credential)
        {
            return ValueTask.FromResult<TransferResult?>(NoWayToLogIn(options, capabilities, saslCancelled));
        }

        if (apopTimestamp is not null)
        {
            return SendApopAsync(credential.UserName, Pop3ApopDigest.Compute(apopTimestamp, credential.Password));
        }

        return AllowsUserAndPass(options, capabilities)
            ? SendUserAndPassAsync(credential.UserName, credential.Password)
            : ValueTask.FromResult<TransferResult?>(NoWayToLogIn(options, capabilities, saslCancelled));
    }

    private TransferResult NoWayToLogIn(Pop3LoginOptions options, Pop3Capabilities? capabilities, bool saslCancelled) =>
        saslCancelled ? AuthenticationCancelled() : NoWayToLogIn(options, capabilities);

    /// <summary>
    /// Writes the <c>-v</c> lines curl 8.21.0's <c>Curl_sasl_is_blocked</c> writes when no way
    /// of logging in is possible, and fails with exit 67 <c>Login denied</c> (BL-810,
    /// BL-1221): <c>no auth mechanism was offered or recognized</c> when <c>CAPA</c> listed no
    /// mechanism curl knows or was refused, <c>no overlap</c> when it listed none the
    /// credentials and login options allow, and otherwise <c>no auth mechanism offered could
    /// be selected</c> followed by why each allowed one was not.
    /// </summary>
    private TransferResult NoWayToLogIn(Pop3LoginOptions options, Pop3Capabilities? capabilities)
    {
        string[] known = KnownOfferedMechanisms(capabilities);
        string[] enabled = known.Where(mechanism => IsAllowed(options, mechanism)).ToArray();
        if (enabled.Length > 0 && context.Credentials is { } credential)
        {
            ReportWhyNoneWasChosen(credential, mechanism => enabled.Contains(mechanism, StringComparer.OrdinalIgnoreCase));
        }
        else
        {
            context.Events.ReportInfo(known.Length == 0 ? Pop3SessionMessages.NoSaslMechanismOffered : Pop3SessionMessages.NoSaslMechanismOverlap);
        }

        return LoginDenied();
    }

    /// <summary>
    /// The SASL mechanisms <c>CAPA</c> listed that curl 8.21.0 knows; none when it was refused.
    /// </summary>
    private static string[] KnownOfferedMechanisms(Pop3Capabilities? capabilities) =>
        (capabilities?.SaslMechanisms ?? []).Where(Pop3LoginOptions.IsKnownMechanism).ToArray();

    /// <summary>
    /// Writes <c>no auth mechanism offered could be selected</c>, then, as curl 8.21.0's
    /// Schannel build does: <c>auth EXTERNAL not chosen with password</c> when EXTERNAL is
    /// enabled and a password was given, and each enabled mechanism's reason in
    /// <see cref="UnchosenMechanisms" /> order (measured 2026-10-02, BL-1221 Notes).
    /// </summary>
    /// <param name="credential">The user name and password given with <c>-u</c>.</param>
    /// <param name="isEnabled">Whether a mechanism was both offered and allowed, in any case.</param>
    private void ReportWhyNoneWasChosen(NetworkCredential credential, Func<string, bool> isEnabled)
    {
        context.Events.ReportInfo(Pop3SessionMessages.NoSaslMechanismSelectable);
        if (isEnabled(ExternalMechanism) && credential.Password.Length > 0)
        {
            context.Events.ReportInfo(Pop3SessionMessages.SaslExternalNotChosenWithPassword);
        }

        foreach (string mechanism in UnchosenMechanisms.Where(isEnabled))
        {
            foreach (string line in WhyUnchosen(mechanism, credential))
            {
                context.Events.ReportInfo(string.Format(CultureInfo.InvariantCulture, line, mechanism));
            }
        }
    }

    /// <summary>
    /// The <c>-v</c> lines curl 8.21.0's <c>sasl_unchosen</c> writes for an enabled
    /// <paramref name="mechanism" />, each a format with <c>{0}</c> for its name: <c>not
    /// builtin</c> for those its Schannel build reports so; otherwise <c>is missing
    /// CURLOPT_XOAUTH2_BEARER</c> for a bearer mechanism without <c>--oauth2-bearer</c>, and
    /// <c>is missing username</c> when the user name is empty.
    /// </summary>
    private IEnumerable<string> WhyUnchosen(string mechanism, NetworkCredential credential)
    {
        if (NotBuiltInMechanisms.Contains(mechanism))
        {
            yield return Pop3SessionMessages.SaslMechanismNotBuiltIn;
            yield break;
        }

        if (BearerMechanisms.Contains(mechanism) && mail.BearerToken is null)
        {
            yield return Pop3SessionMessages.SaslMechanismMissingBearer;
        }

        if (credential.UserName.Length == 0)
        {
            yield return Pop3SessionMessages.SaslMechanismMissingUserName;
        }
    }

    /// <summary>
    /// Whether curl would enable <paramref name="mechanism" />: the login options name it, or
    /// allow every way and it is not EXTERNAL, which curl tries only when named.
    /// </summary>
    private static bool IsAllowed(Pop3LoginOptions options, string mechanism) =>
        options.Method == Pop3LoginMethod.Any
            ? !mechanism.Equals(ExternalMechanism, StringComparison.OrdinalIgnoreCase)
            : mechanism.Equals(options.RequiredMechanism, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether <c>USER</c>/<c>PASS</c> may be sent: the login options allow any way, and
    /// <c>CAPA</c> listed <c>USER</c> or was refused.
    /// </summary>
    private static bool AllowsUserAndPass(Pop3LoginOptions options, Pop3Capabilities? capabilities) =>
        options.Method == Pop3LoginMethod.Any && (capabilities is null || capabilities.AdvertisesUser);

    private async ValueTask<TransferResult?> SendApopAsync(string user, string digest)
    {
        await channel.SendAsync("APOP " + user + " " + digest, "APOP " + user + " " + Pop3DiagnosticLogLines.DigestNotLogged).ConfigureAwait(false);
        Pop3Response response = await channel.ReadResponseAsync().ConfigureAwait(false);
        return response.IsOk
            ? LoggedIn("APOP")
            : TransferResult.Failure(
                CurlExitCode.LoginDenied,
                string.Format(CultureInfo.InvariantCulture, Pop3SessionMessages.AuthenticationFailed, (int)RefusalCode(response)));
    }

    private async ValueTask<TransferResult?> SendUserAndPassAsync(string user, string password)
    {
        return await SendCredentialAsync("USER " + user).ConfigureAwait(false)
            ?? await SendCredentialAsync("PASS " + password, "PASS " + Pop3DiagnosticLogLines.PasswordNotLogged).ConfigureAwait(false)
            ?? LoggedIn("USER and PASS");
    }

    private async ValueTask<TransferResult?> SendCredentialAsync(string command, string? logged = null)
    {
        await channel.SendAsync(command, logged).ConfigureAwait(false);
        Pop3Response response = await channel.ReadResponseAsync().ConfigureAwait(false);
        return response.IsOk
            ? null
            : TransferResult.Failure(
                CurlExitCode.LoginDenied,
                string.Format(CultureInfo.InvariantCulture, Pop3SessionMessages.AccessDenied, RefusalCode(response)));
    }

    /// <summary>How the SASL step of a login ended.</summary>
    private enum SaslOutcome
    {
        /// <summary>No mechanism was chosen, so the login goes on without SASL.</summary>
        NoneChosen,

        /// <summary>Every mechanism chosen was cancelled with <c>*</c>; the login goes on without SASL.</summary>
        Cancelled,

        /// <summary>An exchange was accepted or refused.</summary>
        Finished,
    }
}
