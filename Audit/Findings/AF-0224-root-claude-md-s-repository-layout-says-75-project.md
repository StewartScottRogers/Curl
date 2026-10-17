---
id: AF-0224
title: Root CLAUDE.md's repository layout says 75 projects; Curl.slnx lists 79
auditor: truthfulness
severity: Low
status: accepted
reason: 
key: truthfulness:CLAUDE.md:repository-layout-project-count:false-statement
reproduction: none
task: BL-2277
tasks: BL-2277
found: 2026-10-15
found-at: 3e1062e68644127a82ba2f5ef33ceb1044cc6f67
scorecard: 2026-10-15_0156.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0224 - Root CLAUDE.md's repository layout says 75 projects; Curl.slnx lists 79

## Summary

Low finding from the truthfulness auditor at `CLAUDE.md:265`: Root CLAUDE.md's repository layout says 75 projects; Curl.slnx lists 79. Reappeared; previously AF-0001. Reported by an auditor flagged unreliable in 2026-10-15_0156.md.

## Evidence

Location: `CLAUDE.md:265`

The layout block in the root CLAUDE.md reads '...  <- 75 projects, one flat alphabetical run'. Curl.slnx lists 79 .csproj entries, every one a Curl.* directory at the root, and 79 Curl.* directories exist. AF-0212's literal '73 projects' is gone, but the corrected count is still wrong.

## Reproduction

Run from the repository root:

```powershell
(Select-String -Path Curl.slnx -Pattern '\.csproj"').Count; (Select-String -Path CLAUDE.md -Pattern '\d+ projects, one flat').Line.Trim()
```

- Expected: 79, and the CLAUDE.md line names 79 projects
- Actual: 79, and the CLAUDE.md line reads '... 75 projects, one flat alphabetical run'

## Re-audits

- 2026-10-16 | 2026-10-16_1623.md | reproduces: yes | Ran the reproduction: 93, then '... 84 projects, one flat alphabetical run'. The count in CLAUDE.md changed (from 75 to 84), but it still does not match the 93 .csproj entries in Curl.slnx. 93 csproj files are also on disk (ls */*.csproj).

## Log

- 2026-10-15: filed proposed.
- 2026-10-15: proposed -> accepted.
