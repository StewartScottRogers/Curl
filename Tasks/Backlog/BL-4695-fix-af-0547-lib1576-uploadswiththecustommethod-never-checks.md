---
id: BL-4695
title: Fix AF-0547: Lib1576_UploadsWithTheCustomMethod never checks the custom method, and checks the read callback only for null
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitTests]
requirement: none
created: 2026-11-03
completed:
---
# BL-4695 — Fix AF-0547: Lib1576_UploadsWithTheCustomMethod never checks the custom method, and checks the read callback only for null

## Goal

The defect the audit office reported as AF-0547 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0547 (Low, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0547-lib1576-uploadswiththecustommethod-never-checks-th.md`.

Location: `Curl.Conformance.LibTest.UnitTests/Lib1564To1588Tests.cs:362`

Location: `Curl.Conformance.LibTest.UnitTests/Lib1564To1588Tests.cs:362`

The body asserts the exit code, 'Assert.IsNotNull(read);' and the follow mode. Nothing checks the custom request method the name promises, and any read callback passes.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.LibTest.UnitTests/Lib1564To1588Tests.cs -Pattern 'Lib1576_UploadsWithTheCustomMethod' -Context 0,12
```

- Expected: An assertion on the custom request method and on what the read callback uploads.
- Actual: Assert.IsNotNull(read); is the only check of the upload.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-03: Created.
