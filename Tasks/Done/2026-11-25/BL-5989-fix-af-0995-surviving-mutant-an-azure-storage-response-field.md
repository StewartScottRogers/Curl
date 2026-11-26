---
id: BL-5989
title: Fix AF-0995: Surviving mutant: an Azure Storage response field line starting with whitespace can be accepted as a field (>= -> >)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.AzureStorage.UnitLibrary/Http/AzureStorageHttpResponseHead.cs, Curl.Extended.Protocol.AzureStorage.UnitTests/Http/AzureStorageHttpResponseHeadTests.cs]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-5989 — Fix AF-0995: Surviving mutant: an Azure Storage response field line starting with whitespace can be accepted as a field (>= -> >)

## Goal

The defect the audit office reported as AF-0995 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0995 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extended.Protocol.AzureStorage.UnitLibrary/Http/AzureStorageHttpResponseHead.cs:31`

Location: `Curl.Extended.Protocol.AzureStorage.UnitLibrary/Http/AzureStorageHttpResponseHead.cs:31`

Mutant 'line.AsSpan(0, colon).IndexOfAny(' ', '\t') >= 0' -> '> 0' survived. A field line such as ' X: y' (obs-fold carrying a colon) then parses as a field named ' X' instead of failing MalformedHeaderField, so the transfer ends 0 instead of failing. SendAsync_MalformedField_FailsMalformedHeaderField's rows ' folded' and '\tfolded' have no colon and are refused by the colon <= 0 clause first, so whitespace at index 0 before a colon is never tested.

Reproduction: the audit office's mutation test changed `Curl.Extended.Protocol.AzureStorage.UnitLibrary/Http/AzureStorageHttpResponseHead.cs line 31` as the evidence above describes (the mutant operator is `>=`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Extended.Protocol.AzureStorage.UnitLibrary/Http/AzureStorageHttpResponseHead.cs line 31`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Killing test: `AzureStorageHttpResponseHeadTests.ParseField_WhitespaceBeforeTheName_FailsMalformedHeaderField`
  (rows ` X: y` and `\tX: y`). With `>= 0` changed to `> 0`, both rows fail (262 tests, 2 failed); reverted,
  the build has no warnings and the fast tests of all 115 test projects pass.
- The finding's stated effect does not happen through a transfer: `AzureStorageHttpClientConnection.ReadHeadLineAsync`
  refuses a line starting with a space or tab before `ParseField` is called. Rows added to
  `SendAsync_MalformedField_FailsMalformedHeaderField` therefore passed with the mutant applied, so the new test
  calls `ParseField` directly. No production code changed.
- `touches` widened to the new test file; no other task in Doing named it.
- Plan: test only, written directly (no pipeline subagents), so no quality run was needed.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: 97c33b77 9742157d.
- 2026-11-25: Doing -> Done. A direct ParseField test now kills the AF-0995 mutant: a field line starting with whitespace and holding a colon is refused.
