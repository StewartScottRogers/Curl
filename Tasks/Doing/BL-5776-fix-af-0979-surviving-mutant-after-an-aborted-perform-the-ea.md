---
id: BL-5776
title: Fix AF-0979: Surviving mutant: after an aborted perform the easy handle can keep Digest as still available (?? false -> ?? true)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedEasyTransfer.Parsed.cs]
requirement: none
created: 2026-11-22
completed:
---
# BL-5776 — Fix AF-0979: Surviving mutant: after an aborted perform the easy handle can keep Digest as still available (?? false -> ?? true)

## Goal

The defect the audit office reported as AF-0979 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0979 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Parsed.cs:134`

Location: `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Parsed.cs:134`

Invoke-MutationTest -Library Curl.Engine.UnitLibrary -MaxMutants 40 -Seed 891023 (3 tests that already fail left out): survived. Mutated 'keptDigestStaysAvailable = result?.Report?.DigestStaysAvailable ?? false;' to '?? true'. After a perform that was aborted, or that returned no report, the handle would treat the kept Digest challenge as reusable, so the next perform on the handle could answer it again. That decides whether an Authorization header is sent. No test covers the null-result or null-report case.

Reproduction: the audit office's mutation test changed `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Parsed.cs line 134` as the evidence above describes (the mutant operator is `false`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Parsed.cs line 134`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing. Criteria at claim: b55ceb80 9742157d.
