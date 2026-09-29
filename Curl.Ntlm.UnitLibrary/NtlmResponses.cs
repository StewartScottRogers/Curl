namespace Curl.Ntlm;

/// <summary>
/// What answering a challenge produces (MS-NLMP section 3.3): the two responses an
/// AUTHENTICATE message carries, and the keys session security derives from them
/// (section 3.4), which SMB signing needs and HTTP does not.
/// </summary>
/// <param name="LmChallengeResponse">The LM, LMv2 or client-challenge LM response.</param>
/// <param name="NtChallengeResponse">The NTLMv1, NTLM2 session or NTLMv2 response.</param>
/// <param name="SessionBaseKey">The 16-byte <c>SessionBaseKey</c>.</param>
/// <param name="KeyExchangeKey">
/// The 16-byte <c>KeyExchangeKey</c> (section 3.4.5.1): it encrypts the exported session
/// key under <see cref="NtlmNegotiateFlags.NegotiateKeyExchange" />
/// (<see cref="NtlmResponseComputation.EncryptSessionKey" />) and is the exported session
/// key without it.
/// </param>
public sealed record NtlmResponses(
    byte[] LmChallengeResponse,
    byte[] NtChallengeResponse,
    byte[] SessionBaseKey,
    byte[] KeyExchangeKey);
