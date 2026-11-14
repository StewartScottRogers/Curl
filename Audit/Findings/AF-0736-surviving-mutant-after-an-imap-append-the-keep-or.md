---
id: AF-0736
title: Surviving mutant: after an IMAP APPEND the keep-or-LOGOUT choice can be inverted
auditor: quality
severity: High
status: proposed
reason:
key: quality:Curl.Original.Protocol.Imap.UnitLibrary/ImapSession.cs:AppendAsync-eq:surviving-mutant
reproduction: mutation Curl.Original.Protocol.Imap.UnitLibrary/ImapSession.cs:539:==
task: none
tasks:
found: 2026-11-13
found-at: 01e399058f0175f566ebf05c3a643be509c45063
scorecard: 2026-11-13_2321.md
duplicate-of:
closed:
closed-how:
closed-by:
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

## Log

- 2026-11-13: filed proposed.
