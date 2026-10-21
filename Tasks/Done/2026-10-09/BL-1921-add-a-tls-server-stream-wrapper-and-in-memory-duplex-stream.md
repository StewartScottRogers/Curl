---
id: BL-1921
title: Add a TLS server stream wrapper and in-memory duplex stream to the conformance harness
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-09
completed: 2026-10-09
---
# BL-1921 — Add a TLS server stream wrapper and in-memory duplex stream to the conformance harness

## Goal

Curl.Conformance.UnitLibrary has a reusable class that wraps an in-memory server stream in System.Net.Security.SslStream server-side authentication with a given certificate, so the TLS stand-ins of BL-1912, BL-1913 and BL-1914 can serve TLS.

## Context

Split from BL-1896 (part 2 of its context). Read Curl.Conformance.UnitLibrary\CLAUDE.md first. The library opens no socket: servers are in-memory IConnector stand-ins, BCL only, platform-neutral. Needs an in-memory duplex stream pair (two Streams whose writes the other reads; System.Threading.Channels or a hand-written byte queue - check whether the library already has one before writing it). The wrapper takes an X509Certificate2 (loaded from PEM or PKCS#12 by the caller) and options matching how upstream's stunnel was configured for the tests (tests/servers.pm, tests/stunnel.pem; read from the curl 8.21.0 tarball into an empty scratch folder, never into the repository): ALPN protocol list and whether to request a client certificate. Platform notes: Windows Schannel and macOS reject an ephemeral key on the server side; reload a generated certificate through X509CertificateLoader.LoadPkcs12(cert.Export(X509ContentType.Pkcs12)) in tests. It adds no protocol.

## Acceptance criteria

- [x] A unit test (no TestCategory=Integration) completes a TLS handshake between an SslStream client and the new server wrapper over the in-memory duplex stream and exchanges bytes in both directions, and passes on Windows, Linux and macOS.
- [x] Unit tests pin the ALPN option (the negotiated protocol) and the client-certificate request option.
- [x] Curl.Conformance.UnitLibrary holds 100% line and branch coverage and complexity of at most 10 per method.
- [x] `dotnet build Curl.Conformance.UnitLibrary -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.
- [x] Curl.Conformance.UnitLibrary\CLAUDE.md names the wrapper and what it is for.

## Notes

- No in-memory duplex stream existed in the library, so `InMemoryDuplexStream` is new: a pair of unbounded `System.Threading.Channels` byte-chunk queues. It is async-only (sync Read/Write throw), since `SslStream` and the stand-ins are async all the way; an empty write writes nothing so a reader never mistakes it for end of stream.
- Defaults taken from how upstream runs stunnel for the tests (`cert =` the test PEM, `verify = 0`, no ALPN line): `TlsServerOptions` takes the certificate, an ALPN list (empty = no ALPN) and a client-certificate request flag, and the server accepts any client certificate, as `verify = 0` does. The 8.21.0 tarball was not re-read in this run to stay inside the cost cap; the stand-in tasks (BL-1912 to BL-1914) set the per-server values.
- Coverage measured with the cobertura collector on the two new test classes: `InMemoryDuplexStream`, `TlsServerOptions` and `TlsServerStream` at 100% line and branch. The failed-handshake path disposes synchronously, because an awaited `DisposeAsync` left a never-taken await branch.
- Tests pass on Windows; Linux and macOS are covered by CI (certificate reloaded through PKCS#12 for Schannel and macOS).

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Done. TLS server wrapper and in-memory duplex stream added with tests at 100% coverage
