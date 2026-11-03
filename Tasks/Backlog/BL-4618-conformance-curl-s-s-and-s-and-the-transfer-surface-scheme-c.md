---
id: BL-4618
title: Conformance: curl's -s and -S and the transfer-surface scheme coverage check for every extended scheme
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4505, BL-4506, BL-4507, BL-4502, BL-4503, BL-4463, BL-4611, BL-4612, BL-4613, BL-4614, BL-4615, BL-4616, BL-4617]
touches: [Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/Silent, Documentation/Conformance/Extended/CurlSurface-Transfer.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4618 — Conformance: curl's -s and -S and the transfer-surface scheme coverage check for every extended scheme

## Goal

Every extended scheme's `-s` and `-S` change only what curl's manual says they change, and a check proves the transfer-surface part matrices cover every scheme every extension registers, closing BL-4496.

## Context

The last part of BL-4496: its parts BL-4612 to BL-4617 cover URL errors and the transfer options per scheme group in `CurlSurface-Transfer-<Part>.md`. This task (1) adds `-s` (no progress meter, no error message) and `-s -S` (error message shown) cases for every scheme in `Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/Silent/`, with rows in `Documentation/Conformance/Extended/CurlSurface-Transfer.md`, expected values from curl 8.22.0's manual ("-s", "-S"); and (2) adds a test that every scheme each extension's `*Extension.cs` registers appears in the transfer-surface matrices for every check BL-4496 names, so a new scheme cannot skip them. Defects go in `KnownFailures.txt` with a filed High fix task, as in BL-4496.

Split from BL-4496 (the transfer surface for every extended protocol), which holds the whole plan; read its Context. Part of the extended-protocol conformance plan Stewart approved on 2026-11-02: follow `Documentation/Conformance/Extended/README.md`.

## Acceptance criteria

- [ ] `Documentation/Conformance/Extended/CurlSurface-Transfer.md` lists `-s` and `-S` for every extended scheme with a `CurlSurface:` ID mapped to a test or to `n/a: <reason>`, and the harness's matrix check passes.
- [ ] Cases in `Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/Silent/` run curl in process for every scheme and assert stdout, stderr and exit code, naming the manual-page source.
- [ ] A test fails when a scheme some extension registers has no row for one of BL-4496's checks in the `CurlSurface-Transfer*.md` matrices, and passes on the tree.
- [ ] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt` with the ID of its filed High `Fix <Name> conformance: ...` task; the known-failures check passes.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green; no test needs `TestCategory=Integration`, opens a real socket or starts a real process.
- [ ] `Curl.Conformance.Extended.UnitLibrary` stays at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`), if this task changes it.
- [ ] No package added; every test is platform-neutral (no drive letters, Windows-only paths or text).

## Notes

## Log

- 2026-11-02: Created.
