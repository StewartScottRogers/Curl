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
completed: 2026-11-02
---
# BL-4625 — Conformance: curl's transfer options (-v, -o, -O, -w, --fail, -m) for redis and rediss, with the messaging-streams transfer case runner

## Goal

Curl's generic transfer options behave for `redis`, `rediss` as curl's manual says any transfer does, checked through the real curl command line, with every gap fixed or listed with its fix task.

## Context

Schemes: `redis`, `rediss`.

Build the shared runner first, TransferSurfaceCaseRun.cs in the same folder: given a scheme's route (TCP, TLS with `-k`, or UDP), URL and canned server, it runs each check and asserts stdout, stderr and exit code, so the later parts (BL-4626 mqtt5, BL-4627 coap, BL-4623 xmpp, BL-4624 dns) add only a case file. Create the matrix file with its heading and the redis rows. Redis's canned server: `Curl.Conformance.Extended.UnitLibrary/Redis/RedisReferenceServer.cs` and `Curl.Conformance.Extended.UnitTests/Redis/RedisCaseRun.cs` show the `HELLO 3` exchange.

Split from BL-4614 (itself split from BL-4496, which holds the whole plan; read its Context). Part of the extended-protocol conformance plan Stewart approved on 2026-11-02: follow `Documentation/Conformance/Extended/README.md` (harness `ExtendedConformanceHarness.RunAsync`, `ScriptedReferenceServer`/`ScriptedDatagramServer` for canned server bytes, `ReferenceRoute.Tcp`/`Tls`/`Udp`, and a fake `TimeProvider` for `-m` as in `Curl.Conformance.Extended.UnitTests/Harness/ExtendedCurlRunTimeoutTests.cs`). Take the schemes from each library's `*Extension.cs`, not from this text, if they differ. Expected values come from curl's manual page for curl 8.22.0 (curl.se/docs/manpage.html, "-w", "--fail", "-m", "EXIT CODES") and from each extension's ADRs and `CLAUDE.md`; where an extension's ADR deliberately differs, the case pins the ADR's answer and names it. Requirement IDs are `CurlSurface:<option>-<n>`, rows in `Documentation/Conformance/Extended/CurlSurface-Transfer-MessagingStreams.md` (one table, `| ID | Level | Requirement | Covered by |`). One case file per scheme family in `Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/MessagingStreams/`, never a central switch. A defect in an extension library or the engine is listed in `KnownFailures.txt` with a filed High task `Fix <Name> conformance: <requirement>` (pipeline feature; touches that library, its `.UnitTests` and `KnownFailures.txt`; criteria: the case passes, a focused unit test pins the fix (ADR-0482), the line is removed).

## Acceptance criteria

- [x] `TransferSurfaceCaseRun.cs` exists in `Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/MessagingStreams/` and runs a check for a scheme from its route, URL and server alone.
- [x] `Documentation/Conformance/Extended/CurlSurface-Transfer-MessagingStreams.md` lists, for each of `redis`, `rediss`, the checks `-v` (connection lines, and TLS lines for TLS schemes, on stderr, nothing extra on stdout), `-o file` in a temporary directory, `-O`, `-w` (`%{exitcode}`, `%{errormsg}`, `%{url}`, `%{url_effective}`, `%{scheme}`, `%{remote_ip}`, `%{remote_port}`, `%{size_download}`, `%{time_total}` on the injected clock), `--fail` and `--fail-with-body` (exit 22 where the library maps a server error to it) and `-m` (exit 28 at the deadline on the injected clock), each with a `CurlSurface:` ID mapped to a test or to `n/a: <reason>`, and the harness's matrix check passes.
- [x] Cases in `Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/MessagingStreams/RedisTransferSurfaceCases.cs` run curl in process for every one of those schemes and assert stdout, stderr and exit code for each check, naming the expected value's manual-page or ADR source.
- [x] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt` with the ID of its filed High `Fix <Name> conformance: ...` task; the known-failures check passes.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green; no test needs `TestCategory=Integration`, opens a real socket or starts a real process.
- [x] `Curl.Conformance.Extended.UnitLibrary` stays at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`), if this task changes it.
- [x] No package added; every test is platform-neutral (no drive letters, Windows-only paths or text).
## Notes

- Runner: `TransferSurfaceCaseRun` takes a `TransferSurfaceRoute` (`Tcp`, `Tls` - adds `-k` and a fresh self-signed certificate - or `Udp`) and runs every check on a `ManualClock` that never moves unless the check advances it, so `%{time_total}` is `0.000000` and `RunPastMaxTimeAsync` drives `-m` past its deadline once a silent server has curl's first bytes. Its own fake clock rather than the one in `Harness/ExtendedCurlRunTimeoutTests.cs`, which is private to that test and outside this task's touches. `RunPastMaxTimeAsync` swaps in a silent stream server, so a UDP scheme (coap, dns) needs a silent datagram server added there by its part.
- Decided (default): IDs are `CurlSurface:<Option>-<Scheme>-<n>` so each scheme's known failure is listed on its own; `-O` runs with `--output-dir` in a temporary directory so nothing lands in the test's working directory.
- Measured in process: everything but `-v` already behaves for both schemes. `--fail`/`--fail-with-body` stay exit 8 on an error reply, pinned to ADR-1079 (Redis maps no server error to 22). `-v` writes nothing at all to standard error, for redis and rediss alike: listed in `KnownFailures.txt` against BL-4645 (High, filed). The rediss TLS line is pinned as a prefix per platform (`* schannel: ` on Windows, `* SSL connection using TLSv1.` elsewhere); BL-4645 measures real curl before it is relied on. Filed as BL-4645, not the next free ID in this checkout, because origin already uses up to BL-4632 and `dedupe` rewrites only Markdown, not `KnownFailures.txt`.
- `Curl.Conformance.Extended.UnitLibrary` is not changed, so its quality criterion holds as it was; no production code changed, so no Measure-CodeQuality run. Every test is platform-neutral (temporary paths from `Path.GetTempPath`, the TLS prefix chosen by `OperatingSystem.IsWindows()`); CI on Linux and macOS is left to the shift's CI watch.
- `dotnet build Curl.slnx -warnaserror` clean; fast tests green (Curl.Conformance.Extended.UnitTests 971 passed, 1 skipped as before).

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 70861bb4 ac6109d4 2bf36aee fe617ef5 be674468 9438adeb ef6c9026.
- 2026-11-02: Doing -> Done. redis and rediss pass curl's -o, -O, -w, --fail, --fail-with-body and -m checks through the new messaging-streams transfer runner; -v listed against BL-4645
