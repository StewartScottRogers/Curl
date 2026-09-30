# ADR-0228 — A WebSocket upgrade answers no 401: curl 8.21.0 sends it once

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-951.

## Context

`WsProtocolHandler` sends the upgrade with the pre-emptive `Authorization` from
`IHttpAuthenticator.CreateAuthorizationAsync` (ADR-0227) and fails any status but `101` with
exit 22, `Refused WebSocket upgrade: <code>` (ADR-0128). BL-951 was filed on the assumption
that curl drives the upgrade through its HTTP code and so answers a `401` as HTTP does: the
challenge through `CreateAuthorizationAsync`, a second leg through
`ContinueAuthorizationAsync` (ADR-0181), the upgrade sent again.

### Measured on 2026-09-29

curl 8.21.0 (mingw, Schannel), `Record-CurlExchange.ps1` against `ws://127.0.0.1:18951/c`,
with a second connection or the same connection ready to answer `101`:

| Options | First reply | Requests sent | Outcome |
| --- | --- | --- | --- |
| `--digest -u u:p` (`-Script`, one connection) | `401`, `WWW-Authenticate: Digest realm="r", nonce="abc", qop="auth"` | 1, no `Authorization` | exit 22, `Refused WebSocket upgrade: 401` |
| `--anyauth -u u:p` | `401` with Digest, NTLM and Basic challenges | 1, no `Authorization` | exit 22, same message |
| `--ntlm -u u:p` | `401`, bare `NTLM` | 1, `Authorization: NTLM <Type 1>` | exit 22, same message |
| `--ntlm -u u:p` | `401`, `NTLM <Type 2>` | 1, `Authorization: NTLM <Type 1>` | exit 22, same message |
| `--basic -u u:p` | `401` with the same challenges | 1, `Authorization: Basic dTpw` | exit 22, same message |

curl never sends the upgrade a second time, on the same connection or a new one: the
WebSocket code refuses any status but `101` as soon as the head is read, before libcurl's
HTTP authentication would pick the next request.

## Decision

- A `ws://` or `wss://` upgrade is sent once. A `401` - whatever its challenges, whatever was
  sent - fails the transfer with exit 22 and `Refused WebSocket upgrade: 401`, exactly as any
  other status but `101` does. `WsProtocolHandler` never calls
  `IHttpAuthenticator.ContinueAuthorizationAsync`, and calls `CreateAuthorizationAsync` once,
  with no challenges, for the pre-emptive value (Basic, Bearer, NTLM's Type 1, Negotiate's
  first token).
- `Curl.Protocol.Ws.UnitTests` pins it: a Digest `401` followed by a `101` on the same
  connection sends one request and fails with 22; an NTLM Type 1 answered by a Type 2 `401`
  does the same and never asks for a continuation.

## Consequences

- The task's premise is withdrawn rather than implemented; the drop-in contract is curl's
  measured behaviour, and a retry would send a request curl does not send.
- Digest, NTLM's second leg and a Negotiate continuation are not usable over `ws://`, as they
  are not with curl 8.21.0.
- curl's `-v` also writes `Server auth using <scheme> with user '<user>'` before a request
  that carries or prepares a credential, and `Basic authentication problem, ignoring.` after a
  refused Basic value; neither is written for any scheme yet. That is an HTTP and WebSocket
  verbose-output gap, not an upgrade-retry one.

## Alternatives considered

- **Answer the 401 as HTTP does.** Lost: curl 8.21.0 does not, so scripts would see an extra
  request and a different exit code.
