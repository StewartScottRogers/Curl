---
id: BL-2811
title: Fix AF-0284: README says Libcurl and Curl.Api are created and empty; both hold thousands of lines of implemented API
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [README.md]
requirement: none
created: 2026-10-19
completed:
---
# BL-2811 — Fix AF-0284: README says Libcurl and Curl.Api are created and empty; both hold thousands of lines of implemented API

## Goal

The defect the audit office reported as AF-0284 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0284 (High, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0284-readme-says-libcurl-and-curl-api-are-created-and-e.md`.

Location: `README.md:68`

Location: `README.md:68`

README.md lines 67-71: "Where it stands: the engine is extracted and `Curl.Api.UnitLibrary`'s seam is defined; the console is being re-based onto the engine through it. `Libcurl` and `Curl.Api` are created and empty until their own phases: `Libcurl` is filled area by area next, then `Curl.Api` over it, then upstream's `tests/libtest` cases are ported to measure it." The tree has Libcurl with 113 .cs files (9406 lines: CurlGlobal, CurlEasy, options table, setters, multi, share, MIME) and Curl.Api with 71 .cs files (5397 lines: CurlClient, CurlHttpMessageHandler), and Curl.Conformance.LibTest.UnitLibrary already holds 295 Lib*.cs libtest ports driving Libcurl. The same README's own project table (line 60-61) describes both as working APIs. An agent reading 'Where it stands' would believe the APIs are unimplemented and could plan to create or fill them.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path README.md -Pattern 'created and empty' | ForEach-Object { $_.Line.Trim() }; foreach ($p in 'Libcurl','Curl.Api') { '{0}: {1} .cs files' -f $p, @(Get-ChildItem $p -Recurse -Filter *.cs | Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' }).Count }
```

- Expected: No 'created and empty' statement, or Libcurl and Curl.Api with no source files.
- Actual: created and empty until their own phases: `Libcurl` is filled area by area next, then / Libcurl: 113 .cs files / Curl.Api: 71 .cs files

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-19: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
