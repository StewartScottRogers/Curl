# ADR-0873: A SHA Digest challenge fails with exit 94 where the SSPI build is matched

- **Status:** Accepted
- **Date:** 2026-10-23

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-3281
(found by BL-3233's differential case `auth-digest256`).

## Context

`DigestAuthenticator` answered SHA-256 and SHA-512-256 challenges, and their `-sess` variants,
on every platform. That is right for curl's own Digest code, which the OpenSSL builds on Linux
and macOS use. On Windows, curl's Schannel build hands Digest to SSPI, and SSPI does not answer
those algorithms. Measured with `Record-CurlExchange.ps1` against a 401 carrying the challenge,
using `-sS --digest -u u:p`:

| Challenge `algorithm=` | Windows inbox curl 8.21.0 (Schannel) | curl 8.22.0 (Schannel, ucrt64) |
| --- | --- | --- |
| `SHA-256`, then a second `MD5` challenge (`auth-digest256`) | one request, exit 94 | one request, exit 94 |
| `SHA-256`, `sha-256`, `SHA-256-sess`, `SHA-512-256`, `SHA-512-256-sess` | one request, exit 94 | - |
| `MD5`, `MD5-sess` | answered, exit 0 | - |

On every exit 94, stderr is `curl: (94) An authentication function returned an error` and
stdout is empty.

## Decision

Where `DigestAuthenticator` matches the SSPI build (`matchesSspiBuild`, set on Windows by the
engine's composition), a first challenge naming any SHA algorithm throws
`HttpAuthenticationFailedException` with exit 94 and curl's message. The HTTP handler turns that
into the transfer's failure, as it does for NTLM's SSPI failure. `DigestAlgorithm.IsAnsweredBySspi`
marks MD5 and MD5-sess as the only ones SSPI answers. Off Windows nothing changes: curl's own
Digest code answers SHA-256 and SHA-512-256.

The tests in `DigestAuthenticatorTests` pin each platform's answer separately, with
`OSCondition`: exit 94 on Windows, and the SHA-256 answer elsewhere.

## Consequences

- On Windows the differential case `auth-digest256` matches curl: exit 94 after one request.
  On Linux and macOS, Curl and curl both get `sha256 ok`.
- A Windows user who needs SHA-256 Digest gets curl's Windows behaviour, not a working answer.
  That is the cost of being a drop-in replacement for the platform's curl.
