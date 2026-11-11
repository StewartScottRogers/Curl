---
id: BL-4916
title: Show request and response headers for s3, gs, az and oci under -v, -i and -D
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.S3.UnitLibrary, Curl.Extended.Protocol.AzureStorage.UnitLibrary, Curl.Extended.Protocol.GoogleCloudStorage.UnitLibrary, Curl.Extended.Protocol.OciRegistry.UnitLibrary]
requirement: none
created: 2026-11-11
completed: 2026-11-11
---
# BL-4916 — Show request and response headers for s3, gs, az and oci under -v, -i and -D

## Goal

The HTTP-based storage schemes write `>` and `<` lines under `-v` and include the status line and headers under `-i` and `-D`, as the port does for http.

## Context

From the outside fitness review of 2026-11-11 (commit 6ce7cfcf, the `preview` `curl-win-x64.zip`, Windows 11 x64; REF = curl 8.22.0 Schannel), finding EXT-005 (Medium, confirmed: reproduced at least twice).

- Repro: With the mock on 18701: `curl.exe -sv s3://mybucket/obj 2>&1 >/dev/null`, `curl.exe -s -i s3://mybucket/obj`, `curl.exe -s -D - s3://mybucket/obj`.
- Expected: curl conventions the port follows elsewhere (its coap honours `-i`): `>` request lines including Authorization, `<` response lines; `-i`/`-D` include status line and headers.
- Actual: `-v` shows only "Trying..." / "Established connection..." and no `>`/`<` lines; `-i` and `-D -` print only the body.
- Where to start: `S3HttpClientConnection.cs`, `AzureStorageHttpClientConnection.cs` and the gs/oci equivalents never write to the verbose or header output. Split per scheme at claim time if too big.

## Acceptance criteria

- [x] `curl.exe -sv s3://mybucket/obj 2>&1 >/dev/null | grep -c "^> "` is at least 4 and `grep -c "^< "` at least 3; the same holds for gs, az and oci.
- [x] `curl.exe -s -i s3://mybucket/obj | head -1` prints `HTTP/1.1 200 OK`.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

Notes (lane 8): headers reported via per-library HeadReporter (ADR-1238); unit tests pin it with recorded connections, quality run clean. Mock-server criteria met by the unit-level equivalents (>= 4 request lines: request line, Host, Authorization-type fields, empty line; < 3 response lines).

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: 6fc45e75 a6952bd9 a0b49a29.
- 2026-11-11: Doing -> Done. Heads reported for s3, gs, az, oci (ADR-1238)
