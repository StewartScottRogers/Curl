---
id: BL-1765
title: Fix AF-0087: Requirements FR-075 cites two tests by names they no longer have
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation]
requirement: none
created: 2026-10-08
completed:
---
# BL-1765 — Fix AF-0087: Requirements FR-075 cites two tests by names they no longer have

## Goal

The defect the audit office reported as AF-0087 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0087 (Low, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0087-requirements-fr-075-cites-two-tests-by-names-they.md`.

Location: `Documentation/Product/Requirements.md:227`

Location: `Documentation/Product/Requirements.md:227`

FR-075 says it is shown by `HttpResponseHeadReaderTests.ReadAsync_ConnectionResetWhileReadingTheHead_ReturnsRecvError` and `HttpResponseBodyReaderTests.CopyAsync_PeerResetsMidBody_ThrowsExit56AfterWritingWhatArrived`. Neither method exists. The tests were split by platform: Curl.Protocol.Http.UnitTests/HttpResponseHeadReaderTests.cs:383 ReadAsync_ConnectionResetWhileReadingTheHead_ReturnsRecvErrorWithTheWinsockWords and :397 ...WithTheSocketErrorsOwnWords, and HttpResponseBodyReaderTests.cs:156 CopyAsync_PeerResetsMidBody_ThrowsExit56WithTheWinsockWordsAfterWritingWhatArrived and :168 ...WithTheSocketErrorsOwnWordsAfterWritingWhatArrived. FR-075's 'Recv failure: Connection was reset' wording is also only the Windows answer. The cited names are prefixes of the real ones, so a reader can still find them.

Reproduction, from the finding:

Run from the repository root:

```powershell
(Get-ChildItem -Path *.UnitTests -Recurse -Filter *.cs | Select-String -Pattern 'ReadAsync_ConnectionResetWhileReadingTheHead_ReturnsRecvError\(|CopyAsync_PeerResetsMidBody_ThrowsExit56AfterWritingWhatArrived\(').Count; (Select-String -Path Documentation/Product/Requirements.md -SimpleMatch 'ReadAsync_ConnectionResetWhileReadingTheHead_ReturnsRecvError`').LineNumber
```

- Expected: Either both cited methods exist (count 2) or Requirements.md cites the current names (no line number).
- Actual: 0, then 227.

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
