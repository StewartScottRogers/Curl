# ADR-0278 — A WebSocket upgrade writes curl's `Server auth using` and Basic or Bearer problem lines

- **Status:** Accepted
- **Date:** 2026-09-30

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-953.

## Context

ADR-0231 and BL-955 gave the WebSocket upgrade Negotiate's `-v` lines only. For every other
scheme `WsProtocolHandler` wrote nothing, while the HTTP handler writes
`Server auth using <scheme> with user '<user>'` (ADR-0231, `HttpAuthUsingLines`).

`Record-CurlExchange.ps1` was run on 2026-09-30 with curl 8.21.0 (Windows, Schannel) against
`ws://127.0.0.1:18953/c -v`, each answered with a 401 unless named:

| Arguments | Lines |
| --- | --- |
| `-u u:p --basic` | `Server auth using Basic with user 'u'` after `using HTTP/1.x`; `Basic authentication problem, ignoring.` between the 401's status line and its `WWW-Authenticate: Basic realm="x"` |
| `-u u:p --digest` | `Server auth using Digest with user 'u'`, no `Authorization` sent |
| `-u u:p --digest`, answered 101 | the same line, and no problem line |
| `-u u:p --ntlm` | `Server auth using NTLM with user 'u'`, then `Authorization: NTLM <Type 1>` |
| `-u u:p --anyauth` | neither line |
| `--oauth2-bearer tok`, 401 `WWW-Authenticate: Bearer` | `Server auth using Bearer with user ''`; `Bearer authentication problem, ignoring.` before the challenge |
| `-u u:p`, 401 with `Digest ...` only | no problem line |
| `-u u:p`, 401 with `Digest realm="x", Basic realm="y"` | the problem line before that header |
| `-u u:p`, 401 with two `WWW-Authenticate: Basic` headers (one lower case) | the problem line before each |
| `-u u:p`, 403 with `WWW-Authenticate: Basic` | no problem line |
| `-u u:p -H "Authorization: X y"` | no `Server auth using` line, but the problem line still written |

This is libcurl's `output_auth_headers` (the line is written only when the picked scheme,
which is the whole allowed set before a challenge, is a single scheme) and
`Curl_http_input_auth` (on a 401, each challenge offering Basic or Bearer when that scheme
was picked reports a problem).

## Decision

1. `WsAuthUsingLines` writes the `Server auth using` line before the upgrade request:
   Negotiate as before; Digest for `--digest` alone with a user; otherwise the scheme the
   `Authorization` value starts with, when it is the one scheme allowed, Basic and Bearer
   silenced when an `-H` value names `Authorization`.
2. `WsAuthProblemLines` writes `<Basic|Bearer> authentication problem, ignoring.` before each
   `WWW-Authenticate` header of a 401, once for each of its comma-separated challenges that
   offers the picked scheme, in any case, when Basic or Bearer is the one scheme allowed and
   there is a user or a bearer token.
3. The lines are decided in the WebSocket library from the request and the value, as the
   HTTP handler decides its own (`HttpAuthUsingLines`), rather than reported by the
   authenticator: a protocol library references no other protocol, and the authenticator's
   `Events` seam (BL-848) carries only what an authenticator itself reports while it steps.
4. The HTTP handler's Basic and Bearer problem lines are not part of this decision.

## Consequences

- `curl -v` on a `ws://` or `wss://` upgrade with `--basic`, `--digest`, `--ntlm`,
  `--oauth2-bearer` or `--anyauth` writes what curl 8.21.0 writes, in its order; the built
  `curl.exe` was compared with real curl for `--basic`, `--digest`, `--ntlm` and `--anyauth`.
