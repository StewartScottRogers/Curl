---
id: BL-4612
title: Conformance: curl's URL errors (exit 3, 6, 7) for every extended scheme
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4505, BL-4506, BL-4507, BL-4502, BL-4503, BL-4463, BL-4611]
touches: [Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/UrlErrors, Documentation/Conformance/Extended/CurlSurface-Transfer-UrlErrors.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4612 — Conformance: curl's URL errors (exit 3, 6, 7) for every extended scheme

## Goal

Every extended scheme fails a malformed URL with exit 3, an unresolvable host with exit 6 and a refused connection with exit 7, as curl's manual ("EXIT CODES", curl 8.22.0) says, checked through the real curl command line.

## Context

For every scheme in each extension library's `*Extension.cs` (BL-4496 lists them, including `mcp+stdio` and `curl-proto-<scheme>`, which use the stand-in process starter where they get that far): a malformed URL (bad port, bad percent-encoding, a space) gives exit 3; an unresolvable host gives exit 6 (the harness feature BL-4611 adds); a port with no route gives exit 7. One shared data-driven test with one row file per scheme, or one case file per scheme, in `Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/UrlErrors/`; never a central switch. Rows go in the part matrix `CurlSurface-Transfer-UrlErrors.md` with `CurlSurface:` IDs (a `CurlSurface-<Part>.md` holds `CurlSurface:` IDs, BL-4476); a check that does not apply (no host in `mcp+stdio`) is `n/a: <reason>`. Where an extension's ADR deliberately differs, the case pins the ADR's answer and names it. A defect in an extension library or the engine goes in `KnownFailures.txt` with a filed High `Fix <Name> conformance: <requirement>` task (pipeline feature; touches that library, its `.UnitTests` and `KnownFailures.txt`; criteria: the case passes, a focused unit test pins the fix (ADR-0482), the line is removed).

Split from BL-4496 (the transfer surface for every extended protocol), which holds the whole plan; read its Context. Part of the extended-protocol conformance plan Stewart approved on 2026-11-02: follow `Documentation/Conformance/Extended/README.md`.

## Acceptance criteria

- [ ] `Documentation/Conformance/Extended/CurlSurface-Transfer-UrlErrors.md` lists exit 3 (bad port, bad percent-encoding, a space), exit 6 and exit 7 for every extended scheme, each with a `CurlSurface:` ID mapped to a test or to `n/a: <reason>`, and the harness's matrix check passes.
- [ ] Cases in `Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/UrlErrors/` run curl in process for every scheme and assert stdout, stderr and exit code for each check, naming the manual-page or ADR source.
- [ ] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt` with the ID of its filed High `Fix <Name> conformance: ...` task; the known-failures check passes.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green; no test needs `TestCategory=Integration`, opens a real socket or starts a real process.
- [ ] `Curl.Conformance.Extended.UnitLibrary` stays at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`), if this task changes it.
- [ ] No package added; every test is platform-neutral (no drive letters, Windows-only paths or text).

## Notes

## Log

- 2026-11-02: Created.
