---
id: BL-4625
title: Conformance: curl's transfer options (-v, -o, -O, -w, --fail, -m) for redis and rediss, with the messaging-streams transfer case runner
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4505, BL-4506, BL-4507, BL-4502, BL-4503, BL-4463]
touches: [Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/MessagingStreams/TransferSurfaceCaseRun.cs, Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/MessagingStreams/RedisTransferSurfaceCases.cs, Documentation/Conformance/Extended/CurlSurface-Transfer-MessagingStreams.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4625 — Conformance: curl's transfer options (-v, -o, -O, -w, --fail, -m) for redis and rediss, with the messaging-streams transfer case runner

## Goal

Curl's generic transfer options behave for `redis`, `rediss` as curl's manual says any transfer does, checked through the real curl command line, with every gap fixed or listed with its fix task.

## Context

Schemes: `redis`, `rediss`.

Build the shared runner first, TransferSurfaceCaseRun.cs in the same folder: given a scheme's route (TCP, TLS with `-k`, or UDP), URL and canned server, it runs each check and asserts stdout, stderr and exit code, so the later parts (BL-4626 mqtt5, BL-4627 coap, BL-4623 xmpp, BL-4624 dns) add only a case file. Create the matrix file with its heading and the redis rows. Redis's canned server: `Curl.Conformance.Extended.UnitLibrary/Redis/RedisReferenceServer.cs` and `Curl.Conformance.Extended.UnitTests/Redis/RedisCaseRun.cs` show the `HELLO 3` exchange.

Split from BL-4614 (itself split from BL-4496, which holds the whole plan; read its Context). Part of the extended-protocol conformance plan Stewart approved on 2026-11-02: follow `Documentation/Conformance/Extended/README.md` (harness `ExtendedConformanceHarness.RunAsync`, `ScriptedReferenceServer`/`ScriptedDatagramServer` for canned server bytes, `ReferenceRoute.Tcp`/`Tls`/`Udp`, and a fake `TimeProvider` for `-m` as in `Curl.Conformance.Extended.UnitTests/Harness/ExtendedCurlRunTimeoutTests.cs`). Take the schemes from each library's `*Extension.cs`, not from this text, if they differ. Expected values come from curl's manual page for curl 8.22.0 (curl.se/docs/manpage.html, "-w", "--fail", "-m", "EXIT CODES") and from each extension's ADRs and `CLAUDE.md`; where an extension's ADR deliberately differs, the case pins the ADR's answer and names it. Requirement IDs are `CurlSurface:<option>-<n>`, rows in `Documentation/Conformance/Extended/CurlSurface-Transfer-MessagingStreams.md` (one table, `| ID | Level | Requirement | Covered by |`). One case file per scheme family in `Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/MessagingStreams/`, never a central switch. A defect in an extension library or the engine is listed in `KnownFailures.txt` with a filed High task `Fix <Name> conformance: <requirement>` (pipeline feature; touches that library, its `.UnitTests` and `KnownFailures.txt`; criteria: the case passes, a focused unit test pins the fix (ADR-0482), the line is removed).

## Acceptance criteria

- [ ] `TransferSurfaceCaseRun.cs` exists in `Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/MessagingStreams/` and runs a check for a scheme from its route, URL and server alone.
- [ ] `Documentation/Conformance/Extended/CurlSurface-Transfer-MessagingStreams.md` lists, for each of `redis`, `rediss`, the checks `-v` (connection lines, and TLS lines for TLS schemes, on stderr, nothing extra on stdout), `-o file` in a temporary directory, `-O`, `-w` (`%{exitcode}`, `%{errormsg}`, `%{url}`, `%{url_effective}`, `%{scheme}`, `%{remote_ip}`, `%{remote_port}`, `%{size_download}`, `%{time_total}` on the injected clock), `--fail` and `--fail-with-body` (exit 22 where the library maps a server error to it) and `-m` (exit 28 at the deadline on the injected clock), each with a `CurlSurface:` ID mapped to a test or to `n/a: <reason>`, and the harness's matrix check passes.
- [ ] Cases in `Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/MessagingStreams/RedisTransferSurfaceCases.cs` run curl in process for every one of those schemes and assert stdout, stderr and exit code for each check, naming the expected value's manual-page or ADR source.
- [ ] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt` with the ID of its filed High `Fix <Name> conformance: ...` task; the known-failures check passes.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green; no test needs `TestCategory=Integration`, opens a real socket or starts a real process.
- [ ] `Curl.Conformance.Extended.UnitLibrary` stays at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`), if this task changes it.
- [ ] No package added; every test is platform-neutral (no drive letters, Windows-only paths or text).
## Notes

## Log

- 2026-11-02: Created.
