---
id: BL-5779
title: Fix AF-0982: An -F file part that shrinks before it is sent (e.g. --trace-ascii into the same file) exits 0 where curl fails with exit 26
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary/Multipart/MultipartFormBodyBuilder.cs]
requirement: none
created: 2026-11-22
completed:
---
# BL-5779 — Fix AF-0982: An -F file part that shrinks before it is sent (e.g. --trace-ascii into the same file) exits 0 where curl fails with exit 26

## Goal

The defect the audit office reported as AF-0982 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0982 (High, conformance auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Core.UnitLibrary/Multipart/MultipartFormBodyBuilder.cs`

Location: `Curl.Core.UnitLibrary/Multipart/MultipartFormBodyBuilder.cs`

Reference: curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel ... WinLDAP. Seed 891023, case 266: '-F f=@f.txt -E 1 --trace-ascii f.txt'. Cut down to '-sS -F f=@f.txt --trace-ascii f.txt' with f.txt holding 'file body\n' (10 bytes). curl sizes the part from the file before the trace file truncates it, then reads 0 bytes, prints 'curl: (26) client mime read EOF fail, only 192/202 of needed bytes read' and exits 26. Curl sends the same 385 request bytes with the part empty and exits 0 with no error. Curl reads the part's content when the body is built, after the trace has truncated the file, and never checks it against the size the file had when the options were read. ADR-0036 point 2 says a body of known length whose read comes up short fails with exit 26, so the divergence is not deliberate. I did not find the exact line where the size is taken.

Reproduction, from the finding:

Run from the repository root:

```powershell
Set-Location $env:TEMP; 'file body' | Set-Content -NoNewline f.txt; Add-Content f.txt "`n" -NoNewline; & (Join-Path (Get-Location -PSProvider FileSystem).Drive.Root 'repos\Curl.audit\20261122-135121\planted\Curl.Console\bin\Release\net10.0\curl.exe') -sS -o NUL -F f=@f.txt --trace-ascii f.txt http://127.0.0.1:1/ ; $LASTEXITCODE
```

- Expected: As the reference curl against a listening loopback server (Record-CurlExchange.ps1 -Curl curl.exe -CurlArgs '-sS','-F','f=@f.txt','--trace-ascii','f.txt',<url>, run from a folder holding the 10-byte f.txt): exit 26, 'curl: (26) client mime read EOF fail, only 192/202 of needed bytes read'
- Actual: Under Record-CurlExchange.ps1 with the same arguments and fixture: exit 0, no error, 385 request bytes with an empty file part

The finding closes only when a later re-audit by the conformance auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-22: Created.
