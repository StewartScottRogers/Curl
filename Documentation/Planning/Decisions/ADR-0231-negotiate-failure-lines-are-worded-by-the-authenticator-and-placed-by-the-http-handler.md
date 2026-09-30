# ADR-0231 — A Negotiate context's `-v` failure line is worded by the authenticator and placed by the HTTP handler

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-843.

## Context

ADR-0176 answers `--negotiate` with no ticket as curl does, sending nothing, but wrote none
of curl's `-v` lines. curl 8.21.0 writes the context's failure before the first request and
again for the 401, and `Server auth using Negotiate with user '...'` before the request.
The failure comes from the security context's step, which lives behind
`ISecurityContext` in `Curl.Authentication.UnitLibrary`; the lines land in the HTTP
handler's output, which the authenticator cannot see and which it is asked about before the
connection even opens.

### Measured on 2026-09-29, curl 8.21.0 Schannel (Windows), `Record-CurlExchange.ps1`

`--negotiate -u : -v` against `401` + `WWW-Authenticate: Negotiate`, no ticket:

```
* using HTTP/1.x
* InitializeSecurityContext failed: SEC_E_NO_CREDENTIALS (0x8009030e) - No credentials are available in the security package
* Server auth using Negotiate with user ''
> GET / HTTP/1.1
...
* Request completely sent off
< HTTP/1.1 401 Unauthorized
* InitializeSecurityContext failed: SEC_E_NO_CREDENTIALS (0x8009030e) - No credentials are available in the security package
< WWW-Authenticate: Negotiate
< Content-Length: 4
```

- With no `-u` the lines are the same: `Curl_input_negotiate` still steps a context for the
  401 (so the failure is written) though libcurl answers no 401 without a user.
- `-u D\u:p` writes `with user 'D\u'`: the user name as given, domain included.
- When the 401 carries `WWW-Authenticate: Basic ...` before `WWW-Authenticate: Negotiate`, the
  failure line lands just before the Negotiate header, not the first challenge.
- `-u u:p` writes `Server auth using Basic with user 'u'`, `--digest` `... Digest ...` before
  both requests (the first sends no `Authorization`), and `--oauth2-bearer`
  `Server auth using Bearer with user ''`: the same line serves every scheme (BL-954).

The Linux line (MIT Kerberos, curl 8.18.0) is ADR-0176's:
`gss_init_sec_context() failed: No credentials were supplied, or the credentials were unavailable or inaccessible. SPNEGO cannot find mechanisms to negotiate. `
with its trailing space.

## Decision

- **The authenticator words the failure.** `NegotiateHttpAuthenticator` reports
  `NegotiateFailureLines.For(status, wordsAsSspi)` to `HttpAuthRequest.Events` whenever a
  step fails. The wording is the platform curl's: SSPI on Windows, GSS-API elsewhere (a
  constructor argument overrides it for tests). `SEC_E_NO_CREDENTIALS` and MIT's
  no-credentials pair are measured; the other statuses are worded with the SSPI status and
  MIT major message each maps to - `NoMechanism` as `SEC_E_SECPKG_NOT_FOUND` / "An unsupported
  mechanism was requested", `Refused` as `SEC_E_LOGON_DENIED` / "Unspecified GSS failure",
  `MalformedToken` as `SEC_E_INVALID_TOKEN` / "Invalid token was supplied" - since no curl
  on hand can be made to produce them. macOS uses the GSS-API wording; its Heimdal build is
  not measured.
- **`HttpAuthRequest` carries the sink.** It gains `Events`, an init-only
  `ITransferEvents` defaulting to `NoTransferEvents.Instance`, so no authenticator, fake or
  caller changes. The HTTP handler sets it, and the WebSocket handler since BL-955 (below).
- **The HTTP handler places the lines.** The first request's value is made before the
  connection opens, into an `HttpInfoLineRecorder`; its lines are written after
  `using HTTP/1.x` and before the request, then `Server auth using Negotiate with user '...'`
  when the request goes out with Negotiate picked (a Negotiate value, or the first request
  with `--negotiate` alone). For a 401, `HttpResponseHeadReader.DefersFrom` holds back the
  `WWW-Authenticate` header offering Negotiate and every header after it until the retry is
  decided, so the authenticator's lines, written straight to the transfer's events, land
  just before that header.
- **No `-u` still steps.** With `--negotiate` alone and no `-u`, a 401 offering Negotiate
  steps a context through `NegotiateHttpAuthenticator.StepWithoutAnsweringAsync` - reported,
  disposed, never sent - as `Curl_input_negotiate` does. `--anyauth` does not, as libcurl's
  pick before the challenge is not Negotiate there.
- **Only Negotiate's line for now.** The `Server auth using <scheme>` line for Basic, Digest,
  Bearer and NTLM, and `Proxy auth using ...`, follow the same pattern but change `-v` output
  across the HTTP tests and depend on conditions (custom `Authorization`, redirects to
  another host) to measure first; BL-954 does them.

### Amendment: the WebSocket upgrade (BL-955, 2026-09-29)

Decided by Claude under Stewart's delegation. Measured with curl 8.21.0 Schannel and
`Record-CurlExchange.ps1`: `--negotiate -u : -v ws://...` (and with no `-u`) against the same
`401 Negotiate` writes `using HTTP/1.x`, the failure, `Server auth using Negotiate with user ''`,
the upgrade request, `Request completely sent off`, the status line, the failure again just
before `WWW-Authenticate: Negotiate`, the rest of the head with `Refused WebSocket upgrade: 401`
before its blank line, and `closing connection #0`. The upgrade is still sent only once
(ADR-0228). The error message differs from HTTP's refusal: `curl: (22)` carries the context's
failure, not `Refused WebSocket upgrade: 401`, and a `101` that closes with no frames ends
`curl: (52)` with it too, since curl's first `failf` fills the error buffer.

- `WsProtocolHandler` asks for the upgrade's value with `Events` set to a `WsInfoLineRecorder`,
  writes what it recorded after `using HTTP/1.x`, then the `Server auth using Negotiate` line
  when Negotiate is picked (`WsNegotiateInfoLines.PicksNegotiate`, the HTTP rule for a first
  request).
- A `401` offering Negotiate, to an upgrade that sent no `Authorization` and allows Negotiate,
  is stepped through `CreateAuthorizationAsync` with the head's challenges; the value is
  discarded, and the lines it reports are written before the first header offering Negotiate.
  An upgrade that did send a value is not stepped again: no curl on hand has a ticket to show
  what it writes then.
- A failed transfer's message is the first line the upgrade's step reported, else the 401
  step's, else the handler's own.

## Consequences

- `--negotiate -v` with no ticket writes both platform curls' lines in their order.
- A header deferral means a `no chunk, no close, no size` line for a 401 answered by
  Negotiate would be written before the deferred headers rather than after them; no test or
  measurement covers that 401 shape yet.
- `HttpAuthRequest` equality now includes `Events`; a test comparing whole requests
  compares with `Events` reset.

## Alternatives considered

- **Carry the failure's text on `SecurityContextStep`.** Every context implementation would
  word curl's lines, and the step would carry presentation text; the status is enough to
  word them in one place.
- **Have the HTTP handler word the failure.** It would need the context's status, which
  `IHttpAuthenticator` does not return, and would put SSPI and GSS-API text in the HTTP
  library.
- **Write the 401's line at the end of the head.** Simpler, but curl writes it at the
  header, before the ones after it.
