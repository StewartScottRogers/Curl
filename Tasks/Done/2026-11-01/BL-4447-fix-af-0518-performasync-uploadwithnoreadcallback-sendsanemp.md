---
id: BL-4447
title: Fix AF-0518: PerformAsync_UploadWithNoReadCallback_SendsAnEmptyBody checks only the request line, not the empty body
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitTests]
requirement: none
created: 2026-11-01
completed: 2026-11-01
---
# BL-4447 — Fix AF-0518: PerformAsync_UploadWithNoReadCallback_SendsAnEmptyBody checks only the request line, not the empty body

## Goal

The defect the audit office reported as AF-0518 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0518 (Low, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0518-performasync-uploadwithnoreadcallback-sendsanempty.md`.

Location: `Curl.Engine.UnitTests/ManagedEasyTransferTests.cs:552`

Location: `Curl.Engine.UnitTests/ManagedEasyTransferTests.cs:552`

The test asserts CurlCode.Ok and StringAssert.StartsWith(request, "PUT /up HTTP/1.1\r\n"). Nothing checks Content-Length: 0 or the bytes after the head, so a transfer that sent a non-empty body would pass.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Engine.UnitTests/ManagedEasyTransferTests.cs -Pattern 'SendsAnEmptyBody' -Context 0,12
```

- Expected: The body asserts the whole request (an empty body, e.g. Content-Length: 0).
- Actual: Line 552: StringAssert.StartsWith(Encoding.ASCII.GetString(easyBytes), "PUT /up HTTP/1.1\r\n"); is the only byte check.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The test now asserts the request line, a `Content-Length: 0` header, and that nothing follows the blank line ending the head, so a transfer that sent a body fails it. Touches left whole-project: the run's permissions refused the narrowing commit and push; only ManagedEasyTransferTests.cs changed.
- Measure-CodeQuality.ps1 not run: only a test file changed, no library's production code.

## Log

- 2026-11-01: Created.
- 2026-11-01: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-01: Doing -> Done. PerformAsync_UploadWithNoReadCallback_SendsAnEmptyBody asserts Content-Length: 0 and no bytes after the request head
