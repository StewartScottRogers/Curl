---
id: BL-2212
title: Fix AF-0210: %{redirect_url} is empty for a non-3xx response carrying Location; curl writes the resolved target
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary]
requirement: none
created: 2026-10-14
completed:
---
# BL-2212 — Fix AF-0210: %{redirect_url} is empty for a non-3xx response carrying Location; curl writes the resolved target

## Goal

The defect the audit office reported as AF-0210 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0210 (Medium, conformance auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0210-redirect-url-is-empty-for-a-non-3xx-response-carry.md`.

Location: `Curl.Protocol.Http.UnitLibrary/HttpRedirectLocation.cs:47`

Location: `Curl.Protocol.Http.UnitLibrary/HttpRedirectLocation.cs:47`

Reference: curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Found by hand probe after the differential run (seed 32526082, 300 cases), reduced to -s -w %{redirect_url} URL. Response 'HTTP/1.1 <status>\r\nLocation: /z\r\nContent-Length: 0\r\n\r\n': for 301 and 302 both binaries write http://127.0.0.1:50976/z; for 200, 201 and 404 curl writes http://127.0.0.1:50976/z and Curl writes nothing (both exit 0). Cause: HttpRedirectLocation.Find line 47 'if (head.StatusLine.StatusCode is < 300 or >= 400) { return null; }' drops the Location of every non-3xx response, so TransferReport.RedirectUrl and %{redirect_url} are empty. A script reading %{redirect_url} (for example a 201 Created Location) can tell the binaries apart. Phase 2: no ADR records this as deliberate; ADR-0015's TransferReport table says RedirectUrl is 'the last response's Location, resolved against the request URL, whether or not -L was given; null when there is none', with no 3xx restriction, so the code contradicts it.

Reproduction, from the finding:

Run from the repository root:

```powershell
foreach($p in @(@('curl','C:\Program Files\Git\ucrt64\bin\curl.exe'),@('candidate',(Resolve-Path Curl.Console/bin/Release/net10.0/curl.exe).Path))){ & ./Record-CurlExchange.ps1 -Port 50976 -Curl $p[1] -OutDirectory "$env:TEMP\ac-ru\$($p[0])" -Response 'HTTP/1.1 201 Created\r\nLocation: /z\r\nContent-Length: 0\r\n\r\n' -CurlArgs @('-s','-w','%{redirect_url}','http://127.0.0.1:50976/') | Out-Null; $p[0] + ': [' + [IO.File]::ReadAllText("$env:TEMP\ac-ru\$($p[0])\stdout.bin") + ']' }
```

- Expected: curl: [http://127.0.0.1:50976/z] and candidate: [http://127.0.0.1:50976/z]
- Actual: curl: [http://127.0.0.1:50976/z] and candidate: []

The finding closes only when a later re-audit by the conformance auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-14: Created.
