---
id: AF-0567
title: CI red 54.97 minutes from run 41549222390 (6dfcd0aa): a stale known-failure line for Flow_FromTheBroker_IsAnsweredWithFlowOk, filed 32 minutes late because shift starts were refused over orphaned BL-2969
auditor: process
severity: Low
status: closed
reason: Re-audit 2026-11-03_1615.md: the reproduction no longer reproduces.
key: process:logs:ci-run-41549222390:ci-red
reproduction: none
task: BL-4715
tasks: BL-4715
found: 2026-11-03
found-at: ef78cb0569375bf8475eb1a2da931cfd224d52f4
scorecard: 2026-11-03_0025.md
duplicate-of:
closed: 2026-11-03
closed-how: reliable-reaudit
closed-by: 2026-11-03_1615.md
---
# AF-0567 - CI red 54.97 minutes from run 41549222390 (6dfcd0aa): a stale known-failure line for Flow_FromTheBroker_IsAnsweredWithFlowOk, filed 32 minutes late because shift starts were refused over orphaned BL-2969

## Summary

Low finding from the process auditor at `logs/DarkFactory-20261102-174304.log:31`: CI red 54.97 minutes from run 41549222390 (6dfcd0aa): a stale known-failure line for Flow_FromTheBroker_IsAnsweredWithFlowOk, filed 32 minutes late because shift starts were refused over orphaned BL-2969.

## Evidence

Location: `logs/DarkFactory-20261102-174304.log:31`

Spell 2026-11-03T00:22:12Z to 01:17:10Z, 54.97 minutes, from run 41549222390 on 6dfcd0aa (logs/ci-runs.json:797). Shift starts at 17:25:01 and 17:40:37 Arizona were refused: 'BL-2969 is in Doing and held by no lane' (DarkFactory-20261102-172455.log:1, DarkFactory-20261102-174031.log:1). CI watch first ran at 17:54:19 and filed BL-4584, 32 minutes after the run failed. L2 claimed it at 17:57:20 and finished in 2.5 minutes with nothing to change: 'its stale known-failure line was removed in 844d53972'. That commit (BL-4563, 00:56:31Z) was the real fix; it was integrated by L2 at 17:57:01 when it pushed the stopped lane's BL-4563 work. Runs 41556437882, 41556616280 and 41556972129 after it were cancelled; 41557110247 went green at 01:17:10Z. Cause: a lane integrated a fix that made an Extended conformance case pass without dropping its KnownFailures line.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-11-02 -LogRoot Z:\repos\Curl.audit\20261103-002553\logs -CiRunsJson Z:\repos\Curl.audit\20261103-002553\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Where-Object runId -eq 41549222390; Select-String -Path Z:\repos\Curl.audit\20261103-002553\logs\DarkFactory-20261102-1*.log -Pattern 'BL-4584|41549222390|refuse'
```

- Expected: No CI red spell over 30 minutes.
- Actual: Spell 2026-11-03T00:22:12Z to 01:17:10Z, 54.97 minutes, runId 41549222390; refused shift starts at 17:25:01 and 17:40:37; BL-4584 filed 17:54:19.

## Re-audits

- 2026-11-03 | 2026-11-03_1017.md | reproduces: yes | The given reproduction shows the 54.97-minute spell from 41549222390 and the 'refuse ... BL-2969 is in Doing and held by no lane' lines at 17:25:01 and 17:40:37. In this window the same main cause returned: run 41632220793 (27b710a0) went red at 12:19:28Z on stale known-failure lines for the ProtoJSON RoundTrip tests kept by a merge. BL-4724, 'Already fixed by 1df901115'; BL-4726, 'its stale Know~'. The spell lasted 44.7 minutes, over the 30-minute rule. The late-filing part did not recur: the tasks were filed 1 minute after the run, and this window's logs have no 'refuse' phase.
- 2026-11-03 | 2026-11-03_1615.md | reproduces: no | The reproduction still shows the historical spell (41549222390, 54.97 min) and the 'refuse BL-2969 is in Doing and held by no lane' lines at 17:25 and 17:40 on 2026-11-02. I re-audited by mechanism over this window (-Since 2026-11-03, logs 20261103-161553: three shifts, 44 tasks done). No coordinator log has a 'refuse' line, and no shift start was refused over an orphan. The CI red spells this window crossed (171.72, 122.97 and 71.8 min) have a different cause: failure logs gh could not read, and an idle coordinator that stopped watching CI. That cause is filed as new findings, not as this one.

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted.
- 2026-11-03: accepted -> closed. Re-audit 2026-11-03_1615.md: the reproduction no longer reproduces.
