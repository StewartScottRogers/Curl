---
id: BL-4612
title: Conformance: curl's URL errors (exit 3, 6, 7) for every extended scheme
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4505, BL-4506, BL-4507, BL-4502, BL-4503, BL-4463, BL-4619]
touches: [Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/UrlErrors, Documentation/Conformance/Extended/CurlSurface-Transfer-UrlErrors.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4612 — Conformance: curl's URL errors (exit 3, 6, 7) for every extended scheme

## Goal

Every extended scheme fails a malformed URL with exit 3, an unresolvable host with exit 6 and a refused connection with exit 7, as curl's manual ("EXIT CODES", curl 8.22.0) says, checked through the real curl command line.

## Context

For every scheme in each extension library's `*Extension.cs` (BL-4496 lists them, including `mcp+stdio` and `curl-proto-<scheme>`, which use the stand-in process starter where they get that far): a malformed URL (bad port, bad percent-encoding, a space) gives exit 3; an unresolvable host gives exit 6 (the harness feature BL-4619 adds); a port with no route gives exit 7. One shared data-driven test with one row file per scheme, or one case file per scheme, in `Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/UrlErrors/`; never a central switch. Rows go in the part matrix `CurlSurface-Transfer-UrlErrors.md` with `CurlSurface:` IDs (a `CurlSurface-<Part>.md` holds `CurlSurface:` IDs, BL-4476); a check that does not apply (no host in `mcp+stdio`) is `n/a: <reason>`. Where an extension's ADR deliberately differs, the case pins the ADR's answer and names it. A defect in an extension library or the engine goes in `KnownFailures.txt` with a filed High `Fix <Name> conformance: <requirement>` task (pipeline feature; touches that library, its `.UnitTests` and `KnownFailures.txt`; criteria: the case passes, a focused unit test pins the fix (ADR-0482), the line is removed).

Split from BL-4496 (the transfer surface for every extended protocol), which holds the whole plan; read its Context. Part of the extended-protocol conformance plan Stewart approved on 2026-11-02: follow `Documentation/Conformance/Extended/README.md`.

## Acceptance criteria

- [x] `Documentation/Conformance/Extended/CurlSurface-Transfer-UrlErrors.md` lists exit 3 (bad port, bad percent-encoding, a space), exit 6 and exit 7 for every extended scheme, each with a `CurlSurface:` ID mapped to a test or to `n/a: <reason>`, and the harness's matrix check passes.
- [x] Cases in `Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/UrlErrors/` run curl in process for every scheme and assert stdout, stderr and exit code for each check, naming the manual-page or ADR source.
- [x] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt` with the ID of its filed High `Fix <Name> conformance: ...` task; the known-failures check passes.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green; no test needs `TestCategory=Integration`, opens a real socket or starts a real process.
- [x] `Curl.Conformance.Extended.UnitLibrary` stays at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`), if this task changes it.
- [x] No package added; every test is platform-neutral (no drive letters, Windows-only paths or text).

## Notes

- Plan: one case file per extension in `CurlSurface/Transfer/UrlErrors/` (its schemes as data rows), shared `UrlErrorChecks` and `UrlErrorScheme`; no reference server, a stand-in process starter (no program starts) and a clock that never moves. 37 schemes, 178 cases, 185 matrix rows (7 `n/a`).
- Measured real curl 8.22.0 with Record-CurlExchange.ps1 for `http://` URLs of the same shapes and pinned that text for every scheme; decisions in ADR-1217 (s3/gs reach 6 and 7 through their endpoint variable; az/abfs/abfss exit 6 n/a; UDP refused text is the harness connector's; mcp+stdio and the helper scheme, checked as `demo`, check only exit 3).
- Failures filed and listed in KnownFailures.txt: BL-4652 (harness datagram connector ignores `.invalid`, so coap, coaps, dns, webtransport give exit 7 not 6) and BL-4653 (oci prefixes its exit 6 and 7 text). Also found outside the harness: BL-4654 (a closed UDP port on Windows gives exit 2 via the real socket).
- Curl.Conformance.Extended.UnitLibrary is not changed by this task, so its coverage criterion holds unchanged (no quality run needed). Tests are platform-neutral: no paths in expected text; the helper file name follows PluginRun's `.cmd` rule. CI on Linux/macOS: platform-neutral and fast tests green here.
- `dotnet build Curl.slnx -warnaserror` clean; `dotnet test --filter "TestCategory!=Integration"` green (115 test projects passed).

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 33eff2c7 e6993618 fe617ef5 be674468 9438adeb ef6c9026.
- 2026-11-02: Doing -> Done. Every extended scheme's URL errors (exit 3, 6, 7) are checked in process; failures listed under BL-4652 and BL-4653
