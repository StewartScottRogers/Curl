---
id: BL-2458
title: Libcurl: curl_version, curl_version_info and the feature bits
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2456]
touches: [Libcurl/Misc/Version, Libcurl.UnitTests/Misc/Version]
requirement: none
created: 2026-10-15
completed:
---
# BL-2458 — Libcurl: curl_version, curl_version_info and the feature bits

## Goal

Libcurl has `CurlVersion.Text` (`curl_version`), `CurlVersion.Info(CurlVersionAge)` (`curl_version_info` with every `CURLVERSION_*` age) and the `CURL_VERSION_*` feature bits, taken from `ICurlEngine.VersionInfo`, matching `curl -V`.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [ ] A test pins the version string, protocol list and feature bits for each age against the fake engine, and one test checks they agree with `curl -V` output from `Curl.Console`.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [ ] `Measure-CodeQuality.ps1 -Library Libcurl` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [ ] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

## Log

- 2026-10-15: Created.
- 2026-10-16: Backlog -> Doing.
