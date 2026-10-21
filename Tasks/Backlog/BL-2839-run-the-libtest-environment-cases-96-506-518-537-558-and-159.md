---
id: BL-2839
title: Run the libtest environment cases 96, 506, 518, 537, 558 and 1592: TrackMemory, getrlimit, threaded-resolver and a stalled name lookup
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitTests/UpstreamConformanceTests.cs, Curl.Conformance.UnitLibrary/UpstreamPlatformFeatureTable.cs]
requirement: none
created: 2026-10-19
completed:
---
# BL-2839 — Run the libtest environment cases 96, 506, 518, 537, 558 and 1592: TrackMemory, getrlimit, threaded-resolver and a stalled name lookup

## Goal

Each of upstream cases 96, 506, 518, 537, 558 and 1592 is either on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` or has a reason recorded in Notes and, where it is lasting, in an ADR.

## Context

Measured 2026-10-19 on Windows, each is skipped (gap scorecard 2026-10-19_1329 has 96 `debug-build-only` and the rest `libcurl-api`):

- 96 ("curl memory tracking operational", `curl --version`) and 558 (`lib558`, same name) need the `TrackMemory` feature: upstream's debug build records every allocation to a memory log. 1330 is the unit-test twin (BL-2849). C# has no malloc tracking; decide in an ADR whether the harness stands in (report `TrackMemory` and give the case the output it expects from `--version`) or the cases stay off because they test a C allocator.
- 506 ("HTTP with shared cookie list (and dns cache)", `lib506`) needs `!threaded-resolver,!c-ares`, a libcurl built with the synchronous resolver; Curl's resolver is `Curl.Networking.UnitLibrary`'s asynchronous one, listed as `threaded-resolver` in `UpstreamCurlPlatform` (ADR-0695). Decide whether the case can run with the feature reported absent (the shared DNS cache is what it tests) or must stay off.
- 518 (more than `FD_SETSIZE` descriptors open) and 537 (`lib537`, a file-descriptor limit) need `getrlimit`. `Lib518` and `Lib537` exist in `Curl.Conformance.LibTest.UnitLibrary`; they open many real files, so they belong to this machine's descriptor limit. Decide whether to report `getrlimit` on Linux and macOS and run them there (`ulimit`-bound), or record why not.
- 1592 (`lib1592`, "remove handle while resolving, do not block", host `a-site-never-accessed.example.org`) is skipped by `UpstreamCaseScreening` because the command goes to a host name on the internet. The case needs a name lookup that never answers; provide a harness resolver stand-in that stalls for that name (never `Thread.Sleep`; the injected `TimeProvider`) and exempt that case from the internet-host screen.

Follow ADR-0687/0678/0698 for any stand-in and keep it in the harness. Lanes run Windows only: a case that only Linux or macOS runs is judged by `CI` there (ADR-0488).

## Acceptance criteria

- [ ] Each of the six cases is, in Notes, either "stood in for" (and how), "listed", or "stays off" with a reason of one sentence; a lasting reason is in one new ADR "Decided by Claude under Stewart's delegation" naming the case numbers.
- [ ] Every case reported as listed ends `Passed` (or `Inconclusive` for another platform's feature) with `$env:CURL_UPSTREAM_CASE='<n>'` and is on `PassingUpstreamCases.txt`.
- [ ] Each new stand-in has a unit test that fails without it (the stalled lookup on a manual `TimeProvider`, the reported feature in `UpstreamCurlPlatformTests`).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Curl.Conformance.UnitLibrary` keeps 100% line and branch coverage.
- [ ] After the push, `CI` is green on Linux and macOS for the commit.

## Notes

## Log

- 2026-10-19: Created.
