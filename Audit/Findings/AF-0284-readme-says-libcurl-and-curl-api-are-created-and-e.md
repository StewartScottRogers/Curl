---
id: AF-0284
title: README says Libcurl and Curl.Api are created and empty; both hold thousands of lines of implemented API
auditor: truthfulness
severity: High
status: closed
reason: Re-audit 2026-10-19_2134.md: the reproduction no longer reproduces.
key: truthfulness:README.md:Libcurl-and-Curl.Api-created-and-empty:false-statement
reproduction: none
task: BL-2811
tasks: BL-2811
found: 2026-10-18
found-at: e17dfce54cf6f2d08fc7c4abe0655128f237ff3b
scorecard: 2026-10-18_1905.md
duplicate-of:
closed: 2026-10-19
closed-how: reliable-reaudit
closed-by: 2026-10-19_2134.md
---
# AF-0284 - README says Libcurl and Curl.Api are created and empty; both hold thousands of lines of implemented API

## Summary

High finding from the truthfulness auditor at `README.md:68`: README says Libcurl and Curl.Api are created and empty; both hold thousands of lines of implemented API.

## Evidence

Location: `README.md:68`

README.md lines 67-71: "Where it stands: the engine is extracted and `Curl.Api.UnitLibrary`'s seam is defined; the console is being re-based onto the engine through it. `Libcurl` and `Curl.Api` are created and empty until their own phases: `Libcurl` is filled area by area next, then `Curl.Api` over it, then upstream's `tests/libtest` cases are ported to measure it." The tree has Libcurl with 113 .cs files (9406 lines: CurlGlobal, CurlEasy, options table, setters, multi, share, MIME) and Curl.Api with 71 .cs files (5397 lines: CurlClient, CurlHttpMessageHandler), and Curl.Conformance.LibTest.UnitLibrary already holds 295 Lib*.cs libtest ports driving Libcurl. The same README's own project table (line 60-61) describes both as working APIs. An agent reading 'Where it stands' would believe the APIs are unimplemented and could plan to create or fill them.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path README.md -Pattern 'created and empty' | ForEach-Object { $_.Line.Trim() }; foreach ($p in 'Libcurl','Curl.Api') { '{0}: {1} .cs files' -f $p, @(Get-ChildItem $p -Recurse -Filter *.cs | Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' }).Count }
```

- Expected: No 'created and empty' statement, or Libcurl and Curl.Api with no source files.
- Actual: created and empty until their own phases: `Libcurl` is filled area by area next, then / Libcurl: 113 .cs files / Curl.Api: 71 .cs files

## Re-audits

- 2026-10-19 | 2026-10-19_2134.md | reproduces: no | Ran the reproduction: Select-String found no 'created and empty' line in README.md; it printed only 'Libcurl: 115 .cs files' and 'Curl.Api: 71 .cs files'. README.md line 68 now reads '`Libcurl` and `Curl.Api` are implemented, not empty'.

## Log

- 2026-10-18: filed proposed.
- 2026-10-19: proposed -> accepted.
- 2026-10-19: accepted -> closed. Re-audit 2026-10-19_2134.md: the reproduction no longer reproduces.
