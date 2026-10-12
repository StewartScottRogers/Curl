---
id: AF-0146
title: CLAUDE.md's layout says 71 projects; the root holds 73 Curl.* projects
auditor: truthfulness
severity: Low
status: closed
reason: Re-audit 2026-10-11_1638.md: the reproduction no longer reproduces.
key: truthfulness:CLAUDE.md:RepositoryLayoutProjectCount:false-statement
reproduction: none
task: BL-1965
tasks: BL-1965
found: 2026-10-10
found-at: 1b27494521dec4bdaa3fe60c8dc7a3fc73874253
scorecard: 2026-10-10_0123.md
duplicate-of:
closed: 2026-10-11
closed-how: reliable-reaudit
closed-by: 2026-10-11_1638.md
---
# AF-0146 - CLAUDE.md's layout says 71 projects; the root holds 73 Curl.* projects

## Summary

Low finding from the truthfulness auditor at `CLAUDE.md:253`: CLAUDE.md's layout says 71 projects; the root holds 73 Curl.* projects.

## Evidence

Location: `CLAUDE.md:253`

The Repository layout tree in the root CLAUDE.md reads '├── ...  ← 71 projects, one flat alphabetical run'. The repository root holds 73 Curl.* directories, each with a .csproj (Curl.slnx lists the same 73 plus 5 shared projects), so the count is stale by two (for example Curl.Conformance.SshServer.UnitLibrary and its UnitTests twin).

## Reproduction

Run from the repository root:

```powershell
Select-String -Path CLAUDE.md -Pattern '\d+ projects, one flat' | ForEach-Object { $_.Line.Trim() }; @(Get-ChildItem -Directory -Filter 'Curl.*' | Where-Object { Get-ChildItem $_.FullName -Filter *.csproj }).Count
```

- Expected: The number in the CLAUDE.md line equals the count printed after it.
- Actual: '... 71 projects, one flat alphabetical run' then 73

## Re-audits

- 2026-10-11 | 2026-10-11_0051.md | reproduces: no | Ran the reproduction from the tree root. Select-String printed '...                            73 projects, one flat alphabetical run', and the count of Curl.* directories holding a .csproj was 73. CLAUDE.md's layout count matches the root, so the finding no longer reproduces.
- 2026-10-11 | 2026-10-11_1638.md | reproduces: no | Ran the reproduction: CLAUDE.md's line reads '... 73 projects, one flat alphabetical run', and the count of Curl.* directories holding a csproj is 73. They agree.

## Log

- 2026-10-10: filed proposed.
- 2026-10-10: proposed -> accepted.
- 2026-10-11: accepted -> closed. Re-audit 2026-10-11_1638.md: the reproduction no longer reproduces.
