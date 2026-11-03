---
id: BL-4632
title: Conformance: curl's transfer options (-v, -o, -O, -w, --fail, -m) for the broker schemes stomp, stomps
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4505, BL-4506, BL-4507, BL-4502, BL-4503, BL-4463]
touches: [Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/Brokers/Stomp, Documentation/Conformance/Extended/CurlSurface-Transfer-Brokers-Stomp.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4632 — Conformance: curl's transfer options (-v, -o, -O, -w, --fail, -m) for the broker schemes stomp, stomps

## Goal

Curl's generic transfer options behave for the schemes `stomp`, `stomps` as curl's manual says any transfer does, checked through the real curl command line in process, with every gap fixed or listed with its fix task.

## Context

Split from BL-4613 (the broker schemes' part of BL-4496, the transfer surface for every extended protocol), one task per broker library so each fits one run; read BL-4496's and BL-4613's Context. Follow `Documentation/Conformance/Extended/README.md`: run against a ScriptedReferenceServer as Stomp/StompRun.cs uses it, ports 61613 and 61614; `ReferenceRoute.Tls` with `ReferenceCertificate.CreateSelfSigned` and `-k` for the TLS scheme; a fake `TimeProvider` for `-m` as in `Harness/ExtendedCurlRunTimeoutTests.cs` (its clock is private there: copy what this folder needs into one file in it). Take the schemes from the library's `*Extension.cs` if they differ from this text. Expected values come from curl's manual page for curl 8.22.0 (curl.se/docs/manpage.html, "-w", "--fail", "-m", "EXIT CODES") and from the extension's ADRs and `CLAUDE.md`; where an extension's ADR deliberately differs, the case pins the ADR's answer and names it. This part's rows go in its own matrix `CurlSurface-Transfer-Brokers-Stomp.md` with IDs `CurlSurface:Stomp<option>-<n>` (for example `CurlSurface:StompVerbose-1`), so they never clash with another `CurlSurface-*.md` matrix. A defect in the extension library or the engine is listed in `KnownFailures.txt` with a filed High task `Fix <Name> conformance: <requirement>` (pipeline feature; touches that library, its `.UnitTests` and `KnownFailures.txt`; criteria: the case passes, a focused unit test pins the fix (ADR-0482), the line is removed).

## Acceptance criteria

- [x] `Documentation/Conformance/Extended/CurlSurface-Transfer-Brokers-Stomp.md` lists, for each of `stomp`, `stomps`, the checks `-v` (connection lines, and TLS lines for the TLS scheme, on stderr, nothing extra on stdout), `-o file` in a temporary directory, `-O`, `-w` (`%{exitcode}`, `%{errormsg}`, `%{url}`, `%{url_effective}`, `%{scheme}`, `%{remote_ip}`, `%{remote_port}`, `%{size_download}`, `%{time_total}` on the injected clock), `--fail` and `--fail-with-body` (exit 22 where the library maps a server error to it) and `-m` (exit 28 at the deadline on the injected clock), each with a `CurlSurface:` ID mapped to a test or to `n/a: <reason>`, and the harness's matrix check passes.
- [x] Cases in `Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/Brokers/Stomp/` (one case file per scheme) run curl in process for each of those schemes and assert stdout, stderr and exit code for each check, naming the expected value's manual-page or ADR source.
- [x] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt` with the ID of its filed High `Fix <Name> conformance: ...` task; the known-failures check passes.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green; no test needs `TestCategory=Integration`, opens a real socket or starts a real process.
- [x] `Curl.Conformance.Extended.UnitLibrary` stays at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`), if this task changes it.
- [x] No package added; every test is platform-neutral (no drive letters, Windows-only paths or text).

## Notes

- Cases: `CurlSurface/Transfer/Brokers/Stomp/StompTransferSurfaceCases.cs` (stomp, 61613) and `StompsTransferSurfaceCases.cs` (stomps, 61614, `ReferenceRoute.Tls` with `-k`), sharing `StompTransferChecks.cs`. All 14 cases pass on the first run, so `KnownFailures.txt` needed no line and no fix task was filed.
- IDs: `CurlSurface:Stomp<option>-<n>`, `n` 1 for `stomp` and 2 for `stomps`, as the task's ID pattern asks.
- `--fail`/`--fail-with-body`: ADR-1059 deliberately ends a STOMP `ERROR` frame with exit 8 (`STOMP server sent ERROR: <message>`), and STOMP has no HTTP response code for `--fail` to act on, so those rows pin exit 8 and name ADR-1059 rather than 22 (the task's "where an extension's ADR deliberately differs" rule). ADR-1059 already records the decision, so no new ADR.
- Clock: reused `RealtimeTransferClock` and `NeverAnsweringServer` from `CurlSurface/Transfer/Realtime` (same assembly, read only) rather than copying the clock, since the Realtime part landed with exactly that clock after this task was written; a copy would only duplicate it.
- `Curl.Conformance.Extended.UnitLibrary` is unchanged, so no quality run was needed.
- Platform: temporary directories through `Path.Combine`, in-process loopback routes and the platform's TLS line (`* schannel: ` on Windows, `* SSL connection using ` elsewhere, as the Realtime checks do); no drive letters or OS error text, so CI on Linux and macOS is met by staying platform-neutral with the fast tests green here.
- `dotnet build Curl.slnx -warnaserror` clean; `dotnet test --filter "TestCategory!=Integration"` green.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 94caf871 4a81acb9 fe617ef5 be674468 9438adeb ef6c9026.
- 2026-11-02: Doing -> Done. STOMP transfer-surface matrix and 14 cases pass for stomp and stomps
