namespace Curl.Ntlm;

/// <summary>
/// One AV_PAIR of a CHALLENGE message's target information (MS-NLMP section 2.2.2.1): its
/// <see cref="Id" /> and its raw <see cref="Value" />. An identifier MS-NLMP does not name is
/// kept as its number.
/// </summary>
/// <param name="Id">The pair's <c>AvId</c>.</param>
/// <param name="Value">The pair's value bytes, <c>AvLen</c> of them.</param>
public sealed record NtlmAvPair(NtlmAvId Id, byte[] Value);
