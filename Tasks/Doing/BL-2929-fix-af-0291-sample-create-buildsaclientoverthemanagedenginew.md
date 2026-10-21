---
id: BL-2929
title: Fix AF-0291: Sample_Create_BuildsAClientOverTheManagedEngineWithoutSendingAnything has no assertion
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Api.UnitTests]
requirement: none
created: 2026-10-21
completed:
---
# BL-2929 — Fix AF-0291: Sample_Create_BuildsAClientOverTheManagedEngineWithoutSendingAnything has no assertion

## Goal

The defect the audit office reported as AF-0291 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0291 (Low, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0291-sample-create-buildsaclientoverthemanagedenginewit.md`.

Location: `Curl.Api.UnitTests/Readme/ReadmeTests.cs:79`

Location: `Curl.Api.UnitTests/Readme/ReadmeTests.cs:79`

Step 0 candidate (no-assertion), read in full. The body is only await ReadmeSamples.CreateClientAsync(); and the sample builds a CurlClient and disposes it. Nothing checks that the client is over the managed engine or that nothing was sent, which is what the name states. It fails only if Create throws.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Api.UnitTests/Readme/ReadmeTests.cs -Pattern 'Sample_Create_BuildsAClientOverTheManagedEngineWithoutSendingAnything' -Context 0,3
```

- Expected: The body asserts the engine kind and that no transfer was performed.
- Actual: The body is { await ReadmeSamples.CreateClientAsync(); } with no Assert.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
