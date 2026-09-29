---
id: BL-880
title: Answer a post-handshake CertificateRequest in the hand-built TLS 1.3 client
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Tls.UnitLibrary, Curl.Tls.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-880 — Answer a post-handshake CertificateRequest in the hand-built TLS 1.3 client

## Goal

With `post_handshake_auth` in `Tls13ClientSettings.ExtensionOrder`, the hand-built TLS 1.3 client sends the empty extension and answers a `CertificateRequest` the server sends after the handshake (RFC 8446 section 4.6.2) with Certificate, CertificateVerify (when it has a certificate) and Finished, so offering the extension, as both `ClientHelloProfile.Schannel` and `ClientHelloProfile.OpenSsl` do, is honest.

## Context

- Both measured profiles (BL-787, ADR-0140) list `post_handshake_auth` (49). `Tls13ClientStream` does not handle a post-handshake `CertificateRequest` today, so BL-820 cannot send the profiles' extension order until this lands.
- RFC 8446 section 4.6.2: the request's `certificate_request_context` is non-empty and echoed; a client that did not offer the extension treats one as `unexpected_message`. Section 4.4: the authenticator's transcript is the handshake transcript plus the CertificateRequest.
- Code: `Curl.Tls.UnitLibrary/Tls13ClientStream.cs`, `Tls13ClientHelloBuilder.cs`, `PostHandshakeAuthExtension.cs`.

## Acceptance criteria

- [ ] `Curl.Tls.UnitTests`: a test server that sends a post-handshake `CertificateRequest` receives Certificate, CertificateVerify and Finished it can verify when a client certificate is set, and an empty Certificate and Finished when none is; application data flows before and after.
- [ ] Without `post_handshake_auth` offered, the same request fails with `unexpected_message`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Tls.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Filed by BL-820.

## Log

- 2026-09-29: Created.
