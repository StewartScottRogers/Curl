---
id: BL-5182
title: Fix AF-0737: Surviving mutant: a resumed IMAP connection can send a second LOGOUT at shutdown
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Imap.UnitLibrary]
requirement: none
created: 2026-11-14
completed:
---
# BL-5182 — Fix AF-0737: Surviving mutant: a resumed IMAP connection can send a second LOGOUT at shutdown

## Goal

The defect the audit office reported as AF-0737 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0737 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Original.Protocol.Imap.UnitLibrary/ImapSession.cs:345`

Location: `Curl.Original.Protocol.Imap.UnitLibrary/ImapSession.cs:345`

Mutant kept.LogsOutOnShutDown = false -> true in OpenOrResumeAsync survived (seed 641133). The doc comment says a resumed connection sends no LOGOUT at exit unless this transfer leaves it intact too; ImapKeptConnection.ShutDownAsync (lines 84-96) writes <tag> LOGOUT whenever LogsOutOnShutDown is set, and only Remember sets it back to true. With the mutant, a resumed transfer that ends with its own LOGOUT (a failure) is followed at shutdown by a second tagged LOGOUT: different request bytes. No test resumes a kept connection and then fails the second transfer.

Reproduction: the audit office's mutation test changed `Curl.Original.Protocol.Imap.UnitLibrary/ImapSession.cs line 345` as the evidence above describes (the mutant operator is `false`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Original.Protocol.Imap.UnitLibrary/ImapSession.cs line 345`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Killed by `ExecuteAsync_ResumedTransferFails_SendsOneLogoutAndNoSecondAtShutDown` (ImapKeptConnectionTests); with the mutant it fails, reverted all 542 pass. Test-only change.

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing. Criteria at claim: 9e97faca 9742157d.
