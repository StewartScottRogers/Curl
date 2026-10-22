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
completed: 2026-10-21
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

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The create sample now returns its client (already disposed by its own `await using`). The test asserts the engine is `ManagedCurlEngine`, the client owns it, the three options the sample sets, and that the client's private `_transfers` driver is still null: it is created only by the first transfer, and `Curl.Api` exposes no transfer count, so reading it by reflection kept the change inside `Curl.Api.UnitTests` (the task's touches) rather than adding a production member. A rename of the field fails the test loudly (`Assert.IsNotNull` on the `FieldInfo`).
- Verified 2026-10-21: `dotnet build` 0 errors; fast tests green in all 43 test projects (Curl.Api.UnitTests 898 passed, 2 platform skips). No production code changed, so no quality run.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-21: Doing -> Done. Sample_Create test asserts the managed engine, ownership, the sample's options and that no transfer driver was created
