---
id: BL-4874
title: Fix AF-0683: -d body under --limit-rate: a server reset mid-upload is exit 55 Send failure in Curl, exit 56 Recv failure in curl
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary]
requirement: none
created: 2026-11-05
completed:
---
# BL-4874 — Fix AF-0683: -d body under --limit-rate: a server reset mid-upload is exit 55 Send failure in Curl, exit 56 Recv failure in curl

## Goal

The defect the audit office reported as AF-0683 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0683 (High, conformance auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0683-d-body-under-limit-rate-a-server-reset-mid-upload.md`.

Location: `Curl.Original.Protocol.Http.UnitLibrary/HttpRequestBodyWriter.cs:521`

Location: `Curl.Original.Protocol.Http.UnitLibrary/HttpRequestBodyWriter.cs:521`

Reference: curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Found while reducing differential case 137 (seed 843793) and run as a reduced pair `-d abcdefghij --limit-rate 1 http://127.0.0.1:PORT/` against Record-CurlExchange's default exchange, 4 times each. curl: exit 56 every time, `curl: (56) Recv failure: Connection was reset` (once `...was aborted`). Curl: exit 55 every time, `curl: (55) Send failure: Connection was aborted`. Scripts branch on these codes. Cause: the probe that turns a failed write into curl's exit 56 (WriteReadPieceAsync, line 521, `when (waitedOnSource && ...)`) runs only after a StreamBody read that had to wait. A rate-limited -d bytes body is held by --limit-rate just as curl's loop is, but it never sets waitedOnSource, so it keeps exit 55. The -F form of the same command (case 137) gives exit 56 in both. Phase 2: explained in part by ADR-0730, which adds the exit-56 probe for waited-on stream bodies and says that 'any body of bytes ... keep[s] [its] exit code'. It does not cover a bytes body held by --limit-rate, where curl's loop polls for reading. Not in platform-exclusions.json.

Reproduction, from the finding:

Run from the repository root:

```powershell
dotnet build Curl.Console -c Release; & ./Record-CurlExchange.ps1 -Port 63871 -Curl 'C:\Program Files\Git\ucrt64\bin\curl.exe' -OutDirectory $env:TEMP\lr-curl -CurlArgs @('-d','abcdefghij','--limit-rate','1','http://127.0.0.1:63871/'); & ./Record-CurlExchange.ps1 -Port 63871 -Curl ./Curl.Console/bin/Release/net10.0/curl.exe -OutDirectory $env:TEMP\lr-cand -CurlArgs @('-d','abcdefghij','--limit-rate','1','http://127.0.0.1:63871/'); Get-Content $env:TEMP\lr-curl\exitcode.txt, $env:TEMP\lr-cand\exitcode.txt
```

- Expected: Both exit 56, `curl: (56) Recv failure: Connection was reset`.
- Actual: curl exit 56; Curl exit 55, `curl: (55) Send failure: Connection was aborted`.

The finding closes only when a later re-audit by the conformance auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-05: Created.
