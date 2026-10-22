---
id: AF-0323
title: CLAUDE.md's layout says 93 projects; the root holds 98 C# projects (103 in Curl.slnx with the shared projects)
auditor: truthfulness
severity: Low
status: proposed
reason:
key: truthfulness:CLAUDE.md:ProjectCount:false-statement
reproduction: none
task: none
tasks:
found: 2026-10-22
found-at: 167e765b2a009a4887fc971075283ef9d24a73a1
scorecard: 2026-10-22_0130.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0323 - CLAUDE.md's layout says 93 projects; the root holds 98 C# projects (103 in Curl.slnx with the shared projects)

## Summary

Low finding from the truthfulness auditor at `CLAUDE.md:297`: CLAUDE.md's layout says 93 projects; the root holds 98 C# projects (103 in Curl.slnx with the shared projects). Reported by an auditor flagged unreliable in 2026-10-22_0130.md.

## Evidence

Location: `CLAUDE.md:297`

CLAUDE.md:297: '├── ...                           ← 93 projects, one flat alphabetical run'. 98 directories directly under the root hold a same-named .csproj, Curl.slnx lists 98 .csproj paths, and Curl.slnx has 103 <Project> entries counting the five .shproj files.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path CLAUDE.md -Pattern '\d+ projects, one flat'; (Get-ChildItem -Directory | Where-Object { Test-Path (Join-Path $_.FullName ($_.Name + '.csproj')) }).Count
```

- Expected: The count in CLAUDE.md equals the number printed.
- Actual: CLAUDE.md:297 says 93 projects; the command prints 98.

## Re-audits

## Log

- 2026-10-22: filed proposed.
