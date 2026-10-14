---
id: AF-0176
title: ADR-0469 says a kept IMAP connection serves only the same login options; the reuse key leaves login options out
auditor: truthfulness
severity: High
status: closed
reason: Re-audit 2026-10-13_1822.md: the reproduction no longer reproduces.
key: truthfulness:Documentation/Planning/Decisions/ADR-0469-imap-keeps-its-connection-for-the-next-url-and-tags-by-connection-number.md:ImapKeptConnection.LoginKeyOf:stale-adr
reproduction: none
task: BL-2122
tasks: BL-2122
found: 2026-10-12
found-at: 4e6ddbc5b33356117bfec6778ba550ab61454a99
scorecard: 2026-10-12_1653.md
duplicate-of:
closed: 2026-10-13
closed-how: reliable-reaudit
closed-by: 2026-10-13_1822.md
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

- 2026-10-13 | 2026-10-13_0925.md | reproduces: no | Ran the reproduction: 'same user, password, login options' count in ADR-0469 = 0; 'LoginOptions' count in ImapKeptConnection.cs = 0. ADR-0469 decision 2 (lines 25-27) now says 'A kept connection serves only the same user, password and --ssl level; the login options (;AUTH= or --login-options) are not part of that key'. That matches ImapKeptConnection.LoginKeyOf and Serves (line 63, key == loginKey).
- 2026-10-13 | 2026-10-13_1822.md | reproduces: no | Ran the reproduction: 0;0. ADR-0469 no longer says 'same user, password, login options'. Its decision 2 (line 25) now says 'A kept connection serves only the same user, password and --ssl level; the login options (;AUTH= or --login-options) are not part of that key', which matches ImapKeptConnection.cs having no LoginOptions.

## Log

- 2026-10-12: filed proposed.
- 2026-10-12: proposed -> accepted.
- 2026-10-13: accepted -> closed. Re-audit 2026-10-13_1822.md: the reproduction no longer reproduces.
