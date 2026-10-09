---
id: AF-0049
title: CLAUDE.md says global.json is still to be added, but it exists
auditor: truthfulness
severity: Low
status: closed
reason: Re-audit 2026-10-08_2315.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-10-08_0748.md, 2026-10-08_2315.md).
key: truthfulness:CLAUDE.md:global.json:false-statement
reproduction: none
task: BL-1676
tasks: BL-1676
found: 2026-10-07
found-at: 5a627a2fb4baf7b4b2662dc309939ec576dcad20
scorecard: 2026-10-07_0844.md
duplicate-of:
closed: 2026-10-08
closed-how: consecutive
closed-by: 2026-10-08_0748.md, 2026-10-08_2315.md
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

- 2026-10-07 | 2026-10-07_1336.md | reproduces: yes | Ran the reproduction. Select-String still returns CLAUDE.md's line '- .NET Software Development Kit 10 (see `global.json` once added). Target framework: ...', and Test-Path global.json returns True. The document still says global.json is yet to be added although it exists.
- 2026-10-08 | 2026-10-08_0748.md | reproduces: no | Select-String for 'global.json. once added' in CLAUDE.md returned no line; Test-Path global.json returned True. CLAUDE.md now says global.json pins SDK 10.0.401 with rollForward latestFeature, which matches the file.
- 2026-10-08 | 2026-10-08_2315.md | reproduces: no | Select-String for 'global.json. once added' in CLAUDE.md found nothing; Test-Path global.json is True. CLAUDE.md now says global.json pins SDK 10.0.401 with rollForward latestFeature, which matches the file.

## Log

- 2026-10-07: filed proposed.
- 2026-10-07: proposed -> accepted.
- 2026-10-08: accepted -> closed. Re-audit 2026-10-08_2315.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-10-08_0748.md, 2026-10-08_2315.md).
