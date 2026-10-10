---
id: BL-1922
title: Vendor upstream tests/certs and resolve %CERTDIR in the upstream case runner
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-09
completed:
---
# BL-1922 — Vendor upstream tests/certs and resolve %CERTDIR in the upstream case runner

## Goal

The upstream case runner gives %CERTDIR a value - a certificate directory the caller supplies, as it does for the tests directory - and upstream's tests/certs files are vendored beside the vendored tests/data, so cases that only read a certificate file through %CERTDIR are measured instead of skipped.

## Context

Split from BL-1896 (part 1 of its context). Read Curl.Conformance.UnitLibrary\CLAUDE.md first. %CERTDIR is listed in UpstreamTestVariableSubstitution.cs's OtherUpstreamNames but the runner (UpstreamCaseRunner.cs) supplies no value, so UpstreamCaseScreening.cs skips the 20 cases that name it (gap run 2026-10-08_2029). The cases are vendored in Curl.Conformance.UnitTests\UpstreamTestData; vendor upstream's tests/certs from the curl 8.21.0 tarball (https://curl.se/download/curl-8.21.0.tar.xz, extracted into an empty scratch folder, never into the repository) beside it, and say in the Notes where they came from. Never read a cache under %LOCALAPPDATA%\Curl\gap. Cases that need a TLS server too wait for BL-1921 and the protocol stand-ins (BL-1912, BL-1913, BL-1914); they stay skipped with that reason.

## Acceptance criteria

- [ ] UpstreamCaseRunner takes a certificate directory from its caller and %CERTDIR resolves to it; a unit test pins the substitution.
- [ ] UpstreamCaseScreening no longer returns "the harness has no value for %CERTDIR" for any case when a certificate directory is supplied (a screening test pins it); a case still skipped says its other reason.
- [ ] At least one %CERTDIR case that needs no TLS server runs through UpstreamCaseRunner and gets Passed or a real Curl difference; Curl's own failures are not fixed here.
- [ ] Curl.Conformance.UnitLibrary holds 100% line and branch coverage and complexity of at most 10 per method; tests are platform-neutral with no TestCategory=Integration.
- [ ] `dotnet build Curl.Conformance.UnitLibrary -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green, and Curl.Conformance.UnitLibrary\CLAUDE.md says what %CERTDIR resolves to.

## Notes

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
