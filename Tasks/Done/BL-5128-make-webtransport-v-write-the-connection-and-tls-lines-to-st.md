---
id: BL-5128
title: Make webtransport -v write the connection and TLS lines to standard error
priority: Low
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Extended.Protocol.WebTransport.UnitLibrary/Session, Curl.Extended.Protocol.WebTransport.UnitTests, Documentation/Conformance/Extended/CurlSurface-Transfer-Realtime.md]
requirement: none
created: 2026-11-13
completed: 2026-11-13
---
# BL-5128 — Make webtransport -v write the connection and TLS lines to standard error

## Goal

`curl -v webtransport://...` writes curl's connection line and the TLS backend's lines to standard error, as every other extension scheme does.

## Context

From the outside fitness review of 2026-11-13 (commit 56cb97ede, `preview` build), finding EXT-053 (Low, Confirmed twice). Part B: judged against the port's own docs and specs, not curl.

**Repro.** With an aioquic WebTransport server on UDP 127.0.0.1:27083: `curl -sS -m 5 -k -v -d v webtransport://127.0.0.1:27083/wt 2>err.txt`

**Expected.** `Documentation/Conformance/Extended/CurlSurface-Transfer-Realtime.md` row `CurlSurface:verbose-webtransport-1` (MUST): `-v` writes `* Established connection to ... (127.0.0.1 port N) from ...` and the TLS backend lines (`* schannel: ` on Windows, `* SSL connection using ` elsewhere); every other extension scheme does (for example signalrs prints `*   Trying`, `* schannel:` lines and `* Established connection`). Standard output holds the body alone.

**Actual.** stdout `echo:v`, exit 0, `err.txt` empty.

**Where to start.** `Curl.Extended.Protocol.WebTransport.UnitLibrary/Session/WebTransportSession.cs` never feeds the QUIC connect or TLS events to `context.Events` or the verbose sink. Look at how `Curl.Extended.Protocol.SignalR.UnitLibrary` does it and do the same. The conformance row currently says "n/a: webtransport runs over HTTP/3 and the harness has no WebTransport reference server until BL-4607 adds one"; a unit test now covers it, so update its "Covered by" cell.

## Acceptance criteria

- [x] A new test file in `Curl.Extended.Protocol.WebTransport.UnitTests` (for example `WebTransportVerboseTests.cs`), using the in-memory QUIC stand-ins already in that project, proves a verbose transfer raises the connection event that renders `* Established connection to <host> (<ip> port <n>) from ...` and the TLS lines, and that with verbose off nothing is raised.
- [x] The tests are platform-neutral (the TLS backend line is asserted per platform, each in its own `[OSCondition]` test, or by the provider-neutral event).
- [x] `Documentation/Conformance/Extended/CurlSurface-Transfer-Realtime.md` row `CurlSurface:verbose-webtransport-1` names the new test in its "Covered by" cell.
- [x] `dotnet build Curl.Extended.Protocol.WebTransport.UnitLibrary -warnaserror` is clean, and `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app (`dotnet run tool.cs`) or `Record-CurlExchange.ps1`, never Python (the review used aioquic; the unit tests need no server).
- Extension isolation holds: WebTransport references only `Curl.Protocol.Abstractions` and shared layers, never an Original protocol library or another extension.
- Review acceptance check, as a hand check: `curl -sS -k -v -d v webtransport://127.0.0.1:27083/wt 2>&1 >/dev/null | grep -c "Established connection"` prints 1.

- 2026-11-12 (lane 4): Published win-x64 native surl (`dotnet publish Application/Surl.Console -r win-x64`, commit e405b969, includes BL-1059) and ran with `SURL_EXECUTABLE` set. HTTP/3 classes (`FullyQualifiedName~Http3`, 26 tests): 13 of 14 runs 26/26 passed, no exit 45 or `Permission denied`; one run showed 1 failure whose text was not captured (not reproduced in the 12 runs after it). Full `Surl.Conformance` run: 1872 passed, 120 skipped, 1 failed in 11m46s; the one failure is `UpstreamCurlFetchesFromSurlOverHttpsTests`' Curl-port Phase 7 finding ("port defect Http.md does not list", `GetWithInsecure_SameFileTwice...`), HTTPS and the Curl port's behaviour, not HTTP/3. No HTTP/3 `Permission denied` failure, so BL-1059's retry holds. Not filed: the HTTPS finding belongs to the Curl-port phase.
- 2026-11-12 (Claude, lane 1): cause: not the proxy half. The tunnel's origin is the HTTP half, which since BL-1078
  held the connection open on 1297's empty `<data>`; upstream's `sws` closes after a zero-byte reply (sws.c
  `!count`), so curl never saw the close through the tunnel and waited out the 60 s. Fix in the HTTP half: close after
  an empty part on an HTTP/1.1 request, hold on HTTP/1.0 (test 1548's telnet probe) - ADR-0152. `touches` widened to
  `Surl.Protocol.Http.*` and the decisions folder; no task in Doing names them. Local run of 1297 + 1548: both pass.
  1297 added to the win-x64 passing list and row.
- Done: WebTransportQuicDialer.DialAsync(request, events, token) reports TlsHandshakeEvent and ConnectionOpenedEvent after the QUIC handshake; the handler passes context.Events. Certificate details are not captured (ServerCertificate null, verified true on success). Tests in WebTransportVerboseTests; verbose off is the NoTransferEvents default. Quality run: 100/100, no failing member. Real-server hand check not run (no aioquic server). Committed with the task.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: 5640adb5 d2afcfc2 4ba6bdeb f838949d.
- 2026-11-13: Doing -> Done. webtransport -v raises the TLS handshake and Established connection events; unit tested
