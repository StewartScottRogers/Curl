---
id: BL-4476
title: Conformance: Google Cloud Storage V4 HMAC signing for gs:// (GoogleCloudStorage part 2 of 2)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4505, BL-4506, BL-4507, BL-4502, BL-4503, BL-4475]
touches: [Curl.Conformance.Extended.UnitLibrary/GoogleCloudStorage/Signing, Curl.Conformance.Extended.UnitTests/GoogleCloudStorage/Signing, Documentation/Conformance/Extended/GoogleCloudStorage-Signing.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4476 — Conformance: Google Cloud Storage V4 HMAC signing for gs:// (GoogleCloudStorage part 2 of 2)

## Goal

Curl's `gs://` HMAC-key signing is measured against Cloud Storage's "V4 signing process" through the real curl
command line, a reference endpoint verifying each signature independently, every client requirement is in a matrix
mapped to a test, and every failure is fixed or listed with its fix task.

## Context

Specification: Google Cloud Storage "V4 signing process" with HMAC keys, `Authorization` header form
(cloud.google.com/storage/docs/authentication/signatures): `GOOG4-HMAC-SHA256`, the canonical request (method,
canonical URI and query percent-encoding, canonical and signed headers, payload hash), the string to sign, the
credential scope `date/region/storage/goog4_request` (region `auto` by default), `x-goog-date` and
`x-goog-content-sha256`. Builds on the reference endpoint BL-4475 wrote in
`Curl.Conformance.Extended.UnitLibrary/GoogleCloudStorage/`; add a signature verifier in `GoogleCloudStorage/Signing/`
written from the documentation (BCL HMACSHA256 and SHA256 only), never from the client's `Signing/` code. Use the
documentation's published example values as fixed vectors where it gives them; the run's `TimeProvider` fixes the
date.

Library: `Curl.Extended.Protocol.GoogleCloudStorage.UnitLibrary` (`Signing/GcsRequestSigner`, `GcsCanonicalRequest`,
`GcsUriEncoder`, `GcsHmacKey`). Credentials: `--user ACCESS_ID:SECRET`.

Part of the extended-protocol conformance plan Stewart approved on 2026-11-02. Follow
`Documentation/Conformance/Extended/README.md` and the ADR BL-4462 records: the harness, `[SpecRequirement]` IDs
(`GoogleCloudStorage:<source>-<locator>`), the matrix format and the `KnownFailures.txt` rule. Measure against the
specification, never against upstream curl. One file per case group; nothing added to `Harness/`. This task touches
only its own conformance files so protocols run in parallel: a fault in its own server or cases is fixed here, but a
defect in the extension library is listed in `KnownFailures.txt` with a filed High task
`Fix GoogleCloudStorage conformance: <requirement>` (pipeline feature; touches the library, its `.UnitTests` and
`KnownFailures.txt`; criteria: the case passes, a focused unit test in the library's `.UnitTests` pins the fix
(ADR-0482), the `KnownFailures.txt` line is removed).

## Acceptance criteria

- [ ] `Documentation/Conformance/Extended/GoogleCloudStorage-Signing.md` gives every requirement of the V4 signing
      process that binds a signer a `GoogleCloudStorage:` ID mapped to a test or to `n/a: <reason>`; the harness's
      matrix check passes.
- [ ] Cases in `Curl.Conformance.Extended.UnitTests/GoogleCloudStorage/Signing/` run curl in process with `--user`
      and assert the signed headers, stdout, stderr and exit code for at least: GET, PUT with a body hash, a listing
      with several query parameters in canonical order, an object name with reserved and non-ASCII characters, a
      region other than `auto`, and the reference endpoint's refusal (exit 22) when the signature does not verify.
- [ ] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`
      with the ID of its filed High `Fix GoogleCloudStorage conformance: ...` task; the known-failures check passes.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is
      green; no test needs `TestCategory=Integration`.
- [ ] The code added under `Curl.Conformance.Extended.UnitLibrary/GoogleCloudStorage/Signing/` is at 100% line and
      branch coverage, complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`).
- [ ] No package added; every test is platform-neutral (no drive letters, Windows-only paths or text).

## Notes

## Log

- 2026-11-02: Created.
