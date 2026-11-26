---
id: BL-5987
title: Fix AF-0993: Lib1553_PostsTheMimeWithTracingAndWaitsOnce never checks that the MIME post is set
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitTests/Lib1551To1559Tests.cs]
requirement: none
created: 2026-11-25
completed:
---
# BL-5987 — Fix AF-0993: Lib1553_PostsTheMimeWithTracingAndWaitsOnce never checks that the MIME post is set

## Goal

The defect the audit office reported as AF-0993 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0993 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Conformance.LibTest.UnitTests/Lib1551To1559Tests.cs:166`

Location: `Curl.Conformance.LibTest.UnitTests/Lib1551To1559Tests.cs:166`

Step 1. The test creates var mime = new FakeMimeBuilder(); but none of its assertions use it: they check the exit code, ProgramName, the wait count, that the TransferInfo and Debug callbacks are not null, and the abort line. Lib1553.cs:69-80 sets a part (name/value) and (CurlOption.Mimepost, mime). No assertion in the file mentions Mime, so dropping the Mimepost option or the part would pass. The 'WithTracing' part is checked only with Assert.IsNotNull(seen.Callbacks.Debug).

Reproduction, from the finding:

Run from the repository root:

```powershell
(Select-String -Path Curl.Conformance.LibTest.UnitTests/Lib1551To1559Tests.cs -Pattern 'Assert.*Mime').Count
```

- Expected: At least 1: an assertion that the transfer's MIME post holds the name=value part.
- Actual: 0

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
