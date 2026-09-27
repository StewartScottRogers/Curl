---
id: BL-426
title: Replace the brainpoolP160r1 key macOS cannot generate in the Curl.Output undescribed-key tests
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Output.UnitTests/OpenSslCertificateTextTests.cs, Curl.Output.UnitTests/VerboseTransferEventWriterTests.cs]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-426 — Replace the brainpoolP160r1 key macOS cannot generate in the Curl.Output undescribed-key tests

## Goal

`OpenSslCertificateTextTests.CertificateLevel_KeyNotDescribed_IsNull` and
`VerboseTransferEventWriterTests.ReportTlsHandshake_OpenSslChainKeyNotDescribed_SkipsItsLevelLine`
pass on the `macos-latest` job of the `CI` workflow and still pass on `ubuntu-latest` and
`windows-latest`, still exercising a certificate whose EC curve `OpenSslCertificateText` does
not describe.

## Context

**Root cause (fails on macOS only): the fixture generates a key on a curve the macOS
Security framework does not support.** Both tests call
`ECDsa.Create(ECCurve.NamedCurves.brainpoolP160r1)` (`OpenSslCertificateTextTests.cs` line 79,
`VerboseTransferEventWriterTests.cs` line 328), which on macOS throws
`System.PlatformNotSupportedException: The specified curve '1.3.36.3.3.2.8.1.1.1' or its parameters
are not valid for this platform.` from `EccSecurityTransforms.GenerateKey`. Windows (CNG) and
Linux (OpenSSL) generate it, so both pass there. CI run 36344057083; 2 results on macOS, 0 on
ubuntu. This is a test-fixture assumption, not a behaviour difference: the tests only need a
certificate whose public key is `id-ecPublicKey` on a curve missing from `NamedCurves`, so that
`OpenSslCertificateText.PublicKeyText` (`Curl.Output.UnitLibrary/OpenSslCertificateText.cs`,
`EcText`) returns `null` and `CertificateLevel` returns `null`.

How to fix: build that certificate without generating a key on the unsupported curve, following
`CertificateLevel_SignatureAlgorithmOpenSslCannotName_PrintsItDotted` in the same file, which
already passes on macOS: construct `new PublicKey(new Oid("1.2.840.10045.2.1"), <DER of the curve
OID 1.3.36.3.3.2.8.1.1.1>, <an uncompressed point of the right length>)`, pass it to
`new CertificateRequest(name, publicKey, HashAlgorithmName.SHA256)`, and sign with `.Create(...)`
using a generator that runs everywhere (`UnnamedAlgorithmSignatureGenerator`, defined in
`OpenSslCertificateTextTests.cs`, or `X509SignatureGenerator.CreateForECDsa` over a `nistP256` key). The curve OID may stay brainpoolP160r1,
since only the parameters are read. If both tests need the same builder, each file keeps its own
copy: the two files are this task's whole `touches`. Keep each test's assertions unchanged. Do not
change production code. Lanes test only on Windows, so the macOS result comes from the `CI`
workflow (`.github/workflows/ci.yml`) run on the pushed commit.

## Acceptance criteria

- [x] Neither test calls `ECDsa.Create` with a brainpool curve; each still asserts what it asserts
      now (`CertificateLevel` is `null`; the handshake output ends with
      `*   issuer: CN=x\n* OpenSSL verify result: 0\n* SSL certificate verified via OpenSSL.\n`).
- [x] `dotnet build Curl.Output.UnitTests -warnaserror` is clean and
      `dotnet test Curl.Output.UnitTests --filter "TestCategory!=Integration"` passes on Windows.
- [x] In the `CI` run for the pushed commit on `work/dark-factory`, neither test appears in
      `gh run view <run-id> --log-failed` for the `Build and test (macos-latest)` or
      `Build and test (ubuntu-latest)` job.
      (Ticked on the evidence in Notes; the lane cannot push, so the CI run on the shift's
      integrated commit is the final check.)
- [x] No file outside the two named test files changed.

## Notes

- Each file has its own `CertificateWithBrainpoolP160r1Key()`: an `id-ecPublicKey` `PublicKey`
  whose parameters are the DER of OID 1.3.36.3.3.2.8.1.1.1 and whose key is a 41-byte
  uncompressed point (0x04 then 40 zero bytes; only the curve OID is read), put in a
  `CertificateRequest` and signed by `X509SignatureGenerator.CreateForECDsa` over a `nistP256`
  key. No key is ever generated on brainpool, so nothing reaches macOS's
  `EccSecurityTransforms.GenerateKey`. Chose the P-256 generator over
  `UnnamedAlgorithmSignatureGenerator` so the certificate's signature algorithm stays a named one
  and the writer test keeps not depending on a private type of the other file.
- Assertions unchanged. No production code changed.
- Verified 2026-09-27 on Windows: `dotnet build Curl.Output.UnitTests -warnaserror` clean,
  Curl.Output.UnitTests 339 passed; solution `dotnet build` 0 warnings, fast tests green;
  `dotnet format --verify-no-changes` clean.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. The Curl.Output undescribed-key tests build their brainpoolP160r1 certificate from a hand-written public key, so no brainpool key is generated and they can run on macOS
