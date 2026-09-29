---
id: BL-851
title: Make SASL exchanges asynchronous and give security contexts GSS Wrap and Unwrap
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests, Curl.Protocol.Imap.UnitLibrary, Curl.Protocol.Imap.UnitTests, Curl.Protocol.Pop3.UnitLibrary, Curl.Protocol.Pop3.UnitTests, Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests, Curl.Console.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-851 — Make SASL exchanges asynchronous and give security contexts GSS Wrap and Unwrap

## Goal

`ISaslAuthenticator` and `ISaslExchange` let an exchange await its initial response and each answer, and an `ISecurityContext` can wrap and unwrap GSS-API messages once complete, so BL-538 can answer SASL NTLM and GSSAPI through the NTLM and Negotiate seam with no behaviour change for the mechanisms built today.

## Context

- Found by BL-538 (2026-09-29). `ISaslExchange.InitialResponse` and `Respond` are synchronous, but `ISecurityContext.NextTokenAsync` is asynchronous (ADR-0176) because the hand-built Kerberos route asks a KDC for the service ticket on its first step. SASL GSSAPI's initial response is that first token, so it cannot be computed synchronously; `.Result` and `.Wait()` are forbidden (root `CLAUDE.md`).
- RFC 4752 section 3.1: once the GSSAPI context completes, the server sends a wrapped 4-byte security-layer offer and the client answers with a wrapped 4-byte choice (curl: no security layer, maximum size 0, plus the authorization identity). `ISecurityContext` has no Wrap/Unwrap. `Curl.Kerberos`'s `KerberosGssContext` has `Wrap`/`Unwrap` (BL-691, ADR-0171); on Windows `NegotiateAuthentication.Wrap`/`Unwrap` gives SSPI's.
- Callers of the SASL contract: `SmtpSaslAuthentication.cs`, `ImapAuthentication.cs`, `Pop3Login.cs`, their test fakes (`Fakes/FakeSaslAuthenticator.cs`, `Fakes/ScriptedSaslAuthenticator.cs`), `Curl.Authentication`'s `SaslAuthenticator`, `ScriptedSaslExchange`, `ChallengeSaslExchange`, and `Curl.Console.UnitTests/TransferContextFactoryMailTests.cs` (calls `Begin` and reads `InitialResponse`).
- Implementers of `ISecurityContext`: `SystemSecurityContext`, `SspiNegotiateSecurityContext`, `FallbackSecurityContext`, `HandBuiltKerberosSecurityContext`, `HandBuiltNtlmSecurityContext`, the test `ScriptedSecurityContext`, and fakes in `Curl.Console.UnitTests/CurlCompositionNegotiateTests.cs` and `CurlCompositionNtlmTests.cs`. A separate optional interface (e.g. `ISecurityMessageProtection`) or a new member the fakes implement are both acceptable; record the choice in an ADR.

## Acceptance criteria

- [ ] `ISaslExchange` exposes an awaitable initial response and an awaitable answer taking a `CancellationToken`, and the SMTP, IMAP and POP3 handlers await them; the existing SMTP, IMAP and POP3 SASL tests pass unchanged in what they assert.
- [ ] A completed `ISecurityContext` can wrap and unwrap a message on the system route (NegotiateAuthentication) and the hand-built Kerberos route (`KerberosGssContext`), each pinned by a test in `Curl.Authentication.UnitTests` (the hand-built one against `FakeGssAcceptor`); NTLM and incomplete contexts refuse with a documented status or exception.
- [ ] An ADR records the async SASL contract and the message-protection seam, marked "Decided by Claude under Stewart's delegation".
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for every touched library.

## Notes

## Log

- 2026-09-29: Created.
