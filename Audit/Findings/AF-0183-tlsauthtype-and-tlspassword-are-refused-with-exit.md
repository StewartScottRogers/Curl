---
id: AF-0183
title: --tlsauthtype and --tlspassword are refused with exit 2; the reference curl warns they are deprecated and exits 0
auditor: conformance
severity: High
status: closed
reason: Re-audit 2026-10-14_0453.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-10-13_1822.md, 2026-10-14_0453.md).
key: conformance:--tlsauthtype:refused-on-schannel:refused-option
reproduction: none
task: BL-2142
tasks: BL-2142
found: 2026-10-13
found-at: c86d04eeb8477108eb6d9195bb036a4ddaecc5fc
scorecard: 2026-10-13_0925.md
duplicate-of:
closed: 2026-10-14
closed-how: consecutive
closed-by: 2026-10-13_1822.md, 2026-10-14_0453.md
---
# AF-0183 - --tlsauthtype and --tlspassword are refused with exit 2; the reference curl warns they are deprecated and exits 0

## Summary

High finding from the conformance auditor at `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs:268`: --tlsauthtype and --tlspassword are refused with exit 2; the reference curl warns they are deprecated and exits 0. Reported by an auditor flagged unreliable in 2026-10-13_0925.md.

## Evidence

Location: `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs:268`

Reference: curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Seed 1215104239 cases 72, 103, 224, 297 (--tlsauthtype) and 158, 201, 267 (--tlspassword) all differ the same way. Reduced to '--tlsauthtype 1 URL' and '--tlspassword x URL'. curl prints 'Warning: --tlsauthtype is deprecated and has no function anymore' (likewise for --tlspassword), sends the request and exits 0. Curl prints 'curl: option --tlsauthtype: the installed libcurl version does not support this' plus the try-help line, sends nothing and exits 2. The cause is CommandLineOptionTable.cs:268-269: both options are marked .RefusedBySchannelBuild(). Phase 2, explained by ADR-0433: Curl targets curl 8.21.0 (Gap/Baselines/target.json), and this reference is 8.22.0, so this is version skew between the targeted and the installed reference curl. ADR-0151, ADR-0229 and ADR-0328 cover these options' TLS-SRP behaviour. A script running against this machine's curl can still tell the two binaries apart.

## Reproduction

Run from the repository root:

```powershell
foreach($a in @(@('--tlsauthtype','1','http://127.0.0.1:50961/'),@('--tlspassword','x','http://127.0.0.1:50961/'))){ foreach($p in @(@('curl','C:\Program Files\Git\ucrt64\bin\curl.exe'),@('candidate',(Resolve-Path Curl.Console/bin/Release/net10.0/curl.exe).Path))){ & ./Record-CurlExchange.ps1 -Port 50961 -Curl $p[1] -OutDirectory "$env:TEMP\ac-tls\$($p[0])" -CurlArgs $a | Out-Null; $a[0] + ' ' + $p[0] + ' exit ' + (Get-Content "$env:TEMP\ac-tls\$($p[0])\exitcode.txt") } }
```

- Expected: --tlsauthtype curl exit 0, --tlsauthtype candidate exit 0, --tlspassword curl exit 0, --tlspassword candidate exit 0
- Actual: --tlsauthtype curl exit 0, --tlsauthtype candidate exit 2, --tlspassword curl exit 0, --tlspassword candidate exit 2

## Re-audits

- 2026-10-13 | 2026-10-13_1822.md | reproduces: no | Ran as written against curl 8.22.0 (ucrt64): '--tlsauthtype curl exit 0', '--tlsauthtype candidate exit 0', '--tlspassword curl exit 0', '--tlspassword candidate exit 0'.
- 2026-10-14 | 2026-10-14_0453.md | reproduces: no | Ran the reproduction against curl 8.22.0 Schannel (Git ucrt64): --tlsauthtype curl exit 0, candidate exit 0; --tlspassword curl exit 0, candidate exit 0. Both print 'Warning: --tlsauthtype is deprecated and has no function anymore' (and the same for --tlspassword).

## Log

- 2026-10-13: filed proposed.
- 2026-10-13: proposed -> accepted.
- 2026-10-14: accepted -> closed. Re-audit 2026-10-14_0453.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-10-13_1822.md, 2026-10-14_0453.md).
