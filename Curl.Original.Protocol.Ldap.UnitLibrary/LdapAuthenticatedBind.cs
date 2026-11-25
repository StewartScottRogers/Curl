using System.Net;
using System.Net.Security;
using System.Security.Cryptography;
using Curl.Ntlm;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Ldap;

/// <summary>
/// Binds with <c>-u</c>'s credentials for <c>--ntlm</c>, <c>--negotiate</c> and <c>--digest</c> as
/// curl's WinLDAP build does (ADR-1246): the Sicily NTLM bind (tag <c>0x8a</c>) for <c>--ntlm</c>, or the SASL bind (tag <c>0xa3</c>): DIGEST-MD5, or for <c>--negotiate</c> GSS-SPNEGO after a rootDSE read (BL-5314), never a simple bind, so the password never crosses
/// the wire in clear.
/// </summary>
/// <remarks>
/// The NTLM bind sends the negotiate message, answers the challenge in the reply with an NTLM
/// response built from the <c>-u</c> credentials (tag <c>0x8b</c>), and fails with <c>Local Error</c>
/// when the reply carries no decodable challenge; a server that refuses either step fails with
/// its own result code. When the challenge grants sealing, the bind exchanges a session key and
/// every later message goes through the SASL security layer sealed with NTLM session security
/// (BL-5142, ADR-1267).
/// </remarks>
internal static class LdapAuthenticatedBind
{
    private const string NtlmSignature = "NTLMSSP\0";

    private const int NegotiateFlags = unchecked((int)0xe20882b7);

    private const int NegotiateMessageLength = 32;

    private const int ClientNonceBytes = 16;

    private const int SicilyResponseChoice = 11;

