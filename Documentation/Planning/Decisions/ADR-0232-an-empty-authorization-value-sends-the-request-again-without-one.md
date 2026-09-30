# ADR-0232 — An empty `Authorization` value sends the request again without one

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-844.

## Context

Under `--anyauth -u :`, a `401` offering only Negotiate, with no ticket, ended Curl's
transfer on that first `401`: `IHttpAuthenticator` answering `null` means "no retry" to the
HTTP handler, and a Negotiate context without a ticket makes no token. curl sends one more
request.

### Measured on 2026-09-29, `Record-CurlExchange.ps1`

`--anyauth -u : -v -s` against `401` + `WWW-Authenticate: Negotiate` + `Content-Length: 4` +
`deny`, no ticket, on curl 8.21.0 Schannel (Windows) and curl 8.18.0 OpenSSL + MIT
Kerberos 1.22.1 (Linux, WSL): the same on both, but for the failure line's words (ADR-0231).

1. The first request carries no `Authorization`. The `401`'s head is written with no failure
   line, then `Ignoring the response-body`, `setting size while ignoring`, the head's end and
   `Connection #0 to host ... left intact`.
2. `Issue another request to this URL: '...'`, `Reusing existing http: connection with host
   ...`, the context's failure line, `Server auth using Negotiate with user ''`, and the same
   request again, still without `Authorization`.
3. The second `401`'s status line, the failure line again just before
   `WWW-Authenticate: Negotiate`, the rest of the head, the body `deny` to stdout, exit 0.

(The recording server closes each connection after its response, so curl's reused
connection died and it sent the second request once more on a fresh one, stepping a
context and writing both lines again; that is the server's doing, not the exchange's.)

This is libcurl's shape: before the challenge `--anyauth` has picked nothing, so the `401`
steps no context (`Curl_input_negotiate` runs only when Negotiate was already picked);
`Curl_http_auth_act` then picks Negotiate and asks for the request again whatever the
context will make; the request steps the context as it goes out (`Curl_output_negotiate`),
sending no header when it fails; and the second `401`, with Negotiate now picked, steps a
context that fails, which sets `authproblem` and ends the transfer on that response.

## Decision

- **The empty string is the answer "again, without a header".** `IHttpAuthenticator`'s
  `CreateAuthorizationAsync` may answer a challenge with `string.Empty`: the scheme was
  picked but made no credential, so the request is sent again with no `Authorization`. No
  new type or member: the HTTP head formatter already sends no header for an empty value,
  and `null` keeps meaning "take the response". Only `RankedHttpAuthenticator` answers it,
  for Negotiate picked after a challenge when it was not the one scheme allowed
  (`--anyauth`, `--negotiate --basic` and the like), with `-u` given; `--negotiate` alone
  still answers `null`, as its first request already picked Negotiate.
- **It cannot loop.** When the request that sent the empty value draws another `401`, the
  handler asks `ContinueAuthorizationAsync` with the empty value as what was sent;
  `RankedHttpAuthenticator` steps a Negotiate context without answering, as
  `Curl_input_negotiate` does, so `-v` shows the failure, and answers `null`. The handler
  also takes an empty answer from `ContinueAuthorizationAsync` as `null`, so no
  authenticator can make it send the same request forever.
- **The handler counts an empty value as Negotiate picked** for
  `Server auth using Negotiate with user '...'` and for where a `401`'s lines go.
- **A retry's lines go where the retry is.** When the request that drew a `401` was not sent
  with Negotiate picked, what the authenticator reports while answering it is recorded and
  written before the retry, after `Reusing existing ...` and before `Server auth using ...`,
  as curl steps that context on the way out; when it was, the lines are written straight to
  the transfer, just before the Negotiate challenge header (ADR-0231). A recorded line of a
  `401` that draws no retry is written at once, where it was written before.

## Consequences

- `--anyauth -u :` without a ticket sends the two requests curl sends and ends on the second
  `401` with exit 0, with curl's `-v` lines in curl's order on both platforms.
- A request sent again on a fresh connection keeps its recorded lines and writes them
  again, as curl steps a context for each send.
- The WebSocket handler asks only before any challenge, so it never sees the empty answer.
