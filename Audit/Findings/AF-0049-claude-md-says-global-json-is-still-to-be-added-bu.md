---
id: AF-0049
title: CLAUDE.md says global.json is still to be added, but it exists
auditor: truthfulness
severity: Low
status: proposed
reason:
key: truthfulness:CLAUDE.md:global.json:false-statement
task: none
found: 2026-10-07
found-at: 5a627a2fb4baf7b4b2662dc309939ec576dcad20
scorecard: 2026-10-07_0844.md
closed:
closed-by:
---
# AF-0049 - CLAUDE.md says global.json is still to be added, but it exists

## Summary

Low finding from the truthfulness auditor at `CLAUDE.md:14`: CLAUDE.md says global.json is still to be added, but it exists.

## Evidence

Location: `CLAUDE.md:14`

CLAUDE.md:14 reads '.NET Software Development Kit 10 (see `global.json` once added).' global.json is at the repository root and pins {"sdk": {"version": "10.0.401", "rollForward": "latestFeature"}}. The statement is stale: it calls the file future work and does not point at the SDK pin that exists.

## Reproduction

Run from the repository root:

```powershell
(Select-String -Path CLAUDE.md -Pattern 'global.json. once added').Line; Test-Path global.json
```

- Expected: No match for 'once added' (the line points at global.json as it is), then True.
- Actual: - .NET Software Development Kit 10 (see `global.json` once added). Target framework: `net10.0` unless a project states otherwise.  then  True

## Re-audits

## Log

- 2026-10-07: filed proposed.
