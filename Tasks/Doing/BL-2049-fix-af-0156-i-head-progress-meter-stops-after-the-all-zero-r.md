---
id: BL-2049
title: Fix AF-0156: -I (HEAD) progress meter stops after the all-zero row; curl 8.21.0 draws three more rows with the Content-Length as Total
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary]
requirement: none
created: 2026-10-11
completed:
---
# BL-2049 — Fix AF-0156: -I (HEAD) progress meter stops after the all-zero row; curl 8.21.0 draws three more rows with the Content-Length as Total

## Goal

The defect the audit office reported as AF-0156 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0156 (Low, conformance auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0156-i-head-progress-meter-stops-after-the-all-zero-row.md`.

Location: `Curl.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs:290`

Location: `Curl.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs:290`

Reference: curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Not visible to the differential tool (seed 586388576, 300 cases), which drops meter rows; found by targeted probes -I URL, -I -o b.txt URL, --head -w %{http_code} -o body.txt URL, all with 'Content-Length: 6'. Reduced to: -I http://127.0.0.1:PORT/. Exit code and stdout match. curl's stderr after the two header lines: the zero row, then three rows '  0      6   0      0 ...' (Total 6), then CRLF. Curl's: the zero row, then CRLF only. Same in 4 runs. A plain GET draws its rows correctly, so only the HEAD path loses them. Cause read in code: CopyAsync starts with 'if (!HasBody(head, noBody)) { return; }' before 'ExpectedLength = framing.ContentLength; Progress.ReportDownloaded(BytesWritten, ExpectedLength);' (lines 290-303), so a HEAD response never reports its expected size and the recorder ('when the handler reported any bytes, three done lines') draws no done lines. Phase 2: no ADR under Documentation/Planning/Decisions records this divergence.

Reproduction, from the finding:

Run from the repository root:

```powershell
$o="$env:TEMP\ac-head"; $a=@('-I','http://127.0.0.1:50991/'); $r='HTTP/1.1 200 OK\r\nContent-Length: 6\r\n\r\n'; & ./Record-CurlExchange.ps1 -Port 50991 -Response $r -Curl 'C:\Program Files\Git\mingw64\bin\curl.exe' -OutDirectory "$o\curl" -CurlArgs $a | Out-Null; & ./Record-CurlExchange.ps1 -Port 50991 -Response $r -Curl (Resolve-Path Curl.Console/bin/Release/net10.0/curl.exe) -OutDirectory "$o\candidate" -CurlArgs $a | Out-Null; 'curl rows with Total 6: ' + ([regex]::Matches((Get-Content -Raw "$o\curl\stderr.txt"), '\r100 +6 |\r +0 +6 ')).Count + ' / Curl: ' + ([regex]::Matches((Get-Content -Raw "$o\candidate\stderr.txt"), '\r100 +6 |\r +0 +6 ')).Count
```

- Expected: curl rows with Total 6: 3 / Curl: 3
- Actual: curl rows with Total 6: 3 / Curl: 0

The finding closes only when a later re-audit by the conformance auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-11: Created.
- 2026-10-11: Backlog -> Doing.
