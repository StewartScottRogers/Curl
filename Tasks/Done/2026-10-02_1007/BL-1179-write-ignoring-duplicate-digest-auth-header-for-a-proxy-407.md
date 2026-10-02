---
id: BL-1179
title: Write Ignoring duplicate digest auth header. for a proxy 407 when no proxy credentials were given
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1175]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1179 — Write Ignoring duplicate digest auth header. for a proxy 407 when no proxy credentials were given

## Goal

Under `-v`, a forward proxy's `407` whose `Proxy-Authenticate` headers carry more than one Digest challenge writes `* Ignoring duplicate digest auth header.` before each later one even when no proxy credentials (`-U`) were given, as curl 8.21.0 does for the origin's `401` without `-u` (BL-1175 Notes).

## Context

BL-1175 made `HttpAuthProblemLines` write the duplicate line, but `HttpProtocolHandler.ReportAuthProblemLines` reads the proxy's headers only when `plan.ProxyAuthRequest` is set, which it is not without proxy credentials. Measure first with `Record-CurlExchange.ps1`: `curl -s -S -v -x http://127.0.0.1:<P> http://example.invalid/` against a `407` with `Proxy-Authenticate: Digest realm="r", nonce="a", Digest realm="s", nonce="c"`.

## Acceptance criteria

- [x] Measured and pinned in Notes with the curl version.
- [x] An `HttpProtocolHandlerTests` case shows the duplicate line written where curl writes it for a proxy `407` with no `-U`.
- [x] `dotnet test --filter "TestCategory!=Integration"` is green and `Measure-CodeQuality.ps1` reports no failing member for `Curl.Protocol.Http.UnitLibrary`.

## Notes

Measured 2026-10-02, curl 8.21.0 (Windows, Schannel), with `Record-CurlExchange.ps1`,
`curl -s -S -v -x http://127.0.0.1:P http://example.invalid/`, no `-U`, exit 0:

- One `407` header `Proxy-Authenticate: Digest realm="r", nonce="a", Digest realm="s", nonce="c"`:
  `* Ignoring duplicate digest auth header.` just before that header; no problem line.
- The two challenges in two `Proxy-Authenticate` headers: the duplicate line just before the second.

Finding: no production change was needed. BL-1175's "known gap" was wrong:
`HttpProtocolHandler` builds `plan.ProxyAuthRequest` for every forward proxy
(`ProxyAuthRequestOf`), with or without a proxy credential, so `ReportAuthProblemLines`
already reads the proxy's head without `-U`. The new test
`ExecuteAsync_ProxyDigest407WithoutProxyCredentialsVerbose_WritesDuplicateDigestLineBeforeTheProxyChallengeHeader`
pins curl's measured output and passed against the code as it was. The `feature` pipeline
shrank to measure, test and verify, since there was nothing to plan or implement.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. A proxy 407 with two Digest challenges and no -U is pinned writing curl's duplicate digest line under -v
