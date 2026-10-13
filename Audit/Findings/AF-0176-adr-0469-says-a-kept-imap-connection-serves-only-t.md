---
id: AF-0176
title: ADR-0469 says a kept IMAP connection serves only the same login options; the reuse key leaves login options out
auditor: truthfulness
severity: High
status: accepted
reason: 
key: truthfulness:Documentation/Planning/Decisions/ADR-0469-imap-keeps-its-connection-for-the-next-url-and-tags-by-connection-number.md:ImapKeptConnection.LoginKeyOf:stale-adr
reproduction: none
task: none
tasks:
found: 2026-10-12
found-at: 4e6ddbc5b33356117bfec6778ba550ab61454a99
scorecard: 2026-10-12_1653.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0176 - ADR-0469 says a kept IMAP connection serves only the same login options; the reuse key leaves login options out

## Summary

High finding from the truthfulness auditor at `Documentation/Planning/Decisions/ADR-0469-imap-keeps-its-connection-for-the-next-url-and-tags-by-connection-number.md:25`: ADR-0469 says a kept IMAP connection serves only the same login options; the reuse key leaves login options out. Reported by an auditor flagged unreliable in 2026-10-12_1653.md.

## Evidence

Location: `Documentation/Planning/Decisions/ADR-0469-imap-keeps-its-connection-for-the-next-url-and-tags-by-connection-number.md:25`

ADR-0469 (Accepted), decision 2: 'A kept connection serves only the same user, password, login options (`;AUTH=` or `--login-options`) and `--ssl` level; one kept for another login is closed'. The code does something else. Curl.Protocol.Imap.UnitLibrary/ImapKeptConnection.cs:57 LoginKeyOf is string.Join('\0', context.Credentials?.UserName, context.Credentials?.Password, context.SslLevel), and its doc comment says: 'The ;AUTH= login options are not part of it: curl reuses a connection whose URLs differ only in them (upstream test1982, BL-2089).' ImapProtocolHandler.cs:110/147 reuses a kept connection whenever kept.Serves(loginKey). ImapKeptConnectionTests.ExecuteAsync_SecondUrlChangingOnlyTheLoginOptions_ReusesTheConnectionAsUpstreamTest1982 checks that the connection is reused. An agent following the ADR would put the login options back into the key and break test1982. Phase 2 note: task BL-2089 (Tasks/Done/2026-10-12_0448) removed the login options from the key on purpose and corrected LoginKeyOf's doc comment, but did not amend ADR-0469.

## Reproduction

Run from the repository root:

```powershell
(Select-String -Path Documentation/Planning/Decisions/ADR-0469-*.md -Pattern 'same user, password, login options').Count; (Select-String -Path Curl.Protocol.Imap.UnitLibrary/ImapKeptConnection.cs -Pattern 'LoginOptions').Count
```

- Expected: 0 then 0 (the ADR no longer lists login options in the reuse rule), or a non-zero second count (the key includes login options as the ADR says)
- Actual: 1 then 0: the ADR lists login options in the reuse rule, and the reuse key never reads them

## Re-audits

## Log

- 2026-10-12: filed proposed.
- 2026-10-12: proposed -> accepted.
