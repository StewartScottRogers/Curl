---
id: BL-1763
title: Fix AF-0085: -v omits curl's '{ [0 bytes data]' line when a read-to-close HTTP body ends with an empty read (e.g. --ignore-content-length on an empty body)
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary]
requirement: none
created: 2026-10-08
completed:
---
# BL-1763 — Fix AF-0085: -v omits curl's '{ [0 bytes data]' line when a read-to-close HTTP body ends with an empty read (e.g. --ignore-content-length on an empty body)

## Goal

The defect the audit office reported as AF-0085 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0085 (Low, conformance auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0085-v-omits-curl-s-0-bytes-data-line-when-a-read-to-cl.md`.

Location: `Curl.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs:414`

Location: `Curl.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs:414`

Reference: curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Differential run seed 70867401: case 245 (--haproxy-clientip - --verbose --follow --ignore-content-length URL) differs in stderr only; exit codes 0/0, stdout identical. Reduced by dropping one option at a time to '-v --ignore-content-length URL' (dropping --ignore-content-length or -v removes the difference). Against the canned 'HTTP/1.1 200 OK, Content-Length: 0' response, curl writes '{ [0 bytes data]' after the '< ' empty line and before the meter rows; Curl writes nothing there (diff: '16d15 < { [0 bytes data]'). Cause: the read-to-close loop in HttpResponseBodyReader (lines 332-340) ends on read == 0 without a data event, and ReportReceived (lines 414-420) reports nothing for an empty span. Phase 2: no ADR records this as deliberate.

Reproduction, from the finding:

Run from the repository root:

```powershell
$o="$env:TEMP\af-icl"; $a=@('-v','--ignore-content-length','http://127.0.0.1:50997/'); & ./Record-CurlExchange.ps1 -Port 50997 -Curl 'C:\Program Files\Git\mingw64\bin\curl.exe' -OutDirectory "$o\curl" -CurlArgs $a | Out-Null; & ./Record-CurlExchange.ps1 -Port 50997 -Curl (Resolve-Path Curl.Console/bin/Release/net10.0/curl.exe) -OutDirectory "$o\candidate" -CurlArgs $a | Out-Null; 'curl: ' + @(Select-String -Path "$o\curl\stderr.txt" -SimpleMatch 'bytes data]').Count + ' / Curl: ' + @(Select-String -Path "$o\candidate\stderr.txt" -SimpleMatch 'bytes data]').Count
```

- Expected: curl: 1 / Curl: 1
- Actual: curl: 1 / Curl: 0

The finding closes only when a later re-audit by the conformance auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Measured curl 8.21.0 (mingw, Schannel) on 2026-10-08 with `Record-CurlExchange.ps1 -Response`:
  `-v --ignore-content-length` against `Content-Length: 0` and plain `-v` against
  `Connection: close` with no body both write `{ [0 bytes data]` after `< `; with a 5-byte
  body (either way) curl writes only `{ [5 bytes data]`, no empty line at the close. So the
  rule is: a read-to-close body that closed without a byte reports one empty data event.
- Fix: `HttpResponseBodyReader.EndAtClose` reports `Events.ReportDataReceived([])` when a
  read-to-close body ends with nothing received. This also fixes the no-Content-Length case,
  which the finding did not name.
- Tests: `HttpProtocolHandlerTests.Events.cs` -
  `ExecuteAsync_ReadToCloseBodyClosingWithoutAByte_ReportsOneEmptyDataEvent` (both cases) and
  `ExecuteAsync_ReadToCloseBodyWithBytes_ReportsNoEmptyDataEventAtTheClose`. The tests live in
  `Curl.Protocol.Http.UnitTests`, the library's own test project.
- Reproduction run against the Debug build (`Curl.Console/bin/Debug/net10.0/curl.exe`, the
  build this lane makes): `curl: 1 / Curl: 1`.
- `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary`: 0 failing members.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
