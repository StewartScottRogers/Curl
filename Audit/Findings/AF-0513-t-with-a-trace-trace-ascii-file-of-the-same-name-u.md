---
id: AF-0513
title: -T with a --trace/--trace-ascii file of the same name uploads 0 bytes and exits 0 where curl exits 26
auditor: conformance
severity: High
status: closed
reason: Re-audit 2026-11-01_1016.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-11-01_0144.md, 2026-11-01_1016.md).
key: conformance:Curl.Console/CurlCommandRunner.cs:--upload-file:exit-code
reproduction: none
task: BL-4435
tasks: BL-4435
found: 2026-10-31
found-at: 23bacbc6c04b8e06a4eecb79636da4891dca4c01
scorecard: 2026-10-31_1149.md
duplicate-of:
closed: 2026-11-01
closed-how: consecutive
closed-by: 2026-11-01_0144.md, 2026-11-01_1016.md
---
# AF-0513 - -T with a --trace/--trace-ascii file of the same name uploads 0 bytes and exits 0 where curl exits 26

## Summary

High finding from the conformance auditor at `Curl.Console/CurlCommandRunner.cs:4023`: -T with a --trace/--trace-ascii file of the same name uploads 0 bytes and exits 0 where curl exits 26.

## Evidence

Location: `Curl.Console/CurlCommandRunner.cs:4023`

Reference: curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Differential seed 599444422, case 5: '--mail-from - -T sub/f.txt --delegation 99999999999999999999 --trace sub/f.txt http://127.0.0.1:PORT/' differs in exitcode, stderr and request. Reduced by dropping one option at a time (the difference stays) to '-T sub/f.txt --trace sub/f.txt URL'. '--trace-ascii sub/f.txt' differs the same way, and '-T sub/f.txt' alone matches (both exit 0, 112 request bytes). sub/f.txt holds the 9 bytes 'sub body\n'. curl sizes the upload at 9 bytes before the trace file truncates it, sends no request, prints 'curl: (26) client read function EOF fail, only 0/9 of needed bytes read' and exits 26. Curl opens the -T file only at transfer time (TransferOpeningUploadFileAsync, fileSystem.OpenForReadAsync at line 4023), after TransferEventOutput has already opened and truncated the trace file. It therefore sees an empty file, sends 'PUT /f.txt HTTP/1.1 ... Content-Length: 0' (103 request bytes) and exits 0. A script can tell the two apart by the exit code. Phase 2: ADR-0051 decides that the -T file is opened after the transfer's warning lines, but it does not cover the upload being sized after the trace file is truncated, so it does not explain this difference.

## Reproduction

Run from the repository root:

```powershell
dotnet build Curl.Console -c Release -v q; $d=(New-Item -ItemType Directory -Force (Join-Path $env:TEMP 'af-trace-upload')).FullName; New-Item -ItemType Directory -Force (Join-Path $d 'sub') | Out-Null; [IO.File]::WriteAllText((Join-Path $d 'sub/f.txt'), 'sub body'+[char]10); $exe=(Resolve-Path Curl.Console/bin/Release/net10.0/curl.exe).Path; $rec=(Resolve-Path Record-CurlExchange.ps1).Path; Push-Location $d; [Environment]::CurrentDirectory=$d; & $rec -Port 50411 -Curl $exe -OutDirectory (Join-Path $d 'out') -CurlArgs '-T','sub/f.txt','--trace','sub/f.txt','http://127.0.0.1:50411/' *> $null; Pop-Location; Get-Content (Join-Path $d 'out/exitcode.txt'); Get-Content (Join-Path $d 'out/stderr.txt') | Select-String '^curl:'
```

- Expected: 26 and 'curl: (26) client read function EOF fail, only 0/9 of needed bytes read', with an empty request.bin (curl 8.22.0 Schannel gives this with -Curl set to Git's ucrt64 curl.exe)
- Actual: 0, no curl: line, and request.bin holds 'PUT /f.txt HTTP/1.1' with 'Content-Length: 0' (103 bytes)

## Re-audits

- 2026-11-01 | 2026-11-01_0144.md | reproduces: no | Ran the reproduction as given: port 50411 is blocked on this machine (Record-CurlExchange.ps1: 'An attempt was made to access a socket in a way forbidden by its access permissions'), so no out/ folder was written. Reran it unchanged except for port 60213: Curl exits 26 with 'curl: (26) client read function EOF fail, only 0/9 of needed bytes read'. The same script with the reference curl 8.22.0 (Schannel) gives the same curl: (26) line. Curl now matches curl's exit 26 instead of exiting 0.
- 2026-11-01 | 2026-11-01_1016.md | reproduces: no | Ran the reproduction against the Release build of the audited tree. Port 50411 is in a Windows-reserved range on this host (SocketException 'access forbidden'), so I reran it unchanged except for port 58801. Curl: exitcode.txt 26, stderr `curl: (26) client read function EOF fail, only 0/9 of needed bytes read`. Reference curl 8.22.0 Schannel, same command on port 58802: exit 26, identical message. Curl no longer uploads 0 bytes and exits 0.

## Log

- 2026-10-31: filed proposed.
- 2026-10-31: proposed -> accepted.
- 2026-11-01: accepted -> closed. Re-audit 2026-11-01_1016.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-11-01_0144.md, 2026-11-01_1016.md).
