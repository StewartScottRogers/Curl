# ADR-0025 — Digest answers as curl's own Digest code does, on every platform

- **Status:** Accepted
- **Date:** 2026-09-26

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions").

## Context

BL-217 answers Digest challenges behind `IHttpAuthenticator` (ADR-0014). The task asks for
RFC 7616: MD5, SHA-256, SHA-512-256, the `-sess` variants, `qop=auth` and `userhash`.

curl has two Digest implementations. Most builds, including the OpenSSL build on Linux and
macOS, use its own code (`lib/vauth/digest.c`). Builds with `USE_WINDOWS_SSPI` - the
Schannel builds on Windows, including the mingw reference build (ADR-0018) - hand Digest to
Windows' WDigest package instead (`lib/vauth/digest_sspi.c`).

Measured on 2026-09-26 against a loopback server that answers every request with
`401` and one `WWW-Authenticate: Digest ...` header (commands and full values in BL-217's
Notes):

| Challenge | mingw curl 8.21.0 (SSPI) | curl 8.21.0, OpenSSL (`curlimages/curl:8.21.0`) |
| --- | --- | --- |
| MD5, `qop="auth,auth-int"` | `username="Mufasa",realm="...",...,qop="auth",opaque="..."` - no blanks, qop quoted, 32 hex-digit cnonce | `username="Mufasa", realm="...", ..., qop=auth, response="...", opaque="..."` - 16-character base64 cnonce |
| `algorithm=SHA-256` | no answer, exit 94 | answered |
| `algorithm=SHA-512-256`, `userhash=true` | no answer, exit 94 | answered, user name hashed |
| `algorithm=md5-sess`, `qop="auth-int"` | no answer, exit 94 | answered |
| `algorithm=MD5-sess`, no qop | answered, `algorithm=MD5-sess` | no answer (curl rejects `-sess` without qop) |

The SSPI output is produced by the operating system, not by curl, so there is no curl source
to follow for it, and it cannot do most of what the task asks for.

## Decision

- `DigestAuthenticator` answers exactly as curl 8.21.0's own Digest code does, on every
  platform: the challenge read by `Curl_auth_decode_digest_http_message` and
  `Curl_auth_digest_get_pair`, the value built by `auth_create_digest_http_message`, its
  field order, `", "` separators, unquoted `qop`, `auth_digest_string_quoted` escaping,
  and a cnonce of 12 random bytes in base64 (`DigestClientNonce.CreateRandom`).
- The user name and password are hashed and sent in the platform credential encoding,
  as Basic sends them (ADR-0022).
- The authenticator keeps no state (ADR-0014), so every answer carries `nc=00000001`, and
  curl's check that a second challenge without `stale=true` means bad credentials is left
  to whoever decides retries.
- SHA-512/256 is not in the base class library, so `Sha512Slash256` implements it from
  FIPS 180-4; no package is added.

## Consequences

- On Linux and macOS, Digest values match the platform's curl byte for byte apart from
  the random cnonce.
- On Windows, Curl answers SHA-256, SHA-512-256, `userhash` and `auth-int` challenges that
  the reference build refuses with exit 94, and formats the MD5 answer as curl's own code
  does rather than as WDigest does. A server cannot tell the difference: both are valid
  RFC 7616 answers, and a random cnonce means no script can depend on the bytes. A
  challenge with `-sess` and no qop, which WDigest answers, gets no answer.
- If a Windows user ever needs WDigest's exact behaviour, that is a separate implementation
  behind the same interface.

## Alternatives considered

- **Reproduce WDigest on Windows.** Matches the reference build's bytes, but the task's
  SHA-2 and `userhash` requirements would have to be dropped on Windows, and WDigest's
  behaviour has no source to follow, only measurements.
- **Call WDigest through SSPI.** Needs platform interop that native AOT and Linux builds
  must work around, for a value no server distinguishes.
