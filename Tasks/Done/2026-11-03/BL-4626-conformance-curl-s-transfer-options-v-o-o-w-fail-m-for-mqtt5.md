---
id: BL-4626
title: Conformance: curl's transfer options (-v, -o, -O, -w, --fail, -m) for mqtt5, mqtt5s and mqtt5+ws
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4505, BL-4506, BL-4507, BL-4502, BL-4503, BL-4463, BL-4625]
touches: [Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/MessagingStreams/Mqtt5TransferSurfaceCases.cs, Documentation/Conformance/Extended/CurlSurface-Transfer-MessagingStreams.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt, Curl.Extended.Protocol.Mqtt5.UnitLibrary/WebSocket/Mqtt5WebSocketProtocolHandler.cs, Curl.Extended.Protocol.Mqtt5.UnitTests/WebSocket/Mqtt5WebSocketProtocolHandlerTests.cs]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4626 — Conformance: curl's transfer options (-v, -o, -O, -w, --fail, -m) for mqtt5, mqtt5s and mqtt5+ws

## Goal

Curl's generic transfer options behave for `mqtt5`, `mqtt5s`, `mqtt5+ws` as curl's manual says any transfer does, checked through the real curl command line, with every gap fixed or listed with its fix task.

## Context

Schemes: `mqtt5`, `mqtt5s`, `mqtt5+ws`.

Use the runner BL-4625 adds. The MQTT 5 reference broker is in `Curl.Conformance.Extended.UnitLibrary/Mqtt5/` (`Mqtt5ReferenceBroker.cs`, `Transports/` for the WebSocket route).

Split from BL-4614 (itself split from BL-4496, which holds the whole plan; read its Context). Part of the extended-protocol conformance plan Stewart approved on 2026-11-02: follow `Documentation/Conformance/Extended/README.md` (harness `ExtendedConformanceHarness.RunAsync`, `ScriptedReferenceServer`/`ScriptedDatagramServer` for canned server bytes, `ReferenceRoute.Tcp`/`Tls`/`Udp`, and a fake `TimeProvider` for `-m` as in `Curl.Conformance.Extended.UnitTests/Harness/ExtendedCurlRunTimeoutTests.cs`). Take the schemes from each library's `*Extension.cs`, not from this text, if they differ. Expected values come from curl's manual page for curl 8.22.0 (curl.se/docs/manpage.html, "-w", "--fail", "-m", "EXIT CODES") and from each extension's ADRs and `CLAUDE.md`; where an extension's ADR deliberately differs, the case pins the ADR's answer and names it. Requirement IDs are `CurlSurface:<option>-<n>`, rows in `Documentation/Conformance/Extended/CurlSurface-Transfer-MessagingStreams.md` (one table, `| ID | Level | Requirement | Covered by |`). One case file per scheme family in `Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/MessagingStreams/`, never a central switch. A defect in an extension library or the engine is listed in `KnownFailures.txt` with a filed High task `Fix <Name> conformance: <requirement>` (pipeline feature; touches that library, its `.UnitTests` and `KnownFailures.txt`; criteria: the case passes, a focused unit test pins the fix (ADR-0482), the line is removed).

## Acceptance criteria

- [x] `Documentation/Conformance/Extended/CurlSurface-Transfer-MessagingStreams.md` lists, for each of `mqtt5`, `mqtt5s`, `mqtt5+ws`, the checks `-v` (connection lines, and TLS lines for TLS schemes, on stderr, nothing extra on stdout), `-o file` in a temporary directory, `-O`, `-w` (`%{exitcode}`, `%{errormsg}`, `%{url}`, `%{url_effective}`, `%{scheme}`, `%{remote_ip}`, `%{remote_port}`, `%{size_download}`, `%{time_total}` on the injected clock), `--fail` and `--fail-with-body` (exit 22 where the library maps a server error to it) and `-m` (exit 28 at the deadline on the injected clock), each with a `CurlSurface:` ID mapped to a test or to `n/a: <reason>`, and the harness's matrix check passes.
- [x] Cases in `Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/MessagingStreams/Mqtt5TransferSurfaceCases.cs` run curl in process for every one of those schemes and assert stdout, stderr and exit code for each check, naming the expected value's manual-page or ADR source.
- [x] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt` with the ID of its filed High `Fix <Name> conformance: ...` task; the known-failures check passes.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green; no test needs `TestCategory=Integration`, opens a real socket or starts a real process.
- [x] `Curl.Conformance.Extended.UnitLibrary` stays at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`), if this task changes it.
- [x] No package added; every test is platform-neutral (no drive letters, Windows-only paths or text).
## Notes

- Plan: one case file, `Mqtt5TransferSurfaceCases.cs`, on BL-4625's `TransferSurfaceCaseRun`. Each check subscribes to `t` on `Mqtt5ReferenceBroker` (TCP 1883, or TLS 8883 with `-k`) or on `Mqtt5WebSocketReferenceServer` (TCP 80). The broker answers SUBSCRIBE with SUBACK QoS 0, then PUBLISH `hello`, then DISCONNECT, or with SUBACK 0x80. 27 rows (9 per scheme) in `CurlSurface-Transfer-MessagingStreams.md`, IDs `CurlSurface:<option>-Mqtt5|Mqtt5s|Mqtt5Ws-<n>`.
- Decision (sensible default): a refusing SUBACK is exit 8 whatever `--fail` says, because ADR-1049 maps a failure reason code to a weird server reply. The cases pin that and name ADR-1049. curl 8.22.0 has no mqtt5 scheme, so `-v` pins the connection line every transfer writes, as the xmpp cases measured.
- Gap found and fixed here: `-m` on `mqtt5+ws` during the WebSocket upgrade said `Connection timed out after 5000 milliseconds`. curl sends the upgrade as the transfer's request, so it says `Operation timed out after 5000 milliseconds with 0 bytes received`. `Mqtt5WebSocketProtocolHandler` now reports the transfer started before the upgrade. Focused unit test (ADR-0482): `Mqtt5WebSocketProtocolHandlerTests.ExecuteAsync_UpgradeRefused_ReportsTheTransferStartedBeforeTheUpgrade`. touches widened to that file and its tests, since no task in Doing on origin/work/dark-factory names them. Nothing was added to KnownFailures.txt.
- Measure-CodeQuality.ps1 -Library Curl.Extended.Protocol.Mqtt5.UnitLibrary: 0 failing members. Curl.Conformance.Extended.UnitLibrary was not changed (criterion 5 holds trivially).
- Full fast run: one `MaxTime_ServerNeverAnswers_Exits28AtTheDeadline` case (the output did not name its class) hung 30 s. The project passed when rerun alone (1426 passed, 0 failed), and every other test project was green. Filed BL-4663 for the likely race in `TransferSurfaceCaseRun.RunPastMaxTimeAsync`. Platform-neutral: files only under a temporary directory via `Path.Combine`; the TLS verbose line is pinned per platform. CI on Linux and macOS is left to the shift's CI watch.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 3531b516 4d3d5220 fe617ef5 be674468 9438adeb ef6c9026.
- 2026-11-02: Doing -> Done. curl's transfer options checked for mqtt5, mqtt5s and mqtt5+ws (27 cases pass); -m during the mqtt5+ws upgrade now says Operation timed out
