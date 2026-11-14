---
id: BL-5181
title: Fix AF-0736: Surviving mutant: after an IMAP APPEND the keep-or-LOGOUT choice can be inverted
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Imap.UnitLibrary]
requirement: none
created: 2026-11-14
completed: 2026-11-14
---
# BL-5181 — Fix AF-0736: Surviving mutant: after an IMAP APPEND the keep-or-LOGOUT choice can be inverted

## Goal

The defect the audit office reported as AF-0736 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0736 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Original.Protocol.Imap.UnitLibrary/ImapSession.cs:539`

Location: `Curl.Original.Protocol.Imap.UnitLibrary/ImapSession.cs:539`

Mutant await LogoutOrKeepAsync(result.ExitCode == CurlExitCode.Ok) -> (result.ExitCode != CurlExitCode.Ok) survived every Curl.Original.Protocol.Imap.UnitTests test (seed 641133). LogoutOrKeepAsync(keeps) holds the session for reuse when keeps is true and sends LOGOUT otherwise (ImapSession.cs:905-915). Inverted, a successful upload sends LOGOUT at once and a refused one keeps the connection, so a multi-URL upload (curl -T a imap://h/INBOX imap://h/INBOX) sends different request bytes: an extra LOGOUT and reconnect, or none after a failure. The APPEND tests run on connections that cannot hold a session, so both branches still end in LOGOUT there.

Reproduction: the audit office's mutation test changed `Curl.Original.Protocol.Imap.UnitLibrary/ImapSession.cs line 539` as the evidence above describes (the mutant operator is `==`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Original.Protocol.Imap.UnitLibrary/ImapSession.cs line 539`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Killed by `ExecuteAsync_AppendSucceeds_KeepsTheConnectionWithoutLogout` and `ExecuteAsync_AppendRefused_SendsLogoutInsteadOfKeepingTheConnection` (ImapKeptConnectionTests); both fail with the mutant, all pass reverted. Production code unchanged.

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing. Criteria at claim: 6e36a605 9742157d.
- 2026-11-14: Doing -> Done. Added two tests killing the inverted keep-or-LOGOUT mutant
