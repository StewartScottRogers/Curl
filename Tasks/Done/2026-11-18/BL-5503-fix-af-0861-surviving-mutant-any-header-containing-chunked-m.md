---
id: BL-5503
title: Fix AF-0861: Surviving mutant: any header containing 'chunked' makes the reported upload length -1
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs]
requirement: none
created: 2026-11-18
completed: 2026-11-18
---
# BL-5503 — Fix AF-0861: Surviving mutant: any header containing 'chunked' makes the reported upload length -1

## Goal

The defect the audit office reported as AF-0861 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0861 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs:132`

Location: `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs:132`

Mutant 'header.StartsWith("Transfer-Encoding:") && header.Contains("chunked")' -> '||' survived Curl.Engine.UnitTests. With ||, every Transfer-Encoding header, and any custom header whose value holds 'chunked' (e.g. 'X-Mode: chunked'), makes HttpRequestBodyLengthOf answer -1. That value is ContentLengthUploadOf, CURLINFO_CONTENT_LENGTH_UPLOAD, so the getinfo value is wrong for a known-length POST. No test sends a non-Transfer-Encoding header containing 'chunked', or a Transfer-Encoding header without it.

Reproduction: the audit office's mutation test changed `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs line 132` as the evidence above describes (the mutant operator is `&&`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs line 132`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Killed by `Cgr127Tests.PerformAsync_PostWithHeaderThatIsNotChunkedTransferEncoding_ContentLengthUploadIsTheBodySize` (both rows fail with the `||` mutant; all pass reverted). Test-only change.


## Log

- 2026-11-18: Created.
- 2026-11-18: Backlog -> Doing. Criteria at claim: bbe53f00 9742157d.
- 2026-11-18: Doing -> Done. Test added that kills the mutant
