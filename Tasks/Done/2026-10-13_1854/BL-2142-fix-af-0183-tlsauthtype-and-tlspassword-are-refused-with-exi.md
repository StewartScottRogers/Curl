---
id: BL-2142
title: Fix AF-0183: --tlsauthtype and --tlspassword are refused with exit 2; the reference curl warns they are deprecated and exits 0
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console.UnitTests]
requirement: none
created: 2026-10-13
completed: 2026-10-13
---
# BL-2142 — Fix AF-0183: --tlsauthtype and --tlspassword are refused with exit 2; the reference curl warns they are deprecated and exits 0

## Goal

The defect the audit office reported as AF-0183 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0183 (High, conformance auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0183-tlsauthtype-and-tlspassword-are-refused-with-exit.md`.

Location: `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs:268`

Location: `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs:268`

Reference: curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Seed 1215104239 cases 72, 103, 224, 297 (--tlsauthtype) and 158, 201, 267 (--tlspassword) all differ the same way. Reduced to '--tlsauthtype 1 URL' and '--tlspassword x URL'. curl prints 'Warning: --tlsauthtype is deprecated and has no function anymore' (likewise for --tlspassword), sends the request and exits 0. Curl prints 'curl: option --tlsauthtype: the installed libcurl version does not support this' plus the try-help line, sends nothing and exits 2. The cause is CommandLineOptionTable.cs:268-269: both options are marked .RefusedBySchannelBuild(). Phase 2, explained by ADR-0433: Curl targets curl 8.21.0 (Gap/Baselines/target.json), and this reference is 8.22.0, so this is version skew between the targeted and the installed reference curl. ADR-0151, ADR-0229 and ADR-0328 cover these options' TLS-SRP behaviour. A script running against this machine's curl can still tell the two binaries apart.

Reproduction, from the finding:

Run from the repository root:

```powershell
foreach($a in @(@('--tlsauthtype','1','http://127.0.0.1:50961/'),@('--tlspassword','x','http://127.0.0.1:50961/'))){ foreach($p in @(@('curl','C:\Program Files\Git\ucrt64\bin\curl.exe'),@('candidate',(Resolve-Path Curl.Console/bin/Release/net10.0/curl.exe).Path))){ & ./Record-CurlExchange.ps1 -Port 50961 -Curl $p[1] -OutDirectory "$env:TEMP\ac-tls\$($p[0])" -CurlArgs $a | Out-Null; $a[0] + ' ' + $p[0] + ' exit ' + (Get-Content "$env:TEMP\ac-tls\$($p[0])\exitcode.txt") } }
```

- Expected: --tlsauthtype curl exit 0, --tlsauthtype candidate exit 0, --tlspassword curl exit 0, --tlspassword candidate exit 0
- Actual: --tlsauthtype curl exit 0, --tlsauthtype candidate exit 2, --tlspassword curl exit 0, --tlspassword candidate exit 2

The finding closes only when a later re-audit by the conformance auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes
- Measured curl 8.22.0 Schannel (2026-10-13): all six TLS-SRP options (`--tlsuser`, `--tlspassword`, `--tlsauthtype` and their `--proxy-` forms) warn `Warning: --<name> is deprecated and has no function anymore` with any value, empty included; `-s` first hides it; `--no-<name>` is not reversible; last-argument is `requires parameter`. So all six, not only the two the finding named, became `CommandLineOption.NoFunctionValue` rows on every platform (ADR-0480; ADR-0479 already retargets to 8.22.0).
- `CommandLineOptions`' six TLS-SRP properties are now get-only and always null, kept so `Curl.Console`'s mapping still compiles; BL-2152 (filed) removes them, the mapping and the hand-built TLS-SRP client in Networking and Tls, and checks the OpenSSL `Features:` line.
- Touches widened to `Curl.Cli.UnitTests` and `Curl.Console.UnitTests` for the tests that pinned the old refusal; no task in Doing on the shared branch (BL-2128, BL-2138) names either.
- Reproduction rerun: `--tlsauthtype` and `--tlspassword` exit 0 for both curl and candidate. Fast tests: 34 projects passed, 0 failed. Measure-CodeQuality not run (only removed code and swapped rows to an existing, covered row builder); CI's quality job checks it.

## Log

- 2026-10-13: Created.
- 2026-10-13: Backlog -> Doing.
- 2026-10-13: Doing -> Done. --tlsuser, --tlspassword, --tlsauthtype and their --proxy- forms warn 'is deprecated and has no function anymore' and run the transfer, as curl 8.22.0 does
