---
id: BL-2220
title: Carry out the %days[N] instruction in upstream test files so cases 31, 46, 61, 483, 506 and 1415 run
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-14
completed:
---
# BL-2220 — Carry out the %days[N] instruction in upstream test files so cases 31, 46, 61, 483, 506 and 1415 run

## Goal

Make the upstream test-file expander carry out `%days[N]`, so cases 31, 46, 61, 483, 506 and 1415 run instead of being screened "the harness does not carry out %days".

## Context

Source: Stewart's 2026-10-14 request to raise the gap office's behaviour score (aim 97-99%) by making the shared in-process harness run cases it leaves unmeasured; the 8.22.0 gap run 2026-10-13_1406 had 38 `harness-unsupported` cases, and running `Curl.Conformance.UnitTests` on 2026-10-14 names these six for `%days`. The gap tool uses `Curl.Conformance.UnitLibrary`, so a case that runs here runs at the next gap run.

`%days[N]` appears in cookie-jar lines, e.g. `.host.foo.com	TRUE	/we/want	FALSE	%days[400]	test2	yes` (test61.curltest line 75): upstream's `runtests.pl` `prepro` replaces it with the time N days from now as epoch seconds. Read `runtests.pl` / `testutil.pm` of curl-8_22_0 (https://github.com/curl/curl/tree/curl-8_22_0/tests) for the exact arithmetic. Work in `UpstreamTestInstructions` / `UpstreamTestFileExpander` (`UnsupportedInstructions` lists `%days` today) and inject a `TimeProvider` so the tests are deterministic (never read the real clock; `Thread.Sleep` is forbidden).

## Acceptance criteria

- [ ] `%days[N]` expands to the epoch second N days after the injected `TimeProvider`'s now, pinned by `UpstreamTestFileExpanderTests` for N = 0, 400 and a value inside a longer line; `%days` is no longer in `UnsupportedInstructions`.
- [ ] Cases 31, 46, 61, 483, 506 and 1415 are no longer screened out. Each that passes is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; each that runs but fails is named in Notes as a real gap, with a follow-up task per distinct cause.
- [ ] Platform-neutral tests; `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary` shows no member outside a gate; `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` is green.
- [ ] `Curl.Conformance.UnitLibrary/CLAUDE.md` says `%days[N]` is carried out.

## Notes

## Log

- 2026-10-14: Created.
