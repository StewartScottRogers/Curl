using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Ldap;

/// <summary>
/// Binds as curl's OpenLDAP build does when the auth mask or <c>--login-options AUTH=</c> asks for
/// SASL instead of a simple bind (BL-5435): reads <c>supportedSASLMechanisms</c> from the rootDSE,
/// then runs the mechanism an <see cref="ISaslAuthenticator" /> picks among those offered and allowed
/// (BL-5907, ADR-1359). A refused bind is never retried as a simple bind.
/// </summary>
internal static class LdapOpenLdapSaslBind
{
    private const string AuthPrefix = "AUTH=";
    private const string AnyMechanism = "*";
    private const string External = "EXTERNAL";
    private const string Gssapi = "GSSAPI";
    private const string CramMd5 = "CRAM-MD5";
    private const string Ntlm = "NTLM";
    private const string Login = "LOGIN";
    private const string ServiceName = "ldap";
    private const int InvalidCredentials = 49;

    private static readonly string[] KnownMechanisms = [External, Gssapi, LdapDigestMd5Bind.Mechanism, CramMd5, Ntlm, "XOAUTH2", "OAUTHBEARER", "PLAIN", Login];

    // What AUTH=* and a mask naming no SASL mechanism allow: every mechanism but EXTERNAL.
    private static readonly string[] DefaultMechanisms = [.. KnownMechanisms.Where(mechanism => mechanism != External)];

    // The mechanisms curl's Curl_sasl_init allows for each auth scheme; none maps to CRAM-MD5.
    private static readonly (HttpAuthSchemes Scheme, string[] Mechanisms)[] MechanismsByScheme =
    [
        (HttpAuthSchemes.Basic, ["PLAIN", Login]),
        (HttpAuthSchemes.Digest, [LdapDigestMd5Bind.Mechanism]),
        (HttpAuthSchemes.Ntlm, [Ntlm]),
        (HttpAuthSchemes.Bearer, ["OAUTHBEARER", "XOAUTH2"]),
        (HttpAuthSchemes.Negotiate, [Gssapi]),
    ];

    // The challenges each mechanism must answer before the server's success counts; the others need none.
    private static readonly Dictionary<string, int> RequiredAnswers = new(StringComparer.OrdinalIgnoreCase)
    {
        [Login] = 1,
        [CramMd5] = 1,
        [Ntlm] = 1,
        [Gssapi] = 2,
    };

    /// <summary>
    /// Whether <paramref name="loginOptions" /> are what curl's <c>Curl_sasl_parse_url_auth_option</c> accepts (STD-184): empty, or
    /// <c>;</c>-separated items that are each exactly <c>AUTH=</c> and a known SASL mechanism or <c>*</c>. Anything else, an empty item included, is exit 49.
    /// </summary>
    public static bool AreWellFormed(string? loginOptions) =>
        string.IsNullOrEmpty(loginOptions)
        || loginOptions.Split(';').All(item =>
            item.StartsWith(AuthPrefix, StringComparison.OrdinalIgnoreCase)
            && (item[AuthPrefix.Length..] == AnyMechanism || KnownMechanisms.Contains(item[AuthPrefix.Length..], StringComparer.OrdinalIgnoreCase)));

    /// <summary>Whether <paramref name="loginOptions" /> carry an <c>AUTH=</c> option, which selects SASL.</summary>
    public static bool HasAuthOption(string? loginOptions) => NamedMechanisms(loginOptions).Length > 0;

    /// <summary>
    /// The mechanisms the transfer may bind with: those its <c>AUTH=</c> options name (<c>*</c> adding every one but
    /// <c>EXTERNAL</c>), else those of the auth mask's schemes, else every one but <c>EXTERNAL</c>.
    /// </summary>
    public static IReadOnlyList<string> AllowedMechanisms(string? loginOptions, HttpAuthSchemes schemes)
    {
        string[] named = NamedMechanisms(loginOptions);
        if (named.Length > 0)
        {
            return named.Contains(AnyMechanism) ? [.. DefaultMechanisms, .. named] : named;
        }

        string[] fromMask = [.. MechanismsByScheme.Where(entry => schemes.HasFlag(entry.Scheme)).SelectMany(entry => entry.Mechanisms)];
        return fromMask.Length > 0 ? fromMask : DefaultMechanisms;
    }

