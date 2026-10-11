# ADR-0476: a Digest cnonce follows the matched build - hex through SSPI, base64 from curl's own code

- Status: Accepted
- Date: 2026-10-11
- Decided by Claude under Stewart's delegation.
- Task: BL-2048 (audit finding AF-0155); amends ADR-0025 and ADR-0472.

## Context

ADR-0472 made `DigestAuthenticator` write SSPI's `Authorization` layout where curl 8.21.0's
Schannel build is matched, but the composition root kept drawing the cnonce from
`DigestClientNonce.CreateRandom` (12 random bytes in base64, 16 characters), which is what
curl's own `vauth/digest.c` sends in the OpenSSL build. The result mixed two builds. AF-0155
measured the Schannel build with `Record-CurlExchange.ps1` against
`WWW-Authenticate: Digest realm="r", nonce="abc", qop="auth"`: three runs, each
`cnonce="..."` 32 lower-case hex digits (e.g. `889edd63675c2328372ba0489df3b38f`), which
ADR-0025's table had already recorded for SSPI.

## Decision

`DigestClientNonce.ForBuild(matchesSspiBuild)` picks the cnonce source:
`CreateRandomHex` (32 lower-case hex digits) for the SSPI layout, `CreateRandom` (16 base64
characters) for curl's own. `CurlComposition.CreateHttpAuthenticator` passes the same
`matchesSspiBuild` value to both `ForBuild` and `DigestAuthenticator`, so the layout and the
cnonce always come from one build.

## Consequences

- On Windows, Curl's Digest answer with `qop` matches the Schannel build's in shape; only the
  random cnonce and the response hash over it differ, as between two runs of curl.
- Off Windows, and under the hand-built NTLM contexts the upstream conformance runner uses,
  nothing changes.
- `DigestAuthenticator` still takes the nonce source as a `Func<string>`, so its tests keep a
  fixed cnonce.
