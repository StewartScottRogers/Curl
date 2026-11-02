---
id: BL-4498
title: Conformance: S3 REST API operations for s3:// (S3 part 1 of 2)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4462]
touches: [Curl.Conformance.Extended.UnitLibrary/S3, Curl.Conformance.Extended.UnitTests/S3, Documentation/Conformance/Extended/S3.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4498 — Conformance: S3 REST API operations for s3:// (S3 part 1 of 2)

## Goal

Curl's `s3://` client is measured against the Amazon S3 REST API's object operations, multipart upload, listing,
error responses and endpoint addressing through the real curl command line, every client requirement it implements
is in a matrix mapped to a test, and every failure is fixed or listed with its fix task.

## Context

Specification: Amazon S3 API Reference (docs.aws.amazon.com/AmazonS3/latest/API): GetObject, PutObject,
CreateMultipartUpload, UploadPart, CompleteMultipartUpload (parts in ascending order, ETags), AbortMultipartUpload if
used, ListObjectsV2 (`list-type=2`, `prefix`, `delimiter`, `continuation-token`, `IsTruncated`), "Error responses"
(the `Error` body and codes), and the user guide's virtual-hosted-style and path-style requests and Regions. SigV4
signing is part 2 (BL-4499); here the reference endpoint accepts any well-formed `Authorization` header and checks
only the operation.

Library: `Curl.Extended.Protocol.S3.UnitLibrary` (`CLAUDE.md`: `Client/S3ProtocolHandler`, `S3Configuration`
(`--user` and the `AWS_*` variables), `S3Endpoint`, `S3Client`, `S3Operations`, `Xml/`, `Http/S3HttpClientConnection`;
ADR-1013, ADR-1018, ADR-1090). `-T` uploads (multipart above the library's threshold), an empty key or one ending in
`/` lists, anything else downloads; a status of 300 or more is exit 22. Give every case its environment through the
run's injected environment reader, never the process's.

Part of the extended-protocol conformance plan Stewart approved on 2026-11-02. Follow
`Documentation/Conformance/Extended/README.md` and the ADR BL-4462 records: the harness, `[SpecRequirement]` IDs
(`S3:<source>-<locator>`), the matrix format and the `KnownFailures.txt` rule. Measure against the specification,
never against upstream curl. The reference endpoint implements the API from the specification itself and references
no `Curl.Extended.Protocol.*` project. One file per case group; nothing added to `Harness/`. This task touches only
its own conformance files so protocols run in parallel: a fault in its own server or cases is fixed here, but a
defect in the extension library is listed in `KnownFailures.txt` with a filed High task
`Fix S3 conformance: <requirement>` (pipeline feature; touches the library, its `.UnitTests` and
`KnownFailures.txt`; criteria: the case passes, a focused unit test in the library's `.UnitTests` pins the fix
(ADR-0482), the `KnownFailures.txt` line is removed).

## Acceptance criteria

- [ ] `Documentation/Conformance/Extended/S3.md` gives every requirement of this scope that binds a client (each
      MUST and SHOULD, and each header or parameter the API documents as required) an `S3:` ID mapped to a test or to
      `n/a: <reason>`, links to `S3-Signing.md` (BL-4499), and the harness's matrix check passes.
- [ ] A reference S3 endpoint in `Curl.Conformance.Extended.UnitLibrary/S3/` lets BL-4499 add signature verification
      in `S3/Signing/` without editing this task's files.
- [ ] Cases in `Curl.Conformance.Extended.UnitTests/S3/` run curl in process and assert HTTP requests, stdout, stderr
      and exit code for at least: download, a single PUT, a multipart upload with the parts and the
      CompleteMultipartUpload body checked, a part failing mid-upload, a listing over several continuation pages with
      a delimiter, an `Error` body with exit 22, virtual-hosted and path-style addressing, a bucket name that forces
      path style, a key needing percent-encoding, and a custom endpoint from the `AWS_*` variables.
- [ ] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`
      with the ID of its filed High `Fix S3 conformance: ...` task; the known-failures check passes.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is
      green; no test needs `TestCategory=Integration`.
- [ ] The code added under `Curl.Conformance.Extended.UnitLibrary/S3/` is at 100% line and branch coverage,
      complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`).
- [ ] No package added; every test is platform-neutral (no drive letters, Windows-only paths or text).

## Notes

## Log

- 2026-11-02: Created.
