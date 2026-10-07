---
id: BL-1236
title: Pin the TLS 1.3 client-authentication handshake to RFC 8448 section 6's trace
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Tls.UnitLibrary, Curl.Tls.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1236 — Pin the TLS 1.3 client-authentication handshake to RFC 8448 section 6's trace

## Goal

`Tls13ClientHandshake`, given RFC 8448 section 6's client random, key share and client certificate, sends that trace's ClientHello, accepts its server flight (with the `CertificateRequest`), and answers with the trace's client `Certificate`, `CertificateVerify` and `Finished` byte for byte, installing the trace's handshake and application traffic secrets.

## Context

- Today `Curl.Tls.UnitTests` pins RFC 8448 sections 3 and 4 end to end (`Rfc8448ClientHandshakeTests.cs`, `Rfc8448KeyScheduleTests.cs`, `Rfc8448RecordTests.cs`) and section 5's HelloRetryRequest handshake (`HelloRetryRequestHandshakeSendsBothTraceClientHellosAndTheTraceFinished`), but section 6 only through `Rfc8448HandshakeMessageTests.DecodeReadsTheClientAuthenticationCertificateRequest`: no test drives a client that answers a `CertificateRequest` against a published trace. `Tls13ClientHandshake` sends a client certificate through `TlsClientCertificate` (`TlsClientCertificate.cs`) and `SendClientCertificate` (`Tls13ClientHandshake.cs`, around line 983).
- RFC 8448 section 6, "Client Authentication" (https://www.rfc-editor.org/rfc/rfc8448#section-6): the client and server messages, the client's RSA key and certificate, and every secret, key and IV of the handshake. The client's `CertificateVerify` is RSA-PSS (`rsa_pss_rsae_sha256`) with a random salt, so it cannot be reproduced by signing; drive it with a test `TlsSigningKey` that returns the trace's signature for the trace's content (`TlsSigningKey`'s abstract members are `private protected` and no test derives from it yet; `Curl.Tls.UnitLibrary.csproj` grants `InternalsVisibleTo` to the test project, and if a test class still cannot override them, add the smallest internal seam that lets a test supply the signature, named for what it does), and separately check that `RsaTlsSigningKey` with the trace's private key produces a signature the trace's public key verifies over the same content.
- Store the new messages beside the others in `Rfc8448Messages.cs`, named for section 6 as the existing ones are for sections 3 to 5, copied from the RFC text.
- This is a pinning task: the code is expected to pass. If a byte differs, fix the handshake so it matches the trace and say what was wrong in `Notes`.

## Acceptance criteria

- [x] A test in `Curl.Tls.UnitTests` drives the section 6 client through the trace: its ClientHello equals the trace's; after the server flight it sends the trace's client `Certificate`, `CertificateVerify` and `Finished` in that order at the handshake level; the installed handshake and application traffic secrets equal the trace's; `ClientCertificateSent` is true.
- [x] A test signs the trace's `CertificateVerify` content with `RsaTlsSigningKey` built from the section 6 client key and verifies the result with the client certificate's public key (`System.Security.Cryptography.RSA`, PSS, SHA-256). Met with section 2's RSA key and section 3's certificate instead: the RFC does not publish section 6's client key (see Notes).
- [x] The trace's messages are cited with "RFC 8448 section 6" in the test comments; every other TLS test passes unchanged.
- [x] `dotnet build Curl.slnx -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes on every platform with no test needing `TestCategory=Integration`; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Tls.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Pinned in `Curl.Tls.UnitTests/Rfc8448ClientAuthenticationHandshakeTests.cs`; section 6's ClientHello, server flight and client flight added to `Rfc8448Messages.cs` as `ClientAuthentication*`, copied from the RFC text (every length checked against the octet count the RFC prints).
- The handshake passed on the first run: ClientHello, client Certificate, CertificateVerify and Finished, the handshake and application traffic secrets, the exporter and the resumption master secrets all equal the trace. No production change.
- No seam was needed: a private test class in the friend assembly can override `TlsSigningKey`'s `private protected` members. `TraceSignatureSigningKey` fits only RSA-PSS on an `rsaEncryption` key, returns the trace's signature and records the hash and content it was asked to sign; a second test verifies the trace's signature under the client certificate's public key over that content, which proves the client built the trace's CertificateVerify content.
- Choice (sensible default): RFC 8448 section 6 says "private keys for the certificates used in this example are not shown", so the second criterion cannot use the section 6 client key. Instead `RsaTlsSigningKey` built from section 2's published RSA key signs the section 6 CertificateVerify content with `rsa_pss_rsae_sha256`, and section 3's server certificate (which carries that key) verifies it with `System.Security.Cryptography.RSA`, PSS, SHA-256.
- Gates: `dotnet build Curl.slnx -warnaserror` clean; fast tests green (Curl.Tls.UnitTests 1283 passed); `Measure-CodeQuality.ps1 -Library Curl.Tls.UnitLibrary` reports 100% line, 100% branch, 0 failing members.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. Tls13ClientHandshake is pinned to RFC 8448 section 6's client-authentication trace byte for byte
