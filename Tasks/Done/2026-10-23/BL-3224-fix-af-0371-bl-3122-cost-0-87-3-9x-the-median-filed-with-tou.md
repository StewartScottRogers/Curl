---
id: BL-3224
title: Fix AF-0371: BL-3122 cost $0.87 (3.9x the median): filed with touches naming only Curl.Networking.UnitLibrary, requeued behind BL-3127, then rerun on Opus
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-23
completed: 2026-10-22
---
# BL-3224 — Fix AF-0371: BL-3122 cost $0.87 (3.9x the median): filed with touches naming only Curl.Networking.UnitLibrary, requeued behind BL-3127, then rerun on Opus

## Goal

What drove the past run's cost that the audit office reported as AF-0371 is found, and any avoidable waste in it is fixed, or the task records that there is none.

## Context

Filed from audit finding AF-0371 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0371-bl-3122-cost-0-87-3-9x-the-median-filed-with-touch.md`.

Location: `logs/BL-3122-20261022-143901-L2.jsonl`

Location: `logs/BL-3122-20261022-143901-L2.jsonl`

BL-3122 costUsd=0.87, claims=2, against a bar of 0.662. Run 1 (L2, 14:43:26): 8 turns, $0.296, result: 'I moved it back to Backlog before changing any code, because it waits on BL-3127.' Commit 6507ddd5b widened touches from [Curl.Networking.UnitLibrary] to add Curl.Networking.UnitTests/TcpConnectorQuicTests.UdpTunnel.cs. The requeue push waited until 14:54:44 (about 10 minutes). Run 2 (L6, 14:55:06): 21 turns, $0.574, including a refused Bash command ('Permission to use Bash with command head -5; cd .') and a by-hand mutant check (test 1/1, then 0/1). The fix was test-only ('needed only a new test, no production code change'), yet it ran on Opus ('names HTTP/3').

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-22 -LogRoot Z:\repos\Curl.audit\20261022-190247\logs -CiRunsJson Z:\repos\Curl.audit\20261022-190247\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-3122'
```

- Expected: costUsd at most 3 times the median (0.662).
- Actual: BL-3122 claims=2 minutes=5.52 costUsd=0.87 outcome=done

The finding describes a past run's cost, which no fix can change, so no re-audit can close it: the first audit after this task reaches Done closes it (closed-how task-done-historical, ADR-0422 amended 2026-10-14). Look for avoidable waste in what drove the cost and fix its cause, or record that there is none.

## Acceptance criteria

- [x] The cause of the cost is named in this task, and either fixed or recorded as unavoidable.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Cause: BL-3122 was filed with `touches` naming only Curl.Networking.UnitLibrary; its test-only fix needed Curl.Networking.UnitTests/TcpConnectorQuicTests.UdpTunnel.cs, which BL-3127 held, so run 1 (8 turns, $0.296) widened touches and requeued, and run 2 (21 turns, $0.574) did the work. Two claims doubled the cost; the 10-minute push wait cost time, not dollars.
- Run 2 ran on Opus because the factory's model rule picks opus when the task names HTTP/3; the rule is deliberate and keys on the task text, not on whether the change is test-only. Choosing sonnet would be a guess that risks a retried run costing more.
- Unavoidable, no fix: the requeue was the correct response to a real file overlap found only by reading code (lane rules 3 and 4), a one-off for a Low task at $0.87 against a $0.662 bar. The refused Bash command and by-hand mutant check were small. No change to RunDarkFactory.ps1 is warranted.

## Log

- 2026-10-23: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: af4de8c2 9742157d.
- 2026-10-22: Doing -> Done. AF-0371 cost cause recorded as unavoidable (touches overlap requeue, Opus by HTTP/3 rule)
