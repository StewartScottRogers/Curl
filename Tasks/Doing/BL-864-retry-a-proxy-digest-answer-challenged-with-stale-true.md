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
completed:
---
# BL-864 — Retry a proxy Digest answer challenged with stale=true

## Goal

When a CONNECT that sent a Digest answer gets a `407` whose Digest challenge carries `stale=true`, `TcpConnector` answers again with the new nonce, as curl 8.21.0 does, instead of ending with exit 7.

## Context

BL-602 (see `Tasks/Doing/BL-602-answer-a-connect-tunnel-s-407-with-the-proxy-auth-scheme-cho.md`, Notes, or its archived copy once done) and `Documentation/Planning/Decisions/ADR-0186-a-connect-tunnel-answers-a-407-through-the-injected-proxy-authenticator.md` made `TcpConnector` (`Curl.Networking.UnitLibrary/TcpConnector.cs`) answer a CONNECT `407` through `HttpProxyTunnelOptions.ProxyAuthenticator`. The measurements in BL-602's Notes were taken with curl 8.21.0 (mingw, Schannel) and `Record-CurlExchange.ps1` acting as the proxy; follow the same method.

ADR-0186 decision 5: a `407` to a CONNECT that already sent a credential ends with `CurlExitCode` 7 (`CONNECT tunnel failed, response 407`). curl instead re-answers a Digest challenge carrying `stale=true` with the new nonce. Check whether the authenticator in `HttpProxyTunnelOptions.ProxyAuthenticator` (the ranked authenticator from `Curl.Authentication.UnitLibrary`, referenced by the test project only) already exposes a stale challenge; if it does not, the stale check is made in `TcpConnector` from the `Proxy-Authenticate` header, since `Curl.Authentication.UnitLibrary` is outside this task's touches. If the ADR's decision 5 text needs amending, add an ADR note in the task Notes and file a docs follow-up rather than widening touches.

## Acceptance criteria

- [ ] Measured with `Record-CurlExchange.ps1 -Connections 3` as the proxy: `curl -s -S -p -x http://127.0.0.1:<P> -U u:p --proxy-digest http://example.test/`, replies `407` Digest `nonce="a"`, then `407` Digest `nonce="b", stale=true`, then `HTTP/1.1 200 Connection established`. Every CONNECT request, the connection count and the exit code are pinned in this task's Notes with the curl version; also measured: a second `stale=true` challenge, to learn how many stale retries curl makes.
- [ ] `TcpConnector` sends a second Digest answer using nonce `b` for the stale challenge, and the tunnel opens; a `TcpConnectorTests` case shows the requests match the measured ones.
- [ ] `TcpConnector` never sends more Digest answers than curl did in the measurement; after that, it fails with `CurlExitCode` 7 and curl's stderr text, shown by a test.
- [ ] A `407` without `stale=true` after a Digest answer still ends with exit 7, as in BL-602.
- [ ] `dotnet build Curl.Networking.UnitLibrary -warnaserror` and `dotnet build Curl.Networking.UnitTests -warnaserror` are clean.
- [ ] `dotnet test --filter "TestCategory!=Integration"` is green across the solution; no new test needs `TestCategory=Integration`.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` reports 100% line and 100% branch coverage and no failing member for `Curl.Networking.UnitLibrary`.

## Notes

## Log

- 2026-09-29: Created.
- 2026-10-01: Backlog -> Doing.
