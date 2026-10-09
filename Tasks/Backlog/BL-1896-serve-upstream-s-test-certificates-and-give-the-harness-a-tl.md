---
id: BL-1896
title: Serve upstream's test certificates and give the harness a TLS server stream (%CERTDIR)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-09
completed:
---
# BL-1896 — Serve upstream's test certificates and give the harness a TLS server stream (%CERTDIR)

## Goal

The runner loads upstream's test certificates and keys (tests/certs) and can serve a TLS-wrapped in-memory connection, giving %CERTDIR a value so the 20 cases that name it are measured.

## Context

Harness: Curl.Conformance.UnitLibrary (UpstreamCaseRunner.cs runs a case; UpstreamCaseScreening.cs decides what it skips and why; SwsHttpServerConnector.cs is the in-memory IConnector stand-in for upstream's sws HTTP server). Read Curl.Conformance.UnitLibrary\CLAUDE.md first. The library opens no socket: servers are in-memory IConnector/IDatagramConnector stand-ins, hand-written in the BCL only, platform-neutral, held to 100% line and branch coverage and complexity at most 10. Skip counts are from gap run 2026-10-08_2029. 20 cases are skipped for %CERTDIR. Upstream wraps servers in stunnel with certificates in tests/certs (read them from the tarball; the cached tests folder may hold only data, so say in the Notes where the certificates were taken from, and have the runner take a certificate directory from the caller as it does for the tests directory). This task: (1) %CERTDIR resolves to that directory; (2) a reusable class in Curl.Conformance.UnitLibrary wraps an in-memory server stream in System.Net.Security.SslStream server-side authentication with a named upstream certificate (PEM or PKCS#12 via X509Certificate2; BCL only), with ALPN and client-certificate request options as stunnel was configured for the tests. It adds no protocol; BL-1912, BL-1913 and BL-1914 use it. Cases that only read a certificate file through %CERTDIR need only (1). Check that SslStream over an in-memory duplex stream works on all three operating systems in a unit test. Reference: the upstream curl 8.21.0 release tarball (curl-8_21_0). The cases are vendored in Curl.Conformance.UnitTests\UpstreamTestData (all 2,016 of curl 8.21.0's tests/data files); use them, never a cache under %LOCALAPPDATA%\Curl\gap, which the audit guard refuses a lane. Read tests/server/*.c, tests/*.pl and tests/*.py from the tarball (https://curl.se/download/curl-8.21.0.tar.xz) into an empty scratch folder, never into the repository.

## Acceptance criteria

- [ ] A unit test completes a TLS handshake between an SslStream client and the new server wrapper over an in-memory duplex stream and exchanges bytes, and at least 10 named %CERTDIR upstream cases run through UpstreamCaseRunner and get Passed or a real Curl difference.
- [ ] UpstreamCaseScreening no longer returns a skip reason of the form "the harness has no value for %CERTDIR" for those cases (a screening test in Curl.Conformance.UnitTests pins it); any case still skipped for another reason says that reason.
- [ ] Failures the newly measured cases reveal in Curl itself are not fixed here; the next gap run files them. Cases that fail only because of a stand-in fault are fixed in the stand-in.
- [ ] Curl.Conformance.UnitLibrary holds 100% line and branch coverage and complexity of at most 10 per method; tests run on Windows, Linux and macOS with no TestCategory=Integration.
- [ ] `dotnet build Curl.Conformance.UnitLibrary -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.
- [ ] Curl.Conformance.UnitLibrary\CLAUDE.md states what the runner now does for this.
- [ ] Interactive check, not a lane gate: `dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "<tests/data>" <folder without blanks>/raw.json <case numbers>` reports the named cases as measured, not skipped.

## Notes

## Log

- 2026-10-09: Created.
