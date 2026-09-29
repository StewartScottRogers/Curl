# ADR-0187 — A forward proxy's 407 is answered once, like a 401

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-603.

## Context

A plain `http://` request through `-x` goes to the proxy in absolute form, written by
`HttpProtocolHandler` (FR-090). Until BL-603 the handler sent a pre-emptive
`Proxy-Authorization: Basic` for `-U` whatever `--proxy-digest` or `--proxy-anyauth` said,
and took any `407` as the result. ADR-0186 made CONNECT tunnels answer a `407`; this is the
same for the forward case. curl 8.21.0 (mingw, Schannel) was measured with
`Record-CurlExchange.ps1` as the proxy (the bytes are in BL-603's Notes):

| Run | curl 8.21.0 |
| --- | --- |
| `--proxy-basic`, 407 | `Basic dTpw` up front, no retry; exit 0 with the 407's body. Under `-f` exit 22, `The requested URL returned error: 407`. |
| `--proxy-digest`, 407 Digest | Nothing up front; the retry carries Digest with `uri="/"`, the origin form, not the absolute URL. A new connection when the 407 closes, the same one when it keeps alive. |
| `--proxy-digest`, 407 twice | No third request; exit 0 with the second 407, or exit 22 under `-f`. |
| `--proxy-digest`, 407 offering only Basic | No retry. |
| `--proxy-anyauth`, 407 Basic or Digest | Nothing up front; the retry answers with the offered scheme. A second 407 is the result. |
| `-U u:p -u a:b`, Basic | Both up front, `Proxy-Authorization` first; the 407 is the result. |
| `--proxy-anyauth -u a:b`, 407 then 401 | Origin Basic up front; the retry adds proxy Basic and keeps the origin Basic; the 401 is the result. |
| `--proxy-digest -u a:b --digest`, 407, 401, 200 | Three requests: the 407 answered, then the 401, the proxy answer kept. |
| `--proxy-anyauth -u a:b --digest`, 401, 407, 200 | Three requests: the 401 answered, then the 407, the origin answer kept. |

In the last two runs curl sends the kept Digest answer with `nc=00000002` and a new hash:
libcurl's Digest counts each use of a nonce.

## Decision

- `HttpProtocolHandler` takes the proxy scheme set as an optional constructor argument
  (`proxyAuthSchemes`, Basic by default). `Curl.Console` passes
  `CommandLineOptions.ProxyAuthSchemes`, the same value the tunnel gets through
  `HttpProxyTunnelOptions` (ADR-0186). It is not added to `HttpRequestOptions`: the handler is
  built per option group already, as the tunnel options are, and the contract in
  `Curl.Protocol.Abstractions` stays unchanged.
- The forward proxy's request to the authenticator is the origin's (method, URL, origin-form
  target) with the proxy's credential, no bearer token, the proxy scheme set and
  `IsProxy`. Asked with no challenges it gives the pre-emptive value, which only a Basic pick
  sends.
- A 407 is answered once, on ADR-0034's terms for a 401: only when the request that drew it
  sent no `Proxy-Authorization` (else only a multi-leg handshake goes on, through
  `ContinueAuthorizationAsync`), the body is not a stream, the response has
  `Proxy-Authenticate` values and the authenticator answers them. Each retry keeps the other
  header as it was, so one 407 and one 401 can both be answered in either order.
- A kept Digest answer is sent again unchanged (`nc=00000001`, the same hash), not counted on
  to `nc=00000002`: `IHttpAuthenticator` keeps no state between calls (ADR-0014). Filed as a
  follow-up.

## Consequences

- Basic, Digest and `--proxy-anyauth` through a forward proxy match curl for every measured
  case byte for byte, but for the nonce count of a Digest answer kept across a second
  challenge. A proxy or server that insists on an increasing `nc` refuses that third request
  where curl's succeeds.
- NTLM and Negotiate towards a proxy still send nothing, as `RankedHttpAuthenticator` answers
  neither for a proxy (BL-604); the handler already routes a second leg through
  `ContinueAuthorizationAsync`, so BL-604 needs no handler change for it.
- The `-v` lines `Proxy auth using Digest with user 'u'` and
  `Basic authentication problem, ignoring.` are not reported, as the origin's are not.

## Alternatives considered

- **Add `ProxyAuthSchemes` to `HttpRequestOptions`.** Per transfer rather than per handler,
  but a change to the shared contract for a value that is per option group anyway, and
  `Curl.Protocol.Abstractions` was held by another task.
- **Ask the authenticator afresh for the kept header on every retry.** A new cnonce with
  `nc=00000001`: as far from curl's bytes as resending, and it draws a nonce curl does not.
- **Retry any number of 407s.** curl gives up after the first refused answer.
