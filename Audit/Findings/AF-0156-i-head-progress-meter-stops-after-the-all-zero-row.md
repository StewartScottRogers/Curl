---
id: AF-0156
title: -I (HEAD) progress meter stops after the all-zero row; curl 8.21.0 draws three more rows with the Content-Length as Total
auditor: conformance
severity: Low
status: proposed
reason:
key: conformance:Curl.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs:CopyAsync-head-progress-meter:stderr
reproduction: none
task: none
tasks:
found: 2026-10-11
found-at: 22f394602532a6922adbc7fa91b3739b62d2112a
scorecard: 2026-10-11_0051.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0156 - -I (HEAD) progress meter stops after the all-zero row; curl 8.21.0 draws three more rows with the Content-Length as Total

## Summary

Low finding from the conformance auditor at `Curl.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs:290`: -I (HEAD) progress meter stops after the all-zero row; curl 8.21.0 draws three more rows with the Content-Length as Total. Reported by an auditor flagged unreliable in 2026-10-11_0051.md.

## Evidence

Location: `Curl.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs:290`

Reference: curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Not visible to the differential tool (seed 586388576, 300 cases), which drops meter rows; found by targeted probes -I URL, -I -o b.txt URL, --head -w %{http_code} -o body.txt URL, all with 'Content-Length: 6'. Reduced to: -I http://127.0.0.1:PORT/. Exit code and stdout match. curl's stderr after the two header lines: the zero row, then three rows '  0      6   0      0 ...' (Total 6), then CRLF. Curl's: the zero row, then CRLF only. Same in 4 runs. A plain GET draws its rows correctly, so only the HEAD path loses them. Cause read in code: CopyAsync starts with 'if (!HasBody(head, noBody)) { return; }' before 'ExpectedLength = framing.ContentLength; Progress.ReportDownloaded(BytesWritten, ExpectedLength);' (lines 290-303), so a HEAD response never reports its expected size and the recorder ('when the handler reported any bytes, three done lines') draws no done lines. Phase 2: no ADR under Documentation/Planning/Decisions records this divergence.

## Reproduction

Run from the repository root:

```powershell
$o="$env:TEMP\ac-head"; $a=@('-I','http://127.0.0.1:50991/'); $r='HTTP/1.1 200 OK\r\nContent-Length: 6\r\n\r\n'; & ./Record-CurlExchange.ps1 -Port 50991 -Response $r -Curl 'C:\Program Files\Git\mingw64\bin\curl.exe' -OutDirectory "$o\curl" -CurlArgs $a | Out-Null; & ./Record-CurlExchange.ps1 -Port 50991 -Response $r -Curl (Resolve-Path Curl.Console/bin/Release/net10.0/curl.exe) -OutDirectory "$o\candidate" -CurlArgs $a | Out-Null; 'curl rows with Total 6: ' + ([regex]::Matches((Get-Content -Raw "$o\curl\stderr.txt"), '\r100 +6 |\r +0 +6 ')).Count + ' / Curl: ' + ([regex]::Matches((Get-Content -Raw "$o\candidate\stderr.txt"), '\r100 +6 |\r +0 +6 ')).Count
```

- Expected: curl rows with Total 6: 3 / Curl: 3
- Actual: curl rows with Total 6: 3 / Curl: 0

## Re-audits

## Log

- 2026-10-11: filed proposed.
