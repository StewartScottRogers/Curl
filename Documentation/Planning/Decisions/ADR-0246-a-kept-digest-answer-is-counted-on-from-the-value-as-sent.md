# ADR-0246 — A kept Digest answer is counted on from the value as sent

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-869.

## Context

ADR-0187 answers one 407 and one 401 in either order, each retry keeping the other header as
it was sent. curl 8.21.0 does not send the kept Digest answer again unchanged: it counts its
nonce on to `nc=00000002`, keeps the `cnonce` and sends the hash for that count (measured,
BL-603 Notes, cases `Uu-digest-407d-401d-200` and `Uu-any-401d-407d-200`).
`IHttpAuthenticator` keeps no state between calls (ADR-0014), so it had no way to know the
`cnonce` or the count.

## Decision

- `IHttpAuthenticator` gains `RepeatAuthorization(request, sentAuthorization)`, a default
  interface method that gives the value as sent.
- `DigestAuthenticator.RepeatAuthorization` reads the sent value back - its parameters with
  the same reader as a challenge's (`DigestChallengeParameters.ReadPairs`), its `cnonce` and
  its hexadecimal `nc` - and builds the answer again with `nc` one higher and the request's
  method and target. A value that is not a Digest answer with a `qop`, a `cnonce` and an `nc`,
  or a request with no credential, is sent as it was. `RankedHttpAuthenticator` hands every
  value to it, so every other scheme's value is sent as it was.
- `HttpProtocolHandler` asks for it for the header a retry keeps: the `Authorization` value
  when it answers a 407, the `Proxy-Authorization` value when it answers a 401.

## Consequences

- Both measured cases now match curl byte for byte; a server that insists on an increasing
  `nc` accepts the third request.
- The authenticator stays stateless (ADR-0014): the sent value is the state.
- `Curl.Console`'s `AwsSigV4HttpAuthenticator` wraps the ranked authenticator and does not
  forward the new method yet, so `curl.exe` still sends the kept answer unchanged until a
  follow-up task makes it forward (filed in BL-869's Notes).
- Other resends that keep a header (after a 417, on a fresh connection) still send it as it
  was; curl's behaviour there is not measured.

## Alternatives considered

- **Keep per-transfer Digest state in the authenticator.** Breaks ADR-0014 and needs a
  transfer identity the contract does not have.
- **Keep the challenges in the request plan and ask afresh with a fixed cnonce.** Needs the
  `cnonce` and count threaded through `HttpAuthRequest` for one scheme's sake; the sent value
  already carries both.
