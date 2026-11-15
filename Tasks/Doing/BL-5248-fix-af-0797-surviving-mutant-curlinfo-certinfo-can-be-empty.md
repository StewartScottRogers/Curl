---
id: BL-5248
title: Fix AF-0797: Surviving mutant: CURLINFO_CERTINFO can be empty for certificate text that starts with its PEM block
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary]
requirement: none
created: 2026-11-15
completed:
---
# BL-5248 — Fix AF-0797: Surviving mutant: CURLINFO_CERTINFO can be empty for certificate text that starts with its PEM block

## Goal

The defect the audit office reported as AF-0797 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0797 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Engine.UnitLibrary/TransferInfoFromReport.CertInfo.cs:34`

Location: `Curl.Engine.UnitLibrary/TransferInfoFromReport.CertInfo.cs:34`

Mutant 'if (pemStart < 0)' -> 'if (pemStart <= 0)' in CertInfoRecordsOf survived (seed 5851). With the PEM block at offset 0 (no label lines before it), the mutant returns no records instead of the 'Cert:' record, so a certinfo reader sees nothing for that certificate. The tests only cover text with label lines first, or none at all.

Reproduction: the audit office's mutation test changed `Curl.Engine.UnitLibrary/TransferInfoFromReport.CertInfo.cs line 34` as the evidence above describes (the mutant operator is `<`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Engine.UnitLibrary/TransferInfoFromReport.CertInfo.cs line 34`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing. Criteria at claim: b882b1a9 9742157d.
