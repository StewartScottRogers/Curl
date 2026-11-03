---
id: BL-4499
title: Conformance: AWS Signature Version 4 signing for s3:// (S3 part 2 of 2)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4505, BL-4506, BL-4507, BL-4502, BL-4503, BL-4498]
touches: [Curl.Conformance.Extended.UnitLibrary/S3/Signing, Curl.Conformance.Extended.UnitTests/S3/Signing, Documentation/Conformance/Extended/S3-Signing.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4499 — Conformance: AWS Signature Version 4 signing for s3:// (S3 part 2 of 2)

## Goal

Curl's `s3://` request signing is measured against AWS Signature Version 4 as the Amazon S3 API reference specifies
it, a reference endpoint verifying each signature independently, through the real curl command line; every signer
requirement is in a matrix mapped to a test, and every failure is fixed or listed with its fix task.

## Context

Specification: Amazon S3 API Reference, "Authenticating Requests (AWS Signature Version 4)" and its "Signature
Calculations for the Authorization Header: Transferring Payload in a Single Chunk" page (canonical request: method,
canonical URI with S3's single encoding, sorted canonical query string, lower-case canonical headers, signed headers,
`x-amz-content-sha256` hashed payload or `UNSIGNED-PAYLOAD`; string to sign with `AWS4-HMAC-SHA256`, `x-amz-date` and
the `date/region/s3/aws4_request` scope; the derived signing key; the `Authorization` header), plus the session
token header (`x-amz-security-token`) if the library sends one. Use the page's published example requests and
signatures as fixed vectors; the run's `TimeProvider` fixes the date. Builds on the reference endpoint BL-4498 wrote
in `Curl.Conformance.Extended.UnitLibrary/S3/`; add a verifier in `S3/Signing/` written from the documentation (BCL
HMACSHA256 and SHA256 only), never from the client's `Signing/` code.

Library: `Curl.Extended.Protocol.S3.UnitLibrary` (`Signing/S3RequestSigner`, `S3CanonicalRequest`, `S3UriEncoder`,
`S3Credentials`, `S3SigningRequest`). Credentials from `--user` and the `AWS_*` variables (`Client/S3Configuration`),
given through the run's injected environment reader.

Part of the extended-protocol conformance plan Stewart approved on 2026-11-02. Follow
`Documentation/Conformance/Extended/README.md` and the ADR BL-4462 records: the harness, `[SpecRequirement]` IDs
(`S3:SigV4-<section>-<n>`), the matrix format and the `KnownFailures.txt` rule. One file per case group; nothing added
to `Harness/`. This task touches only its own conformance files so protocols run in parallel: a fault in its own
server or cases is fixed here, but a defect in the extension library is listed in `KnownFailures.txt` with a filed
High task `Fix S3 conformance: <requirement>` (pipeline feature; touches the library, its `.UnitTests` and
`KnownFailures.txt`; criteria: the case passes, a focused unit test in the library's `.UnitTests` pins the fix
(ADR-0482), the `KnownFailures.txt` line is removed).

## Acceptance criteria

- [x] `Documentation/Conformance/Extended/S3-Signing.md` gives every requirement of SigV4 header signing that binds a
      signer an `S3:` ID mapped to a test or to `n/a: <reason>` (query-string presigning and chunked signing are
      `n/a` unless the library uses them); the harness's matrix check passes.
- [x] Cases in `Curl.Conformance.Extended.UnitTests/S3/Signing/` run curl in process and assert the signed headers,
      stdout, stderr and exit code for at least: the documentation's GET, PUT and listing examples reproduced byte for
      byte, a key with reserved, space and non-ASCII characters, several query parameters including empty values,
      `UNSIGNED-PAYLOAD` where the library uses it, a session token, another region, and the endpoint's refusal (exit
      22) of a request whose signature does not verify.
- [x] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`
      with the ID of its filed High `Fix S3 conformance: ...` task; the known-failures check passes.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is
      green; no test needs `TestCategory=Integration`.
- [x] The code added under `Curl.Conformance.Extended.UnitLibrary/S3/Signing/` is at 100% line and branch coverage,
      complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`).
- [x] No package added; every test is platform-neutral (no drive letters, Windows-only paths or text).

## Notes

- Verifier: `S3/Signing/S3SigV4ReferenceAuthorizer` and `S3SigV4CanonicalRequest`, written from the SigV4 single-chunk page with BCL HMACSHA256/SHA256 only, plugged into `S3ReferenceEndpoint` through `IS3RequestAuthorizer`. `S3SigV4ReferenceAuthorizerTests` reproduces the published GET Object, PUT Object and ListObjects signatures byte for byte, plus every fault path.
- Cases (`S3SigV4SigningCases`, 7): run at the examples' time 2013-05-24 with their access key; GET test.txt, PUT test$file.text (the example's payload hash), listing (three parameters, empty `prefix=`), reserved/space/non-ASCII key, session token from AWS_* variables, eu-west-1, wrong secret refused 403 SignatureDoesNotMatch exit 22. Each asserts the Authorization header byte for byte against the verifier. All pass; nothing added to KnownFailures.txt.
- Decision (ADR-1211): the client cannot send the examples' exact requests (region host, no Range/Date/storage-class, ListObjectsV2), so the examples are reproduced byte for byte by the verifier tests and the curl cases send the nearest request. The library never sends UNSIGNED-PAYLOAD, presigned URLs or chunked signing: those rows are n/a.
- Measured: Measure-CodeQuality.ps1 -Library Curl.Conformance.Extended.UnitLibrary: 0 failing members, 830/830 tests. Solution build -warnaserror clean, fast tests green. Tests are platform-neutral (temp directory via Path.Combine, no OS text).

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 5081addd b8b2e0a0 94856415 d8e3fb0e 3a7c1703 ef6c9026.
- 2026-11-02: Doing -> Done. S3 SigV4 header signing is verified by a documentation-written verifier through the curl command line, with a full requirement matrix