    /// <summary>Binds as <paramref name="kind" /> says.</summary>
    /// <param name="kind">One of <see cref="LdapBindKind.Ntlm" />, <see cref="LdapBindKind.Negotiate" /> or <see cref="LdapBindKind.Digest" />.</param>
    /// <param name="exchange">The session to bind on.</param>
    /// <param name="credentials">The <c>-u</c> credentials.</param>
    /// <param name="host">The URL's host, for the digest-uri <c>ldap/host</c>.</param>
    /// <param name="overTls">Whether the session is <c>ldaps</c>, so a negotiated bind asks for no signing or sealing.</param>
    /// <param name="tokenSource">Produces the SPNEGO tokens of a negotiate bind for <paramref name="credentials" />.</param>
    /// <param name="cancellationToken">Cancels the bind.</param>
    /// <returns><see langword="null" /> once bound; otherwise the failed transfer's result.</returns>
    public static async ValueTask<TransferResult?> BindAsync(LdapBindKind kind, LdapExchange exchange, NetworkCredential credentials, string host, bool overTls, ILdapLogonTokenSource tokenSource, CancellationToken cancellationToken)
    {
        LdapBindReply reply = await ChosenBindAsync(kind, exchange, credentials, host, overTls, tokenSource, cancellationToken).ConfigureAwait(false);
        return reply.Status != LdapBindReplyStatus.Answered ? LdapBind.WinLdapBindFailed(WinLdapResultText.Timeout)
            : reply.IsSuccess ? null
            : await LdapBind.UnbindAndFailAsync(exchange, LdapBind.WinLdapBindFailed(reply.ResultCode), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Makes the bind <paramref name="kind" /> names: DIGEST-MD5, negotiated or Sicily NTLM.</summary>
    private static ValueTask<LdapBindReply> ChosenBindAsync(LdapBindKind kind, LdapExchange exchange, NetworkCredential credentials, string host, bool overTls, ILdapLogonTokenSource tokenSource, CancellationToken cancellationToken) =>
        kind switch
        {
            LdapBindKind.Digest => DigestAsync(exchange, credentials, host, cancellationToken, overTls: overTls),
            LdapBindKind.Negotiate => NegotiateAsync(exchange, credentials, host, overTls, tokenSource, cancellationToken),
            _ => NtlmAsync(exchange, credentials, cancellationToken),
        };

    /// <summary>
    /// Reads the rootDSE as WinLDAP's <c>LDAP_AUTH_NEGOTIATE</c> does, then binds with SASL <c>GSS-SPNEGO</c>
    /// (tokens from <paramref name="tokenSource" /> with <c>-u</c>'s credentials) when the server offers it, otherwise with
    /// SASL <c>DIGEST-MD5</c>; never the Sicily bind (BL-5314). A server that closes during the reads is a <c>Timeout</c>.
    /// </summary>
    private static async ValueTask<LdapBindReply> NegotiateAsync(LdapExchange exchange, NetworkCredential credentials, string host, bool overTls, ILdapLogonTokenSource tokenSource, CancellationToken cancellationToken)
    {
        if (await WinLdapLogonBind.ReadOffersSpnegoAsync(exchange, false, cancellationToken).ConfigureAwait(false) is not { } offersSpnego)
        {
            return new LdapBindReply(LdapBindReplyStatus.Closed, 0);
        }

        if (!offersSpnego)
        {
            return await DigestAsync(exchange, credentials, host, cancellationToken, overTls: overTls).ConfigureAwait(false);
        }

        ProtectionLevel protection = overTls ? ProtectionLevel.None : ProtectionLevel.EncryptAndSign;
        ILdapLogonAuthentication authentication = tokenSource.ForCredential(credentials).Start(LdapLogonPackage.Negotiate, "ldap/" + host, protection);
        LdapBindReply reply = new(LdapBindReplyStatus.Answered, WinLdapResultText.LocalError);
        try
        {
            reply = await WinLdapLogonBind.SaslBindAsync(exchange, authentication, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (reply.IsSuccess && protection != ProtectionLevel.None)
            {
                exchange.StartSecurityLayer(authentication, WinLdapResultText.Unavailable);
            }
            else
            {
                authentication.Dispose();
            }
        }

        return reply;
    }

    /// <summary>
    /// Sends the Sicily negotiate, then <c>sicilyResponse</c> answering the challenge in the answer's
    /// matchedDN with <c>-u</c>'s credentials; an answer without a usable challenge is a <c>Local Error</c>.
    /// </summary>
    private static async ValueTask<LdapBindReply> NtlmAsync(LdapExchange exchange, NetworkCredential credentials, CancellationToken cancellationToken)
    {
        LdapBindReply reply = await exchange.BindWithRequestAsync(
            messageId => LdapSicilyBind.NegotiateRequest(exchange.Writer, messageId, NegotiateMessage()),
            cancellationToken).ConfigureAwait(false);
        if (!reply.IsSuccess)
        {
            return reply;
        }

        NtlmChallengeMessage? challenge = NtlmChallengeMessage.Decode(reply.MatchedDn).Message;
        if (challenge is null)
        {
            return new LdapBindReply(LdapBindReplyStatus.Answered, WinLdapResultText.LocalError);
        }

        NtlmAuthenticateMessage answer = new NtlmChallengeAnswerer(TimeProvider.System, new SystemNtlmRandomSource())
            .AnswerWithKeyExchange(challenge, credentials.UserName, credentials.Password, out byte[] exportedSessionKey);
        try
        {
            if (!answer.TryEncode(out byte[]? response))
            {
                return new LdapBindReply(LdapBindReplyStatus.Answered, WinLdapResultText.LocalError);
            }

            LdapBindReply bound = await exchange.SicilyBindAsync(string.Empty, SicilyResponseChoice, response, cancellationToken).ConfigureAwait(false);
            if (bound.IsSuccess && Seals(answer.Flags))
            {
                exchange.StartSecurityLayer(new NtlmLdapLogonAuthentication(NtlmSessionSecurity.CreateClient(answer.Flags, exportedSessionKey)), WinLdapResultText.Unavailable);
            }

            return bound;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(exportedSessionKey);
        }
    }

    /// <summary>
    /// Gets a value indicating whether the server granted sealing with keys <see cref="NtlmSessionSecurity" />
    /// can derive: SEAL, extended session security and 128-bit keys; otherwise the session stays unsealed.
    /// </summary>
    private static bool Seals(NtlmNegotiateFlags flags) =>
        flags.HasFlag(NtlmNegotiateFlags.NegotiateSeal | NtlmNegotiateFlags.NegotiateExtendedSessionSecurity | NtlmNegotiateFlags.Negotiate128);

    /// <summary>
    /// Names DIGEST-MD5, then answers the challenge of the <c>saslBindInProgress</c> reply; a challenge it cannot answer
    /// (no nonce, or no <c>auth-conf</c> with a supported cipher unless <paramref name="acceptsQopAuth" /> or <paramref name="overTls" />, where TLS already protects the session and <c>qop=auth</c> answers, BL-5854) sends nothing more
    /// and fails as WinLDAP's <c>Operations Error</c>, or the OpenLDAP dialect's <c>Local Error</c> (ADR-1302).
    /// </summary>
    internal static async ValueTask<LdapBindReply> DigestAsync(LdapExchange exchange, NetworkCredential credentials, string host, CancellationToken cancellationToken, bool acceptsQopAuth = false, bool overTls = false)
    {
        LdapBindReply initial = await exchange.BindWithRequestAsync(messageId => InitialDigestRequest(exchange.Writer, messageId, acceptsQopAuth), cancellationToken).ConfigureAwait(false);
        if (FailureWithoutChallenge(initial) is { } failure)
        {
            return failure;
        }

        bool answersQopAuth = acceptsQopAuth || overTls;
        string clientNonce = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(ClientNonceBytes));
        byte[]? probe = LdapDigestMd5Bind.AnswerRequest(exchange.Writer, 0, initial.ServerSaslCredentials, credentials.UserName, credentials.Password, "ldap/" + host, clientNonce, answersQopAuth);
        if (probe is null)
        {
            return UnanswerableChallenge(acceptsQopAuth);
        }

        LdapBindReply bound = await exchange.BindWithRequestAsync(
            messageId => LdapDigestMd5Bind.AnswerRequest(exchange.Writer, messageId, initial.ServerSaslCredentials, credentials.UserName, credentials.Password, "ldap/" + host, clientNonce, answersQopAuth)!,
            cancellationToken).ConfigureAwait(false);
        if (bound.IsSuccess)
        {
            if (!answersQopAuth)
            {
                StartConfidentialityLayer(exchange, initial.ServerSaslCredentials, credentials, clientNonce);
            }
        }

        return bound;
    }

    // The bind's result when the initial reply is not saslBindInProgress: a plain success skipped the mechanism,
    // so it is refused as Protocol Error rather than treated as bound; null when a challenge came.
    private static LdapBindReply? FailureWithoutChallenge(LdapBindReply initial) =>
        initial.Status == LdapBindReplyStatus.Answered && initial.ResultCode == LdapBindReply.SaslBindInProgress ? null
            : initial.IsSuccess ? new LdapBindReply(LdapBindReplyStatus.Answered, WinLdapResultText.ProtocolError)
            : initial;

    // The first DIGEST-MD5 bind: the OpenLDAP dialect sends no credentials field (BL-5907), WinLDAP an empty one.
    private static byte[] InitialDigestRequest(LdapBerWriter writer, int messageId, bool acceptsQopAuth) =>
        acceptsQopAuth
            ? LdapRequests.SaslBindWithoutCredentials(writer, messageId, LdapDigestMd5Bind.Mechanism)
            : LdapDigestMd5Bind.InitialRequest(writer, messageId);

    // A challenge with no answer: WinLDAP's Operations Error, the OpenLDAP dialect's Local Error (ADR-1302).
    private static LdapBindReply UnanswerableChallenge(bool acceptsQopAuth) =>
        new(LdapBindReplyStatus.Answered, acceptsQopAuth ? WinLdapResultText.LocalError : WinLdapResultText.OperationsError);

    // Seals the rest of the session when the answer to the challenge chose qop=auth-conf.
    private static void StartConfidentialityLayer(LdapExchange exchange, byte[] challenge, NetworkCredential credentials, string clientNonce)
    {
        if (LdapDigestMd5Bind.SecurityLayerFor(challenge, credentials.UserName, credentials.Password, clientNonce) is { } layer)
        {
            exchange.StartSecurityLayer(layer, WinLdapResultText.Unavailable);
        }
    }

    /// <summary>The NTLMSSP negotiate (type 1) message with no domain and no workstation.</summary>
    private static byte[] NegotiateMessage()
    {
        byte[] message = new byte[NegotiateMessageLength];
        System.Text.Encoding.ASCII.GetBytes(NtlmSignature).CopyTo(message, 0);
        message[8] = 1;
        BitConverter.GetBytes(NegotiateFlags).CopyTo(message, 12);
        message[20] = NegotiateMessageLength;
        message[28] = NegotiateMessageLength;
        return message;
    }
}