    /// <summary>Binds with SASL as described on the type; <see langword="null" /> once bound, otherwise the failed result.</summary>
    public static async ValueTask<TransferResult?> BindAsync(LdapExchange exchange, ISaslAuthenticator authenticator, ITransferContext context)
    {
        CancellationToken cancellationToken = context.CancellationToken;
        if (await ReadOfferedMechanismsAsync(exchange, cancellationToken).ConfigureAwait(false) is not { } offered)
        {
            return await LdapBind.OpenLdapBindFailedAsync(exchange, new LdapBindReply(LdapBindReplyStatus.Closed, 0), cancellationToken).ConfigureAwait(false);
        }

        IReadOnlyList<string> allowed = AllowedMechanisms(context.Mail?.LoginOptions, context.Http?.AuthSchemes ?? HttpAuthSchemes.Basic);

        // Without a user curl starts no mechanism but EXTERNAL.
        List<string> candidates = [.. offered.Where(mechanism => allowed.Contains(mechanism, StringComparer.OrdinalIgnoreCase)
            && (context.Credentials is not null || IsMechanism(mechanism, External)))];
        try
        {
            return await BindWithCandidatesAsync(exchange, authenticator, CreateRequest(context), candidates, cancellationToken).ConfigureAwait(false);
        }
        catch (SaslAuthenticationFailedException failure)
        {
            return await LdapBind.UnbindAndFailAsync(exchange, TransferResult.Failure(failure.ExitCode, failure.Message), cancellationToken).ConfigureAwait(false);
        }
    }

    // OpenLDAP's rootDSE search sets no time limit.
    private static ValueTask<List<string>?> ReadOfferedMechanismsAsync(LdapExchange exchange, CancellationToken cancellationToken)
    {
        int messageId = exchange.TakeMessageId();
        byte[] search = LdapRequests.RootDseSearch(exchange.Writer, messageId, WinLdapLogonBind.SupportedSaslMechanisms, timeLimit: 0);
        return WinLdapLogonBind.ReadRootDseAnswerAsync(exchange, messageId, WinLdapLogonBind.SupportedSaslMechanisms, search, cancellationToken);
    }

    private static SaslRequest CreateRequest(ITransferContext context) =>
        new(
            context.Credentials,
            context.Mail?.SaslAuthorizationIdentity,
            context.Mail?.BearerToken,
            RequiredMechanism: null,
            context.Mail?.ServiceName ?? ServiceName,
            context.Url.IdnHost,
            context.Url.Port);

    // Tries the authenticator's picks in turn: a cancelled mechanism gives way to the next one.
    private static async ValueTask<TransferResult?> BindWithCandidatesAsync(LdapExchange exchange, ISaslAuthenticator authenticator, SaslRequest request, List<string> candidates, CancellationToken cancellationToken)
    {
        bool cancelled = false;
        while (Choose(authenticator, request, candidates) is { } mechanism)
        {
            if (await AttemptAsync(exchange, authenticator, request, mechanism, cancellationToken).ConfigureAwait(false) is { } answered)
            {
                return answered.IsSuccess ? null : await SaslBindFailedAsync(exchange, answered, cancellationToken).ConfigureAwait(false);
            }

            candidates.RemoveAll(candidate => IsMechanism(candidate, mechanism));
            cancelled = true;
        }

        return cancelled
            ? await AuthenticationCancelledAsync(exchange, cancellationToken).ConfigureAwait(false)
            : await LdapBind.OpenLdapLoginDeniedAsync(exchange, cancellationToken).ConfigureAwait(false);
    }

    // DIGEST-MD5 is this library's own exchange; the authenticator runs every other mechanism.
    private static async ValueTask<LdapBindReply?> AttemptAsync(LdapExchange exchange, ISaslAuthenticator authenticator, SaslRequest request, string mechanism, CancellationToken cancellationToken) =>
        IsMechanism(mechanism, LdapDigestMd5Bind.Mechanism)
            ? await LdapAuthenticatedBind.DigestAsync(exchange, request.Credential!, request.Host, cancellationToken, acceptsQopAuth: true).ConfigureAwait(false)
            : await RunMechanismAsync(exchange, authenticator.Begin(mechanism, request), cancellationToken).ConfigureAwait(false);

    // curl prefers EXTERNAL, which an authenticator picks only when it is the required mechanism.
    private static string? Choose(ISaslAuthenticator authenticator, SaslRequest request, List<string> candidates) =>
        (candidates.Contains(External, StringComparer.OrdinalIgnoreCase)
            ? authenticator.ChooseMechanism(request with { RequiredMechanism = External }, candidates)
            : null)
        ?? authenticator.ChooseMechanism(request, candidates);

