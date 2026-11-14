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
completed:
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

- [ ] A new test file in `Curl.Extended.Protocol.WebTransport.UnitTests` (for example `WebTransportVerboseTests.cs`), using the in-memory QUIC stand-ins already in that project, proves a verbose transfer raises the connection event that renders `* Established connection to <host> (<ip> port <n>) from ...` and the TLS lines, and that with verbose off nothing is raised.
- [ ] The tests are platform-neutral (the TLS backend line is asserted per platform, each in its own `[OSCondition]` test, or by the provider-neutral event).
- [ ] `Documentation/Conformance/Extended/CurlSurface-Transfer-Realtime.md` row `CurlSurface:verbose-webtransport-1` names the new test in its "Covered by" cell.
- [ ] `dotnet build Curl.Extended.Protocol.WebTransport.UnitLibrary -warnaserror` is clean, and `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app (`dotnet run tool.cs`) or `Record-CurlExchange.ps1`, never Python (the review used aioquic; the unit tests need no server).
- Extension isolation holds: WebTransport references only `Curl.Protocol.Abstractions` and shared layers, never an Original protocol library or another extension.
- Review acceptance check, as a hand check: `curl -sS -k -v -d v webtransport://127.0.0.1:27083/wt 2>&1 >/dev/null | grep -c "Established connection"` prints 1.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: 5640adb5 d2afcfc2 4ba6bdeb f838949d.
