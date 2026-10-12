---
id: BL-2105
title: Fix AF-0170: -T <file> with -O on a URL ending in / saves the response over the upload file instead of curl_response, without curl's No remote filename warning
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console]
requirement: none
created: 2026-10-12
completed:
---
# BL-2105 — Fix AF-0170: -T <file> with -O on a URL ending in / saves the response over the upload file instead of curl_response, without curl's No remote filename warning

## Goal

The defect the audit office reported as AF-0170 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0170 (High, conformance auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0170-t-file-with-o-on-a-url-ending-in-saves-the-respons.md`.

Location: `Curl.Console/CurlCommandRunner.cs:3936`

Location: `Curl.Console/CurlCommandRunner.cs:3936`

Reference: curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Differential seed 1478778587, case 281 (--sslv2 -T f.txt --remote-name http://127.0.0.1:PORT/), reduced to -T f.txt -O http://127.0.0.1:PORT/. Both send the same PUT /f.txt and exit 0. curl prints 'Warning: No remote filename, uses "curl_response"' and writes the response body to curl_response, leaving f.txt (9 bytes) untouched. Curl prints no warning and writes the response to f.txt, which ends up 0 bytes: the uploaded source file is destroyed. Cause: ResolveOutputFile (CurlCommandRunner.cs:3924-3936) takes the remote name from the transfer URL after UploadTransferUrl.TryResolve (line 2186) has appended the upload file name, while curl takes it from the URL as given. FallsBackToDefaultRemoteName (line 3951) has the same cause, so the warning is missing too. Phase 2: no ADR records this as deliberate (ADR-0029 and ADR-0051 checked).

Reproduction, from the finding:

Run from the repository root:

```powershell
$r=(Get-Location).Path; $o="$env:TEMP\ac-tO"; foreach($s in 'curl','candidate'){ New-Item -ItemType Directory -Force "$o\$s" | Out-Null; Set-Content -NoNewline "$o\$s\f.txt" 'file body' }; $a=@('-T','f.txt','-O','http://127.0.0.1:50982/'); $c=(Resolve-Path Curl.Console/bin/Release/net10.0/curl.exe).Path; foreach($p in @(@('curl','C:\Program Files\Git\mingw64\bin\curl.exe'),@('candidate',$c))){ Set-Location "$o\$($p[0])"; [Environment]::CurrentDirectory="$o\$($p[0])"; & "$r\Record-CurlExchange.ps1" -Port 50982 -Curl $p[1] -OutDirectory "$o\$($p[0])" -CurlArgs $a | Out-Null }; Set-Location $r; [Environment]::CurrentDirectory=$r; foreach($s in 'curl','candidate'){ $s + ': f.txt=' + (Get-Item "$o\$s\f.txt").Length + ' curl_response=' + (Test-Path "$o\$s\curl_response") + ' warning=' + [bool](Select-String -Quiet -Path "$o\$s\stderr.txt" -Pattern 'No remote filename') }
```

- Expected: curl: f.txt=9 curl_response=True warning=True / candidate: f.txt=9 curl_response=True warning=True
- Actual: curl: f.txt=9 curl_response=True warning=True / candidate: f.txt=0 curl_response=False warning=False

The finding closes only when a later re-audit by the conformance auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-12: Created.
- 2026-10-12: Backlog -> Doing.
