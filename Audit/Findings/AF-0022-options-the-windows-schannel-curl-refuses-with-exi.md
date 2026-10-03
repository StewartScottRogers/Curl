---
id: AF-0022
title: Options the Windows Schannel curl refuses with exit 2 (--http2, --http2-prior-knowledge, --http3, --http3-only, --tlsuser, --tlspassword, --tlsauthtype, --ssl-sessions) are accepted by Curl
auditor: conformance
severity: Medium
status: proposed
reason:
key: conformance:Curl.Cli.UnitLibrary/CommandLineOptionTable.cs:build-feature-options:refused-option
task: none
found: 2026-10-02
found-at: 337ed10b42ddd4d09991deaecb10826c2dedba00
scorecard: 2026-10-02_1400.md
closed:
closed-by:
---
# AF-0022 - Options the Windows Schannel curl refuses with exit 2 (--http2, --http2-prior-knowledge, --http3, --http3-only, --tlsuser, --tlspassword, --tlsauthtype, --ssl-sessions) are accepted by Curl

## Summary

Medium finding from the conformance auditor at `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs`: Options the Windows Schannel curl refuses with exit 2 (--http2, --http2-prior-knowledge, --http3, --http3-only, --tlsuser, --tlspassword, --tlsauthtype, --ssl-sessions) are accepted by Curl.

## Evidence

Location: `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs`

Reference: curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. The inbox C:\Windows\System32\curl.exe (curl 8.21.0 (Windows) Schannel, features without HTTP2/HTTP3/TLS-SRP) also lacks them. Differential seed 863948043 cases 14, 21, 54, 57, 58, 71, 73, 85, 97, 99, 118, 124, 139, 155, 167, 208, 220, 240, 241, 243, 249, 263, 273, 276, 288 (25 cases). For each, curl stops at 'curl: option --X: the installed libcurl version does not support this' plus the try line, exit 2, no request. Curl runs the transfer instead: exit 0, or 16/56 for HTTP/2 prior knowledge against an HTTP/1 server, or 3 'HTTP/3 requested for non-HTTPS URL'. Or it fails at a later option's check: --tlsauthtype '' gives 'blank argument where content is expected' (cases 58, 220, 241); later options give their own errors (85, 97, 167, 240). Reduced: -s --tlsuser 1 URL, -s --http2 URL, -s --ssl-sessions f.txt URL: curl exit 2, Curl exit 0. Phase 2: explained by ADR-0141 (HTTP/2 hand-built and accepted on every platform), ADR-0144 (HTTP/3 hand-built), ADR-0151 (the ten TLS options accepted everywhere, including --ssl-sessions) and ADR-0328 (--tlsuser runs TLS-SRP as curl's OpenSSL build). These are recorded decisions to diverge from the platform build's refusal. Whether that is acceptable for a drop-in replacement is Stewart's call at triage.

## Reproduction

Run from the repository root:

```powershell
dotnet build Curl.Console -c Release -nologo -v q | Out-Null; foreach ($x in @(@('curl', "$env:ProgramFiles\Git\mingw64\bin\curl.exe"), @('candidate', '.\Curl.Console\bin\Release\net10.0\curl.exe'))) { foreach ($o in @('--http2'), @('--tlsuser', '1'), @('--ssl-sessions', 'f.txt')) { $d = "$env:TEMP\feat\$($x[0])"; & .\Record-CurlExchange.ps1 -Port 48295 -Curl $x[1] -OutDirectory $d -CurlArgs (@('-s') + $o + 'http://127.0.0.1:48295/') *> $null; '{0} {1}: exit {2}' -f $x[0], ($o -join ' '), (Get-Content "$d\exitcode.txt") } }
```

- Expected: candidate as curl: --http2 exit 2, --tlsuser 1 exit 2, --ssl-sessions f.txt exit 2, each with 'the installed libcurl version does not support this'
- Actual: curl: exit 2 for all three. candidate: exit 0 for all three, and the GET is sent

## Re-audits

## Log

- 2026-10-02: filed proposed.
