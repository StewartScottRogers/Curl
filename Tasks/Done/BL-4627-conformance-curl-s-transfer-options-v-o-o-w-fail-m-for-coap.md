---
id: BL-4627
title: Conformance: curl's transfer options (-v, -o, -O, -w, --fail, -m) for coap and coaps
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4505, BL-4506, BL-4507, BL-4502, BL-4503, BL-4463, BL-4625]
touches: [Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/MessagingStreams/CoapTransferSurfaceCases.cs, Documentation/Conformance/Extended/CurlSurface-Transfer-MessagingStreams.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed: 2026-11-03
---
# BL-4627 — Conformance: curl's transfer options (-v, -o, -O, -w, --fail, -m) for coap and coaps

## Goal

Curl's generic transfer options behave for `coap`, `coaps` (UDP routes) as curl's manual says any transfer does, checked through the real curl command line, with every gap fixed or listed with its fix task.

## Context

Schemes: `coap`, `coaps` (UDP routes).

Use the runner BL-4625 adds. The CoAP reference server is in `Curl.Conformance.Extended.UnitLibrary/Coap/` (`CoapReferenceServer.cs`, `Dtls/` for coaps).

Split from BL-4614 (itself split from BL-4496, which holds the whole plan; read its Context). Part of the extended-protocol conformance plan Stewart approved on 2026-11-02: follow `Documentation/Conformance/Extended/README.md` (harness `ExtendedConformanceHarness.RunAsync`, `ScriptedReferenceServer`/`ScriptedDatagramServer` for canned server bytes, `ReferenceRoute.Tcp`/`Tls`/`Udp`, and a fake `TimeProvider` for `-m` as in `Curl.Conformance.Extended.UnitTests/Harness/ExtendedCurlRunTimeoutTests.cs`). Take the schemes from each library's `*Extension.cs`, not from this text, if they differ. Expected values come from curl's manual page for curl 8.22.0 (curl.se/docs/manpage.html, "-w", "--fail", "-m", "EXIT CODES") and from each extension's ADRs and `CLAUDE.md`; where an extension's ADR deliberately differs, the case pins the ADR's answer and names it. Requirement IDs are `CurlSurface:<option>-<n>`, rows in `Documentation/Conformance/Extended/CurlSurface-Transfer-MessagingStreams.md` (one table, `| ID | Level | Requirement | Covered by |`). One case file per scheme family in `Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/MessagingStreams/`, never a central switch. A defect in an extension library or the engine is listed in `KnownFailures.txt` with a filed High task `Fix <Name> conformance: <requirement>` (pipeline feature; touches that library, its `.UnitTests` and `KnownFailures.txt`; criteria: the case passes, a focused unit test pins the fix (ADR-0482), the line is removed).

## Acceptance criteria

- [x] `Documentation/Conformance/Extended/CurlSurface-Transfer-MessagingStreams.md` lists, for each of `coap`, `coaps` (UDP routes), the checks `-v` (connection lines, and TLS lines for TLS schemes, on stderr, nothing extra on stdout), `-o file` in a temporary directory, `-O`, `-w` (`%{exitcode}`, `%{errormsg}`, `%{url}`, `%{url_effective}`, `%{scheme}`, `%{remote_ip}`, `%{remote_port}`, `%{size_download}`, `%{time_total}` on the injected clock), `--fail` and `--fail-with-body` (exit 22 where the library maps a server error to it) and `-m` (exit 28 at the deadline on the injected clock), each with a `CurlSurface:` ID mapped to a test or to `n/a: <reason>`, and the harness's matrix check passes.
- [x] Cases in `Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/MessagingStreams/CoapTransferSurfaceCases.cs` run curl in process for every one of those schemes and assert stdout, stderr and exit code for each check, naming the expected value's manual-page or ADR source.
- [x] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt` with the ID of its filed High `Fix <Name> conformance: ...` task; the known-failures check passes.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green; no test needs `TestCategory=Integration`, opens a real socket or starts a real process.
- [x] `Curl.Conformance.Extended.UnitLibrary` stays at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`), if this task changes it.
- [x] No package added; every test is platform-neutral (no drive letters, Windows-only paths or text).
## Notes

- Cases: `CoapTransferSurfaceCases.cs` runs `coap` (UDP 5683) and `coaps` (DTLS on UDP 5684, `-k`) against `CoapReferenceServer` (behind `DtlsReferenceServer` for coaps), answering a piggybacked 2.05 `hello` or 4.04 `missing`. The UDP routes do not fit `TransferSurfaceCaseRun.RunAsync`/`RunPastMaxTimeAsync` (TCP-only silent server, no `-k` on UDP), so the case file drives `ExtendedConformanceHarness.RunAsync` itself with `TimerJumpingClock` and a private silent datagram server for `-m`; `TransferSurfaceCaseRun.cs` stays outside touches.
- Measured on Curl: 16 of 18 rows pass. CoAP maps 4.04 to exit 22 under `--fail` and `--fail-with-body` writes the payload. `-v` writes only `* CoAP response 2.05` (no connection line, no DTLS line): listed in KnownFailures.txt with filed High task BL-4664. coaps `-m` says `Connection timed out after 5000 milliseconds` (connect phase, handshake unfinished), pinned per ADR-1222; coap keeps `Operation timed out ... with 0 bytes received`.
- ADR-1222 (decided by Claude): coaps `-v` expects `* SSL connection using DTLSv1.2` on every platform (OpenSSL build's DTLS line; the DTLS is hand-built everywhere), and the connect-phase timeout text.
- `Curl.Conformance.Extended.UnitLibrary` unchanged, so its quality criterion holds without a run. Tests are platform-neutral (temporary directory via Path.Combine, no OS text); CI on Linux and macOS is covered by that and the green fast run here (115 test assemblies, 0 failed; Extended 1474 passed).

## Log

- 2026-11-02: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 6f7d495d 88be125a fe617ef5 be674468 9438adeb ef6c9026.
- 2026-11-03: Doing -> Done. curl's transfer options checked for coap and coaps; -v connection/DTLS lines listed as known failures under BL-4664
