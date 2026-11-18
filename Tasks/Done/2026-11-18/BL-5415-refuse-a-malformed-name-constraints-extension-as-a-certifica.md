---
id: BL-5415
title: Refuse a malformed Name Constraints extension as a certificate error, not exit 35
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitLibrary/CertificateNameConstraints.cs, Curl.Networking.UnitTests]
requirement: none
created: 2026-11-17
completed: 2026-11-17
---
# BL-5415 — Refuse a malformed Name Constraints extension as a certificate error, not exit 35

## Goal

A `--cacert` handshake whose chain carries a malformed Name Constraints extension fails as a certificate trust error (exit 60, as curl does), not as a generic exit 35 connect error.

## Context

Found by the shift-end review of 2026-11-17 (ADR-0710; silent-failure-hunter and type-design-analyzer both reported it, code-reviewer noted it below its bar).

- `Curl.Networking.UnitLibrary/CertificateNameConstraints.cs:75` reads each authority's Name Constraints extension with `new AsnReader(extension.RawData, AsnEncodingRules.DER).ReadSequence()`; `ReadSubtrees` (`:88` onward) calls `PeekTag`/`ReadOctetString`. Nothing catches `AsnContentException` (which does not derive from `CryptographicException`).
- It is called from `ServerCertificateVerification.cs:530` (`ConstraintViolationOf`, on any chain error once `--cacert` is given) and `:559` (last check in `CaCertificateFileAnchorsChain`), added by BL-5375 (commit 12efd51c2f).
- Intermediates come from the server, so the server controls those bytes. Bad DER (or BER that is not strict DER) throws inside the validation callback; the TLS providers' catch-alls (`SslStreamTlsProvider.cs` ~488, `HandBuiltTlsProvider.cs` ~725) turn it into exit 35 with a generic `SslConnectError` text. Before BL-5375 the extension was never read, so such a chain passed; real curl gives a trust error (Schannel) or exit 60.

## Acceptance criteria

- [x] `CertificateNameConstraints.FindViolation` catches `AsnContentException` (and `CryptographicException`) while decoding a Name Constraints extension or a leaf's names, and reports the chain as refused rather than throwing.
- [x] A transfer with `--cacert` whose chain carries a malformed Name Constraints extension ends with exit 60 and curl's certificate-problem error text, not exit 35.
- [x] A unit test in `Curl.Networking.UnitTests` builds a CA with a malformed Name Constraints extension and pins the refusal; it passes on Windows, Linux and macOS.
- [x] `dotnet build` is clean and the fast tests pass; `Curl.Networking.UnitLibrary` keeps 100% line and branch coverage.

## Notes

- Fix: `FindViolation(X509Chain)` catches `AsnContentException` and `CryptographicException` and returns `NotPermitted` (refused). Both providers then give exit 60 (Schannel build: `schannel: error 0x00004000`; OpenSSL build: the chain error stays, so the existing exit 60 path). Tests are platform-neutral. Quality run: 0 failing members.

## Log

- 2026-11-17: Created.
- 2026-11-17: Backlog -> Doing. Criteria at claim: 4189673e fe7e4ddd a07774e8 6b1ca54f.
- 2026-11-17: Doing -> Done. Malformed Name Constraints refused as exit 60
