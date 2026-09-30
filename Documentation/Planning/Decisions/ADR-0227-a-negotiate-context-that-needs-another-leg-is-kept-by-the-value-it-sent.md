# ADR-0227 — A Negotiate context that needs another leg is kept, found again by the value it sent

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-842.

## Context

ADR-0176 answers HTTP Negotiate with one leg: `NegotiateHttpAuthenticator` makes a context,
takes its first token and disposes of it. ADR-0181 gave `IHttpAuthenticator`
`ContinueAuthorizationAsync`, which the HTTP handler calls for a 401 to a request that
already sent `Authorization`, and NTLM answers it statelessly: a fresh context stepped
through its Type 1 message and then the Type 2 challenge, because a fresh NTLM context
writes the same Type 1 every time.

Negotiate cannot do that. A Kerberos AP-REQ carries a fresh authenticator (time stamp,
sequence number, sub-session key) every time a context is made, so a second context's token
is not the one the acceptor answered, and SSPI or GSS-API refuse the acceptor's token on it.
When a 401 carries `WWW-Authenticate: Negotiate <token>` - SPNEGO needing another leg, or an
acceptor's reply the context must see - only the context that sent the first token can make
the next one.

curl 8.21.0's `Curl_input_negotiate` (lib/http_negotiate.c) keeps the context on the
connection: a 401 carrying a token steps it with that token and the request is sent again;
a bare `Negotiate` after a token was sent is `CURLE_LOGIN_DENIED`, which the HTTP code turns
into "take the 401", as is a token the context cannot step or a context already complete.
libcurl answers no 401 at all without `-u` (`Curl_http_auth_act` checks for a user first).

### Measured on 2026-09-29

With `Record-CurlExchange.ps1`, a loopback server answering `401 Unauthorized`,
`WWW-Authenticate: Negotiate oRQwEqADCgEBoQsGCSqGSIb3EgECAg==` and the body `deny`, curl
8.21.0 (mingw, Schannel, SSPI) on a Windows 11 machine in no domain, `--negotiate -u : -v`:
one `GET` with no `Authorization`, `InitializeSecurityContext failed: SEC_E_NO_CREDENTIALS`
before it, `InitializeSecurityContext failed: SEC_E_INVALID_HANDLE` after the 401 (curl stepped
a context that never got a handle with the server's token), the body `deny`, exit 0. Curl does
the same: no first token, so no request carries one and the 401 is answered afresh with
nothing. A continuation that succeeds needs a KDC and a service principal, which no lane
machine has; its bytes are pinned from curl's source and RFC 4559, with scripted tokens.

## Decision

- `NegotiateHttpAuthenticator` keeps a context whose step answered `ContinueNeeded` with a
  token, in a concurrent dictionary keyed by the header value that token made. A context that
  completes, fails or makes no token is disposed at once, as before.
- `ContinueAuthorizationAsync(sentAuthorization, challenges)` takes the context that made
  `sentAuthorization` out of the dictionary, steps it with the base64-decoded token of the
  first `Negotiate` challenge, and answers `Negotiate <next token>`; the context is kept again
  if it still needs a leg. No context for the value sent, no `Negotiate` challenge, a bare
  one, a token that is not base64, or a step that fails answers `null`, so the transfer ends
  on the 401 with exit 0, as curl's `LOGIN_DENIED` does; the context is disposed.
- `RankedHttpAuthenticator.ContinueAuthorizationAsync` hands the continuation to Negotiate
  when the request sent a `Negotiate ` value, is for the origin, and `-u` was given; else to
  NTLM as before.
- `IHttpAuthenticator` is unchanged in shape; its remarks now say that a Negotiate context
  awaiting a leg is the one piece of state kept between calls.
- `WsProtocolHandler` calls `CreateAuthorizationAsync`, so `ws://` and `wss://` send the
  pre-emptive Negotiate token as HTTP does. The WebSocket handler still answers no 401 (any
  status but 101 is exit 22), so it has no continuation.

The key is the header value because it is exactly what the contract already hands back
(`sentAuthorization`), so neither the interface nor the HTTP handler changes, and two
transfers under `-Z` each find their own context. Two contexts that make the same first
token (only an NTLM-in-SPNEGO token could, and ADR-0176 suppresses those on Windows) share a
key; the later replaces and disposes the earlier, whose transfer then ends on its 401.

## Consequences

- A multi-leg Negotiate exchange works on the connection it started on, since the HTTP
  handler resends on the same connection as for NTLM.
- A context whose last token drew a `200` is never continued. Under this decision alone it
  stayed in the dictionary until the process ended, at most one per transfer; ADR-0248
  (BL-950) disposes of it once the response is not a challenge.
- A `200` carrying the acceptor's final token (mutual authentication) is not checked, as
  curl 8.21.0 does not check it either; ADR-0248 ends the kept context there instead.

## Alternatives considered

- **Replay a fresh context, as NTLM does.** Rejected: a Kerberos authenticator differs every
  time, so the acceptor's token would not fit the second context.
- **Change `IHttpAuthenticator` to hand the handler an opaque per-connection state.** Every
  authenticator, fake and both handlers would change for what a dictionary keyed by the value
  sent already gives.
- **One slot for the last context.** Simpler, but two transfers under `-Z` would steal each
  other's context.
