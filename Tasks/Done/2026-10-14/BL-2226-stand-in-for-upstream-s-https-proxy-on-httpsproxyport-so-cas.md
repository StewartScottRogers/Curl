---
id: BL-2226
title: Stand in for upstream's HTTPS proxy on %HTTPSPROXYPORT so cases 1586, 1630-1632, 1647 and 3401 run
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2225]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-14
completed: 2026-10-14
---
# BL-2226 — Stand in for upstream's HTTPS proxy on %HTTPSPROXYPORT so cases 1586, 1630-1632, 1647 and 3401 run

## Goal

Stand in for upstream's `https-proxy` server (stunnel in front of the proxy-mode `sws`) on `%HTTPSPROXYPORT`, so cases 1586, 1630, 1631, 1632, 1647 and 3401 run instead of skipping "the harness has no value for %HTTPSPROXYPORT".

## Context

Source: Stewart's 2026-10-14 request to raise the gap office's behaviour score (aim 97-99%) by making the shared in-process harness run cases it leaves unmeasured; running `Curl.Conformance.UnitTests` on 2026-10-14 names these six (counted in the 8.22.0 gap run 2026-10-13_1406 under `needs-server:https-proxy`-style reasons, `https` and `ftp`). The gap tool uses `Curl.Conformance.UnitLibrary`.

Upstream's `servers.pm` (curl-8_22_0) starts `https-proxy` as stunnel on `%HTTPSPROXYPORT` in front of the http proxy on `%PROXYPORT`. Model it like `MailTlsServerConnector` / `HttpsServerConnector`: `TlsRelayConnection` / `TlsServerStream` with `test-localhost.pem` from `%CERTDIR/certs/` (see its CLAUDE.md text), relaying the decrypted bytes to the `sws` emulation's proxy-port handling (`SwsHttpServerConnector.ProxyPort`, `ProxyReceivedBytes`). `%HTTPSPROXYPORT` has a value only when the caller names a certificate directory, as `%HTTPSPORT` does. Depends on BL-2225 (CONNECT through the HTTP proxy to the HTTPS stand-in), which 1647 (https-proxy to an https origin) needs.

## Acceptance criteria

- [x] A connection to `%HTTPSPROXYPORT` runs the TLS handshake and then behaves as the proxy port; tests in a new `HttpsProxyServerConnectorTests` pin the handshake, a plain proxied GET, a CONNECT, and the unreachable cases (no certificate directory means no value).
- [x] Screening lets `https-proxy` cases run; cases 1586, 1630, 1631, 1632, 1647 and 3401 are no longer screened out. Each that passes is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; each that runs but fails is named in Notes as a real gap, with a follow-up task per distinct cause.
- [x] Platform-neutral tests; `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary` shows no member outside a gate; `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` is green; `Curl.Conformance.UnitLibrary/CLAUDE.md` describes the new connector.

## Notes

- `%HTTPSPROXYPORT` is 9010: the next port after `%HTTP2PORT` (9009) that no stand-in uses. Upstream allocates ports dynamically, so any free value is faithful.
- The runner puts `HttpsProxyServerConnector` in front of every other stand-in only for a case naming `https-proxy`, with `test-localhost.pem` as stunnel presents it.
- Results (Windows): 1630, 1631, 1632 and 3401 pass and are listed. 1586 runs and fails - Curl sends the second proxied GET twice (`<verify><proxy>` differs at line 13) - filed as BL-2241. 1647 still skips, for its libtest `<client><tool>` (not a proxy gap).
- `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary`: 0 failing members.

## Log

- 2026-10-14: Created.
- 2026-10-14: Backlog -> Doing.
- 2026-10-14: Doing -> Done. https-proxy stand-in on %HTTPSPROXYPORT; 1630-1632 and 3401 pass, 1586 filed as BL-2241
