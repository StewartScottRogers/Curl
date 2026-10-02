---
id: BL-864
title: Retry a proxy Digest answer challenged with stale=true
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-602]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-29
completed: 2026-10-01
---
# BL-864 — Retry a proxy Digest answer challenged with stale=true

## Goal

When a CONNECT that sent a Digest answer gets a `407` whose Digest challenge carries `stale=true`, `TcpConnector` answers again with the new nonce, as curl 8.21.0 does, instead of ending with exit 7.

## Context

BL-602 (see `Tasks/Doing/BL-602-answer-a-connect-tunnel-s-407-with-the-proxy-auth-scheme-cho.md`, Notes, or its archived copy once done) and `Documentation/Planning/Decisions/ADR-0186-a-connect-tunnel-answers-a-407-through-the-injected-proxy-authenticator.md` made `TcpConnector` (`Curl.Networking.UnitLibrary/TcpConnector.cs`) answer a CONNECT `407` through `HttpProxyTunnelOptions.ProxyAuthenticator`. The measurements in BL-602's Notes were taken with curl 8.21.0 (mingw, Schannel) and `Record-CurlExchange.ps1` acting as the proxy; follow the same method.

ADR-0186 decision 5: a `407` to a CONNECT that already sent a credential ends with `CurlExitCode` 7 (`CONNECT tunnel failed, response 407`). curl instead re-answers a Digest challenge carrying `stale=true` with the new nonce. Check whether the authenticator in `HttpProxyTunnelOptions.ProxyAuthenticator` (the ranked authenticator from `Curl.Authentication.UnitLibrary`, referenced by the test project only) already exposes a stale challenge; if it does not, the stale check is made in `TcpConnector` from the `Proxy-Authenticate` header, since `Curl.Authentication.UnitLibrary` is outside this task's touches. If the ADR's decision 5 text needs amending, add an ADR note in the task Notes and file a docs follow-up rather than widening touches.

## Acceptance criteria

- [x] Measured with `Record-CurlExchange.ps1 -Connections 3` as the proxy: `curl -s -S -p -x http://127.0.0.1:<P> -U u:p --proxy-digest http://example.test/`, replies `407` Digest `nonce="a"`, then `407` Digest `nonce="b", stale=true`, then `HTTP/1.1 200 Connection established`. Every CONNECT request, the connection count and the exit code are pinned in this task's Notes with the curl version; also measured: a second `stale=true` challenge, to learn how many stale retries curl makes.
- [x] `TcpConnector` sends a second Digest answer using nonce `b` for the stale challenge, and the tunnel opens; a `TcpConnectorTests` case shows the requests match the measured ones.
- [x] `TcpConnector` never sends more Digest answers than curl did in the measurement; after that, it fails with `CurlExitCode` 7 and curl's stderr text, shown by a test.
- [x] A `407` without `stale=true` after a Digest answer still ends with exit 7, as in BL-602.
- [x] `dotnet build Curl.Networking.UnitLibrary -warnaserror` and `dotnet build Curl.Networking.UnitTests -warnaserror` are clean.
- [x] `dotnet test --filter "TestCategory!=Integration"` is green across the solution; no new test needs `TestCategory=Integration`.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1` reports 100% line and 100% branch coverage and no failing member for `Curl.Networking.UnitLibrary`.

## Notes

- Measured curl 8.21.0 (x86_64-w64-mingw32, Schannel) 2026-10-01 with `Record-CurlExchange.ps1 -Port 18864 -Connections 3` as the proxy, `-s -S -v -p -x http://127.0.0.1:18864 -U u:p --proxy-digest http://example.test/`, each `407` being `HTTP/1.1 407 Proxy Authentication Required` + `Proxy-Authenticate: Digest realm="r", nonce="<n>", qop="auth"[, stale=true]` + `Content-Length: 0` + `Connection: close`; the exit 56 after the opened tunnel is the recorder closing before the GET inside it is answered:
  - 407 `nonce="a"`, 407 `nonce="b", stale=true`, 200: three connections, three CONNECTs: none; `Proxy-Authorization: Digest username="u",realm="r",nonce="a",uri="example.test:80",cnonce="d287019335fcd47238a8b68d206b7c2b",nc=00000001,response="261498e9636d101495da39e5ec43444f",qop="auth"`; `Proxy-Authorization: Digest username="u",realm="r",nonce="b",uri="example.test:80",cnonce="9063b3b916723d2bbffbb44838396fe1",nc=00000001,response="c9f932550dc5be461ee0942fc8ad7d7d",qop="auth"` (each CONNECT otherwise `Host: example.test:80`, `User-Agent: curl/8.21.0`, `Proxy-Connection: Keep-Alive`). `-v`: `Connect me again please` after each `407`, no `Digest authentication problem, ignoring.` for the stale one; tunnel opens; exit 56.
  - A second stale challenge (`nonce="c", stale=true`, then 200, `-Connections 5`): four connections, answers for `a`, `b`, `c`, each a fresh cnonce with `nc=00000001`; tunnel opens.
  - Stale every time (`b` to `m` stale, `-Connections 14`): six connections - none, `a`, `b`, `c`, `d`, `e` - and six `Connect me again please` lines, then `curl: (7) Could not connect to server`, exit 7. So curl's limit is on reconnects (five), not on stale answers: the sixth stale `407` is not answered.
  - `a`, `b` stale, then `c` without stale: three connections, `Digest authentication problem, ignoring.`, `curl: (7) CONNECT tunnel failed, response 407`, exit 7.
- The ranked authenticator does not expose staleness (`DigestChallengeBuilder` ignores `stale`), and `Curl.Authentication.UnitLibrary` is outside the touches, so the check is `DigestStaleChallenge.IsOfferedIn` in `Curl.Networking.UnitLibrary`; a stale `407` to a sent `Digest` value is answered through `CreateAuthorizationAsync`, as a first challenge is. Decision recorded in ADR-0334 (Decided by Claude under Stewart's delegation).
- ADR note: ADR-0186 decision 5 ("One answer per connect") is amended by ADR-0334 for stale Digest; the edit to ADR-0186 itself is BL-1147, since that file is outside this task's touches. The same stale retry for a `401` in the HTTP handler is BL-1148.
- Choice: the reconnect cap (`MaxProxyReconnects = 5`) applies to every redial, as curl's message is its reconnect limit's. Stale challenges on a connection the proxy keeps open are not capped: the recorder closes every connection, so that limit could not be measured.
- Tests: `TcpConnectorTests.ProxyDigestStale` pins the measured CONNECTs for nonce `b` byte for byte (curl's own-code Digest format, ADR-0025, cnonces injected), the six-connection limit, a non-stale `407` after a stale answer, and Basic not renewed; `DigestStaleChallengeTests` the parser. Networking 2450 passed; full fast run green; `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` 100% line, 100% branch, 0 failing (a first run flagged `UdpChannelOpener.OpenFrom` line 53, whose test skips when the port after a taken one is busy; the re-run covered it).
## Log

- 2026-09-29: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. A proxy's stale Digest 407 to CONNECT is answered again with the new nonce, five reconnects at most, as curl 8.21.0
