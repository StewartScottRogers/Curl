---
id: BL-4475
title: Conformance: Google Cloud Storage XML API operations for gs:// (GoogleCloudStorage part 1 of 2)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4505, BL-4506, BL-4507, BL-4502, BL-4503]
touches: [Curl.Conformance.Extended.UnitLibrary/GoogleCloudStorage, Curl.Conformance.Extended.UnitTests/GoogleCloudStorage, Documentation/Conformance/Extended/GoogleCloudStorage.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4475 — Conformance: Google Cloud Storage XML API operations for gs:// (GoogleCloudStorage part 1 of 2)

## Goal

Curl's `gs://` client is measured against the Cloud Storage XML API's object operations through the real curl command
line, every client requirement it implements is in a matrix mapped to a test, and every failure is fixed or listed
with its fix task.

## Context

Specification: Google Cloud Storage XML API (cloud.google.com/storage/docs/xml-api): GET Object, PUT Object, List
Objects (`list-type=2`, `delimiter`, `prefix`, `continuation-token`, `ListBucketResult`), "XML API error responses",
the required request headers, path-style requests to `storage.googleapis.com`, OAuth 2.0 bearer authorization, and
the `STORAGE_EMULATOR_HOST` endpoint override. V4 HMAC signing is part 2 (BL-4476); here use bearer and anonymous
requests.

Library: `Curl.Extended.Protocol.GoogleCloudStorage.UnitLibrary` (`CLAUDE.md`: `Client/GcsProtocolHandler`,
`GcsEndpoint`, `GcsOperations`, `GcsClient`, `Xml/`; ADR-1099).

Part of the extended-protocol conformance plan Stewart approved on 2026-11-02. Follow
`Documentation/Conformance/Extended/README.md` and the ADR BL-4462 records: the harness, `[SpecRequirement]` IDs
(`GoogleCloudStorage:<source>-<locator>`), the matrix format and the `KnownFailures.txt` rule. Measure against the
specification, never against upstream curl. The reference server implements the API from the specification itself
and references no `Curl.Extended.Protocol.*` project. One file per case group; nothing added to `Harness/`. This task
touches only its own conformance files so protocols run in parallel: a fault in its own server or cases is fixed
here, but a defect in the extension library is listed in `KnownFailures.txt` with a filed High task
`Fix GoogleCloudStorage conformance: <requirement>` (pipeline feature; touches the library, its `.UnitTests` and
`KnownFailures.txt`; criteria: the case passes, a focused unit test in the library's `.UnitTests` pins the fix
(ADR-0482), the `KnownFailures.txt` line is removed).

## Acceptance criteria

- [x] `Documentation/Conformance/Extended/GoogleCloudStorage.md` gives every requirement of this scope that binds a
      client (each MUST, SHOULD, and each header or parameter the API documents as required) a `GoogleCloudStorage:`
      ID mapped to a test or to `n/a: <reason>`, links to `GoogleCloudStorage-Signing.md` (BL-4476), and the
      harness's matrix check passes.
- [x] A reference XML API endpoint in `Curl.Conformance.Extended.UnitLibrary/GoogleCloudStorage/` lets BL-4476 add
      signature checking in `GoogleCloudStorage/Signing/` without editing this task's files.
- [x] Cases in `Curl.Conformance.Extended.UnitTests/GoogleCloudStorage/` run curl in process and assert wire bytes,
      stdout, stderr and exit code for at least: download, upload with `-T`, a listing with `delimiter` over several
      pages (continuation token and `NextMarker`), an object name needing percent-encoding, an `Error` body with exit
      22, bearer from `--oauth2-bearer`, anonymous, and the emulator host from `STORAGE_EMULATOR_HOST`.
- [x] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`
      with the ID of its filed High `Fix GoogleCloudStorage conformance: ...` task; the known-failures check passes.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is
      green; no test needs `TestCategory=Integration`.
- [x] The code added under `Curl.Conformance.Extended.UnitLibrary/GoogleCloudStorage/` is at 100% line and branch
      coverage, complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`).
- [x] No package added; every test is platform-neutral (no drive letters, Windows-only paths or text).

## Notes
- Reference endpoint: `GcsReferenceServer` (GET/PUT Object, List Objects with prefix, delimiter and paging) behind an injected `IGcsReferenceAuthorizer` (anonymous, bearer); BL-4476 adds `Signing/` with its own authorizer (ADR-1189).
- 10 cases in `GcsXmlApiObjectCases` and `GcsXmlApiListCases`, plus `GcsReferenceServerTests` for the endpoint's own refusals. Matrix: `Documentation/Conformance/Extended/GoogleCloudStorage.md`, 15 rows; `Date` is n/a (signed requests only, part 2).
- Known failure: `XMLAPI-Emulator-1`, the extension reads `STORAGE_EMULATOR_HOST` from the process environment, not the run's (exit 7); filed High BL-4534 and listed in KnownFailures.txt.
- Curl's error line ends in CRLF on Windows; the cases pin `Environment.NewLine`, so each platform checks its own line end. The upload temp file is created in `Path.GetTempPath()` and deleted; no drive letters. CI on Linux and macOS: platform-neutral by that, fast tests green here.
- Measure-CodeQuality.ps1 for Curl.Conformance.Extended.UnitLibrary: 0 failing members (ParseHead and List split to stay at complexity 10 or less).


## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: faf392a4 b75f5d9f 920f3cad cec18b67 d8e3fb0e 1e94fe0f ef6c9026.
- 2026-11-02: Doing -> Done. gs:// measured against the XML API object operations: reference endpoint, 10 cases, matrix; emulator host listed with BL-4534
