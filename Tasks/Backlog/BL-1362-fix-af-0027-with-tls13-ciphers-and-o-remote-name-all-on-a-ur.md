---
id: BL-1362
title: Fix AF-0027: With --tls13-ciphers and -O/--remote-name-all on a URL without a file name, Curl prints the Schannel 'ignoring --tls13-ciphers' warning before 'No remote filename', curl prints them the other way round
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console]
requirement: none
created: 2026-10-03
completed:
---
# BL-1362 — Fix AF-0027: With --tls13-ciphers and -O/--remote-name-all on a URL without a file name, Curl prints the Schannel 'ignoring --tls13-ciphers' warning before 'No remote filename', curl prints them the other way round

## Goal

The defect the audit office reported as AF-0027 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0027 (Medium, conformance auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0027-with-tls13-ciphers-and-o-remote-name-all-on-a-url.md`.

Location: `Curl.Console/CurlComposition.cs:1227`

Location: `Curl.Console/CurlComposition.cs:1227`

Reference: curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Differential run seed 1162681642, 300 cases, case 5 (-r -5 --basic --tls13-ciphers @f.txt --remote-name-all http://127.0.0.1:PORT/) differed in stderr only. Reduced to: --tls13-ciphers x -O http://127.0.0.1:PORT/ (the difference also shows with -O before --tls13-ciphers and with --remote-name-all in place of -O; with --ciphers x -O both match). curl stderr starts 'Warning: No remote filename, uses "curl_response"\r\nWarning: ignoring --tls13-ciphers, not supported by libcurl with Schannel\r\n'. Curl prints the same two lines in the reverse order. Exit code 0 and stdout are identical. The per-transfer warning lines are built in CurlComposition.WarningLinesBeforeEachTransfer (CurlComposition.cs:1225-1233) and printed before the output file name is resolved. The 'No remote filename' line is written later, in CurlCommandRunner.ResolveOutputFileAsync (CurlCommandRunner.cs:3579). Phase 2: ADR-0349 records the warnings and their order among themselves (--capath, --tls13-ciphers, --proxy-tls13-ciphers), 'before that URL's transfer starts'. It says nothing about their order relative to -O's 'No remote filename' warning, so this divergence is not documented.

Reproduction, from the finding:

Run from the repository root:

```powershell
dotnet build Curl.Console -c Release -nologo -v q | Out-Null; foreach ($x in @(@('curl', "$env:ProgramFiles\Git\mingw64\bin\curl.exe"), @('candidate', (Resolve-Path '.\Curl.Console\bin\Release\net10.0\curl.exe').Path))) { $d = "$env:TEMP\t13o\$($x[0])"; & .\Record-CurlExchange.ps1 -Port 48292 -Curl $x[1] -OutDirectory $d -CurlArgs '--tls13-ciphers', 'x', '-O', 'http://127.0.0.1:48292/' *> $null; '{0}: exit {1}, first two stderr lines [{2}]' -f $x[0], (Get-Content "$d\exitcode.txt"), ((Get-Content "$d\stderr.txt" | Select-Object -First 2) -join ' | ') }
```

- Expected: Both print: exit 0, first two stderr lines [Warning: No remote filename, uses "curl_response" | Warning: ignoring --tls13-ciphers, not supported by libcurl with Schannel]
- Actual: curl: exit 0, [Warning: No remote filename, uses "curl_response" | Warning: ignoring --tls13-ciphers, not supported by libcurl with Schannel]; candidate: exit 0, [Warning: ignoring --tls13-ciphers, not supported by libcurl with Schannel | Warning: No remote filename, uses "curl_response"]

The finding closes only when a later re-audit by the conformance auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-03: Created.
