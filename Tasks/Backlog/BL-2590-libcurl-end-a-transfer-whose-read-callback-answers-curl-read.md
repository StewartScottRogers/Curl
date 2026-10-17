---
id: BL-2590
title: Libcurl: end a transfer whose read callback answers CURL_READFUNC_ABORT with CURLE_ABORTED_BY_CALLBACK (test513)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-17
completed:
---
# BL-2590 — Libcurl: end a transfer whose read callback answers CURL_READFUNC_ABORT with CURLE_ABORTED_BY_CALLBACK (test513)

## Goal

A Libcurl transfer whose read callback answers `CURL_READFUNC_ABORT` ends with `CURLE_ABORTED_BY_CALLBACK` (42), so upstream test513 passes.

## Context

Found by BL-2528 (lib513 port). test513 posts with `CURLOPT_POSTFIELDSIZE` 1 and a read callback that returns `CURL_READFUNC_ABORT`; curl 8.22.0 answers 42, Curl answers 52 (`CURLE_GOT_NOTHING`): `test513 fails: <verify><errorcode>: expected exit code 42, got 52`. Run it with `CURL_UPSTREAM_CASE=<n>` and the `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust` test in `Curl.Conformance.UnitTests`.

## Acceptance criteria

- [ ] A focused unit test in `Curl.Engine.UnitTests` shows a read callback answering `CURL_READFUNC_ABORT` ends the transfer with `CURLE_ABORTED_BY_CALLBACK`.
- [ ] test513 passes through the runner on Windows and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Measure-CodeQuality.ps1` reports 0 failing members for the changed library.

## Notes

## Log

- 2026-10-17: Created.
