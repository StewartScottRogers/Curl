# ADR-0156 — NTLM hand-builds DES and answers a challenge as curl 8.21.0 does

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-684.

## Context

NTLM needs DES: `LMOWFv1` encrypts `KGS!@#$%` under the two 7-byte halves of the
uppercased password, and `DESL` encrypts the server challenge under the three 7-byte
thirds of a password hash (MS-NLMP sections 3.3.1 and 6). ADR-0118 takes a primitive from
the BCL only when the BCL offers it "with every parameter curl uses" on Windows, Linux and
macOS. The BCL's `System.Security.Cryptography.DES` refuses the four weak and twelve
semi-weak keys: setting `Key` or calling `SetKey` with `0101010101010101` throws
`CryptographicException` ("Specified key is a known weak key for 'DES'"), measured on
.NET 10 on Windows on 2026-09-28. An empty password's LM hash is DES under exactly that
key (seven zero bytes widened with odd parity), and curl 8.21.0 computes it whenever it
answers with NTLMv1 (`Curl_ntlm_core_mk_lm_hash`, `lib/curl_ntlm_core.c`); OpenSSL's
`DES_set_key_unchecked` and CryptoAPI's `CryptImportKey`, which curl uses, both accept it.

Which responses curl sends is in `Curl_auth_create_ntlm_type3_message`
(`lib/vauth/ntlm.c` at `curl-8_21_0`, lines 609 to 667): NTLMv2 and LMv2 when the
CHALLENGE sets `NTLMFLAG_NEGOTIATE_NTLM2_KEY` (MS-NLMP's
`NTLMSSP_NEGOTIATE_EXTENDED_SESSIONSECURITY`), otherwise NTLMv1 and LM with that flag
cleared from the AUTHENTICATE message. It never sends the NTLM2 session response, never
reads `MsvAvTimestamp`, always sends the LMv2 response, and stamps the NTLMv2 blob with
`time(NULL)`, whole seconds. Its strings are C strings: the password's UTF-8 bytes are
each widened to 16 bits rather than converted to UTF-16, and only ASCII `a` to `z` are
uppercased (the user for `NTOWFv2`, the password for `LMOWFv1`); the domain is never
uppercased.

## Decision

1. **DES is hand-built** as `Curl.Cryptography.Des` (FIPS 46-3, every key accepted,
   parity bits ignored, not constant-time like every table DES), pinned by NIST SP 500-20
   known answers, Grabbe's worked example and the BCL's `DES` for the keys it accepts.
   ADR-0118's table gains the row, and its list of primitives that are not constant-time
   becomes Blowfish, CAST-128, RC4, Camellia, ARIA and DES.
2. **`Curl.Ntlm` hashes strings as curl does**: `NtlmOneWayFunctions` (`NTOWFv1`,
   `LMOWFv1`, `NTOWFv2`) widens UTF-8 bytes and uppercases ASCII only. For ASCII this is
   MS-NLMP's definition, and MS-NLMP 4.2's vectors reproduce; for other characters it is
   curl's answer, which is what a drop-in replacement must send.
3. **`NtlmResponseComputation` computes all three MS-NLMP response kinds** - NTLMv1
   (with the `KXKEY` variants for `NEGOTIATE_LM_KEY` and `REQUEST_NON_NT_SESSION_KEY`),
   NTLMv1 with extended session security, and NTLMv2 - with their session base and key
   exchange keys and `EncryptSessionKey` (RC4), because SMB signing and SASL may need
   what curl's HTTP path does not.
4. **`NtlmChallengeAnswerer` makes curl's choice**, with the NTLMv2 client challenge from
   an injected `INtlmRandomSource` and the time from an injected `TimeProvider`,
   truncated to the second.

## Consequences

- An empty or short password works on every platform with the same bytes curl sends.
- `Curl.Ntlm.UnitLibrary` references `Curl.Cryptography.UnitLibrary` (MD4, RC4, DES) and
  nothing else.
- A non-ASCII password hashes to curl's value, not Windows'; against a Windows server
  that fails exactly as curl's own non-SSPI build does.
