---
id: AF-0850
title: --http3 and --http3-only are accepted (exit 0 / 3) where the Windows Schannel reference curl refuses them with exit 2
auditor: conformance
severity: High
status: closed
reason: Duplicate of AF-0831: the same --http3/--http3-only acceptance on Windows, which Stewart closed on 2026-11-16 as a decided difference (ADR-1282); filed again under a different key. Task BL-5370 deferred.
key: conformance:--http3:accepted-where-reference-refuses:refused-option
reproduction: none
task: BL-5370
tasks: BL-5370
found: 2026-11-17
found-at: 10c240a4859c7868ec16b722cde6234f80cc8fcb
scorecard: 2026-11-17_0430.md
duplicate-of: AF-0831
closed: 2026-11-17
closed-how: duplicate
closed-by: session
---
# AF-0850 - --http3 and --http3-only are accepted (exit 0 / 3) where the Windows Schannel reference curl refuses them with exit 2

## Summary

High finding from the conformance auditor at `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs:339`: --http3 and --http3-only are accepted (exit 0 / 3) where the Windows Schannel reference curl refuses them with exit 2.

## Evidence

Location: `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs:339`

Reference: curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Differential seed 320622, cases 11, 23, 223, 251 and 292. Reduced to: (a) --http3 http://127.0.0.1:PORT/ gives curl exit 2 'curl: option --http3: the installed libcurl version does not support this' with no request, and Curl exit 0 with a GET sent; (b) --http3-only http://127.0.0.1:PORT/ gives curl exit 2 with the same refusal, and Curl exit 3 'curl: (3) HTTP/3 requested for non-HTTPS URL'. In cases 11, 251 and 292 curl refuses --http3/--http3-only first, while Curl goes on to refuse a later option (--hostpubmd5 -, --continue-at -1, --parallel-max-host -1). With --http3 removed, each of those matches byte for byte. The release C:\Windows\System32\curl.exe (curl 8.22.0 (Windows) Schannel) refuses both with exit 2 too, so this is not caused by the reference being a debug build. Curl's -V Features line lists HTTP2 HTTP3; the reference lists neither. Cause: CommandLineOptionTable.cs:339-340 register http3 and http3-only as plain flags. Gap/Baselines/platform-exclusions.json lists only the HTTP/2 options for windows, so this difference is not excluded. Phase 2: explained by ADR-1282 ('--http3 and --http3-only are accepted and curl -V lists HTTP3', superseding ADR-0540), which chose to accept them on every platform. It is not in the exclusions list, so per Conformance.md step 5 it stays a finding.

## Reproduction

Run from the repository root:

```powershell
Push-Location $env:TEMP; & ./Record-CurlExchange.ps1 -Port 57920 -Curl curl.exe -OutDirectory $env:TEMP/h3-curl -CurlArgs @('--http3','http://127.0.0.1:57920/'); & ./Record-CurlExchange.ps1 -Port 57920 -Curl ./Curl.Console/bin/Release/net10.0/curl.exe -OutDirectory $env:TEMP/h3-cand -CurlArgs @('--http3','http://127.0.0.1:57920/'); Pop-Location; Get-Content $env:TEMP/h3-cand/exitcode.txt
```

- Expected: 2, stderr 'curl: option --http3: the installed libcurl version does not support this', no request sent
- Actual: 0, a GET request sent and answered

## Re-audits

## Log

- 2026-11-17: filed proposed.
- 2026-11-17: proposed -> accepted.
- 2026-11-17: accepted -> closed. Duplicate of AF-0831: the same --http3/--http3-only acceptance on Windows, which Stewart closed on 2026-11-16 as a decided difference (ADR-1282); filed again under a different key. Task BL-5370 deferred.
