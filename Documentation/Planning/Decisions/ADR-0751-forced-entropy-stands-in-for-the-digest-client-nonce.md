# ADR-0751 — The debug build's CURL_ENTROPY stands in for the Digest and DIGEST-MD5 client nonce

- Status: Accepted
- Date: 2026-10-21
- Task: BL-2834
- Decided by Claude under Stewart's delegation.

## Context

Upstream cases 159, 823, 832, 835, 869, 878, 881, 907, 934, 937, 1100 and 1647 name `Debug` with `!SSPI` and
`digest` or `NTLM`: they are recorded on a debug build running curl's own Digest, DIGEST-MD5 and NTLM code. 823, 869
and 907 set `CURL_ENTROPY=12345678`, which fixes `Curl_rand` (lib/rand.c), so DIGEST-MD5's `cnonce` - 16 bytes from
`Curl_rand_hex` - is `34333231353332313633323137333231`, the sequence ADR-0678 describes. 1647 sets one too, but its
Digest answer is not checked. The others set nothing: they name `Debug` only because upstream runs them on debug builds.
NTLM's client challenge already has its seam (`Curl.Ntlm`'s `INtlmRandomSource`), and 159 and 1100 send NTLMv1
answers, which use none.

## Decision

1. `PlatformFor` reports `Debug` to a case naming `!SSPI` with `digest` or `NTLM` (`NamesHandBuiltAuthentication`,
   exactly the twelve cases); every other case is judged as before.
2. `DigestClientNonce.ForBuild(bool, Func<int, byte[]>)` and `HexFrom(Func<int, byte[]>)` draw the client nonce from
   the bytes given: 16 bytes in lower-case hex for DIGEST-MD5 (and the SSPI build's HTTP Digest), 12 in base64 for
   curl's own HTTP Digest. `EngineComposition.CreateHttpAuthenticator`, `CreateSaslAuthenticator` and
   `CreateProtocolHandlers`, and `CurlComposition.CreateRunner`, take an optional `digestRandomBytes`; `null`, as
   every production caller passes, is `RandomNumberGenerator.GetBytes`, so neither the tool nor `Libcurl` reads
   `CURL_ENTROPY`.
3. The conformance harness passes `CURL_ENTROPY`'s sequence through `ForcedEntropyWebSocketRandomSource`, one per
   curl run, as the debug build's one per process.
4. `CreateSaslAuthenticator` answers DIGEST-MD5 in curl's own form, not SSPI's, over the hand-built NTLM contexts, as
   `CreateHttpAuthenticator` already does for HTTP Digest (`MatchesSspiBuild`): such a run is matched with the non-SSPI
   build. Production on Windows still answers as SSPI.

## Consequences

832, 835, 878, 881, 934, 937 and 1647 pass and are listed. 823 and 869 still differ on the answer to `rspauth`, 907
on the quoting of a `"` in the user name and realm, and 159 and 1100 on NTLM behaviour; each is a product fix filed
as its own task. The libtest engine (`ManagedCurlEngine`) does not take the seam yet; 1647 needs none.
