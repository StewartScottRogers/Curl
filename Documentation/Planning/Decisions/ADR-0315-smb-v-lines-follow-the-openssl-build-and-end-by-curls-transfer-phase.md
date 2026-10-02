# ADR-0315 — SMB's `-v` lines follow the OpenSSL build and end by curl's transfer phase

- **Status:** Accepted
- **Date:** 2026-10-01
- **Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions").

## Context

BL-598 registers `SmbProtocolHandler` for `smb` and `smbs` in `Curl.Console` and lists both in
`-V` on every platform (ADR-0200, ADR-0021). The Windows Schannel build has no SMB, so its `-v`
cannot be measured; ADR-0200 already says SMB's own text follows a build that has it.

Measured on 2026-10-01 with Ubuntu's curl 8.18.0 (OpenSSL, WSL) and `Record-CurlExchange.ps1
-Script` serving `SmbRecordedExchange`'s replies (BL-598 Notes), after the connector's `Trying` and
`Established connection` lines:

- a download: `{ [11 bytes data]`, then `* shutting down connection #0`, exit 0;
- an upload: no data line, then `* shutting down connection #0`;
- no user (exit 67): `* closing connection #0`;
- a too-small frame answering the negotiate (exit 56): `* too small NetBIOS frame size 5`, then
  `* closing connection #0`;
- a missing file (exit 78): `* shutting down connection #0`, no text line;
- `-T -` (exit 55): `* SMB upload needs to know the size up front`, then
  `* shutting down connection #0`;
- a path with no share (exit 3, nothing connected): `* missing share in URL path for SMB`, then
  `* closing connection #-1`.

## Decision

1. `-v` shows a failure's text as an info line only when curl reports it through `failf`: every
   SMB text except those that are `curl_easy_strerror`'s for their exit code
   (`SmbMessages.IsVerboseLine`).
2. A failure before the session is set up (curl's connect phase: no user, the negotiate, the
   session setup) ends with `closing connection #N`; any outcome after it, success included, ends
   with `shutting down connection #N`. This is libcurl's `multi_done`: the connect phase ends a
   failed transfer as premature, the `DOING` phase does not.
3. A path refused before connecting ends with `closing connection #-1`, as the HTTP and LDAP
   handlers already do for their own pre-connect refusals.
4. Each non-empty read response is reported as data received, giving `{ [N bytes data]`; an
   upload reports no data sent, as curl's SMB code writes its requests without the data trace.
5. The same lines on every platform; connection and TLS lines stay each platform's own.

## Consequences

- The Windows build of Curl prints lines the Windows build of curl never can, as ADR-0200 already
  accepts for SMB as a whole.
- A transfer cut by `-m` is not covered here: the handler's cancellation ends it, and the console's
  own timeout handling prints its lines.

## Alternatives considered

- **Always `closing connection`, as for every failure in gopher.** Contradicts the measured exit-78
  and exit-55 runs, which end `shutting down`.
- **No end line at all.** Every other handler prints one; a script diffing `-v` would see it missing.
