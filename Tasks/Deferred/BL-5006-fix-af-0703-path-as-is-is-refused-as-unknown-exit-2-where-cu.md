---
id: BL-5006
title: Fix AF-0703: --path-as-is is refused as unknown (exit 2) where curl accepts it and sends the request
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary]
requirement: none
created: 2026-11-12
completed:
---
# BL-5006 — Fix AF-0703: --path-as-is is refused as unknown (exit 2) where curl accepts it and sends the request

## Goal

The defect the audit office reported as AF-0703 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0703 (High, conformance auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0703-path-as-is-is-refused-as-unknown-exit-2-where-curl.md`.

Location: `Curl.Cli.UnitLibrary/CurlOptionAliasTable.cs:174`

Location: `Curl.Cli.UnitLibrary/CurlOptionAliasTable.cs:174`

Reference: curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel. Seed 79116. Cases 21 and 25. curl exits 0 and sends GET / HTTP/1.1. Curl exits 2 with 'curl: option --path-as-is: is unknown', and nothing goes on the wire. The option is in CurlHelpTable.cs:165 and CurlOptionAliasTable.cs:174, and CommandLineOptions.cs:2006 documents it. The only grep hit for a quoted "path-as-is" outside doc comments in Curl.Cli.UnitLibrary is the alias table, so no parser case appears to handle it. The reduced command line is `--path-as-is http://127.0.0.1:PORT/` (case 21).

Reproduction, from the finding:

Run from the repository root:

```powershell
& ./Record-CurlExchange.ps1 -Port 51490 -Curl ./Curl.Console/bin/Release/net10.0/curl.exe -OutDirectory $env:TEMP\c -CurlArgs @('--path-as-is','http://127.0.0.1:51490/'); Get-Content $env:TEMP\c\exitcode.txt
```

- Expected: Exit 0; GET / HTTP/1.1 sent.
- Actual: Exit 2; 'curl: option --path-as-is: is unknown'; no request.

The finding closes only when a later re-audit by the conformance auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Deferred. Stewart rejected AF-0703 (2026-11-12): it is audit 20261111-200237's planted --path-as-is removal, not a Curl defect; --path-as-is is registered at CommandLineOptionTable.cs:332. Do not work it.
