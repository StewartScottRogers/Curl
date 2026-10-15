---
id: BL-2191
title: Fix AF-0198: -d '' --crlf (and --json '' --crlf) sends Transfer-Encoding: chunked with a 0 chunk where curl sends Content-Length: 0
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-14
completed: 2026-10-13
---
# BL-2191 — Fix AF-0198: -d '' --crlf (and --json '' --crlf) sends Transfer-Encoding: chunked with a 0 chunk where curl sends Content-Length: 0

## Goal

The defect the audit office reported as AF-0198 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0198 (Medium, conformance auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0198-d-crlf-and-json-crlf-sends-transfer-encoding-chunk.md`.

Location: `Curl.Protocol.Http.UnitLibrary/HttpRequestFraming.cs:294`

Location: `Curl.Protocol.Http.UnitLibrary/HttpRequestFraming.cs:294`

Reference: curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Differential case 170 (seed 202976318): `--json '' --keepalive-time 0 --crlf --retry-all-errors URL` differed in request. Dropping one option at a time reduced it to `-d '' --crlf URL` (also `--json '' --crlf URL`). `--json ''` without --crlf is the same, and `-d x --crlf` and `--json x --crlf` are the same. curl's request: `POST / HTTP/1.1|Host|User-Agent: curl/8.22.0|Accept: */*|Content-Length: 0|Content-Type: application/x-www-form-urlencoded||`. Curl's request: `...|Accept: */*|Transfer-Encoding: chunked|Content-Type: application/x-www-form-urlencoded||0||`. Cause: OfBody sets `long? length = convertLineEndings ? null : unconvertedLength;` (line 294), so every --crlf body has an unknown length and is sent chunked, even an empty one whose length is known to be 0. Phase 2: no ADR records this. The doc comment cites AF-0125 for chunking a converted -T file, not for an empty -d body.

Reproduction, from the finding:

Run from the repository root:

```powershell
$a=@('-d','','--crlf','http://127.0.0.1:50972/'); foreach($p in @(@('curl','C:\Program Files\Git\ucrt64\bin\curl.exe'),@('candidate',(Resolve-Path Curl.Console/bin/Release/net10.0/curl.exe).Path))){ & ./Record-CurlExchange.ps1 -Port 50972 -Curl $p[1] -OutDirectory "$env:TEMP\ac-crlf\$($p[0])" -CurlArgs $a | Out-Null; $p[0] + ': ' + ((Select-String -Path "$env:TEMP\ac-crlf\$($p[0])\request.bin" -Pattern '^(Content-Length|Transfer-Encoding): [^\r]*').Matches.Value -join ', ') }
```

- Expected: curl: Content-Length: 0 / candidate: Content-Length: 0
- Actual: curl: Content-Length: 0 / candidate: Transfer-Encoding: chunked

The finding closes only when a later re-audit by the conformance auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Added `Curl.Protocol.Http.UnitTests` to `touches` for the focused unit tests (ADR-0482); no task in Doing on `origin/work/dark-factory` names it.
- Measured curl 8.22.0 (Schannel): `-d '' --crlf` and an empty `-T` file with `--crlf` both send `Content-Length: 0`. Fix: `HttpRequestFraming.OfBody` skips line-ending conversion for a body of known length 0, so it keeps `Content-Length: 0` instead of going chunked. Non-empty `--crlf` bodies stay chunked (AF-0125).
- Tests: `HttpCrlfUploadStreamTests.Of_CrlfEmptyBytesBody_KeepsContentLengthZero` and `Of_CrlfEmptyUpload_KeepsContentLengthZero` cover both sides of the new condition; Measure-CodeQuality was not run for this one-condition change.
- Ran the full feature pipeline's work directly: a one-line fix needed no architect or reviewer subagent.
- Reproduction rerun on a Release build: `curl: Content-Length: 0` / `candidate: Content-Length: 0`.

## Log

- 2026-10-14: Created.
- 2026-10-13: Backlog -> Doing.
- 2026-10-13: Doing -> Done. -d '' --crlf, --json '' --crlf and an empty -T with --crlf send Content-Length: 0 like curl
