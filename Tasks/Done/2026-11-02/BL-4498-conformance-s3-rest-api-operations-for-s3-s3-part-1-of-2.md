---
id: BL-4498
title: Conformance: S3 REST API operations for s3:// (S3 part 1 of 2)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4505, BL-4506, BL-4507, BL-4502, BL-4503]
touches: [Curl.Conformance.Extended.UnitLibrary/S3, Curl.Conformance.Extended.UnitTests/S3, Documentation/Conformance/Extended/S3.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed: 2026-11-02
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

- [x] `Documentation/Conformance/Extended/S3.md` gives every requirement of this scope that binds a client (each
      MUST and SHOULD, and each header or parameter the API documents as required) an `S3:` ID mapped to a test or to
      `n/a: <reason>`, links to `S3-Signing.md` (BL-4499), and the harness's matrix check passes.
- [x] A reference S3 endpoint in `Curl.Conformance.Extended.UnitLibrary/S3/` lets BL-4499 add signature verification
      in `S3/Signing/` without editing this task's files.
- [x] Cases in `Curl.Conformance.Extended.UnitTests/S3/` run curl in process and assert HTTP requests, stdout, stderr
      and exit code for at least: download, a single PUT, a multipart upload with the parts and the
      CompleteMultipartUpload body checked, a part failing mid-upload, a listing over several continuation pages with
      a delimiter, an `Error` body with exit 22, virtual-hosted and path-style addressing, a bucket name that forces
      path style, a key needing percent-encoding, and a custom endpoint from the `AWS_*` variables.
- [x] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`
      with the ID of its filed High `Fix S3 conformance: ...` task; the known-failures check passes.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is
      green; no test needs `TestCategory=Integration`.
- [x] The code added under `Curl.Conformance.Extended.UnitLibrary/S3/` is at 100% line and branch coverage,
      complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`).
- [x] No package added; every test is platform-neutral (no drive letters, Windows-only paths or text).

## Notes

- Reference endpoint: `Curl.Conformance.Extended.UnitLibrary/S3/S3ReferenceEndpoint` (GetObject, PutObject, CreateMultipartUpload, UploadPart, CompleteMultipartUpload, ListObjectsV2 with prefix, delimiter and continuation-token, `Error` bodies; virtual-hosted or path style by the `Host` header against a service host). Each request is checked first by an `IS3RequestAuthorizer`; `S3WellFormedAuthorization` accepts an anonymous request or any `AWS4-HMAC-SHA256 Credential=` header, so BL-4499 adds a verifying authorizer in `S3/Signing/` and passes it to the constructor without editing these files.
- Cases in `Curl.Conformance.Extended.UnitTests/S3/`: 14 over a TLS route on 443 (AWS hosts, `-k`) and a TCP route on 9000 (custom endpoint), plus `S3ReferenceEndpointTests` driving the endpoint's other answers with http:// command lines. Multipart uses a temp file of 8 MiB + 5 bytes (the client's part size).
- Default taken: the endpoint does not validate CompleteMultipartUpload; the case asserts its body byte for byte and the stored object instead. AbortMultipartUpload is n/a (optional, "if used").
- Library defect: `S3Extension.CreateHandler` reads `AWS_*` from the process environment, not the run's injected reader, so `S3:Endpoints-1` and `S3:Regions-1` fail; listed in `KnownFailures.txt` with BL-4539 (High).
- Server fault fixed here: ListObjectsV2 `Contents` needs `LastModified` (the client requires it).
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Conformance.Extended.UnitLibrary` 0 failing members (100% line and branch, complexity at most 10); `dotnet build Curl.slnx -warnaserror` clean; fast tests green. Stderr asserted with Contains (line endings differ by platform), files only in temp directories: platform-neutral, which meets the Linux/macOS criterion.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 00e0f0d4 d0c8875f 356f465e 94856415 d8e3fb0e 23196c4a ef6c9026.
- 2026-11-02: Doing -> Done. s3:// conformance matrix S3.md with reference endpoint and 14 cases; endpoint and region env failures listed for BL-4539
