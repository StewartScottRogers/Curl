using System.Net;
using System.Security.Cryptography;
using Curl.Ntlm;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Ldap;

/// <summary>
/// Binds with <c>-u</c>'s credentials for <c>--ntlm</c>, <c>--negotiate</c> and <c>--digest</c> as
/// curl's WinLDAP build does (ADR-1246): the Sicily NTLM bind (tag <c>0x8a</c>) or the
/// DIGEST-MD5 SASL bind (tag <c>0xa3</c>), never a simple bind, so the password never crosses
/// the wire in clear.
/// </summary>
/// <remarks>
/// The NTLM bind sends the negotiate message, answers the challenge in the reply with an NTLM
/// response built from the <c>-u</c> credentials (tag <c>0x8b</c>), and fails with <c>Local Error</c>
/// when the reply carries no decodable challenge; a server that refuses either step fails with
/// its own result code.
/// </remarks>
internal static class LdapAuthenticatedBind
{
    private const string NtlmSignature = "NTLMSSP\0";

    private const int NegotiateFlags = unchecked((int)0xa208b207);

    private const int NegotiateMessageLength = 32;

    private const int ClientNonceBytes = 16;

    private const int SicilyResponseChoice = 11;

    /// <summary>Binds as <paramref name="kind" /> says.</summary>
    /// <param name="kind">One of <see cref="LdapBindKind.Ntlm" />, <see cref="LdapBindKind.Negotiate" /> or <see cref="LdapBindKind.Digest" />.</param>
    /// <param name="exchange">The session to bind on.</param>
    /// <param name="credentials">The <c>-u</c> credentials.</param>
    /// <param name="host">The URL's host, for the digest-uri <c>ldap/host</c>.</param>
    /// <param name="cancellationToken">Cancels the bind.</param>
    /// <returns><see langword="null" /> once bound; otherwise the failed transfer's result.</returns>
    public static async ValueTask<TransferResult?> BindAsync(LdapBindKind kind, LdapExchange exchange, NetworkCredential credentials, string host, CancellationToken cancellationToken)
    {
        LdapBindReply reply = kind == LdapBindKind.Digest
            ? await DigestAsync(exchange, credentials, host, cancellationToken).ConfigureAwait(false)
            : await NtlmAsync(exchange, credentials, cancellationToken).ConfigureAwait(false);
        return reply.Status != LdapBindReplyStatus.Answered ? LdapBind.WinLdapBindFailed(WinLdapResultText.Timeout)
            : reply.IsSuccess ? null
            : await LdapBind.UnbindAndFailAsync(exchange, LdapBind.WinLdapBindFailed(reply.ResultCode), cancellationToken).ConfigureAwait(false);
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
            .Answer(challenge, credentials.UserName, credentials.Password);
        return answer.TryEncode(out byte[]? response)
            ? await exchange.SicilyBindAsync(string.Empty, SicilyResponseChoice, response, cancellationToken).ConfigureAwait(false)
            : new LdapBindReply(LdapBindReplyStatus.Answered, WinLdapResultText.LocalError);
    }

    /// <summary>Names DIGEST-MD5, then answers the challenge of the <c>saslBindInProgress</c> reply.</summary>
    private static async ValueTask<LdapBindReply> DigestAsync(LdapExchange exchange, NetworkCredential credentials, string host, CancellationToken cancellationToken)
    {
        LdapBindReply initial = await exchange.BindWithRequestAsync(messageId => LdapDigestMd5Bind.InitialRequest(exchange.Writer, messageId), cancellationToken).ConfigureAwait(false);
        if (initial.Status != LdapBindReplyStatus.Answered || initial.ResultCode != LdapBindReply.SaslBindInProgress)
        {
            return initial;
        }

        string clientNonce = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(ClientNonceBytes));
        byte[]? probe = LdapDigestMd5Bind.AnswerRequest(exchange.Writer, 0, initial.ServerSaslCredentials, credentials.UserName, credentials.Password, "ldap/" + host, clientNonce);
        return probe is null
            ? new LdapBindReply(LdapBindReplyStatus.Answered, WinLdapResultText.LocalError)
            : await exchange.BindWithRequestAsync(
                messageId => LdapDigestMd5Bind.AnswerRequest(exchange.Writer, messageId, initial.ServerSaslCredentials, credentials.UserName, credentials.Password, "ldap/" + host, clientNonce)!,
                cancellationToken).ConfigureAwait(false);
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
