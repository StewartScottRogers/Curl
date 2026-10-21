---
id: BL-2887
title: Register UpstreamTest1707Script in the conformance ratchet and list cases 1707, 1708 and 1710
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-2866]
touches: [Curl.Conformance.UnitTests/UpstreamConformanceTests.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitTests/UpstreamTestData/README.md]
requirement: none
created: 2026-10-21
completed:
---
# BL-2887 — Register UpstreamTest1707Script in the conformance ratchet and list cases 1707, 1708 and 1710

## Goal

Upstream cases 1707, 1708 and 1710 run on the conformance ratchet through `UpstreamTest1707Script` and are on `PassingUpstreamCases.txt`.

## Context

Split from BL-2866, which built `UpstreamTest1707Script` (Curl.Conformance.UnitLibrary) and vendored real curl 8.22.0's manual as `UpstreamTestData/docs/cmdline-opts/curl.txt` (ADR-0719). `UpstreamTest1707ScriptTests.UpstreamCase_RunThroughTheInterpretation_Passes` already runs the three cases through `UpstreamCaseRunner` and they pass. Registering it needs `UpstreamConformanceTests.cs`, which BL-2841 held when BL-2866 ran.

Steps: in `RunCaseOnceAsync` (and the manual `NamedUpstreamCase` path if it builds its own runner), pass `perlCommandScripts: new Dictionary<...> { [UpstreamTest1707Script.ScriptName] = new UpstreamTest1707Script(UpstreamTestDataFolder).RunAsync }` (one static instance), merging with any scripts BL-2865 registers; add 1707, 1708 and 1710 to `PassingUpstreamCases.txt`; list `docs/cmdline-opts/curl.txt` in `UpstreamTestData/README.md` as real curl 8.22.0's `curl -M` text, not refreshed by `Update-UpstreamTestData.ps1` (ADR-0719).

## Acceptance criteria

- [ ] Cases 1707, 1708 and 1710 each end `Passed` with `$env:CURL_UPSTREAM_CASE='<n>'` on Windows and are on `PassingUpstreamCases.txt`.
- [ ] `UpstreamTestData/README.md` lists `docs/cmdline-opts/curl.txt` and where it came from.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-21: Created.
