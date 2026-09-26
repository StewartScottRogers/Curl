# ADR-0022 — Basic and Bearer credentials are sent as the platform curl sends them

- **Status:** Accepted
- **Date:** 2026-09-26

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions").

## Context

BL-216 builds the `Authorization` values for Basic and Bearer behind `IHttpAuthenticator`
(ADR-0014). Two questions were open: which bytes a non-ASCII user name, password or token
becomes, and when a value is sent before any challenge and when only after one.

Measured with the reference build (ADR-0018: curl 8.21.0, mingw), `Record-CurlExchange.ps1`
against a loopback server, arguments passed from PowerShell on a system whose ANSI code page
is Windows-1252 (the full list is in BL-216's Notes):

| Arguments | Authorization sent |
| --- | --- |
| `-u u:p` | `Basic dTpw` |
| `-u é:p€` | `Basic 6TpwgA==`, bytes `E9 3A 70 80`: Windows-1252, not UTF-8 |
| `-u Ω中Ā:p` | `Basic Tz9BOnA=`, bytes `4F 3F 41 3A 70`: `O?A`, Windows' best-fit mapping |
| `--oauth2-bearer té` | `Bearer t` then the single byte `E9` |
| `-u u:p --oauth2-bearer tok` | `Bearer tok` |
| `-u u:p --anyauth`, then a `Basic` challenge | nothing, then `Basic dTpw` |
| `-u u:p --digest`, then a `Basic` challenge | nothing, and no second request |

The mingw build receives its arguments in the system ANSI code page and sends the bytes as
they are. curl on Linux and macOS receives the argument's bytes, which in a UTF-8 locale
are UTF-8.

## Decision

- **Encoding.** `CredentialEncoding.ForPlatform(isWindows)` gives the system ANSI code page
  on Windows (`CodePagesEncodingProvider.Instance.GetEncoding(0)`, whose Windows-1252
  encoder best-fits exactly as measured) and UTF-8 elsewhere. `Curl.Console` passes it to
  `BasicAndBearerAuthenticator`. Basic base64-encodes `user:password` in it; Bearer
  returns the token's bytes as one character per byte, which the HTTP handler's Latin-1
  header writer puts on the wire unchanged.
- **Before the first response** a value is sent only when the allowed set is exactly
  `Basic` or exactly `Bearer`, as libcurl sends only when exactly one scheme is wanted.
- **After a challenge** the scheme is picked from the allowed schemes the challenges
  offer, in libcurl's order: Negotiate, Bearer, Digest, NTLM, Basic. Only a Basic or
  Bearer pick is answered; Digest, NTLM and Negotiate are not built yet, so picking one
  sends nothing. Challenges are read as curl reads them: split at every comma, blanks
  skipped, a known scheme name in any case followed by a blank or the end.
- **Mapping for the option parser.** In curl `--oauth2-bearer` sets the wanted schemes
  from nothing, not on top of the Basic default: `-u u:p --oauth2-bearer tok` wants only
  Bearer and sends `Bearer tok`. So `--oauth2-bearer` with no other scheme option must
  give `AuthSchemes = Bearer`, not `Basic | Bearer`, which sends nothing before a
  challenge.
- **Retries** are not the authenticator's call: it keeps no state (ADR-0014). Whether a
  401 after a pre-emptive Basic is retried (curl does not) is the HTTP handler's.

## Consequences

- Basic and Bearer bytes match the reference for ASCII and non-ASCII credentials on
  Windows, and match curl on Linux and macOS in a UTF-8 locale.
- A system with a different ANSI code page gets that code page, as the reference build
  does there.
- `CodePagesEncodingProvider` is in the shared framework; no package is added.

## Alternatives considered

- **UTF-8 everywhere.** Simpler, but `-u é:p` would send `w6k6cA==` where the reference
  sends `6Tpw`: a different header for any non-ASCII credential on Windows.
- **Always send Basic before a challenge when a credential exists.** Sends a header the
  reference does not send for `--anyauth`, `--digest` and `--basic --digest`.
