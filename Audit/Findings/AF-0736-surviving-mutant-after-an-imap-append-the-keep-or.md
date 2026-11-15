---
id: AF-0736
title: Surviving mutant: after an IMAP APPEND the keep-or-LOGOUT choice can be inverted
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-14_1335.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Original.Protocol.Imap.UnitLibrary/ImapSession.cs:AppendAsync-eq:surviving-mutant
reproduction: mutation Curl.Original.Protocol.Imap.UnitLibrary/ImapSession.cs:539:==
task: BL-5181
tasks: BL-5181
found: 2026-11-13
found-at: 01e399058f0175f566ebf05c3a643be509c45063
scorecard: 2026-11-13_2321.md
duplicate-of:
closed: 2026-11-14
closed-how: mechanical
closed-by: 2026-11-14_1335.md
---
# AF-0736 - Surviving mutant: after an IMAP APPEND the keep-or-LOGOUT choice can be inverted

## Summary

High finding from the quality auditor at `Curl.Original.Protocol.Imap.UnitLibrary/ImapSession.cs:539`: Surviving mutant: after an IMAP APPEND the keep-or-LOGOUT choice can be inverted.

## Evidence

Location: `Curl.Original.Protocol.Imap.UnitLibrary/ImapSession.cs:539`

Mutant await LogoutOrKeepAsync(result.ExitCode == CurlExitCode.Ok) -> (result.ExitCode != CurlExitCode.Ok) survived every Curl.Original.Protocol.Imap.UnitTests test (seed 641133). LogoutOrKeepAsync(keeps) holds the session for reuse when keeps is true and sends LOGOUT otherwise (ImapSession.cs:905-915). Inverted, a successful upload sends LOGOUT at once and a refused one keeps the connection, so a multi-URL upload (curl -T a imap://h/INBOX imap://h/INBOX) sends different request bytes: an extra LOGOUT and reconnect, or none after a failure. The APPEND tests run on connections that cannot hold a session, so both branches still end in LOGOUT there.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Original.Protocol.Imap.UnitLibrary/ImapSession.cs:539:== -Member AppendAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-11-14 | 2026-11-14_1335.md | reproduces: no | Ran the -Site command: ImapSession.cs:539 '==' mutant outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-13: filed proposed.
- 2026-11-14: accepted -> closed. Re-audit 2026-11-14_1335.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
