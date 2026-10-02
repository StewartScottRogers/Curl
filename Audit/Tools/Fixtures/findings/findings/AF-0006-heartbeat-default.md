---
id: AF-0006
title: HeartbeatMinutes help says 5
auditor: truthfulness
severity: High
status: blocked
reason: Waiting on the -HeartbeatMinutes default decision.
key: truthfulness:RunDarkFactory.ps1:HeartbeatMinutes:false-help
task: none
found: 2026-10-01
found-at: 1111111
scorecard: 2026-10-01_0900.md
closed: 
closed-by: 
---
# AF-0006 - HeartbeatMinutes help says 5

## Summary

Fixture finding.

## Evidence

Location: `fixture`

Fixture evidence.

## Reproduction

Run from the repository root:

```powershell
Write-Output fixture
```

- Expected: fixture
- Actual: fixture

## Re-audits

## Log

- 2026-10-01: filed proposed.
- 2026-10-02: proposed -> blocked. Waiting on the -HeartbeatMinutes default decision.
