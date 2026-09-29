# Curl.Ntlm.UnitLibrary

Hand-built NTLM for every platform, per MS-NLMP: encoding and decoding the `NEGOTIATE`,
`CHALLENGE` and `AUTHENTICATE` messages, and computing the NTLMv1, NTLMv2 and session
keys that answer a challenge. It is shared by HTTP and proxy NTLM
(`Curl.Authentication.UnitLibrary`), SASL `NTLM`, SPNEGO (BL-692) and SMB
(`Curl.Protocol.Smb.UnitLibrary`), so it lives in none of them. When SSPI on Windows
answers instead of this library is ADR-0142's routing, not this library's.

Namespace `Curl.Ntlm`. The messages are here (BL-683), written and read as curl 8.21.0's
own `lib/vauth/ntlm.c` writes and reads them: `NtlmNegotiateMessage` (curl's fixed
32-byte Type 1), `NtlmChallengeMessage.Decode` (Type 2, every offset checked, a malformed
message an `NtlmMessageFailure`, never an exception), `NtlmTargetInformation` (its
AV_PAIRs), `NtlmAuthenticateMessage` (Type 3 from supplied responses, within curl's
1024-byte `NTLM_BUFSIZE`) and `NtlmUserName` (curl's `DOMAIN\user` split). The responses
and session keys land under BL-684.

## Rules

- **Base class library plus `Curl.Cryptography.UnitLibrary` only.** No package, and no
  other project reference. The reference to `Curl.Cryptography.UnitLibrary` (for MD4,
  which the BCL lacks) is added by BL-684, the first task that needs it, not before.
- **No network.** The library turns bytes into bytes; it never opens a `Socket` or any
  stream. Its callers carry the tokens over their own connections.
- **Time through `TimeProvider`.** The NTLMv2 timestamp takes the injected
  `TimeProvider`; never `DateTime.Now`.
- **Randomness injected.** The client challenge and the exported session key come from an
  injected source, so MS-NLMP 4.2's test vectors reproduce exactly.
- **Secrets zeroed.** Password hashes, response keys and session keys are cleared with
  `CryptographicOperations.ZeroMemory` once used.
- Tests in `Curl.Ntlm.UnitTests` are platform-neutral and pin MS-NLMP test vectors with
  their section cited beside each.
- Same quality gates as every library: 100% line and branch coverage, cyclomatic
  complexity of at most 10, CRAP of at most 30.
