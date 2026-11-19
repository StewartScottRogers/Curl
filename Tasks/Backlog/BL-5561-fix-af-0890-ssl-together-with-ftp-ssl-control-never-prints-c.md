---
id: BL-5561
title: Fix AF-0890: --ssl together with --ftp-ssl-control never prints curl's 'Warning: --ssl overrides --ftp-ssl-control' (or the reverse) line
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/CommandLineWarning.cs]
requirement: none
created: 2026-11-19
completed:
---
# BL-5561 — Fix AF-0890: --ssl together with --ftp-ssl-control never prints curl's 'Warning: --ssl overrides --ftp-ssl-control' (or the reverse) line

## Goal

The defect the audit office reported as AF-0890 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0890 (Low, conformance auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Cli.UnitLibrary/CommandLineWarning.cs`

Location: `Curl.Cli.UnitLibrary/CommandLineWarning.cs`

Reference: curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Differential seed 446316, case 243 (--ftp-ssl-control --disallow-username-in-url --ssl), differs in stderr only. Reduced to '--ftp-ssl-control --ssl <url>': curl prints 'Warning: --ssl is an insecure option, consider --ssl-reqd instead' then 'Warning: --ssl overrides --ftp-ssl-control'. Curl prints only the first line. With the order reversed ('--ssl --ftp-ssl-control'), curl's second line is 'Warning: --ftp-ssl-control overrides --ssl' and Curl again prints nothing. Exit code (0) and request bytes are the same. No source in the tree produces either line: a grep for 'overrides --ssl' or 'overrides --ftp-ssl-control' in *.cs finds nothing, and CommandLineWarning.cs has override warnings only for --location/--follow (lines 95 and 105). Phase 2: no ADR records it.

Reproduction, from the finding:

Run from the repository root:

```powershell
dotnet build Curl.Console -c Release -nologo -v q; $c=(Resolve-Path Curl.Console/bin/Release/net10.0/curl.exe).Path; Push-Location $env:TEMP; & $c --ftp-ssl-control --ssl http://127.0.0.1:9/ 2>&1 | Select-String '^Warning'; Pop-Location
```

- Expected: Warning: --ssl is an insecure option, consider --ssl-reqd instead
Warning: --ssl overrides --ftp-ssl-control
- Actual: Warning: --ssl is an insecure option, consider --ssl-reqd instead

The finding closes only when a later re-audit by the conformance auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-19: Created.
