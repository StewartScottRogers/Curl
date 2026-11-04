---
id: AF-0623
title: -w %{url} with -T prints the URL as typed; curl prints it with the uploaded file name appended
auditor: conformance
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: conformance:write-out:url-with-upload-file:stdout
reproduction: none
task: none
tasks:
found: 2026-11-03
found-at: 7410f102c809d5232e4f9b210b3ac32486848c27
scorecard: 2026-11-03_1615.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0623 - -w %{url} with -T prints the URL as typed; curl prints it with the uploaded file name appended

## Summary

Medium finding from the conformance auditor at `Curl.Console/CurlCommandRunner.cs:2593`: -w %{url} with -T prints the URL as typed; curl prints it with the uploaded file name appended.

## Evidence

Location: `Curl.Console/CurlCommandRunner.cs:2593`

Reference: curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Differential run, seed 791054, case 44 (--write-out %{url} --upload-file sub/f.txt --disable -3 URL): both send the same PUT /f.txt and exit 0, but curl's stdout is 'http://127.0.0.1:50589/f.txt' and Curl's is 'http://127.0.0.1:50589/'. Smallest reduction: `-w %{url} -T sub/f.txt http://127.0.0.1:PORT/`. It differs even when the file is missing: both exit 26, curl prints 'http://127.0.0.1:50700/f.txt' and Curl prints 'http://127.0.0.1:50700/'. Cause: ReportAsync passes givenUrl, the URL before UploadTransferUrl appends the -T file name, to WriteOutAsync as the %{url} value (CurlCommandRunner.cs:2593, documented at 3305-3307 as 'as the glob expanded it'). curl's tool prints the URL after it has added the file name. Phase 2: ADR-0051 (-T URLs are resolved before the transfer) and ADR-0553 do not record %{url} printing the unresolved URL.

## Reproduction

Run from the repository root:

```powershell
dotnet build Curl.Console -c Release; $a=@('-w','%{url}','-T','sub/f.txt','http://127.0.0.1:50700/'); & .\Record-CurlExchange.ps1 -Port 50700 -Curl 'C:\Program Files\Git\ucrt64\bin\curl.exe' -OutDirectory $env:TEMP\wurl\curl -CurlArgs $a; & .\Record-CurlExchange.ps1 -Port 50700 -Curl Curl.Console\bin\Release\net10.0\curl.exe -OutDirectory $env:TEMP\wurl\cand -CurlArgs $a; Get-Content $env:TEMP\wurl\curl\stdout.bin, $env:TEMP\wurl\cand\stdout.bin
```

- Expected: Both print http://127.0.0.1:50700/f.txt
- Actual: curl prints http://127.0.0.1:50700/f.txt; Curl prints http://127.0.0.1:50700/

## Re-audits

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted. Accepted under Stewart's standing accept of 2026-11-02.
