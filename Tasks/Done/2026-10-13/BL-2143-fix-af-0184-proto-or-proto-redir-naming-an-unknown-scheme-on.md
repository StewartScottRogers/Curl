---
id: BL-2143
title: Fix AF-0184: --proto or --proto-redir naming an unknown scheme only warns and runs (exit 0); the reference curl refuses it as badly used (exit 2)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console.UnitTests]
requirement: none
created: 2026-10-13
completed: 2026-10-13
---
# BL-2143 — Fix AF-0184: --proto or --proto-redir naming an unknown scheme only warns and runs (exit 0); the reference curl refuses it as badly used (exit 2)

## Goal

The defect the audit office reported as AF-0184 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0184 (High, conformance auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0184-proto-or-proto-redir-naming-an-unknown-scheme-only.md`.

Location: `Curl.Cli.UnitLibrary/CommandLineProtocolSet.cs:116`

Location: `Curl.Cli.UnitLibrary/CommandLineProtocolSet.cs:116`

Reference: curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Seed 1215104239 cases 161, 165, 185, 279 and 289 all differ this way. Reduced to '--proto x URL' and '--proto-redir x URL'. curl prints 'curl: unrecognized protocol 'x'', then 'curl: option --proto: is badly used here' and the try-help line, sends nothing and exits 2. Curl prints 'Warning: unrecognized protocol 'x'', sends the request and exits 0. In case 165, '--proto -' with a later bad --tls-max, curl blames --proto and Curl blames --tls-max. The cause is CommandLineProtocolSet.cs:116, which adds CommandLineWarning.UnrecognizedProtocol (CommandLineWarning.cs:375) and goes on instead of refusing. Its doc comment cites a curl 8.21.0 measurement. Phase 2, explained by ADR-0433: Curl targets curl 8.21.0 and this reference is 8.22.0 (ADR-0189 covers the scheme list). This is version skew, still visible to a script through the exit code.

Reproduction, from the finding:

Run from the repository root:

```powershell
$a=@('--proto-redir','x','http://127.0.0.1:50961/'); foreach($p in @(@('curl','C:\Program Files\Git\ucrt64\bin\curl.exe'),@('candidate',(Resolve-Path Curl.Console/bin/Release/net10.0/curl.exe).Path))){ & ./Record-CurlExchange.ps1 -Port 50961 -Curl $p[1] -OutDirectory "$env:TEMP\ac-proto\$($p[0])" -CurlArgs $a | Out-Null; $p[0] + ' exit ' + (Get-Content "$env:TEMP\ac-proto\$($p[0])\exitcode.txt") }
```

- Expected: curl exit 2, candidate exit 2
- Actual: curl exit 2, candidate exit 0

The finding closes only when a later re-audit by the conformance auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Measured the Windows curl 8.22.0 (Git ucrt64) on 2026-10-13: `--proto x`, `http,bogus`, `=bogus`, `-bogus`, `++http`, `+`, `=,http`, `x,-all`, `ipfs`, `rtmp`, `smb` and a 46-character name all print `curl: unrecognized protocol '<name>'` (name cut to 31), `is badly used here` and the try-help line, and exit 2; `--proto-redir x` the same; `-s` drops the first line, `-s -S` keeps it. The first unknown name ends the reading. Matched under ADR-0479 (Curl targets 8.22.0), so no new ADR.
- `smb` stays known: Curl serves it and the OpenSSL build knows it (ADR-0189), as before.
- Added `Curl.Cli.UnitTests` and `Curl.Console.UnitTests` to `touches`: their tests pinned the old warning; no task in Doing names either.
- The `Curl.Console/bin/Release` binary was out of this sandbox's reach, so the reproduction ran through `dotnet run --project Curl.Console`: `--proto-redir x` exits 2 with curl's three lines.

## Log

- 2026-10-13: Created.
- 2026-10-13: Backlog -> Doing.
- 2026-10-13: Doing -> Done. --proto and --proto-redir refuse an unknown scheme with curl 8.22.0's lines and exit 2; build clean, fast tests green
