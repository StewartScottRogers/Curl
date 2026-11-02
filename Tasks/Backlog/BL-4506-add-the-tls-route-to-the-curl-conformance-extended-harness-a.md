---
id: BL-4506
title: Add the TLS route to the Curl.Conformance.Extended harness and fix its hanging rediss test
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-4505]
touches: [Curl.Conformance.Extended.UnitLibrary/Harness, Curl.Conformance.Extended.UnitTests/Harness]
requirement: none
created: 2026-11-02
completed:
---
# BL-4506 — Add the TLS route to the Curl.Conformance.Extended harness and fix its hanging rediss test

## Goal

The Extended conformance harness has a TLS route: a rediss:// run with -k completes against the in-memory TLS reference server and a test asserts exit code, stdout, stderr and plaintext wire bytes.

## Context

Split from BL-4501 (see BL-4501 and BL-4462). The stashed rediss test (RunAsync_RedisPingOverTls_...) hangs or crashes the test host. Suspect ReferenceTcpDialer.ServeAsync's TLS branch (no ALPN in TlsServerOptions; a failed accept is swallowed by ReferenceRun.EndAsync). Reuse HttpsServerConnector and TlsServerStream from Curl.Conformance.UnitLibrary.

## Acceptance criteria

- [ ] A test in Curl.Conformance.Extended.UnitTests/Harness runs rediss:// -k through the TLS route and asserts exit code, stdout, stderr and the plaintext wire bytes recorded at the server.
- [ ] A failed TLS accept surfaces as a test failure instead of being swallowed, and the test finishes without hanging.
- [ ] No real socket, file outside a temporary directory, or process; platform-neutral; no test needs TestCategory=Integration.
- [ ] The build is clean with warnings as errors and the Extended fast tests are green.

## Notes

- Split from BL-4501.
- Continues the stashed work of BL-4501.
- Depends on BL-4505 for the projects and the TCP and UDP routes.
- BL-4505 shipped without the TLS route. Put back: ReferenceTransport.Tls; ReferenceRoute.Tls(port, server, X509Certificate2 certificate) setting a Certificate property; ReferenceCertificate.CreateSelfSigned(hostName) (RSA 2048 CertificateRequest with a DNS SAN, self-signed for a day, exported to PKCS#12 and reloaded with X509CertificateLoader.LoadPkcs12); and in ReferenceTcpDialer.ServeAsync, when route.Certificate is set, `TlsServerStream.AcceptAsync(serverEnd, new TlsServerOptions(route.Certificate, [], RequestClientCertificate: false), TlsServerStream.PlatformServesTls13, cancellationToken)` before serving the plaintext. The original files are in stash be1fae3dcc54b2ebfbdf9ec26b09e3cad51c5201 while it lasts.

## Log

- 2026-11-02: Created.
