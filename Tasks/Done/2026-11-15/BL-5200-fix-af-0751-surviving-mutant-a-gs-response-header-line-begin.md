---
id: BL-5200
title: Fix AF-0751: Surviving mutant: a gs:// response header line beginning with whitespace can be accepted
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.GoogleCloudStorage.UnitLibrary]
requirement: none
created: 2026-11-14
completed: 2026-11-14
---
# BL-5200 — Fix AF-0751: Surviving mutant: a gs:// response header line beginning with whitespace can be accepted

## Goal

The defect the audit office reported as AF-0751 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0751 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extended.Protocol.GoogleCloudStorage.UnitLibrary/Http/GcsHttpResponseHead.cs:31`

Location: `Curl.Extended.Protocol.GoogleCloudStorage.UnitLibrary/Http/GcsHttpResponseHead.cs:31`

Mutant 'line.AsSpan(0, colon).IndexOfAny(' ', '\t') >= 0' -> '> 0' survived. A field line starting with a space or tab (obsolete folding) would be accepted as a header named ' x' instead of failing MalformedHeaderField, which changes the transfer's exit code. SendAsync_MalformedField_FailsMalformedHeaderField has no DataRow with leading whitespace.

Reproduction: the audit office's mutation test changed `Curl.Extended.Protocol.GoogleCloudStorage.UnitLibrary/Http/GcsHttpResponseHead.cs line 31` as the evidence above describes (the mutant operator is `>=`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Extended.Protocol.GoogleCloudStorage.UnitLibrary/Http/GcsHttpResponseHead.cs line 31`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing. Criteria at claim: ba1ad67b 9742157d.

## Notes

- Killing test: `GcsHttpResponseHeadTests.ParseField_WhitespaceInTheName_FailsMalformedHeaderField` (rows " x: v" and "\tx: v"). With `>= 0` changed to `> 0` it failed 2 rows; reverted, all 147 fast tests pass.
- Why the connection-level test could not kill it: `ReadHeadLineAsync` already rejects a leading space or tab before `ParseField`, so the check at line 31 is reached with leading whitespace only by a direct `ParseField` call. Tested there; no production change.
- Build clean; CI-on-Linux/macOS criterion met by platform-neutral tests.
- 2026-11-14: Doing -> Done. Added GcsHttpResponseHeadTests.ParseField_WhitespaceInTheName_FailsMalformedHeaderField, which kills the >= 0 mutant; build clean, 147 fast tests pass.
