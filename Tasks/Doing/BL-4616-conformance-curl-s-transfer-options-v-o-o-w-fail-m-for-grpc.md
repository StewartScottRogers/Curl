---
id: BL-4616
title: Conformance: curl's transfer options (-v, -o, -O, -w, --fail, -m) for grpc, signalr, socketio, webtransport and mcp
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4505, BL-4506, BL-4507, BL-4502, BL-4503, BL-4463]
touches: [Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/Realtime, Documentation/Conformance/Extended/CurlSurface-Transfer-Realtime.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4616 — Conformance: curl's transfer options (-v, -o, -O, -w, --fail, -m) for grpc, signalr, socketio, webtransport and mcp

## Goal

Curl's generic transfer options behave for grpc, signalr, socketio, webtransport and mcp as curl's manual says any transfer does, checked through the real curl command line, with every gap fixed or listed with its fix task.

## Context

Schemes: `grpc`, `grpcs`, `signalr`, `signalrs`, `socketio`, `socketios`, `webtransport`, `mcp`, `mcps`. `webtransport` runs over HTTP/3; where the harness has no QUIC reference route, mark its rows `n/a: <reason>` naming the task that adds one, or file that task.

Split from BL-4496 (the transfer surface for every extended protocol), which holds the whole plan; read its Context. Part of the extended-protocol conformance plan Stewart approved on 2026-11-02: follow `Documentation/Conformance/Extended/README.md` (harness, `ScriptedReferenceServer` for canned server bytes, `ReferenceRoute.Tcp`/`Tls`/`Udp`, fake `TimeProvider` for `-m` as in `ExtendedCurlRunTimeoutTests`). Take the schemes from each library's `*Extension.cs`, not from this text, if they differ. Expected values come from curl's manual page for curl 8.22.0 (curl.se/docs/manpage.html, "-w", "--fail", "-m", "EXIT CODES") and from each extension's ADRs and `CLAUDE.md`; where an extension's ADR deliberately differs, the case pins the ADR's answer and names it. Requirement IDs are `CurlSurface:<option>-<n>`; this task's rows go in its own part matrix (a `CurlSurface-<Part>.md` file holds `CurlSurface:` IDs, BL-4476), so the parts run in parallel. One case file per scheme in this task's folder, never a central switch. A defect in an extension library or the engine is listed in `KnownFailures.txt` with a filed High task `Fix <Name> conformance: <requirement>` (pipeline feature; touches that library, its `.UnitTests` and `KnownFailures.txt`; criteria: the case passes, a focused unit test pins the fix (ADR-0482), the line is removed).

## Acceptance criteria

- [ ] `Documentation/Conformance/Extended/CurlSurface-Transfer-Realtime.md` lists, for each of `grpc`, `grpcs`, `signalr`, `signalrs`, `socketio`, `socketios`, `webtransport`, `mcp`, `mcps`, the checks `-v` (connection lines, and TLS lines for TLS schemes, on stderr, nothing extra on stdout), `-o file` in a temporary directory, `-O`, `-w` (`%{exitcode}`, `%{errormsg}`, `%{url}`, `%{url_effective}`, `%{scheme}`, `%{remote_ip}`, `%{remote_port}`, `%{size_download}`, `%{time_total}` on the injected clock), `--fail` and `--fail-with-body` (exit 22 where the library maps a server error to it) and `-m` (exit 28 at the deadline on the injected clock), each with a `CurlSurface:` ID mapped to a test or to `n/a: <reason>`, and the harness's matrix check passes.
- [ ] Cases in `Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/Realtime/` run curl in process for every one of those schemes and assert stdout, stderr and exit code for each check, naming the expected value's manual-page or ADR source.
- [ ] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt` with the ID of its filed High `Fix <Name> conformance: ...` task; the known-failures check passes.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green; no test needs `TestCategory=Integration`, opens a real socket or starts a real process.
- [ ] `Curl.Conformance.Extended.UnitLibrary` stays at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`), if this task changes it.
- [ ] No package added; every test is platform-neutral (no drive letters, Windows-only paths or text).

## Notes

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 9ed6878f f27a8f90 fe617ef5 be674468 9438adeb ef6c9026.
