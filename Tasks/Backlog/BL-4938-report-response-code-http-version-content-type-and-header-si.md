---
id: BL-4938
title: Report response code, HTTP version, content type and header size for s3, gs, az, oci and grpc
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4916]
touches: [Curl.Extended.Protocol.S3.UnitLibrary, Curl.Extended.Protocol.AzureStorage.UnitLibrary, Curl.Extended.Protocol.GoogleCloudStorage.UnitLibrary, Curl.Extended.Protocol.OciRegistry.UnitLibrary, Curl.Extended.Protocol.Grpc.UnitLibrary]
requirement: none
created: 2026-11-11
completed:
---
# BL-4938 — Report response code, HTTP version, content type and header size for s3, gs, az, oci and grpc

## Goal

`-w` on the HTTP-based extension schemes reports the real status, HTTP version, content type and header size.

## Context

From the outside fitness review of 2026-11-11 (commit 6ce7cfcf, the `preview` `curl-win-x64.zip`, Windows 11 x64; REF = curl 8.22.0 Schannel), finding EXT-016 (Low, confirmed: reproduced at least twice).

- Repro: `curl.exe -s -o out.bin -w "%{http_code} %{response_code} %{http_version} %{content_type} %{size_header}\n" s3://mybucket/obj` against the mock (200, `application/octet-stream`); a grpc unary call with `-w "%{response_code} %{http_version}"`.
- Expected: `200 200 1.1 application/octet-stream <n>` for s3; `200 2` for grpc.
- Actual: `000 000 0  0` for s3/gs/az/oci; `000 0` for grpc.
- Where to start: The handlers never report status, version or headers into the transfer result read by `-w`. Split per scheme at claim time if too big.

## Acceptance criteria

- [ ] The s3 command prints `200 200 1.1 application/octet-stream` and a non-zero header size; gs, az and oci likewise.
- [ ] The grpc command prints `200 2`.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

## Log

- 2026-11-11: Created.
