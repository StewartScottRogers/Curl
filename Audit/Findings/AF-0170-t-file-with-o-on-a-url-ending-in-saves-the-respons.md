---
id: AF-0170
title: -T <file> with -O on a URL ending in / saves the response over the upload file instead of curl_response, without curl's No remote filename warning
auditor: conformance
severity: High
status: accepted
reason: 
key: conformance:--remote-name:upload-file-name:stdout
reproduction: none
task: BL-2105
tasks: BL-2105
found: 2026-10-11
found-at: d8245edb7b661211c6628e9062e9939934939fb2
scorecard: 2026-10-11_1638.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0170 - -T <file> with -O on a URL ending in / saves the response over the upload file instead of curl_response, without curl's No remote filename warning

## Summary

High finding from the conformance auditor at `Curl.Console/CurlCommandRunner.cs:3936`: -T <file> with -O on a URL ending in / saves the response over the upload file instead of curl_response, without curl's No remote filename warning.

## Evidence

Location: `Curl.Console/CurlCommandRunner.cs:3936`

Reference: curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Differential seed 1478778587, case 281 (--sslv2 -T f.txt --remote-name http://127.0.0.1:PORT/), reduced to -T f.txt -O http://127.0.0.1:PORT/. Both send the same PUT /f.txt and exit 0. curl prints 'Warning: No remote filename, uses "curl_response"' and writes the response body to curl_response, leaving f.txt (9 bytes) untouched. Curl prints no warning and writes the response to f.txt, which ends up 0 bytes: the uploaded source file is destroyed. Cause: ResolveOutputFile (CurlCommandRunner.cs:3924-3936) takes the remote name from the transfer URL after UploadTransferUrl.TryResolve (line 2186) has appended the upload file name, while curl takes it from the URL as given. FallsBackToDefaultRemoteName (line 3951) has the same cause, so the warning is missing too. Phase 2: no ADR records this as deliberate (ADR-0029 and ADR-0051 checked).

## Reproduction

Run from the repository root:

```powershell
$r=(Get-Location).Path; $o="$env:TEMP\ac-tO"; foreach($s in 'curl','candidate'){ New-Item -ItemType Directory -Force "$o\$s" | Out-Null; Set-Content -NoNewline "$o\$s\f.txt" 'file body' }; $a=@('-T','f.txt','-O','http://127.0.0.1:50982/'); $c=(Resolve-Path Curl.Console/bin/Release/net10.0/curl.exe).Path; foreach($p in @(@('curl','C:\Program Files\Git\mingw64\bin\curl.exe'),@('candidate',$c))){ Set-Location "$o\$($p[0])"; [Environment]::CurrentDirectory="$o\$($p[0])"; & "$r\Record-CurlExchange.ps1" -Port 50982 -Curl $p[1] -OutDirectory "$o\$($p[0])" -CurlArgs $a | Out-Null }; Set-Location $r; [Environment]::CurrentDirectory=$r; foreach($s in 'curl','candidate'){ $s + ': f.txt=' + (Get-Item "$o\$s\f.txt").Length + ' curl_response=' + (Test-Path "$o\$s\curl_response") + ' warning=' + [bool](Select-String -Quiet -Path "$o\$s\stderr.txt" -Pattern 'No remote filename') }
```

- Expected: curl: f.txt=9 curl_response=True warning=True / candidate: f.txt=9 curl_response=True warning=True
- Actual: curl: f.txt=9 curl_response=True warning=True / candidate: f.txt=0 curl_response=False warning=False

## Re-audits

- 2026-10-12 | 2026-10-12_1653.md | not re-audited | overlaps planted defect PD-201 in Curl.Console/CurlCommandRunner.cs, so the auditor's verdict (reproduces no) is set aside: As given, the reproduction cannot tell on this machine: Set-Content -NoNewline silently creates no file (Windows PowerShell 5.1.26100 and pwsh), so f.txt never exists and both binaries fail to open it. I reran it with the same setup written as [IO.File]::WriteAllText("$o\$s\f.txt",'file body'), everything else unchanged: 'curl: f.txt=9 curl_response=True warning=True exit=0' and 'candidate: f.txt=9 curl_response=True warning=True exit=0'. Both sent the same PUT /f.txt with a 9-byte body. Curl now saves to curl_response and prints curl's 'No remote filename' warning, and leaves the upload file untouched.
- 2026-10-13 | 2026-10-13_0925.md | reproduces: no | Ran the reproduction with the reference path changed from mingw64 to ucrt64\bin\curl.exe (curl 8.22.0). On this machine 'Set-Content -NoNewline <path> <value>' silently created no file: the first run showed exit 26 'cannot open f.txt' for both binaries. The rerun created f.txt with [IO.File]::WriteAllText, everything else verbatim. Output: 'curl: f.txt=9 curl_response=True warning=True' and 'candidate: f.txt=9 curl_response=True warning=True'. Both exit 0 and both print 'Warning: No remote filename, uses "curl_response"'.

## Log

- 2026-10-11: filed proposed.
- 2026-10-11: proposed -> accepted.
