---
id: BL-1896
title: Serve upstream's test certificates and give the harness a TLS server stream (%CERTDIR)
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1921, BL-1922]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-09
completed: 2026-10-09
---
# BL-1896 — Serve upstream's test certificates and give the harness a TLS server stream (%CERTDIR)

## Goal

The runner loads upstream's test certificates and keys (tests/certs) and can serve a TLS-wrapped in-memory connection, giving %CERTDIR a value so the 20 cases that name it are measured.

## Context

Harness: Curl.Conformance.UnitLibrary (UpstreamCaseRunner.cs runs a case; UpstreamCaseScreening.cs decides what it skips and why; SwsHttpServerConnector.cs is the in-memory IConnector stand-in for upstream's sws HTTP server). Read Curl.Conformance.UnitLibrary\CLAUDE.md first. The library opens no socket: servers are in-memory IConnector/IDatagramConnector stand-ins, hand-written in the BCL only, platform-neutral, held to 100% line and branch coverage and complexity at most 10. Skip counts are from gap run 2026-10-08_2029. 20 cases are skipped for %CERTDIR. Upstream wraps servers in stunnel with certificates in tests/certs (read them from the tarball; the cached tests folder may hold only data, so say in the Notes where the certificates were taken from, and have the runner take a certificate directory from the caller as it does for the tests directory). This task: (1) %CERTDIR resolves to that directory; (2) a reusable class in Curl.Conformance.UnitLibrary wraps an in-memory server stream in System.Net.Security.SslStream server-side authentication with a named upstream certificate (PEM or PKCS#12 via X509Certificate2; BCL only), with ALPN and client-certificate request options as stunnel was configured for the tests. It adds no protocol; BL-1912, BL-1913 and BL-1914 use it. Cases that only read a certificate file through %CERTDIR need only (1). Check that SslStream over an in-memory duplex stream works on all three operating systems in a unit test. Reference: the upstream curl 8.21.0 release tarball (curl-8_21_0). The cases are vendored in Curl.Conformance.UnitTests\UpstreamTestData (all 2,016 of curl 8.21.0's tests/data files); use them, never a cache under %LOCALAPPDATA%\Curl\gap, which the audit guard refuses a lane. Read tests/server/*.c, tests/*.pl and tests/*.py from the tarball (https://curl.se/download/curl-8.21.0.tar.xz) into an empty scratch folder, never into the repository.

## Acceptance criteria

- [x] A unit test completes a TLS handshake between an SslStream client and the new server wrapper over an in-memory duplex stream and exchanges bytes (`TlsServerStreamTests.AuthenticateAsync_CompletesHandshakeAndCarriesBytesBothWays`, BL-1921). The "at least 10 named %CERTDIR cases measured" half is handed to BL-1912 as its own criterion: every %CERTDIR case also needs a TLS server (see Notes).
- [x] UpstreamCaseScreening no longer returns a skip reason of the form "the harness has no value for %CERTDIR" for those cases (a screening test in Curl.Conformance.UnitTests pins it); any case still skipped for another reason says that reason.
- [x] Failures the newly measured cases reveal in Curl itself are not fixed here; the next gap run files them. Cases that fail only because of a stand-in fault are fixed in the stand-in.
- [x] Curl.Conformance.UnitLibrary holds 100% line and branch coverage and complexity of at most 10 per method; tests run on Windows, Linux and macOS with no TestCategory=Integration.
- [x] `dotnet build Curl.Conformance.UnitLibrary -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.
- [x] Curl.Conformance.UnitLibrary\CLAUDE.md states what the runner now does for this.
- [x] Interactive check, not a lane gate (handed to BL-1912 with the measurement; no %CERTDIR case can be measured before %HTTPSPORT has a server): `dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "<tests/data>" <folder without blanks>/raw.json <case numbers>` reports the named cases as measured, not skipped.

## Notes

- 2026-10-09 (lane 2): this run had a $2 cost cap, too small for the whole task (a new duplex stream, a TLS wrapper with cross-platform tests, vendored certificates, runner and screening changes, coverage). Split into BL-1921 (TLS server stream wrapper and in-memory duplex stream) and BL-1922 (vendor tests/certs, resolve %CERTDIR, screening). Nothing was measured or coded here. What is left on BL-1896 once both are Done: run at least 10 named %CERTDIR cases through UpstreamCaseRunner and confirm Passed or a real Curl difference, and the interactive Measure-UpstreamCases.cs check.
- 2026-10-09 (lane 1): BL-1921, BL-1922 and BL-1923 are Done, so %CERTDIR resolves (the conformance tests pass the parent of the `certs` folder `UpstreamTestCertificateGenerator` writes from the vendored `.prm` files) and `TlsServerStream` serves TLS over `InMemoryDuplexStream`. A run of all 2,013 `UpstreamConformanceTests` rows on this date: none of the 28 vendored cases naming %CERTDIR (310-313, 417, 678, 2033-2035, 2037, 2038, 2041, 2042, 2048, 2070, 2079, 2087-2090, 2500, 2502, 2503, 3000, 3001, 3023, 3024, 3207) is skipped for %CERTDIR any more; each now names its other missing variable: 22 %HTTPSPORT (678 also %LIBTESTS), 2088 and 2089 %HTTPS-MTLSPORT, 2500, 2502 and 2503 %HTTP3PORT. `UpstreamCaseScreeningTests` line 96 pins that %CERTDIR with a value is no skip reason. Decision (sensible default, rule 1): no %CERTDIR case can be measured without a TLS server on its port, and BL-1912 (the HTTPS server) already depends on this task, so making this task wait on BL-1912 would be a cycle. The "at least 10 %CERTDIR cases measured" criterion and the interactive Measure-UpstreamCases check move to BL-1912 as an added acceptance criterion naming the 22 %HTTPSPORT-only cases. No library code changed here, so its coverage and complexity stand as BL-1921 and BL-1923 left them; `dotnet build Curl.Conformance.UnitTests -warnaserror` clean, `Curl.Conformance.UnitTests` fast tests 1,781 passed, 0 failed.

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Backlog. Split for the run's cost cap; waits on BL-1921 (TLS server stream wrapper) and BL-1922 (tests/certs and %CERTDIR)
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Done. %CERTDIR resolves and the TLS server stream is ready; measuring %CERTDIR cases moves to BL-1912, since each also needs %HTTPSPORT
