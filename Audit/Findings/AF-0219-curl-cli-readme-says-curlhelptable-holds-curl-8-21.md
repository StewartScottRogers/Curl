---
id: AF-0219
title: Curl.Cli README says CurlHelpTable holds curl 8.21.0's 274 help rows; it holds 278, four of them httpsig rows 8.21.0 lacks
auditor: truthfulness
severity: Low
status: accepted
reason: Stewart accepted it on 2026-10-14.
key: truthfulness:Curl.Cli.UnitLibrary/README.md:CurlHelpTable:false-statement
reproduction: none
task: BL-2256
tasks: BL-2256
found: 2026-10-14
found-at: d86a80f0091fbf88c08d7b9b2fa37a9197854bd8
scorecard: 2026-10-14_1815.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0219 - Curl.Cli README says CurlHelpTable holds curl 8.21.0's 274 help rows; it holds 278, four of them httpsig rows 8.21.0 lacks

## Summary

Low finding from the truthfulness auditor at `Curl.Cli.UnitLibrary/README.md:23`: Curl.Cli README says CurlHelpTable holds curl 8.21.0's 274 help rows; it holds 278, four of them httpsig rows 8.21.0 lacks. Reported by an auditor flagged unreliable in 2026-10-14_1815.md.

## Evidence

Location: `Curl.Cli.UnitLibrary/README.md:23`

README.md line 23: '`CurlHelpTable` | curl 8.21.0's 274 help rows (option as shown, description, `CurlHelpCategories`), copied from its generated `src/tool_listhelp.c`'. Curl.Cli.UnitLibrary/CurlHelpTable.cs has 278 row lines ('        new("...'), including lines 103-106 for --httpsig-algo, --httpsig-headers, --httpsig-key and --httpsig-keyid. Those rows were added for a curl 8.22.0 built with --enable-httpsig (ADR-0487), so the count and the 'curl 8.21.0' source are both stale.

## Reproduction

Run from the repository root:

```powershell
(Select-String -Path Curl.Cli.UnitLibrary/CurlHelpTable.cs -Pattern '^        new\("').Count; (Select-String -Path Curl.Cli.UnitLibrary/README.md -Pattern '274 help rows').Count
```

- Expected: The README's row count equals the table's: 274 rows, or the README states 278 and their source.
- Actual: 278 / 1

## Re-audits

## Log

- 2026-10-14: filed proposed.
- 2026-10-14: proposed -> accepted.
