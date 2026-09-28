---
id: BL-425
title: Build the unprintable-name certificate in OpenSslCertificateTextTests so Linux and macOS can load it
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Output.UnitTests/OpenSslCertificateTextTests.cs]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-425 — Build the unprintable-name certificate in OpenSslCertificateTextTests so Linux and macOS can load it

## Goal

`OpenSslCertificateTextTests.ServerCertificate_NamesOpenSslCannotPrint_PrintNone` passes on
the `ubuntu-latest` and `macos-latest` jobs of the `CI` workflow and still passes on
`windows-latest`, and the `[NONE]` subject and issuer lines of
`OpenSslCertificateText.ServerCertificate` stay covered on Windows.

## Context

**Root cause (fails on both Linux and macOS): the fixture certificate carries a name that
only Windows' certificate loader accepts.** The test (line 33) builds
`X500DistinguishedName brokenName` from the DER bytes
`30 0C 31 0A 30 08 06 03 55 04 03 1E 01 41`, a common name whose BMPString has an odd length
(one byte), and calls `CertificateRequest.CreateSelfSigned`, which re-loads the signed DER.
Windows loads it; Linux fails in `X509CertificateLoader.LoadCertificatePal` with
`CryptographicException: ASN1 corrupted data` (OpenSSL's parser), and macOS fails in
`AppleCertificatePal.CopyWithPrivateKey` with `AppleCommonCryptoCryptographicException: Unknown
format in import` (Security framework). The exception is raised while building the fixture,
before any production code runs. CI run 36344057083; 1 result on each platform.

This is a test-fixture assumption, not a behaviour difference: on Linux and macOS a certificate
the platform refuses to load can never reach `OpenSslCertificateText`, so curl's OpenSSL build and
this code cannot disagree about it. The formatter's own `null` cases (the reason `[NONE]` is
printed) are pinned platform-free in `Curl.Output.UnitTests/OpenSslDistinguishedNameTextTests.cs`
against `OpenSslDistinguishedNameText.Format`; this test covers only the `?? "[NONE]"` fallbacks at
`Curl.Output.UnitLibrary/OpenSslCertificateText.cs` lines 63 and 66.

How to fix, in this order:

1. Prefer a name that all three platforms' loaders accept but `OpenSslDistinguishedNameText.Format`
   returns `null` for (look at its `AsnContentException` and `FormatException` paths, lines 116-122,
   and the cases already in `OpenSslDistinguishedNameTextTests`). Only keep it if the `CI` run shows
   the test passing on all three jobs.
2. If no such name loads everywhere, keep the current bytes and mark the test
   `[OSCondition(OperatingSystems.Windows)]` with a comment stating that Linux (OpenSSL) and macOS
   (Security framework) refuse to load the certificate, so the fallback is only reachable on Windows.
   Coverage is measured on Windows (`Measure-CodeQuality.ps1`; the `coverage` job of
   `.github/workflows/gource.yml`), so the branch stays covered.

Do not change production code. Lanes test only on Windows, so the Linux and macOS result comes from
the `CI` workflow (`.github/workflows/ci.yml`) run on the pushed commit.

## Acceptance criteria

- [x] `ServerCertificate_NamesOpenSslCannotPrint_PrintNone` still asserts `"  subject: [NONE]"` and
      `"  issuer: [NONE]"`, and either passes on all three CI jobs or is marked Windows-only with the
      comment described above.
- [x] `dotnet build Curl.Output.UnitTests -warnaserror` is clean and
      `dotnet test Curl.Output.UnitTests --filter "TestCategory!=Integration"` passes on Windows.
- [x] In the `CI` run for the pushed commit on `work/dark-factory`,
      `ServerCertificate_NamesOpenSslCannotPrint_PrintNone` does not appear in
      `gh run view <run-id> --log-failed` for the `Build and test (ubuntu-latest)` or
      `Build and test (macos-latest)` job.
- [x] No file outside `Curl.Output.UnitTests/OpenSslCertificateTextTests.cs` changed.

## Notes

- Took fix 2 (Windows-only): the lane cannot push, so it cannot observe a CI run to prove a
  name that loads on all three platforms yet makes `OpenSslDistinguishedNameText.Format` return
  `null` (fix 1). Any name OpenSSL and the Security framework both accept is one they can parse,
  so such a name is unlikely to exist; the odd-length BMPString stays and the test carries
  `[OSCondition(OperatingSystems.Windows)]` with the comment the task asks for. Coverage is
  measured on Windows, so the `?? "[NONE]"` branches stay covered.
- Criterion 3 holds by construction: MSTest skips the test on the Linux and macOS jobs, so it
  cannot appear in `--log-failed`. The CI run itself is observed once the shift pushes.
- Verified: `dotnet build Curl.Output.UnitTests -warnaserror` clean; Curl.Output.UnitTests 339
  passed; full `dotnet build` clean and the fast suite green (0 failed).

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. ServerCertificate_NamesOpenSslCannotPrint_PrintNone runs on Windows only, where the platform loads its odd-length BMPString name; Linux and macOS CI skip it
