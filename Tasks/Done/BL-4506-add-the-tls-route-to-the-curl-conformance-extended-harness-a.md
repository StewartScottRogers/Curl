---
id: BL-4506
title: Add the TLS route to the Curl.Conformance.Extended harness and fix its hanging rediss test
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-4505]
touches: [Curl.Conformance.Extended.UnitLibrary/Harness, Curl.Conformance.Extended.UnitTests/Harness, Curl.Conformance.Extended.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4506 — Add the TLS route to the Curl.Conformance.Extended harness and fix its hanging rediss test

## Goal

The Extended conformance harness has a TLS route: a rediss:// run with -k completes against the in-memory TLS reference server and a test asserts exit code, stdout, stderr and plaintext wire bytes.

## Context

Split from BL-4501 (see BL-4501 and BL-4462). The stashed rediss test (RunAsync_RedisPingOverTls_...) hangs or crashes the test host. Suspect ReferenceTcpDialer.ServeAsync's TLS branch (no ALPN in TlsServerOptions; a failed accept is swallowed by ReferenceRun.EndAsync). Reuse HttpsServerConnector and TlsServerStream from Curl.Conformance.UnitLibrary.

## Acceptance criteria

- [x] A test in Curl.Conformance.Extended.UnitTests/Harness runs rediss:// -k through the TLS route and asserts exit code, stdout, stderr and the plaintext wire bytes recorded at the server.
- [x] A failed TLS accept surfaces as a test failure instead of being swallowed, and the test finishes without hanging.
- [x] No real socket, file outside a temporary directory, or process; platform-neutral; no test needs TestCategory=Integration.
- [x] The build is clean with warnings as errors and the Extended fast tests are green.

## Notes

- Split from BL-4501.
- Continues the stashed work of BL-4501.
- Depends on BL-4505 for the projects and the TCP and UDP routes.
- BL-4505 shipped without the TLS route. Put back: ReferenceTransport.Tls; ReferenceRoute.Tls(port, server, X509Certificate2 certificate) setting a Certificate property; ReferenceCertificate.CreateSelfSigned(hostName) (RSA 2048 CertificateRequest with a DNS SAN, self-signed for a day, exported to PKCS#12 and reloaded with X509CertificateLoader.LoadPkcs12); and in ReferenceTcpDialer.ServeAsync, when route.Certificate is set, `TlsServerStream.AcceptAsync(serverEnd, new TlsServerOptions(route.Certificate, [], RequestClientCertificate: false), TlsServerStream.PlatformServesTls13, cancellationToken)` before serving the plaintext. The original files are in stash be1fae3dcc54b2ebfbdf9ec26b09e3cad51c5201 while it lasts.
- 2026-11-02 (lane 1): Restored ReferenceTransport.Tls, ReferenceRoute.Tls/Certificate and ReferenceCertificate from the stash; ReferenceTcpDialer.ServeAsync now yields first (as Curl.Conformance's TlsServerConnection does) and runs TlsServerStream.AcceptAsync with no ALPN before serving the plaintext. The rediss test passes in about a second with no hang (--blame-hang-timeout 90s).
- Decision (sensible default): ReferenceRun.EndAsync rethrows the first faulted server's exception (ExceptionDispatchInfo) instead of swallowing it; a server still waiting when curl ends is cancelled, not faulted, so it stays silent. RunAsync_PlaintextToATlsRoute_FailsTheRunWithTheFailedAccept pins the AuthenticationException from AcceptAsync. No close_notify is sent after the script: a shutdown write after curl has closed would fault the run.
- Added Curl.Conformance.Extended.UnitLibrary/CLAUDE.md to touches to keep its description of the routes true (no other Doing task names it).
- Platform: the certificate is made in memory (PKCS#12 reloaded), the failure is SslStream's managed frame check, so the tests are platform-neutral; CI on Linux and macOS is left to the shift's CI watch.
- Measure-CodeQuality.ps1 -Library Curl.Conformance.Extended.UnitLibrary: 100% line, 100% branch, 0 failing members, worst CRAP 8. Extended fast tests 14/14; solution build 0 warnings.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 7f0f0d65 33ed76fe d9a98f17 b6235544.
- 2026-11-02: Doing -> Done. The Extended harness has a TLS route: rediss:// -k runs against TlsServerStream and records the plaintext; a failed TLS accept now fails the run
