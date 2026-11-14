namespace Curl.Ntlm;

/// <summary>
/// Answers a CHALLENGE message with the AUTHENTICATE message curl 8.21.0's
/// <c>Curl_auth_create_ntlm_type3_message</c> builds (<c>lib/vauth/ntlm.c</c> lines 609 to
/// 667): NTLMv2 and LMv2 responses when the challenge carries
/// <see cref="NtlmNegotiateFlags.NegotiateExtendedSessionSecurity" />, with an 8-byte client
/// challenge from <paramref name="randomSource" /> and the current time to the whole second
/// (curl's <c>time(NULL)</c>) from <paramref name="timeProvider" />; otherwise NTLMv1 and
/// LM responses, with that flag cleared from the flags it writes. The session key buffer
/// stays empty, as curl leaves it.
/// </summary>
/// <param name="timeProvider">The clock the NTLMv2 timestamp is read from.</param>
/// <param name="randomSource">The source of the NTLMv2 client challenge.</param>
public sealed class NtlmChallengeAnswerer(TimeProvider timeProvider, INtlmRandomSource randomSource)
{
    /// <summary>
    /// The AUTHENTICATE message answering <paramref name="challenge" /> for curl's
    /// <c>-u</c> <paramref name="userName" /> (split by <see cref="NtlmUserName.SplitDomain" />)
    /// and <paramref name="password" />.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="challenge" /> is null.</exception>
    /// <exception cref="ArgumentException">
    /// The challenge's <see cref="NtlmChallengeMessage.ServerChallenge" /> is not
    /// <see cref="NtlmResponseComputation.ChallengeLength" /> bytes. A decoded challenge
    /// always has that many, so only one built by hand reaches this.
    /// </exception>
    public NtlmAuthenticateMessage Answer(NtlmChallengeMessage challenge, string userName, string password) =>
        AnswerCore(challenge, userName, password, false, out _);

    /// <summary>
    /// Answers as <see cref="Answer" /> does and also exchanges a session key: when the
    /// challenge negotiates <see cref="NtlmNegotiateFlags.NegotiateKeyExchange" />, draws a
    /// 16-byte exported session key from the random source (after the client challenge) and
    /// writes <c>RC4K(KeyExchangeKey, ExportedSessionKey)</c> as the message's
    /// EncryptedRandomSessionKey; without that flag the key exchange key itself is the
    /// exported session key.
    /// </summary>
    /// <param name="challenge">The CHALLENGE to answer.</param>
    /// <param name="userName">curl's <c>-u</c> user name.</param>
    /// <param name="password">The password.</param>
    /// <param name="exportedSessionKey">The 16-byte key both ends derive sign and seal keys from.</param>
    /// <exception cref="ArgumentNullException"><paramref name="challenge" /> is null.</exception>
    public NtlmAuthenticateMessage AnswerWithKeyExchange(NtlmChallengeMessage challenge, string userName, string password, out byte[] exportedSessionKey) =>
        AnswerCore(challenge, userName, password, true, out exportedSessionKey);

    private NtlmAuthenticateMessage AnswerCore(NtlmChallengeMessage challenge, string userName, string password, bool exchangeKey, out byte[] exportedSessionKey)
    {
        ArgumentNullException.ThrowIfNull(challenge);
        (string domain, string user) = NtlmUserName.SplitDomain(userName);
        NtlmNegotiateFlags flags = challenge.Flags;
        NtlmResponses responses;
        if (flags.HasFlag(NtlmNegotiateFlags.NegotiateExtendedSessionSecurity))
        {
            byte[] clientChallenge = new byte[NtlmResponseComputation.ChallengeLength];
            randomSource.Fill(clientChallenge);
            DateTimeOffset now = DateTimeOffset.FromUnixTimeSeconds(timeProvider.GetUtcNow().ToUnixTimeSeconds());
            responses = NtlmResponseComputation.ComputeV2(user, domain, password, challenge.ServerChallenge, clientChallenge, now, challenge.TargetInformation);
        }
        else
        {
            responses = NtlmResponseComputation.ComputeV1(password, challenge.ServerChallenge, flags);
            flags &= ~NtlmNegotiateFlags.NegotiateExtendedSessionSecurity;
        }

        byte[]? encrypted = null;
        exportedSessionKey = responses.KeyExchangeKey;
        if (exchangeKey && flags.HasFlag(NtlmNegotiateFlags.NegotiateKeyExchange))
        {
            exportedSessionKey = new byte[NtlmResponseComputation.SessionKeyLength];
            randomSource.Fill(exportedSessionKey);
            encrypted = NtlmResponseComputation.EncryptSessionKey(responses.KeyExchangeKey, exportedSessionKey);
        }

        return new NtlmAuthenticateMessage(
            flags,
            responses.LmChallengeResponse,
            responses.NtChallengeResponse,
            domain,
            user,
            NtlmAuthenticateMessage.CurlWorkstation,
            encrypted);
    }
}
