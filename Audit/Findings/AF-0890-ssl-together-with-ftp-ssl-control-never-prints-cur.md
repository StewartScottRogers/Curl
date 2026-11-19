---
id: AF-0890
title: --ssl together with --ftp-ssl-control never prints curl's 'Warning: --ssl overrides --ftp-ssl-control' (or the reverse) line
auditor: conformance
severity: Low
status: proposed
reason:
key: conformance:Curl.Cli.UnitLibrary/CommandLineWarning.cs:--ftp-ssl-control:stderr
reproduction: none
task: none
tasks:
found: 2026-11-19
found-at: 6e68cb60b626d421113f6d7d7f35c2a46bbc3b46
scorecard: 2026-11-19_0602.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0890 - --ssl together with --ftp-ssl-control never prints curl's 'Warning: --ssl overrides --ftp-ssl-control' (or the reverse) line

## Summary

Low finding from the conformance auditor at `Curl.Cli.UnitLibrary/CommandLineWarning.cs`: --ssl together with --ftp-ssl-control never prints curl's 'Warning: --ssl overrides --ftp-ssl-control' (or the reverse) line.

## Evidence

Location: `Curl.Cli.UnitLibrary/CommandLineWarning.cs`

Reference: curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Differential seed 446316, case 243 (--ftp-ssl-control --disallow-username-in-url --ssl), differs in stderr only. Reduced to '--ftp-ssl-control --ssl <url>': curl prints 'Warning: --ssl is an insecure option, consider --ssl-reqd instead' then 'Warning: --ssl overrides --ftp-ssl-control'. Curl prints only the first line. With the order reversed ('--ssl --ftp-ssl-control'), curl's second line is 'Warning: --ftp-ssl-control overrides --ssl' and Curl again prints nothing. Exit code (0) and request bytes are the same. No source in the tree produces either line: a grep for 'overrides --ssl' or 'overrides --ftp-ssl-control' in *.cs finds nothing, and CommandLineWarning.cs has override warnings only for --location/--follow (lines 95 and 105). Phase 2: no ADR records it.

## Reproduction

Run from the repository root:

```powershell
dotnet build Curl.Console -c Release -nologo -v q; $c=(Resolve-Path Curl.Console/bin/Release/net10.0/curl.exe).Path; Push-Location $env:TEMP; & $c --ftp-ssl-control --ssl http://127.0.0.1:9/ 2>&1 | Select-String '^Warning'; Pop-Location
```

- Expected: Warning: --ssl is an insecure option, consider --ssl-reqd instead
Warning: --ssl overrides --ftp-ssl-control
- Actual: Warning: --ssl is an insecure option, consider --ssl-reqd instead

## Re-audits

## Log

- 2026-11-19: filed proposed.
