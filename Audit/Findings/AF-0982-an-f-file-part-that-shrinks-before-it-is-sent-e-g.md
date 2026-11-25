---
id: AF-0982
title: An -F file part that shrinks before it is sent (e.g. --trace-ascii into the same file) exits 0 where curl fails with exit 26
auditor: conformance
severity: High
status: closed
reason: Re-audit 2026-11-22_1817.md: the reproduction no longer reproduces.
key: conformance:-F:form-file-shorter-than-sized:exit-code
reproduction: none
task: BL-5779
tasks: BL-5779
found: 2026-11-22
found-at: fdfc7d13ff07752cdeb7bf999a56dd653660b7a2
scorecard: 2026-11-22_1351.md
duplicate-of:
closed: 2026-11-22
closed-how: reliable-reaudit
closed-by: 2026-11-22_1817.md
---
# AF-0982 - An -F file part that shrinks before it is sent (e.g. --trace-ascii into the same file) exits 0 where curl fails with exit 26

## Summary

High finding from the conformance auditor at `Curl.Core.UnitLibrary/Multipart/MultipartFormBodyBuilder.cs`: An -F file part that shrinks before it is sent (e.g. --trace-ascii into the same file) exits 0 where curl fails with exit 26.

## Evidence

Location: `Curl.Core.UnitLibrary/Multipart/MultipartFormBodyBuilder.cs`

Reference: curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel ... WinLDAP. Seed 891023, case 266: '-F f=@f.txt -E 1 --trace-ascii f.txt'. Cut down to '-sS -F f=@f.txt --trace-ascii f.txt' with f.txt holding 'file body\n' (10 bytes). curl sizes the part from the file before the trace file truncates it, then reads 0 bytes, prints 'curl: (26) client mime read EOF fail, only 192/202 of needed bytes read' and exits 26. Curl sends the same 385 request bytes with the part empty and exits 0 with no error. Curl reads the part's content when the body is built, after the trace has truncated the file, and never checks it against the size the file had when the options were read. ADR-0036 point 2 says a body of known length whose read comes up short fails with exit 26, so the divergence is not deliberate. I did not find the exact line where the size is taken.

## Reproduction

Run from the repository root:

```powershell
Set-Location $env:TEMP; 'file body' | Set-Content -NoNewline f.txt; Add-Content f.txt "`n" -NoNewline; & (Join-Path (Get-Location -PSProvider FileSystem).Drive.Root 'repos\Curl.audit\20261122-135121\planted\Curl.Console\bin\Release\net10.0\curl.exe') -sS -o NUL -F f=@f.txt --trace-ascii f.txt http://127.0.0.1:1/ ; $LASTEXITCODE
```

- Expected: As the reference curl against a listening loopback server (Record-CurlExchange.ps1 -Curl curl.exe -CurlArgs '-sS','-F','f=@f.txt','--trace-ascii','f.txt',<url>, run from a folder holding the 10-byte f.txt): exit 26, 'curl: (26) client mime read EOF fail, only 192/202 of needed bytes read'
- Actual: Under Record-CurlExchange.ps1 with the same arguments and fixture: exit 0, no error, 385 request bytes with an empty file part

## Re-audits

- 2026-11-22 | 2026-11-22_1817.md | reproduces: no | The given command names a binary from audit 20261122-135121 that no longer exists, and its URL is port 1. Against port 1 both curl 8.22.0 (Schannel, Git ucrt64) and this tree's Release curl.exe exit 7 ('Failed to connect'), because the connection is refused before the form part is read, so that form cannot tell. Reran the case against a live loopback server with Record-CurlExchange.ps1 (-sS -o NUL -F f=@f.txt --trace-ascii f.txt http://127.0.0.1:47911/, f.txt = 'file body\n'): curl exit 26 'curl: (26) client mime read EOF fail, only 192/202 of needed bytes read'; Curl exit 26 with the same stderr and the same 385 request bytes. No longer reproduces.

## Log

- 2026-11-22: filed proposed.
- 2026-11-22: accepted -> closed. Re-audit 2026-11-22_1817.md: the reproduction no longer reproduces.
