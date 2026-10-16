---
id: BL-2441
title: Libcurl: CurlMime and CurlMimePart
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2395, BL-2406]
touches: [Libcurl/Mime/Mime, Libcurl.UnitTests/Mime/Mime]
requirement: none
created: 2026-10-15
completed:
---
# BL-2441 — Libcurl: CurlMime and CurlMimePart

## Goal

Libcurl has `CurlMime` and `CurlMimePart` (`curl_mime_init/_free/_addpart/_name/_filename/_type/_encoder/_data/_filedata/_data_cb/_subparts/_headers`) and `CURLOPT_MIMEPOST`.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [ ] Fake-engine tests pin every call and the option wiring.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [ ] `Measure-CodeQuality.ps1 -Library Libcurl` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [ ] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

## Log

- 2026-10-15: Created.
