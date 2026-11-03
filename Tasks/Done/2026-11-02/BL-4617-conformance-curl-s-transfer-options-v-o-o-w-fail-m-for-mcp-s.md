---
id: BL-4617
title: Conformance: curl's transfer options (-v, -o, -O, -w, --fail, -m) for mcp+stdio and curl-proto-<scheme>
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4505, BL-4506, BL-4507, BL-4502, BL-4503, BL-4463]
touches: [Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/Processes, Documentation/Conformance/Extended/CurlSurface-Transfer-Processes.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4617 — Conformance: curl's transfer options (-v, -o, -O, -w, --fail, -m) for mcp+stdio and curl-proto-<scheme>

## Goal

Curl's generic transfer options behave for `mcp+stdio` and the `curl-proto-<scheme>` helper scheme as curl's manual says any transfer does, checked through the real curl command line, with every gap fixed or listed with its fix task.

## Context

Schemes: `mcp+stdio` and a `curl-proto-<scheme>` helper scheme. Both start a program, so the cases use the stand-in `IExtensionProcessStarter` overload of `ExtendedConformanceHarness.RunAsync` (BL-4463, ADR-1203); no real process starts. `-m`, connection lines under `-v` and `%{remote_ip}` may have no meaning for a program; pin what the library and its ADRs say, or `n/a: <reason>`.

Split from BL-4496 (the transfer surface for every extended protocol), which holds the whole plan; read its Context. Part of the extended-protocol conformance plan Stewart approved on 2026-11-02: follow `Documentation/Conformance/Extended/README.md` (harness, `ScriptedReferenceServer` for canned server bytes, `ReferenceRoute.Tcp`/`Tls`/`Udp`, fake `TimeProvider` for `-m` as in `ExtendedCurlRunTimeoutTests`). Take the schemes from each library's `*Extension.cs`, not from this text, if they differ. Expected values come from curl's manual page for curl 8.22.0 (curl.se/docs/manpage.html, "-w", "--fail", "-m", "EXIT CODES") and from each extension's ADRs and `CLAUDE.md`; where an extension's ADR deliberately differs, the case pins the ADR's answer and names it. Requirement IDs are `CurlSurface:<option>-<n>`; this task's rows go in its own part matrix (a `CurlSurface-<Part>.md` file holds `CurlSurface:` IDs, BL-4476), so the parts run in parallel. One case file per scheme in this task's folder, never a central switch. A defect in an extension library or the engine is listed in `KnownFailures.txt` with a filed High task `Fix <Name> conformance: <requirement>` (pipeline feature; touches that library, its `.UnitTests` and `KnownFailures.txt`; criteria: the case passes, a focused unit test pins the fix (ADR-0482), the line is removed).

## Acceptance criteria

- [x] `Documentation/Conformance/Extended/CurlSurface-Transfer-Processes.md` lists, for each of `mcp+stdio` and `curl-proto-<scheme>`, the checks `-v` (connection lines, and TLS lines for TLS schemes, on stderr, nothing extra on stdout), `-o file` in a temporary directory, `-O`, `-w` (`%{exitcode}`, `%{errormsg}`, `%{url}`, `%{url_effective}`, `%{scheme}`, `%{remote_ip}`, `%{remote_port}`, `%{size_download}`, `%{time_total}` on the injected clock), `--fail` and `--fail-with-body` (exit 22 where the library maps a server error to it) and `-m` (exit 28 at the deadline on the injected clock), each with a `CurlSurface:` ID mapped to a test or to `n/a: <reason>`, and the harness's matrix check passes.
- [x] Cases in `Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/Processes/` run curl in process for every one of those schemes and assert stdout, stderr and exit code for each check, naming the expected value's manual-page or ADR source.
- [x] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt` with the ID of its filed High `Fix <Name> conformance: ...` task; the known-failures check passes.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green; no test needs `TestCategory=Integration`, opens a real socket or starts a real process.
- [x] `Curl.Conformance.Extended.UnitLibrary` stays at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`), if this task changes it.
- [x] No package added; every test is platform-neutral (no drive letters, Windows-only paths or text).

## Notes

- Pipeline `feature` run directly: no production code changed, so there was nothing to plan or implement. The work is test cases, stand-ins and a matrix inside this task's `touches`.
- Schemes: `mcp+stdio` (McpClient, ADR-1031) and `demo://` served by `curl-proto-demo` (Plugin, ADR-1142), as the existing McpClient/Stdio and Plugin cases use them. Each scheme has one case file in `CurlSurface/Transfer/Processes/` (`McpStdioTransferCases`, `CurlProtoTransferCases`). They share `ProcessTransferRun` (runs under `-sS` on a `ManualClock`), `ManualClock` (a copy of the private fake clock in `ExtendedCurlRunTimeoutTests`, since this task may not touch `Harness/`) and `SilentProcessStarter` (a program that never answers until it is killed, used for `-m`).
- I measured what Curl does in process before pinning anything. `-v` adds nothing to standard error for either scheme: no connection and no TLS, as curl does for `file://`. The TLS rows are `n/a`. `-w` prints an empty `%{remote_ip}` and `-1` for `%{remote_port}`, which is ADR-0119 point 3's no-connection answer. `%{time_total}` is `0.000000` on a clock that never moves. Neither library maps any server answer to exit 22: MCP failures exit 8 and a helper's `error` reply exits 56. So `--fail` and `--fail-with-body` are pinned as leaving the transfer's own exit code and output alone. `-m 100` ends with exit 28 once the injected clock passes the deadline, for both schemes.
- Decision (sensible default, no ADR: it decides no behaviour): IDs are `CurlSurface:<option>-<McpStdio|CurlProto>-<n>`. The parts BL-4613 to BL-4616 file `CurlSurface:` IDs in parallel, and the scheme part keeps these from clashing with theirs.
- `-O` runs under `--output-dir <temp dir>` so the file never lands in the test's working directory. All paths use `Path.Combine` and the helper name uses `PluginRun.DemoName` (`.cmd` on Windows only), so the cases are platform-neutral. CI on Linux and macOS is met by that plus the fast tests being green here.
- Every case passed, so `KnownFailures.txt` is unchanged and no fix task was filed. `Curl.Conformance.Extended.UnitLibrary` is unchanged, so there was no quality run. The build with `-warnaserror` is clean and the fast tests are green: Curl.Conformance.Extended.UnitTests has 947 passed and 1 skipped (unrelated), and its 16 new cases all pass.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: a2b9a5c9 4748870e fe617ef5 be674468 9438adeb ef6c9026.
- 2026-11-02: Doing -> Done. mcp+stdio and curl-proto-<scheme> transfer options (-v, -o, -O, -w, --fail, -m) are pinned by in-process conformance cases and a CurlSurface matrix
