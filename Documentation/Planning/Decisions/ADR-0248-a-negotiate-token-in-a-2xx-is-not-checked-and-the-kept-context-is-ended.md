# ADR-0248 — A Negotiate token in a 2xx is not checked, and the context kept for it is ended

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-950.

## Context

ADR-0227 keeps a Negotiate context whose token needs another leg in
`NegotiateHttpAuthenticator`, keyed by the `Authorization` value it made, and continues it
only on a 401. With Kerberos mutual authentication the acceptor answers the first token with
a `2xx` carrying `WWW-Authenticate: Negotiate <AP-REP>`; that context was never stepped and
stayed in the dictionary until the process ended. ADR-0227 said curl checks that token.

It does not. curl 8.21.0's lib/http.c hands a `WWW-Authenticate` header to
`Curl_http_input_auth` (and so to `Curl_input_negotiate`) only when the status is 401
(`if((401 == k->httpcode) && HD_IS(hd, hdlen, "WWW-Authenticate:"))`, line 3635), and a
`Proxy-authenticate` header only on a 407 (line 3454). On any other status the header is
written and nothing else: after a token was sent, a non-401 moves the connection's Negotiate
state from `GSS_AUTHDONE` to `GSS_AUTHSUCC` (line 4001) and the context is left on the
connection unstepped. Read from the curl-8_21_0 tag's lib/http.c and lib/http_negotiate.c on
2026-09-29. A real exchange needs a KDC and a service principal, which no lane machine has,
so the behaviour is pinned from the source with scripted tokens.

So a `2xx` whose final token the context would accept and one whose token it would reject
end the same way: the `2xx` is the result, its body is written, exit 0, and `-v` shows no
context failure.

## Decision

- `IHttpAuthenticator` gains `EndAuthorization(string sentAuthorization)`, by default doing
  nothing: the response to the request that sent the value was not its challenge, so no leg
  follows.
- `HttpProtocolHandler` calls it, before deciding on a retry, for the `Authorization` value
  a request sent when the response is not a 401, and for the `Proxy-Authorization` value when
  it is not a 407.
- `RankedHttpAuthenticator` hands it to `NegotiateHttpAuthenticator.EndAuthorization`, which
  takes the context kept for the value out of the dictionary and disposes of it without
  stepping it. The other schemes keep nothing between requests.
- The token in the `2xx` is not stepped, so no failure is reported and the transfer's result
  is the `2xx` whatever the token, as in curl.

## Consequences

- The mutual-authentication token is not verified, exactly as in curl 8.21.0; a forged
  `2xx` is taken as curl takes it.
- A context no longer outlives the response that ends its handshake, as far as the HTTP
  handler reaches. `Curl.Console`'s `AwsSigV4HttpAuthenticator` wraps the ranked
  authenticator and does not forward the call yet, and the WebSocket handler does not make
  it on a 101; both are follow-up work (BL-982), and until then those contexts live until
  the process ends, as before.

## Alternatives considered

- **Step the context with the 2xx's token and fail a rejected one**, as the task first
  proposed. Rejected: curl does not, and a drop-in replacement must not fail a transfer
  curl completes, nor write a failure line curl does not.
- **Make the authenticator `IDisposable` and dispose of every kept context at the end of the
  run.** Leaves contexts alive for the whole run under `-Z` and many URLs, and needs the
  composition to change; ending each handshake where curl's state machine ends it is exact.
