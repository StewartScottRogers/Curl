---
id: BL-3358
title: Fix AF-0381: --http2 and --http2-prior-knowledge run a transfer on Windows where the Schannel reference curl refuses them with exit 2
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary]
requirement: none
created: 2026-10-23
completed:
---
# BL-3358 — Fix AF-0381: --http2 and --http2-prior-knowledge run a transfer on Windows where the Schannel reference curl refuses them with exit 2

## Goal

The defect the audit office reported as AF-0381 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0381 (High, conformance auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0381-http2-and-http2-prior-knowledge-run-a-transfer-on.md`.

Location: `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs:338`

Location: `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs:338`

Reference: curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP (no HTTP2 in Features; inbox C:\Windows\System32\curl.exe 8.21.0 Schannel has none either). Differential seed 850517410, cases 38, 98, 206, 229, 259 and 262. Reduced to `--http2 http://127.0.0.1:PORT/`: curl prints `curl: option --http2: the installed libcurl version does not support this` and the try line, exits 2 and sends nothing. Curl sends `GET / HTTP/1.1` with `Upgrade: h2c` and `HTTP2-Settings` and exits 0. Reduced to `--http2-prior-knowledge http://127.0.0.1:PORT/`: curl exits 2 the same way. Curl sends the h2 preface and exits 56 (`Recv failure: Connection was reset`) in cases 38, 229, 259 and the reduced run, and 16 (`nghttp2 shuts down connection with error 6: FRAME_SIZE_ERROR`) in case 206, so the same command line also gives different exit codes from run to run. Case 98 differs only because curl refuses --http2-prior-knowledge before it checks the blank --ciphers. Rows 338-339 (`Flag("http2"...)`, `Flag("http2-prior-knowledge"...)`) have no .RefusedBySchannelBuild(), unlike ssl-sessions at line 268. Phase 2: explained by ADR-0786 (2026-10-23, Stewart's decision, recorded by Claude in BL-3207): 'The Windows build runs HTTP/2 and lists HTTP2 in curl -V', superseding the HTTP/2 part of ADR-0397. The ADR accepts that 'a Windows script that relied on the refusal (exit 2) now sees a transfer'. The run-to-run 16/56 exit on --http2-prior-knowledge against an HTTP/1.1 server is not covered by that ADR.

Reproduction, from the finding:

Run from the repository root:

```powershell
dotnet build Curl.Console -c Release; $c=(Resolve-Path Curl.Console\bin\Release\net10.0\curl.exe).Path; & .\Record-CurlExchange.ps1 -Port 51894 -OutDirectory $env:TEMP\af-h2\curl -CurlArgs @('--http2','http://127.0.0.1:51894/') | Out-Null; & .\Record-CurlExchange.ps1 -Port 51894 -Curl $c -OutDirectory $env:TEMP\af-h2\cand -CurlArgs @('--http2','http://127.0.0.1:51894/') | Out-Null; Get-Content $env:TEMP\af-h2\curl\exitcode.txt,$env:TEMP\af-h2\cand\exitcode.txt
```

- Expected: 2 and 2 (curl: option --http2: the installed libcurl version does not support this; nothing sent)
- Actual: 2 then 0 (Curl sends GET / HTTP/1.1 with Upgrade: h2c and completes the transfer)

The finding closes only when a later re-audit by the conformance auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- 2026-10-23 (lane 1): Not implemented. The criterion expects Windows to refuse `--http2` with exit 2, which would undo ADR-0786, Stewart's own decision (2026-10-22, "do it") that the Windows build runs HTTP/2. The finding itself says its Phase 2 is explained by that ADR. A lane may decide only what Stewart has not decided, so only he can choose between the ADR and this finding. No code changed. The part the ADR does not cover, the 16/56 exit-code flip on `--http2-prior-knowledge` against an HTTP/1.1 server, is filed as BL-3372.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-23: Doing -> Blocked. Stewart: ADR-0786 (your decision) has Windows run --http2/--http2-prior-knowledge, but accepted AF-0381 expects exit 2 - revert ADR-0786 for these options, or close AF-0381 as explained by it?
