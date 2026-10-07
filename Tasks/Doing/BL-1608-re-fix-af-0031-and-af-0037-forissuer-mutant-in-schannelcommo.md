---
id: BL-1608
title: Re-fix AF-0031 and AF-0037 forIssuer mutant in SchannelCommonNameCheck with a CA-issued certificate test
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests]
requirement: none
created: 2026-10-07
completed:
---
# BL-1608 — Re-fix AF-0031 and AF-0037 forIssuer mutant in SchannelCommonNameCheck with a CA-issued certificate test

## Goal

A unit test in `Curl.Networking.UnitTests` fails when `forIssuer: false` in
`SchannelCommonNameCheck.CommonNameMatches` is changed to `forIssuer: true`, so the Schannel
build's `--cacert` common-name decision is pinned for a certificate whose issuer is not itself.

## Context

Re-fix of two accepted audit findings from the quality auditor, which are duplicates of each
other: AF-0031 (Medium; earlier task BL-1373) and AF-0037 (High; earlier task BL-1380). Both
reported that `certificate.GetNameInfo(..., forIssuer: false)` could become `forIssuer: true`
with no test failing. Lanes cannot read `Audit/`, so everything needed is here.

What BL-1373 and BL-1380 fixed: the site at `Curl.Networking.UnitLibrary/TlsFailureMessages.cs:320`
(`GetNameInfo(X509NameType.SimpleName, forIssuer: false)`). That mutant is now killed by
`OpenSslPeerFailedVerification_WithANameMismatchOnAnIssuedCertificate_NamesTheSubjectNotTheIssuer`
in `Curl.Networking.UnitTests/TlsFailureMessagesTests.cs`.

What the 2026-10-07 re-audit (scorecard 2026-10-07_0844, audited commit 5a627a2f) found still
open, for both findings: the second site,
`Curl.Networking.UnitLibrary/SchannelCommonNameCheck.cs:30`:

```csharp
var commonName = certificate.GetNameInfo(X509NameType.DnsName, forIssuer: false);
```

Changed by hand to `forIssuer: true`, the mutant survived: `Passed! - Failed: 0, Passed: 2996`
on the filtered Networking tests. Cause: every test in
`Curl.Networking.UnitTests/SchannelCommonNameCheckTests.cs` builds its certificate with the
private `CreateCertificate` helper, which calls `request.CreateSelfSigned(...)`, so subject and
issuer are the same name and the mutant gives the same answer. With a CA-issued certificate, the
mutant would match the issuer's name instead of the subject's. That would flip the `--cacert`
accept/refuse decision that `ServerCertificateVerification.WithoutNameMismatchSchannelAccepts`
(`Curl.Networking.UnitLibrary/ServerCertificateVerification.cs`, around line 259) makes from
`CommonNameMatches`. A wrongly refused name is a curl exit 60 (`CURLE_PEER_FAILED_VERIFICATION`),
and a wrongly accepted one is a transfer that should have failed.

How to write the tests (test-only change, no production edit expected):
- Reuse the pattern already in `TlsFailureMessagesTests.cs` (`CreateAuthority` and `CreateLeaf`,
  around lines 557-590): a self-signed `CN=Test Authority` CA with basic constraints and
  `KeyCertSign`, and a leaf created with `request.Create(authority, ...)`. Add an issued-certificate
  helper to `SchannelCommonNameCheckTests` (or a shared test helper in the same project). Do not
  edit `TlsFailureMessagesTests.cs` unless you are moving the helper.
- Test 1, for example `CommonNameMatches_WithAnIssuedCertificateWhoseSubjectNamesTheHost_IsTrue`:
  leaf `CN=localhost`, no subjectAltName, issued by `CN=Test Authority`, target `localhost`,
  expected `true`. Under the mutant the issuer name `Test Authority` is compared and the result
  is `false`.
- Test 2, for example `CommonNameMatches_WithAnIssuedCertificateWhoseIssuerNamesTheHost_IsFalse`:
  leaf `CN=other.example`, no subjectAltName, issued by a CA named `CN=localhost`, target
  `localhost`, expected `false`. Under the mutant the result is `true`. This covers the accept
  direction.
- Write ARRANGE/ACT/ASSERT diagnostics the way the file's existing tests do (`Diagnostics.Arrange`,
  `Act`, `Assert`).
- Keep the tests platform-neutral: `CommonNameMatches` is pure and uses no store or network.
  Use no `OSCondition` and no Windows-only certificate API, and do not mark the tests
  `TestCategory=Integration`.

A lane cannot run `Audit/Tools/Invoke-MutationTest.ps1`. Apply the mutant by hand instead, the
same way BL-1373 did: edit line 30 to `forIssuer: true`, run the new tests and see them fail,
restore the line, and record the result in Notes. The findings close only when a reliable
re-audit by the quality auditor confirms the fix, not when this task reaches Done.

## Acceptance criteria

- [ ] `Curl.Networking.UnitTests/SchannelCommonNameCheckTests.cs` has at least the two tests
      described above. Each uses a leaf issued by a separate CA (built with
      `CertificateRequest.Create(issuer, ...)`, not `CreateSelfSigned`), and the CA's name differs
      from the leaf's name.
- [ ] With `Curl.Networking.UnitLibrary/SchannelCommonNameCheck.cs:30` edited by hand to
      `forIssuer: true`, at least one new test fails. With the line restored, all of them pass.
      Notes record both runs (the test names and their pass/fail results).
- [ ] `git diff` shows no change under `Curl.Networking.UnitLibrary`.
- [ ] `dotnet build Curl.Networking.UnitTests -warnaserror` is clean.
- [ ] `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"` reports
      Failed: 0.
- [ ] `SchannelCommonNameCheck.cs` keeps 100% line and branch coverage
      (`powershell -NoProfile -File Measure-CodeQuality.ps1`).

## Notes

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
