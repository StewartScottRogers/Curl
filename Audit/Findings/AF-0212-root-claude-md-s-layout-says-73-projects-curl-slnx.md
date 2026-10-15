---
id: AF-0212
title: Root CLAUDE.md's layout says 73 projects; Curl.slnx lists 75 C# projects
auditor: truthfulness
severity: Low
status: accepted
reason: 
key: truthfulness:CLAUDE.md:RepositoryLayoutProjectCount:false-statement
reproduction: none
task: BL-2214
tasks: BL-2214
found: 2026-10-14
found-at: c1dedd04f2a8e1534e64b664312c5dacc66bdf4a
scorecard: 2026-10-14_0453.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0212 - Root CLAUDE.md's layout says 73 projects; Curl.slnx lists 75 C# projects

## Summary

Low finding from the truthfulness auditor at `CLAUDE.md:262`: Root CLAUDE.md's layout says 73 projects; Curl.slnx lists 75 C# projects. Reappeared; previously AF-0146.

## Evidence

Location: `CLAUDE.md:262`

CLAUDE.md:262 reads '├── ...  ← 73 projects, one flat alphabetical run'. Curl.slnx lists 75 .csproj projects (plus 5 shared-project entries), and 75 Curl.*/*.csproj exist. No reading gives 73: not the total (75), and not the total less the five shown (70).

## Reproduction

Run from the repository root:

```powershell
(Select-String -Path Curl.slnx -Pattern '\.csproj"').Count; (Select-String -Path CLAUDE.md -Pattern '73 projects').Count
```

- Expected: The project count in CLAUDE.md matches the solution: 75, and no '73 projects' line.
- Actual: 75 / 1

## Re-audits

- 2026-10-14 | 2026-10-14_1815.md | reproduces: no | Ran the reproduction: Curl.slnx lists 75 .csproj projects, and '73 projects' matches 0 lines in CLAUDE.md. CLAUDE.md line 265 now reads '75 projects, one flat alphabetical run'.

## Log

- 2026-10-14: filed proposed.
- 2026-10-14: proposed -> accepted.
