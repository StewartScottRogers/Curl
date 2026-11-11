---
id: BL-4904
title: Make s3, gs, az and oci honour -X and -I or refuse them, never GET instead
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.S3.UnitLibrary/Client/S3ProtocolHandler.cs, Curl.Extended.Protocol.S3.UnitTests/S3RequestMethodTests.cs, Curl.Extended.Protocol.AzureStorage.UnitLibrary, Curl.Extended.Protocol.GoogleCloudStorage.UnitLibrary, Curl.Extended.Protocol.OciRegistry.UnitLibrary]
requirement: none
created: 2026-11-11
completed:
---
# BL-4904 — Make s3, gs, az and oci honour -X and -I or refuse them, never GET instead

## Goal

`-X DELETE` and `-I` on a storage scheme send DELETE / HEAD, or fail with a non-zero exit before any request; a destructive request never reports success unsent.

## Context

From the outside fitness review of 2026-11-11 (commit 6ce7cfcf, the `preview` `curl-win-x64.zip`, Windows 11 x64; REF = curl 8.22.0 Schannel), finding EXT-004 (Medium (wrong operation), confirmed: reproduced at least twice).

- Repro: Request-logging HTTP mock on 18701 returning `hello-object` for `GET /mybucket/obj`. `AWS_ENDPOINT_URL=http://127.0.0.1:18701 AWS_ACCESS_KEY_ID=AK AWS_SECRET_ACCESS_KEY=x curl.exe -s -X DELETE s3://mybucket/obj; echo $?`; same with `-I`; likewise gs (`STORAGE_EMULATOR_HOST`), az and oci.
- Expected: DELETE / HEAD sent, or the option refused with a non-zero exit before any request.
- Actual: Server logs only `GET /mybucket/obj`; stdout `hello-object`; exit 0. gs, az and oci the same.
- Where to start: `S3ProtocolHandler.cs` ~line 55 picks the operation only from upload presence and a trailing `/`; the Azure, GCS and OCI handlers do the same. Split per scheme at claim time if too big.

## Acceptance criteria

- [ ] For s3, gs, az and oci, `-X DELETE` sends DELETE (or exits non-zero without sending a GET), and `-I` sends HEAD (or exits non-zero) and never writes `hello-object`.
- [ ] Each scheme's conformance document lists the supported methods.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

## Log

- 2026-11-11: Created.
