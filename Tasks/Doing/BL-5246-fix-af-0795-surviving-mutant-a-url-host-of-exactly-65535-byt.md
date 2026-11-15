---
id: BL-5246
title: Fix AF-0795: Surviving mutant: a URL host of exactly 65535 bytes can be refused with exit 3
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary]
requirement: none
created: 2026-11-15
completed:
---
# BL-5246 — Fix AF-0795: Surviving mutant: a URL host of exactly 65535 bytes can be refused with exit 3

## Goal

The defect the audit office reported as AF-0795 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0795 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Engine.UnitLibrary/TransferPerformer.Setup.cs:171`

Location: `Curl.Engine.UnitLibrary/TransferPerformer.Setup.cs:171`

Mutant 'GetByteCount(url.Host) > MaximumHostLength' -> '>=' in ParsedUrlRefusal survived (seed 5851). MaximumHostLength is 65535 (line 59), and curl accepts hosts up to that length. The mutant refuses one of exactly 65535 bytes with CurlExitCode.UrlMalformat and TooLongHostnameMessage, changing the exit code. No test exercises the boundary.

Reproduction: the audit office's mutation test changed `Curl.Engine.UnitLibrary/TransferPerformer.Setup.cs line 171` as the evidence above describes (the mutant operator is `>`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Engine.UnitLibrary/TransferPerformer.Setup.cs line 171`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing. Criteria at claim: 771ae913 9742157d.
