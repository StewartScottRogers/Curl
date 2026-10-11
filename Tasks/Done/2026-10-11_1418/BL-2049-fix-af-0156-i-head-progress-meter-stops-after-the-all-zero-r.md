---
id: BL-2049
title: Fix AF-0156: -I (HEAD) progress meter stops after the all-zero row; curl 8.21.0 draws three more rows with the Content-Length as Total
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-11
completed: 2026-10-11
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

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Measured curl 8.21.0 (mingw, Schannel) with Record-CurlExchange.ps1 on -I: every HEAD response draws the zero row then three more rows. Their Total is the Content-Length (6 for `Content-Length: 6`), and 0 for a response without one and for a 204 with `Content-Length: 6`.
- Fix: `HttpResponseBodyReader.CopyAsync` now reports 0 bytes out of the Content-Length for a body -I leaves unread (`ReportUnreadBody`), null for 204/304 or --ignore-content-length (the latter by curl's rule that it drops the length, not measured). Bodiless responses without -I still report nothing; not measured here, left as they were. `ExpectedLength` stays unset for -I, so the exit 28 message is unchanged.
- Added Curl.Protocol.Http.UnitTests to touches for the new test `CopyAsync_ResponseWithNoBody_ReportsProgressAsCurlDraws`; no task in Doing on origin/work/dark-factory names it.
- Reproduction after the fix: curl rows with Total 6: 3 / Curl: 3. Every branch of the new method is hit by the test's five rows; Measure-CodeQuality.ps1 not run (cost; one 8-line method, complexity 3).

## Log

- 2026-10-11: Created.
- 2026-10-11: Backlog -> Doing.
- 2026-10-11: Doing -> Done. -I progress rows now match curl 8.21.0; AF-0156 reproduction gives 3 / 3
