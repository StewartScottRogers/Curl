---
id: BL-5724
title: Fix AF-0955: Surviving mutant: a connection reset can be taken for the peer's FIN (HasUnreadEnd catch returns true)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/StreamConnection.cs]
requirement: none
created: 2026-11-21
completed:
---
# BL-5724 — Fix AF-0955: Surviving mutant: a connection reset can be taken for the peer's FIN (HasUnreadEnd catch returns true)

## Goal

The defect the audit office reported as AF-0955 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0955 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Networking.UnitLibrary/StreamConnection.cs:106`

Location: `Curl.Networking.UnitLibrary/StreamConnection.cs:106`

Lines 98-107: HasUnreadEnd peeks one byte, and its catch (SocketException) { return false; } exists because 'a reset fails the peek and is not an end'. Mutated to return true it survived. The method carries [ExcludeFromCodeCoverage] ('measured by the Integration run'), so no fast test reaches it: a reset would be reported as an orderly end of the connection.

Reproduction: the audit office's mutation test changed `Curl.Networking.UnitLibrary/StreamConnection.cs line 106` as the evidence above describes (the mutant operator is `false`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Networking.UnitLibrary/StreamConnection.cs line 106`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing. Criteria at claim: 39f56745 9742157d.
