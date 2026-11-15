---
id: BL-5232
title: Fix AF-0781: Surviving mutant: a header write starting with its line feed can lose that line from the recorded headers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary]
requirement: none
created: 2026-11-15
completed:
---
# BL-5232 — Fix AF-0781: Surviving mutant: a header write starting with its line feed can lose that line from the recorded headers

## Goal

The defect the audit office reported as AF-0781 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0781 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Engine.UnitLibrary/HeaderRecordingStream.cs:84`

Location: `Curl.Engine.UnitLibrary/HeaderRecordingStream.cs:84`

while ((lineFeed = rest.IndexOf((byte)'\n')) >= 0) mutated to > 0 survived. A write whose first byte is '\n' (a bare-LF blank line ending a header block, or a CRLF split across writes) is no longer split there, so TransferHeaderStore.RecordLine gets merged or missing lines, which the header API and %header{} read.

Reproduction: the audit office's mutation test changed `Curl.Engine.UnitLibrary/HeaderRecordingStream.cs line 84` as the evidence above describes (the mutant operator is `>=`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Engine.UnitLibrary/HeaderRecordingStream.cs line 84`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing. Criteria at claim: 72a82d61 9742157d.
