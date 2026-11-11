---
id: BL-4904
title: Make s3, gs, az and oci honour -X and -I or refuse them, never GET instead
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.S3.UnitLibrary/Client/S3ProtocolHandler.cs, Curl.Extended.Protocol.S3.UnitTests/S3RequestMethodTests.cs, Curl.Extended.Protocol.AzureStorage.UnitLibrary, Curl.Extended.Protocol.GoogleCloudStorage.UnitLibrary, Curl.Extended.Protocol.OciRegistry.UnitLibrary, Curl.Extended.Protocol.AzureStorage.UnitTests/Client/AzureStorageRequestMethodTests.cs, Curl.Extended.Protocol.GoogleCloudStorage.UnitTests/Client/GcsRequestMethodTests.cs, Curl.Extended.Protocol.OciRegistry.UnitTests/Client/OciRegistryRequestMethodTests.cs, Curl.Extended.Protocol.S3.UnitLibrary/Client/S3RequestMethod.cs, Documentation/Conformance/Extended/S3.md, Documentation/Conformance/Extended/GoogleCloudStorage.md, Documentation/Conformance/Extended/AzureStorage.md, Documentation/Conformance/Extended/OciRegistry.md]
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

- 2026-11-11 (lane 8): Decision (ADR-1238, Decided by Claude under Stewart's delegation): refuse, not
  implement. `-I` and any `-X` other than GET (no `-T`) / PUT (with `-T`) fail with exit 4 (NotBuiltIn) and
  `<scheme>:// does not support -X <M>; it sends only GET, or PUT with -T` before any connection. Each
  library has its own `Client/<Prefix>RequestMethod.RefusalOf` (extensions never reference each other),
  called first in each handler's `ExecuteAsync`. Real curl not measured: these are extension schemes with
  no upstream answer.
- Touches widened to the three other libraries' test files (the task named their libraries but no test
  project), `S3RequestMethod.cs`, and the four conformance documents (criterion 2). No task in Doing on
  origin/work/dark-factory names any of them.
- Written but NOT built or tested, left uncommitted for the shift to stash: the four `*RequestMethod.cs`,
  the four handler edits, the four `*RequestMethodTests.cs` (S3 at `Curl.Extended.Protocol.S3.UnitTests/`
  as touches named it, the others under `Client/`), a "Request methods" section in each conformance
  document, ADR-1238 and its README row. `dotnet build` fails "SDK not found": global.json pins 10.0.401
  (latestFeature) and only 10.0.100-rc.1, 11.0.100-rc.2 and 11.0.100 are installed (same as BL-4909).
  Left for the next claim: build, fix compile errors in the new tests (the test files were made from
  the S3 one by sed; check each library's connector and handler constructors), run the fast tests,
  Measure-CodeQuality for the four libraries, try the repro with the built curl.exe, tick the criteria.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: da503814 5bc744de a0b49a29.
- 2026-11-11: Doing -> Blocked. Stewart: the .NET SDK 10.0.4xx that global.json pins (10.0.401) is not installed (only 10.0.100-rc.1 and 11.0.100), so nothing builds; please reinstall it.
