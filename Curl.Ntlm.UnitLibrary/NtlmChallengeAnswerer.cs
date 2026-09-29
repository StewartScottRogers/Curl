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
    public NtlmAuthenticateMessage Answer(NtlmChallengeMessage challenge, string userName, string password)
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

        return new NtlmAuthenticateMessage(
            flags,
            responses.LmChallengeResponse,
            responses.NtChallengeResponse,
            domain,
            user,
            NtlmAuthenticateMessage.CurlWorkstation);
    }
}
