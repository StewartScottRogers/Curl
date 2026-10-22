---
id: BL-2458
title: Libcurl: curl_version, curl_version_info and the feature bits
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2456]
touches: [Libcurl/Misc/Version, Libcurl.UnitTests/Misc/Version, Libcurl/CLAUDE.md]
requirement: none
created: 2026-10-15
completed: 2026-10-16
---
# BL-2458 — Libcurl: curl_version, curl_version_info and the feature bits

## Goal

Libcurl has `CurlVersion.Text` (`curl_version`), `CurlVersion.Info(CurlVersionAge)` (`curl_version_info` with every `CURLVERSION_*` age) and the `CURL_VERSION_*` feature bits, taken from `ICurlEngine.VersionInfo`, matching `curl -V`.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [x] A test pins the version string, protocol list and feature bits for each age against the fake engine (`CurlVersionTests.Info_AnyAge_AnswersEveryFieldOfTheNewestAge`, all twelve ages), and `Text_EngineWithTlsBackend_IsTheTailOfCurlVsFirstLine` checks them against the Schannel build's `curl -V` lines; the same check over the real `ManagedCurlEngine` is split to BL-2465 (see Notes).
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Measure-CodeQuality.ps1 -Library Libcurl` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

- Seam as it is: `ICurlEngine.VersionInfo` (`CurlVersionInfo`, ADR-0509) carries version, number,
  host, feature names, protocols and TLS backend; `ManagedCurlEngine` fills only `Protocols` (with
  `smb`/`smbs`, which `-V` leaves out) and leaves features, host and backend empty. So the real engine
  cannot agree with `curl -V` until the engine changes, and `Curl.Engine.UnitLibrary` is held by
  BL-2424 in Doing. Split: Libcurl's `CurlVersion` lands here over fakes; BL-2465 (filed, depends on
  this task) fills the engine's `VersionInfo` from `CurlVersionText` and adds the real-engine agreement
  test, with the mapping spelled out in its Context.
- Decisions in ADR-0591: `Info` ignores the age as upstream's `curl_version_info` does and answers
  every `CURLVERSION_NOW` field; `Text` is `libcurl/<version> <backend>`, the tail of `-V`'s first
  line; feature bits from `lib/version.c`'s name table (exact match); library versions the seam lacks
  are `null`/0. Ages and bits taken from curl 8.22.0's `include/curl/curl.h` (tag `curl-8_22_0`).
- Added `Libcurl/CLAUDE.md` to `touches` for the documentation criterion; no task in Doing names it.
- Measured: Libcurl 100% line, 100% branch, 0 failing members; fast tests all green
  (Libcurl.UnitTests 828).

## Log

- 2026-10-15: Created.
- 2026-10-16: Backlog -> Doing.
- 2026-10-16: Doing -> Done. Libcurl CurlVersion.Text (curl_version) and CurlVersion.Info (curl_version_info, every CURLVERSION age, CURL_VERSION_* bits) read the engine's VersionInfo; real-engine agreement with curl -V is BL-2465
