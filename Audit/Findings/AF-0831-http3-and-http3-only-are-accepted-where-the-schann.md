---
id: AF-0831
title: --http3 and --http3-only are accepted where the Schannel reference curl refuses them with exit 2
auditor: conformance
severity: High
status: proposed
reason:
key: conformance:Curl.Cli.UnitLibrary/CommandLineOptionTable.cs:--http3:refused-option
reproduction: none
task: none
tasks:
found: 2026-11-16
found-at: f28eba04920fb5d8dd2a1c70283f268a1891b7e5
scorecard: 2026-11-16_1333.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0831 - --http3 and --http3-only are accepted where the Schannel reference curl refuses them with exit 2

## Summary

High finding from the conformance auditor at `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs:339`: --http3 and --http3-only are accepted where the Schannel reference curl refuses them with exit 2.

## Evidence

Location: `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs:339`

Reference: curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Seed 413266, cases 61, 62, 79, 110, 160, 251, 293. curl: 'curl: option --http3-only: the installed libcurl version does not support this' + try-help line, exit 2 (and the same for --http3). Curl: '--http3-only http://127.0.0.1:P/' exits 3 with 'curl: (3) HTTP/3 requested for non-HTTPS URL' (cases 61, 79, 251, 293); '-s --http3 http://127.0.0.1:P/' exits 0 and performs the request (case 62, reduced); in cases 110 and 160 Curl reports a later option's error instead, because it does not refuse --http3-only first. Cause: CommandLineOptionTable.cs:339-340 builds both as plain Flag rows. Not listed in Gap/Baselines/platform-exclusions.json for windows. Phase 2: explained by ADR-1282 ('--http3 and --http3-only are accepted and curl -V lists HTTP3', superseding ADR-0540, based on an ngtcp2 build of curl 8.21.0 rather than the platform's reference build); it stays a finding until the exclusions list names it.

## Reproduction

Run from the repository root:

```powershell
dotnet build Curl.Console -c Release -v q | Out-Null; ./Record-CurlExchange.ps1 -Port 62212 -Curl 'C:\Program Files\Git\ucrt64\bin\curl.exe' -OutDirectory $env:TEMP\h3-curl -CurlArgs @('-s','--http3','http://127.0.0.1:62212/') *> $null; ./Record-CurlExchange.ps1 -Port 62212 -Curl ./Curl.Console/bin/Release/net10.0/curl.exe -OutDirectory $env:TEMP\h3-cand -CurlArgs @('-s','--http3','http://127.0.0.1:62212/') *> $null; Get-Content $env:TEMP\h3-curl\exitcode.txt, $env:TEMP\h3-cand\exitcode.txt
```

- Expected: 2 and 2 (curl: option --http3: the installed libcurl version does not support this)
- Actual: 2 and 0 (Curl performs the transfer)

## Re-audits

## Log

- 2026-11-16: filed proposed.
