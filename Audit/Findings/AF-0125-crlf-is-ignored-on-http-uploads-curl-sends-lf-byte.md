---
id: AF-0125
title: --crlf is ignored on HTTP uploads: Curl sends LF bytes with Content-Length where curl converts to CRLF and sends chunked
auditor: conformance
severity: High
status: accepted
reason: 
key: conformance:Curl.Cli.UnitLibrary/CommandLineOptionTable.cs:--crlf:request
reproduction: none
task: BL-1875
tasks: BL-1875
found: 2026-10-08
found-at: cddb276d1d10fbb372f36a32cc1f588fd84c58e8
scorecard: 2026-10-08_2315.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0125 - --crlf is ignored on HTTP uploads: Curl sends LF bytes with Content-Length where curl converts to CRLF and sends chunked

## Summary

High finding from the conformance auditor at `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs:192`: --crlf is ignored on HTTP uploads: Curl sends LF bytes with Content-Length where curl converts to CRLF and sends chunked.

## Evidence

Location: `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs:192`

Reference: curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel. Differential seed 1306208109, case 300: --post303 --crlf --form f=@f.txt --post302 http://127.0.0.1:PORT/ (differs in request). Reduced to --crlf -F f=@f.txt URL. With f.txt = 'file body\n', curl sends 'Transfer-Encoding: chunked' and the part body 'file body\r\n' (request 414 bytes). Curl sends 'Content-Length: 202' and 'file body\n' (395 bytes). The same happens with --crlf -T f.txt (curl: chunked 'b\r\nfile body\r\n'; Curl: Content-Length: 10, 'file body\n') and with --crlf --data-binary @f.txt. Without --crlf both binaries send identical requests. ConvertLineEndings is set at CommandLineOptionTable.cs:192 and flows into TransferContext, but only Curl.Protocol.File.UnitLibrary (CrlfUploadConverter) reads it. No HTTP code converts line endings. Phase 2: ADR-0003's amendment says --crlf applies to uploads generally but measured only file://. It does not record HTTP as a deliberate divergence.

## Reproduction

Run from the repository root:

```powershell
$o="$env:TEMP\cf-crlf"; New-Item -ItemType Directory -Force $o | Out-Null; [IO.File]::WriteAllText("$o\f.txt","file body`n"); $a=@('--crlf','-T',"$o\f.txt",'http://127.0.0.1:51005/'); & ./Record-CurlExchange.ps1 -Port 51005 -Curl 'C:\Program Files\Git\mingw64\bin\curl.exe' -OutDirectory "$o\curl" -CurlArgs $a | Out-Null; & ./Record-CurlExchange.ps1 -Port 51005 -Curl (Resolve-Path Curl.Console/bin/Release/net10.0/curl.exe) -OutDirectory "$o\candidate" -CurlArgs $a | Out-Null; 'curl ' + (Get-Item "$o\curl\request.bin").Length + ' / Curl ' + (Get-Item "$o\candidate\request.bin").Length
```

- Expected: curl 133 / Curl 133 (Transfer-Encoding: chunked, body 'file body\r\n')
- Actual: curl 133 / Curl 114 (Content-Length: 10, body 'file body\n')

## Re-audits

- 2026-10-09 | 2026-10-09_0225.md | not re-audited | overlaps planted defect PD-303 in Curl.Cli.UnitLibrary/CommandLineOptionTable.cs, so the auditor's verdict (reproduces yes) is set aside: Ran the reproduction: 'curl 133 / Curl 114'. curl sends 'Transfer-Encoding: chunked' and the chunk 'b\r\nfile body\r\n\r\n0\r\n\r\n'. Curl sends 'Content-Length: 10' and 'file body\n' with a bare LF, so --crlf is still ignored on HTTP upload.

## Log

- 2026-10-08: filed proposed.
- 2026-10-09: proposed -> accepted.
