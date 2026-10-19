---
id: BL-2670
title: Report the guessed http:// scheme and normalised path in CURLINFO_EFFECTIVE_URL so upstream case 1907 passes
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs, Curl.Engine.UnitLibrary/UrlEffective.cs, Curl.Engine.UnitLibrary/TransferInfoFromReport.cs, Curl.Engine.UnitTests/ManagedEasyTransferTests.cs, Curl.Engine.UnitTests/UrlEffectiveTests.cs, Curl.Engine.UnitTests/TransferInfoFromReportTests.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed: 2026-10-18
---
# BL-2670 — Report the guessed http:// scheme and normalised path in CURLINFO_EFFECTIVE_URL so upstream case 1907 passes

## Goal

`curl_easy_getinfo(CURLINFO_EFFECTIVE_URL)` after a transfer of a URL with no scheme answers the URL as libcurl does, with the guessed `http://` and the dot segments removed, and upstream case 1907 passes.

## Context

Found by BL-2545 (lib1907 port). Case 1907 runs `lib1907` on `%HOSTIP:%HTTPPORT/hello/../1907`. Its `<verify><stdout>` differs at byte 15 (line 1): expected `Effective URL: http://127.0.0.1:8990/1907`, got `Effective URL: 127.0.0.1:8990/hello/../1907`. The request bytes already match (`GET /1907`), so only the reported URL is the raw `CURLOPT_URL` text.

## Acceptance criteria

- [x] `CURLINFO_EFFECTIVE_URL` for a scheme-less URL holds the guessed scheme and the normalised path, with a focused unit test in the fixed library's own `.UnitTests` project.
- [x] Case 1907 passes through the runner on Windows and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

- 2026-10-18: touches narrowed from Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Libcurl, Libcurl.UnitTests to files, by reading ManagedEasyTransfer (TransferInfo.EffectiveUrl), UrlEffective and TransferInfoFromReport; the Libcurl CURLINFO_EFFECTIVE_URL reader only passes TransferInfo.EffectiveUrl through, so it can run beside other Engine tasks.

- 2026-10-18: Fix is one line in ManagedEasyTransfer.PerformedInfoOf: a library handle (no tool state) reports UrlEffective.Normalize of the effective URL; the tool runner path is unchanged.

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 45581baa d9ef3503 4272a6b9.
- 2026-10-18: Doing -> Done. Library handle reports guessed scheme and normalised path; case 1907 on the ratchet
