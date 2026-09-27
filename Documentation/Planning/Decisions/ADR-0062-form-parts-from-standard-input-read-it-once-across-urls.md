# ADR-0062 — `-F` parts from standard input read it once across URLs

- **Status:** Accepted
- **Date:** 2026-09-27

Decided by Claude under Stewart's delegation (task BL-311, 2026-09-27).

## Context

BL-275 taught `MultipartFormBodyBuilder` to read `-F name=@-` and `-F name=<-` parts from an
injected standard-input stream; BL-311 has `CurlCommandRunner` give its default builder the
stream `Program` opens. Measured on 2026-09-26 with curl 8.21.0 (`/mingw64/bin/curl`):

| Case | curl sends |
| --- | --- |
| `printf 'hello\nworld' \| curl -F 'a=<-' URL1 URL2` | URL1: the input, `Content-Length: 159`. URL2: declares `Content-Length: 159` but sends an empty part (148 bytes), exit 26. |
| `curl -F a=@- -F 'b=<-' URL < file` | declares both parts the file's size (280), sends the second empty, exit 26. |

Both failures are curl sizing a part from the input before reading it and then finding it
already consumed.

## Decision

1. The runner's default builder reads standard input as a stream, whole, the first time a
   part asks for it, as curl does for a pipe. The first URL's request matches curl byte for
   byte (the boundary aside).
2. A later part or a later URL gets what the first left: nothing. It is sent as an empty
   part with a `Content-Length` that matches the body, and the transfer exits 0.
3. Curl's mismatched `Content-Length` and exit 26 are not reproduced.

## Consequences

- `-F a=@- URL` and `-F "a=<-" URL`, the forms scripts use, behave as curl does.
- A script that sends one piped form to two URLs, or two standard-input parts, gets exit 0
  and a well-formed empty part where curl gets exit 26. No script can depend on the
  second body, which curl never sends intact.

## Alternatives considered

- **Reproduce the exit 26.** Needs the builder to size parts before reading them and to
  send a request that violates its own `Content-Length`; it copies a defect, and a server
  sees a truncated body.
- **Buffer standard input and resend it to every URL.** Diverges from curl in the other
  direction: curl never sends the input twice.
