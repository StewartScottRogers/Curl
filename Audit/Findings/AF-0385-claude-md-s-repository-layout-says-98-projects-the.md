---
id: AF-0385
title: CLAUDE.md's repository layout says 98 projects; the tree has 101 project directories with a .csproj plus 5 shared projects
auditor: truthfulness
severity: Low
status: accepted
reason: 
key: truthfulness:CLAUDE.md:project-count:false-statement
reproduction: none
task: BL-3362
tasks: BL-3362
found: 2026-10-23
found-at: 32b1dda228a5fded8978ed4ee514a932b0728722
scorecard: 2026-10-23_0618.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0385 - CLAUDE.md's repository layout says 98 projects; the tree has 101 project directories with a .csproj plus 5 shared projects

## Summary

Low finding from the truthfulness auditor at `CLAUDE.md:297`: CLAUDE.md's repository layout says 98 projects; the tree has 101 project directories with a .csproj plus 5 shared projects. Reported by an auditor flagged unreliable in 2026-10-23_0618.md.

## Evidence

Location: `CLAUDE.md:297`

CLAUDE.md line 297, in the layout diagram: '├── ...  ← 98 projects, one flat alphabetical run'. 101 top-level directories hold a .csproj, and Curl.slnx lists 106 <Project> entries (the 101 plus the shared projects Audit, Documentation, Gap, Tasks and .claude/Claude). The count was set by commit 03ebe7766 (BL-3076) and has not kept up with new projects. It is true in substance (a flat run of many projects) but wrong in its number.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path CLAUDE.md -Pattern '98 projects' | ForEach-Object { '{0}:{1}' -f $_.Filename, $_.LineNumber }; (Get-ChildItem -Directory | Where-Object { Test-Path (Join-Path $_.FullName '*.csproj') }).Count
```

- Expected: The stated count equals the measured count (or no number is stated).
- Actual: CLAUDE.md:297 then 101

## Re-audits

- 2026-10-23 | 2026-10-23_2322.md | reproduces: no | Ran the reproduction: CLAUDE.md no longer contains '98 projects', and its repository layout gives no project count at all. The directory count with a .csproj is 103.

## Log

- 2026-10-23: filed proposed.
- 2026-10-23: proposed -> accepted.
