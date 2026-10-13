---
id: BL-2122
title: Fix AF-0176: ADR-0469 says a kept IMAP connection serves only the same login options; the reuse key leaves login options out
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation]
requirement: none
created: 2026-10-13
completed:
---
# BL-2122 — Fix AF-0176: ADR-0469 says a kept IMAP connection serves only the same login options; the reuse key leaves login options out

## Goal

The defect the audit office reported as AF-0176 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0176 (High, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0176-adr-0469-says-a-kept-imap-connection-serves-only-t.md`.

Location: `Documentation/Planning/Decisions/ADR-0469-imap-keeps-its-connection-for-the-next-url-and-tags-by-connection-number.md:25`

Location: `Documentation/Planning/Decisions/ADR-0469-imap-keeps-its-connection-for-the-next-url-and-tags-by-connection-number.md:25`

ADR-0469 (Accepted), decision 2: 'A kept connection serves only the same user, password, login options (`;AUTH=` or `--login-options`) and `--ssl` level; one kept for another login is closed'. The code does something else. Curl.Protocol.Imap.UnitLibrary/ImapKeptConnection.cs:57 LoginKeyOf is string.Join('\0', context.Credentials?.UserName, context.Credentials?.Password, context.SslLevel), and its doc comment says: 'The ;AUTH= login options are not part of it: curl reuses a connection whose URLs differ only in them (upstream test1982, BL-2089).' ImapProtocolHandler.cs:110/147 reuses a kept connection whenever kept.Serves(loginKey). ImapKeptConnectionTests.ExecuteAsync_SecondUrlChangingOnlyTheLoginOptions_ReusesTheConnectionAsUpstreamTest1982 checks that the connection is reused. An agent following the ADR would put the login options back into the key and break test1982. Phase 2 note: task BL-2089 (Tasks/Done/2026-10-12_0448) removed the login options from the key on purpose and corrected LoginKeyOf's doc comment, but did not amend ADR-0469.

Reproduction, from the finding:

Run from the repository root:

```powershell
(Select-String -Path Documentation/Planning/Decisions/ADR-0469-*.md -Pattern 'same user, password, login options').Count; (Select-String -Path Curl.Protocol.Imap.UnitLibrary/ImapKeptConnection.cs -Pattern 'LoginOptions').Count
```

- Expected: 0 then 0 (the ADR no longer lists login options in the reuse rule), or a non-zero second count (the key includes login options as the ADR says)
- Actual: 1 then 0: the ADR lists login options in the reuse rule, and the reuse key never reads them

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- ADR-0469 decision 2 now lists only user, password and `--ssl` level in the reuse key and says why login options are left out (upstream test1982, BL-2089). Markdown-only change; no code touched, so build and tests are unaffected.

## Log

- 2026-10-13: Created.
- 2026-10-12: Backlog -> Doing.
