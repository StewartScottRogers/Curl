# ADR-0361 — A stale Digest `401` or forward proxy `407` is answered again, without limit

- **Status:** Accepted
- **Date:** 2026-10-02

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-1148.
The HTTP handler's counterpart of ADR-0334, which covers a CONNECT's `407`.

## Context

`HttpProtocolHandler` took a `401` (or a forward proxy's `407`) to a request that sent a Digest
answer as the result, because `RankedHttpAuthenticator.ContinueAuthorizationAsync` answers
nothing for Digest. curl 8.21.0 (mingw, Schannel), measured 2026-10-02 with
`Record-CurlExchange.ps1` (BL-1148 Notes), answers a Digest challenge carrying `stale=true`
afresh - a new cnonce, `nc=00000001`, the new nonce - on the same connection when it stays
open and on a new one when it closes. Against a run of stale challenges on closing
connections it answered all 29 the recorder served and then waited for a connection the
recorder no longer accepted: unlike the CONNECT tunnel's five reconnects, the origin retry
has no limit of its own.

## Decision

- `HttpDigestStaleChallenge.IsOfferedIn` finds a Digest challenge whose `stale` parameter is
  `true`, read as `DigestStaleChallenge` reads it in `Curl.Networking.UnitLibrary`. It is a
  copy, not a shared type: protocol libraries reference no other library but the
  abstractions, and the authenticator does not expose staleness.
- `HttpProtocolHandler.AnswerChallengesAsync` answers challenges to a sent `Digest ` value
  through `IHttpAuthenticator.CreateAuthorizationAsync` when they mark it stale, as it
  answers a first challenge. It serves both the origin's `401` and a forward proxy's `407`.
- No cap: every stale challenge is answered, as curl does.

## Consequences

- A server that marks every nonce stale keeps the transfer going until something else ends
  it (`-m`, the server), as with curl.
- The `Digest authentication problem, ignoring.` line curl writes for a non-stale refusal is
  still missing from the HTTP handler; BL-1170 adds it.

## Alternatives considered

- Expose staleness from `Curl.Authentication.UnitLibrary` so both callers share one parser:
  lost because it widens a shared contract for an eleven-line reader, and that library is
  outside BL-1148's `touches`.
- Cap stale answers like the tunnel's five reconnects: lost because curl measured no cap.
