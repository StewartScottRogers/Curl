---
id: AF-0184
title: --proto or --proto-redir naming an unknown scheme only warns and runs (exit 0); the reference curl refuses it as badly used (exit 2)
auditor: conformance
severity: High
status: accepted
reason: 
key: conformance:--proto:unrecognized-protocol:exit-code
reproduction: none
task: BL-2143
tasks: BL-2143
found: 2026-10-13
found-at: c86d04eeb8477108eb6d9195bb036a4ddaecc5fc
scorecard: 2026-10-13_0925.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0184 - --proto or --proto-redir naming an unknown scheme only warns and runs (exit 0); the reference curl refuses it as badly used (exit 2)

## Summary

High finding from the conformance auditor at `Curl.Cli.UnitLibrary/CommandLineProtocolSet.cs:116`: --proto or --proto-redir naming an unknown scheme only warns and runs (exit 0); the reference curl refuses it as badly used (exit 2). Reported by an auditor flagged unreliable in 2026-10-13_0925.md.

## Evidence

Location: `Curl.Cli.UnitLibrary/CommandLineProtocolSet.cs:116`

Reference: curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Seed 1215104239 cases 161, 165, 185, 279 and 289 all differ this way. Reduced to '--proto x URL' and '--proto-redir x URL'. curl prints 'curl: unrecognized protocol 'x'', then 'curl: option --proto: is badly used here' and the try-help line, sends nothing and exits 2. Curl prints 'Warning: unrecognized protocol 'x'', sends the request and exits 0. In case 165, '--proto -' with a later bad --tls-max, curl blames --proto and Curl blames --tls-max. The cause is CommandLineProtocolSet.cs:116, which adds CommandLineWarning.UnrecognizedProtocol (CommandLineWarning.cs:375) and goes on instead of refusing. Its doc comment cites a curl 8.21.0 measurement. Phase 2, explained by ADR-0433: Curl targets curl 8.21.0 and this reference is 8.22.0 (ADR-0189 covers the scheme list). This is version skew, still visible to a script through the exit code.

## Reproduction

Run from the repository root:

```powershell
$a=@('--proto-redir','x','http://127.0.0.1:50961/'); foreach($p in @(@('curl','C:\Program Files\Git\ucrt64\bin\curl.exe'),@('candidate',(Resolve-Path Curl.Console/bin/Release/net10.0/curl.exe).Path))){ & ./Record-CurlExchange.ps1 -Port 50961 -Curl $p[1] -OutDirectory "$env:TEMP\ac-proto\$($p[0])" -CurlArgs $a | Out-Null; $p[0] + ' exit ' + (Get-Content "$env:TEMP\ac-proto\$($p[0])\exitcode.txt") }
```

- Expected: curl exit 2, candidate exit 2
- Actual: curl exit 2, candidate exit 0

## Re-audits

- 2026-10-13 | 2026-10-13_1822.md | reproduces: no | Ran as written against curl 8.22.0 (ucrt64): 'curl exit 2', 'candidate exit 2' for --proto-redir x.

## Log

- 2026-10-13: filed proposed.
- 2026-10-13: proposed -> accepted.
