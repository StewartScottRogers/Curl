---
id: AF-0737
title: Surviving mutant: a resumed IMAP connection can send a second LOGOUT at shutdown
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-14_1335.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Original.Protocol.Imap.UnitLibrary/ImapSession.cs:OpenOrResumeAsync-false:surviving-mutant
reproduction: mutation Curl.Original.Protocol.Imap.UnitLibrary/ImapSession.cs:345:false
task: BL-5182
tasks: BL-5182
found: 2026-11-13
found-at: 01e399058f0175f566ebf05c3a643be509c45063
scorecard: 2026-11-13_2321.md
duplicate-of:
closed: 2026-11-14
closed-how: mechanical
closed-by: 2026-11-14_1335.md
---
# AF-0737 - Surviving mutant: a resumed IMAP connection can send a second LOGOUT at shutdown

## Summary

High finding from the quality auditor at `Curl.Original.Protocol.Imap.UnitLibrary/ImapSession.cs:345`: Surviving mutant: a resumed IMAP connection can send a second LOGOUT at shutdown.

## Evidence

Location: `Curl.Original.Protocol.Imap.UnitLibrary/ImapSession.cs:345`

Mutant kept.LogsOutOnShutDown = false -> true in OpenOrResumeAsync survived (seed 641133). The doc comment says a resumed connection sends no LOGOUT at exit unless this transfer leaves it intact too; ImapKeptConnection.ShutDownAsync (lines 84-96) writes <tag> LOGOUT whenever LogsOutOnShutDown is set, and only Remember sets it back to true. With the mutant, a resumed transfer that ends with its own LOGOUT (a failure) is followed at shutdown by a second tagged LOGOUT: different request bytes. No test resumes a kept connection and then fails the second transfer.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Original.Protocol.Imap.UnitLibrary/ImapSession.cs:345:false -Member OpenOrResumeAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-11-14 | 2026-11-14_1335.md | reproduces: no | Ran the -Site command: ImapSession.cs:345 'false' mutant outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-13: filed proposed.
- 2026-11-14: accepted -> closed. Re-audit 2026-11-14_1335.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
