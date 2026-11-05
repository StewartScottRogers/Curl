---
id: BL-4875
title: Fix AF-0684: -F upload under --limit-rate 1 puts one more body byte on the wire than curl before the server's reset
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console]
requirement: none
created: 2026-11-05
completed:
---
# BL-4875 — Fix AF-0684: -F upload under --limit-rate 1 puts one more body byte on the wire than curl before the server's reset

## Goal

The defect the audit office reported as AF-0684 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0684 (Low, conformance auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0684-f-upload-under-limit-rate-1-puts-one-more-body-byt.md`.

Location: `Curl.Console/RateLimitedUploadStream.cs:63`

Location: `Curl.Console/RateLimitedUploadStream.cs:63`

Reference: curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Differential seed 843793, case 137 (`--user-agent x --no-keepalive --form a=b --limit-rate 1 URL`), differs in request only; reduced to `--form a=b --limit-rate 1 URL`. Original run: curl's request.bin ends `\r\n\r\n--` (185 bytes), Curl's ends `\r\n\r\n---` (186 bytes); both exit 56. Reduced pair, 2 reruns each: curl 195 bytes, Curl 196 bytes every time. Curl's limiter (RateLimitedStream through RateLimitedUploadStream.ReadAsync) lets one body byte more onto the wire than curl in the same window, so the peer sees a different request. The difference depends on timing but was the same on all 3 runs. Phase 2: ADR-0745 (limit-rate holds the upload in the tool) records rate-limiting the upload, not a different pacing.

Reproduction, from the finding:

Run from the repository root:

```powershell
dotnet build Curl.Console -c Release; & ./Record-CurlExchange.ps1 -Port 63871 -Curl 'C:\Program Files\Git\ucrt64\bin\curl.exe' -OutDirectory $env:TEMP\f-curl -CurlArgs @('--form','a=b','--limit-rate','1','http://127.0.0.1:63871/'); & ./Record-CurlExchange.ps1 -Port 63871 -Curl ./Curl.Console/bin/Release/net10.0/curl.exe -OutDirectory $env:TEMP\f-cand -CurlArgs @('--form','a=b','--limit-rate','1','http://127.0.0.1:63871/'); (Get-Item $env:TEMP\f-curl\request.bin).Length; (Get-Item $env:TEMP\f-cand\request.bin).Length
```

- Expected: Equal request lengths (195 and 195), curl's pacing.
- Actual: 195 (curl) and 196 (Curl): one more body byte sent before the reset.

The finding closes only when a later re-audit by the conformance auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-05: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
