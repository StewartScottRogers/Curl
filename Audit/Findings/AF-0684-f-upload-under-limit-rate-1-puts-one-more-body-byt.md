---
id: AF-0684
title: -F upload under --limit-rate 1 puts one more body byte on the wire than curl before the server's reset
auditor: conformance
severity: Low
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: conformance:Curl.Console/RateLimitedUploadStream.cs:--limit-rate:request
reproduction: none
task: BL-4875
tasks: BL-4875
found: 2026-11-05
found-at: aea9f0110c59ad5209aafb9f0f985886a2cb9439
scorecard: 2026-11-05_0819.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0684 - -F upload under --limit-rate 1 puts one more body byte on the wire than curl before the server's reset

## Summary

Low finding from the conformance auditor at `Curl.Console/RateLimitedUploadStream.cs:63`: -F upload under --limit-rate 1 puts one more body byte on the wire than curl before the server's reset.

## Evidence

Location: `Curl.Console/RateLimitedUploadStream.cs:63`

Reference: curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Differential seed 843793, case 137 (`--user-agent x --no-keepalive --form a=b --limit-rate 1 URL`), differs in request only; reduced to `--form a=b --limit-rate 1 URL`. Original run: curl's request.bin ends `\r\n\r\n--` (185 bytes), Curl's ends `\r\n\r\n---` (186 bytes); both exit 56. Reduced pair, 2 reruns each: curl 195 bytes, Curl 196 bytes every time. Curl's limiter (RateLimitedStream through RateLimitedUploadStream.ReadAsync) lets one body byte more onto the wire than curl in the same window, so the peer sees a different request. The difference depends on timing but was the same on all 3 runs. Phase 2: ADR-0745 (limit-rate holds the upload in the tool) records rate-limiting the upload, not a different pacing.

## Reproduction

Run from the repository root:

```powershell
dotnet build Curl.Console -c Release; & ./Record-CurlExchange.ps1 -Port 63871 -Curl 'C:\Program Files\Git\ucrt64\bin\curl.exe' -OutDirectory $env:TEMP\f-curl -CurlArgs @('--form','a=b','--limit-rate','1','http://127.0.0.1:63871/'); & ./Record-CurlExchange.ps1 -Port 63871 -Curl ./Curl.Console/bin/Release/net10.0/curl.exe -OutDirectory $env:TEMP\f-cand -CurlArgs @('--form','a=b','--limit-rate','1','http://127.0.0.1:63871/'); (Get-Item $env:TEMP\f-curl\request.bin).Length; (Get-Item $env:TEMP\f-cand\request.bin).Length
```

- Expected: Equal request lengths (195 and 195), curl's pacing.
- Actual: 195 (curl) and 196 (Curl): one more body byte sent before the reset.

## Re-audits

- 2026-11-05 | 2026-11-05_2025.md | not re-audited | Ran the reproduction 3 times against curl 8.22.0 (x86_64-w64-mingw32) Schannel. request.bin lengths (curl, Curl): (194, 198), (195, 194), (194, 195); both exit 56 every time. The count depends on timing in both binaries, and Curl sometimes sends fewer bytes than curl, so the reproduction does not reliably tell.

## Log

- 2026-11-05: filed proposed.
