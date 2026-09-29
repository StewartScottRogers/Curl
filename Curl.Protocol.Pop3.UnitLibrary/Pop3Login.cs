using System.Globalization;
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
/// <item>No SASL mechanism chosen: <c>APOP &lt;user&gt; &lt;digest&gt;</c>
/// (<see cref="Pop3ApopDigest" />) when the greeting carried a timestamp, whatever
/// <c>CAPA</c> said; refused, exit 67 <c>Authentication failed: &lt;n&gt;</c>, n being 45
/// for <c>-ERR</c> and 42 for another <c>+</c> line.</item>
/// <item>No timestamp: <c>USER</c> then <c>PASS</c> when <c>CAPA</c> listed <c>USER</c> or
/// was refused; either refused, exit 67 <c>Access denied. &lt;c&gt;</c>, c being <c>-</c>
/// or <c>*</c> as above.</item>
/// <item>None of these possible, or <c>AUTH=</c> ruling out the one that is: exit 67
/// <c>Login denied</c>.</item>
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

    private readonly MailRequestOptions mail = context.Mail ?? new MailRequestOptions();

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

        return options.Method != Pop3LoginMethod.Apop && ChooseSaslExchange(options, capabilities) is { } exchange
            ? await AuthenticateAsync(exchange).ConfigureAwait(false)
            : await LogInWithoutSaslAsync(options, capabilities, apopTimestamp).ConfigureAwait(false);
    }

    private static TransferResult LoginDenied() =>
        TransferResult.Failure(CurlExitCode.LoginDenied, Pop3SessionMessages.LoginDenied);

    /// <summary>
    /// The character curl 8.21.0 reports a refusal by: <c>-</c> for <c>-ERR</c>, <c>*</c> for
    /// any other <c>+</c> line, which it takes as a continuation.
    /// </summary>
    private static char RefusalCode(Pop3Response response) => response.Line[0] == '-' ? '-' : '*';

    private static string Encode(byte[] message) =>
        message.Length == 0 ? EmptyMessage : Convert.ToBase64String(message);

    /// <summary>
    /// The challenge a continuation carries: its text after the <c>+</c> and any spaces,
    /// decoded from base64. Text that is not base64 is an empty challenge, so LOGIN, which
    /// ignores its challenges, answers <c>+ !!!</c> as measured.
    /// </summary>
    private static byte[] DecodeChallenge(Pop3Response continuation)
    {
        string text = continuation.Line[1..].TrimStart(' ');
        var challenge = new byte[text.Length];
        return Convert.TryFromBase64String(text, challenge, out int length) ? challenge[..length] : [];
    }

    private ISaslExchange? ChooseSaslExchange(Pop3LoginOptions options, Pop3Capabilities? capabilities)
    {
        if (saslAuthenticator is null || capabilities is null)
        {
            return null;
        }

        var request = new SaslRequest(
            context.Credentials,
            mail.SaslAuthorizationIdentity,
            mail.BearerToken,
            options.RequiredMechanism,
            mail.ServiceName ?? DefaultServiceName,
            context.Url.IdnHost,
            context.Url.Port);
        if (saslAuthenticator.ChooseMechanism(request, capabilities.SaslMechanisms) is { } mechanism)
        {
            return saslAuthenticator.Begin(mechanism, request);
        }

        if (capabilities.SaslMechanisms.Count > 0)
        {
            Pop3DiagnosticLogLines.NoUsableMechanism(context.DiagnosticLog, capabilities.SaslMechanisms);
        }

        return null;
    }

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
    private async ValueTask<TransferResult?> AuthenticateAsync(ISaslExchange exchange)
    {
        byte[]? unsent = mail.SaslInitialResponse ? await exchange.GetInitialResponseAsync(context.CancellationToken).ConfigureAwait(false) : null;
        bool initialResponseDue = !mail.SaslInitialResponse;
        string command = "AUTH " + exchange.Mechanism;
        string? logged = null;
        if (SendsInitialResponseInline(exchange, unsent))
        {
            logged = command + " " + Pop3DiagnosticLogLines.SaslResponseNotLogged;
            command += " " + Encode(unsent!);
            unsent = null;
        }

        await channel.SendAsync(command, logged).ConfigureAwait(false);
        while (true)
        {
            Pop3Response response = await channel.ReadResponseAsync().ConfigureAwait(false);
            if (response.IsOk)
            {
                return unsent is null && !initialResponseDue ? LoggedIn("SASL " + exchange.Mechanism) : LoginDenied();
            }

            if (await AnswerToAsync(response, unsent, initialResponseDue, exchange).ConfigureAwait(false) is not { } answer)
            {
                return LoginDenied();
            }

            unsent = null;
            initialResponseDue = false;
            await channel.SendAsync(Encode(answer), Pop3DiagnosticLogLines.SaslResponseNotLogged).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Whether the initial response goes on the <c>AUTH</c> line: only under <c>--sasl-ir</c>,
    /// and only while the mechanism's name and the base64 fit in 247 characters.
    /// </summary>
    private static bool SendsInitialResponseInline(ISaslExchange exchange, byte[]? initialResponse) =>
        initialResponse is not null
        && exchange.Mechanism.Length + Encode(initialResponse).Length <= MaxInitialResponseLength;

    /// <summary>
    /// The answer to a response other than <c>+OK</c>: the unsent initial response, the one
    /// made now when it is due, or the exchange's answer to the challenge, for a continuation;
    /// <see langword="null" /> for <c>-ERR</c> or a challenge the exchange cannot answer.
    /// </summary>
    private async ValueTask<byte[]?> AnswerToAsync(Pop3Response response, byte[]? unsentInitialResponse, bool initialResponseDue, ISaslExchange exchange)
    {
        if (response.Line[0] != '+')
        {
            return null;
        }

        byte[]? initialResponse = initialResponseDue
            ? await exchange.GetInitialResponseAsync(context.CancellationToken).ConfigureAwait(false)
            : unsentInitialResponse;
        return initialResponse ?? await exchange.RespondAsync(DecodeChallenge(response), context.CancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Logs in with <c>APOP</c> or <c>USER</c>/<c>PASS</c>, which need credentials and a login
    /// option that does not name a SASL mechanism.
    /// </summary>
    private ValueTask<TransferResult?> LogInWithoutSaslAsync(
        Pop3LoginOptions options, Pop3Capabilities? capabilities, string? apopTimestamp)
    {
        if (options.Method == Pop3LoginMethod.Sasl || context.Credentials is not { } credential)
        {
            return ValueTask.FromResult<TransferResult?>(LoginDenied());
        }

        if (apopTimestamp is not null)
        {
            return SendApopAsync(credential.UserName, Pop3ApopDigest.Compute(apopTimestamp, credential.Password));
        }

        return AllowsUserAndPass(options, capabilities)
            ? SendUserAndPassAsync(credential.UserName, credential.Password)
            : ValueTask.FromResult<TransferResult?>(LoginDenied());
    }

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
}
