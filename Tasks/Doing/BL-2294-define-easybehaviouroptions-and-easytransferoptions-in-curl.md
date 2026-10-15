---
id: BL-2294
title: Define EasyBehaviourOptions and EasyTransferOptions in Curl.Api.UnitLibrary with libcurl's defaults
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-2292]
touches: [Curl.Api.UnitLibrary/Options/Behaviour, Curl.Api.UnitTests/Options/Behaviour]
requirement: none
created: 2026-10-15
completed:
---
# BL-2294 — Define EasyBehaviourOptions and EasyTransferOptions in Curl.Api.UnitLibrary with libcurl's defaults

## Goal

`Curl.Api.UnitLibrary/Options/Behaviour` (namespace `Curl.Api.Options.Behaviour`) defines `EasyBehaviourOptions` and `EasyTransferOptions`, immutable records carrying every option of the two groups in the plan's CURLOPT table, each defaulting to libcurl 8.22.0's default.

## Context

ADR-0506, "The engine's input is a CLI-neutral option model"; plan section "CURLOPT groups", rows `EasyBehaviourOptions` and `EasyTransferOptions`. These records are what the engine reads once BL-2308 retargets it, what `Libcurl`'s `SetOption` writes, and what `Curl.Api`'s option records map onto.

Rules for every option-group task (this one, BL-2295 to BL-2300):
- One `sealed record` per group with `init` properties; enumerated values get their own enums in the same folder (for example `TimeCondition` for `CURL_TIMECOND_*`); numbers that libcurl takes in milliseconds or seconds become `TimeSpan`, sizes `long`; string lists `IReadOnlyList<string>`.
- Every property's XML doc names the `CURLOPT_*` it carries (both names where `_LARGE`/`_MS` variants exist) and libcurl's default. Source: curl 8.22.0's `docs/libcurl/opts/CURLOPT_*.md`, <https://github.com/curl/curl/tree/curl-8_22_0/docs/libcurl/opts>. `Curl.Cli.UnitLibrary`'s `LibcurlSourceCode*` already names the `CURLOPT_*` each command-line option sets - use it to check coverage.
- No reference to a protocol library or the engine; `Curl.Api.UnitLibrary` stays the seam only.
- Options the Schannel build answers `CURLE_NOT_BUILT_IN` or `CURLE_UNKNOWN_OPTION` for are still carried; the engine decides.

## Acceptance criteria

- [ ] `EasyBehaviourOptions` and `EasyTransferOptions` exist in `Curl.Api.UnitLibrary/Options/Behaviour` and carry every option in the plan's two rows, each documented with its `CURLOPT_*` name and default.
- [ ] Tests in `Curl.Api.UnitTests/Options/Behaviour` pin every default (a new record equals libcurl's defaults) and a `with` copy changes only the property set.
- [ ] This task's Notes list each `CURLOPT_*` of the plan's two rows with the property that carries it (none left without one).
- [ ] `dotnet build` is clean (warnings are errors).
- [ ] `dotnet test --filter "TestCategory!=Integration"` passes.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged.

## Notes

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