    // The server's last answer, or null when a challenge could not be answered and the exchange was cancelled.
    private static async ValueTask<LdapBindReply?> RunMechanismAsync(LdapExchange exchange, ISaslExchange sasl, CancellationToken cancellationToken)
    {
        byte[]? initial = await sasl.GetInitialResponseAsync(cancellationToken).ConfigureAwait(false);
        LdapBindReply reply = await SendBindAsync(exchange, sasl.Mechanism, initial, cancellationToken).ConfigureAwait(false);
        int answers = 0;
        while (reply is { Status: LdapBindReplyStatus.Answered, ResultCode: LdapBindReply.SaslBindInProgress })
        {
            if (await AnswerAsync(sasl, reply.ServerSaslCredentials, cancellationToken).ConfigureAwait(false) is not { } answer)
            {
                return IsUnanswerable(sasl.Mechanism, answers)
                    ? await CancelAsync(exchange, cancellationToken).ConfigureAwait(false)
                    : reply;
            }

            reply = await SendBindAsync(exchange, sasl.Mechanism, answer, cancellationToken).ConfigureAwait(false);
            answers++;
        }

        return reply.IsSuccess && answers < RequiredAnswers.GetValueOrDefault(sasl.Mechanism)
            ? new LdapBindReply(LdapBindReplyStatus.Answered, InvalidCredentials)
            : reply;
    }

    // CRAM-MD5, NTLM and GSSAPI read the challenge, so one without serverSaslCreds is exit 8.
    private static ValueTask<byte[]?> AnswerAsync(ISaslExchange sasl, byte[] challenge, CancellationToken cancellationToken) =>
        challenge.Length == 0 && ReadsChallenge(sasl.Mechanism)
            ? throw new SaslAuthenticationFailedException(CurlExitCode.WeirdServerReply, "Weird server reply")
            : sasl.RespondAsync(challenge, cancellationToken);

    private static bool ReadsChallenge(string mechanism) =>
        IsMechanism(mechanism, CramMd5) || IsMechanism(mechanism, Ntlm) || IsMechanism(mechanism, Gssapi);

    // A first challenge CRAM-MD5 or NTLM cannot answer, or any GSSAPI cannot, is cancelled; any other unanswered one is a refusal.
    private static bool IsUnanswerable(string mechanism, int answers) =>
        IsMechanism(mechanism, Gssapi) || (answers == 0 && ReadsChallenge(mechanism));

    // curl cancels with a bind naming the empty mechanism and no credentials.
    private static async ValueTask<LdapBindReply?> CancelAsync(LdapExchange exchange, CancellationToken cancellationToken)
    {
        await SendBindAsync(exchange, string.Empty, null, cancellationToken).ConfigureAwait(false);
        return null;
    }

    private static ValueTask<LdapBindReply> SendBindAsync(LdapExchange exchange, string mechanism, byte[]? credentials, CancellationToken cancellationToken) =>
        credentials is null
            ? exchange.BindWithRequestAsync(messageId => LdapRequests.SaslBindWithoutCredentials(exchange.Writer, messageId, mechanism), cancellationToken)
            : exchange.SaslBindAsync(mechanism, credentials, cancellationToken);

    // The OpenLDAP SASL bind (ADR-1343): a server that closed is exit 7; a challenge the client cannot answer (Local Error, as under a
    // server minssf) is exit 67 "Authentication cancelled"; every other failed reply is exit 67 "Login denied", never exit 38.
    private static ValueTask<TransferResult?> SaslBindFailedAsync(LdapExchange exchange, LdapBindReply reply, CancellationToken cancellationToken) =>
        reply.Status == LdapBindReplyStatus.Closed
            ? LdapBind.OpenLdapBindFailedAsync(exchange, reply, cancellationToken)
            : reply.ResultCode == WinLdapResultText.LocalError
                ? AuthenticationCancelledAsync(exchange, cancellationToken)
                : LdapBind.OpenLdapLoginDeniedAsync(exchange, cancellationToken);

    private static ValueTask<TransferResult?> AuthenticationCancelledAsync(LdapExchange exchange, CancellationToken cancellationToken) =>
        LdapBind.UnbindAndFailAsync(exchange, TransferResult.Failure(CurlExitCode.LoginDenied, "Authentication cancelled"), cancellationToken);

    private static bool IsMechanism(string offered, string mechanism) => offered.Equals(mechanism, StringComparison.OrdinalIgnoreCase);

    private static string[] NamedMechanisms(string? loginOptions) =>
        [.. (loginOptions ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(option => option.StartsWith(AuthPrefix, StringComparison.OrdinalIgnoreCase))
            .Select(option => option[AuthPrefix.Length..])];
}
