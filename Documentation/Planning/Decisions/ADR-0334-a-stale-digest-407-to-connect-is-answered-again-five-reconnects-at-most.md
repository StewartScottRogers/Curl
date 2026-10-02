# ADR-0334 — A stale Digest `407` to CONNECT is answered again, with five reconnects at most

- **Status:** Accepted
- **Date:** 2026-10-01

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-864.
Amends ADR-0186 decision 5 ("One answer per connect") for a Digest challenge carrying `stale=true`.

## Context

ADR-0186 decision 5 made a `407` to a CONNECT that already sent a credential the tunnel's
failure, exit 7 `CONNECT tunnel failed, response 407`. curl 8.21.0 (mingw, Schannel), measured
2026-10-01 with `Record-CurlExchange.ps1` as the proxy (BL-864 Notes), does not give up when
the `407` answering a Digest value carries a Digest challenge with `stale=true`: it writes
`Connect me again please`, dials again and sends a fresh Digest answer (new cnonce,
`nc=00000001`) for the new nonce, with no `Digest authentication problem, ignoring.` line.
It does so for every stale challenge, but dials the proxy at most six times in all: after the
sixth connection's `407` it writes `Connect me again please` once more and fails with exit 7
`Could not connect to server`. A `407` without `stale=true` after a stale answer is still
given up on with the problem line and `CONNECT tunnel failed, response 407`.

## Decision

- `DigestStaleChallenge.IsOfferedIn` finds a Digest challenge whose `stale` parameter is
  `true` (any case, quoted or not) among the `Proxy-Authenticate` values.
- `TcpConnector.AnswerProxyChallengeAsync` answers such a `407` to a sent `Digest` value
  afresh through `IHttpAuthenticator.CreateAuthorizationAsync`, as it answers a first
  challenge. The check lives in `Curl.Networking.UnitLibrary` because the ranked
  authenticator in `Curl.Authentication.UnitLibrary` does not expose staleness, and the
  fresh answer it makes is already the one curl sends.
- `ConnectTunnelVerboseLines.ReportReplyHead` writes no Digest problem line for a stale challenge.
- `TcpConnector.ConnectThroughProxyAsync` dials the proxy again at most five times
  (`MaxProxyReconnects`); a sixth reconnect fails with `CurlExitCode.CouldntConnect` and
  `Could not connect to server`, after the `Connect me again please` line.

## Consequences

- curl's request bytes for nonce `b` (cnonce injected) are pinned by
  `TcpConnectorTests.ConnectAsync_WithProxyDigest_WhenTheAnswerIsChallengedStale_AnswersAgainWithTheNewNonce`.
- The reconnect cap applies to every reason to reconnect, not only stale Digest; no other
  measured exchange comes near it.
- Stale challenges answered on a connection the proxy keeps open are not capped: the
  recorder closes every connection, so curl's limit there was not measured.

## Alternatives considered

- Teach `RankedHttpAuthenticator.ContinueAuthorizationAsync` about `stale=true`: cleaner, but
  `Curl.Authentication.UnitLibrary` was outside BL-864's touches, and the HTTP handler's own
  stale handling would want the same change measured for a `401`.
- Cap stale answers rather than connections: curl's message, `Could not connect to server`,
  is its reconnect limit's, not an authentication one.
