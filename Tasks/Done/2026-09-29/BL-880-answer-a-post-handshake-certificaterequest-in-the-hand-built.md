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
completed: 2026-09-29
---
# BL-880 — Answer a post-handshake CertificateRequest in the hand-built TLS 1.3 client

## Goal

With `post_handshake_auth` in `Tls13ClientSettings.ExtensionOrder`, the hand-built TLS 1.3 client sends the empty extension and answers a `CertificateRequest` the server sends after the handshake (RFC 8446 section 4.6.2) with Certificate, CertificateVerify (when it has a certificate) and Finished, so offering the extension, as both `ClientHelloProfile.Schannel` and `ClientHelloProfile.OpenSsl` do, is honest.

## Context

- Both measured profiles (BL-787, ADR-0140) list `post_handshake_auth` (49). `Tls13ClientStream` does not handle a post-handshake `CertificateRequest` today, so BL-820 cannot send the profiles' extension order until this lands.
- RFC 8446 section 4.6.2: the request's `certificate_request_context` is non-empty and echoed; a client that did not offer the extension treats one as `unexpected_message`. Section 4.4: the authenticator's transcript is the handshake transcript plus the CertificateRequest.
- Code: `Curl.Tls.UnitLibrary/Tls13ClientStream.cs`, `Tls13ClientHelloBuilder.cs`, `PostHandshakeAuthExtension.cs`.

## Acceptance criteria

- [x] `Curl.Tls.UnitTests`: a test server that sends a post-handshake `CertificateRequest` receives Certificate, CertificateVerify and Finished it can verify when a client certificate is set, and an empty Certificate and Finished when none is; application data flows before and after.
- [x] Without `post_handshake_auth` offered, the same request fails with `unexpected_message`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Tls.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Filed by BL-820.
- Plan: `Tls13ClientHelloBuilder` builds the empty `post_handshake_auth` when it is in
  `ExtensionOrder`. In `Connected`, `Tls13ClientHandshake` expects a CertificateRequest only
  once the extension was offered (otherwise `unexpected_message`, as before). It answers at
  the Application level with Certificate (context echoed), CertificateVerify when a
  certificate fits, and Finished keyed from `client_application_traffic_secret_N`. All of
  it is computed over `TranscriptHash.Clone()` of the handshake transcript plus the
  request, so the next request starts from the same transcript. The handshake now owns the
  client application secret: `Tls13RecordLayer.SendKeyUpdateAsync` takes N+1 from
  `AdvanceClientApplicationTrafficSecret()`, so a Finished after a client KeyUpdate is
  keyed correctly.
- Decision: a post-handshake request with an empty context is `illegal_parameter`. RFC
  8446 section 4.3.2 reserves the empty context for the in-handshake request. The client
  does not check that contexts are unique, because the RFC puts that duty on the server.
- Decision: `Tls13RecordLayer.AnswerCertificateRequestAsync` answers under the write lock,
  so a KeyUpdate cannot move the write keys between keying the Finished and sending it.
  Only a CertificateRequest takes the lock. A NewSessionTicket stays on the read path
  without the lock, so it cannot stall behind a held-up write (code review).
- `ClientCertificateRequested` and `ClientCertificateSent` now also cover a request after
  the handshake.
- No ADR: these choices follow directly from RFC 8446, not from curl behaviour.
  `Documentation/Planning/Decisions` is in BL-564's `touches`, so the choices are recorded
  here and in `Curl.Tls.UnitLibrary/CLAUDE.md`.
- Tests: `Tls13PostHandshakeAuthenticationTests` has 7 tests: with and without a
  certificate, with application data before and after the request, two requests, after a
  client KeyUpdate, the extension not offered, an empty context, and a missing
  `signature_algorithms`. `Tls13ClientHandshakeTests.APostHandshakeRequestIsAnsweredAtTheApplicationLevelWithNoNewSecrets`
  covers the I/O-free path with SHA-384. `Curl.Tls.UnitTests`: 823 passed.
  `Measure-CodeQuality.ps1 -Library Curl.Tls.UnitLibrary`: 100% line, 100% branch, 0
  failing members.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. The hand-built TLS 1.3 client offers post_handshake_auth from ExtensionOrder and answers a post-handshake CertificateRequest with Certificate, CertificateVerify and Finished
