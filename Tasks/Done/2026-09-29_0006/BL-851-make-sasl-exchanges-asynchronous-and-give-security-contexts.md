---
id: BL-851
title: Make SASL exchanges asynchronous and give security contexts GSS Wrap and Unwrap
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests, Curl.Protocol.Imap.UnitLibrary, Curl.Protocol.Imap.UnitTests, Curl.Protocol.Pop3.UnitLibrary, Curl.Protocol.Pop3.UnitTests, Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests, Curl.Console.UnitTests, Documentation/Planning/Decisions/ADR-0183-sasl-exchanges-are-awaited-and-security-contexts-wrap-and-unwrap.md]
requirement: none
created: 2026-09-29
completed: 2026-09-29
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

- [x] `ISaslExchange` exposes an awaitable initial response and an awaitable answer taking a `CancellationToken`, and the SMTP, IMAP and POP3 handlers await them; the existing SMTP, IMAP and POP3 SASL tests pass unchanged in what they assert.
- [x] A completed `ISecurityContext` can wrap and unwrap a message on the system route (NegotiateAuthentication) and the hand-built Kerberos route (`KerberosGssContext`), each pinned by a test in `Curl.Authentication.UnitTests` (the hand-built one against `FakeGssAcceptor`); NTLM and incomplete contexts refuse with a documented status or exception.
- [x] An ADR records the async SASL contract and the message-protection seam, marked "Decided by Claude under Stewart's delegation".
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for every touched library.

## Notes

- Plan (ADR-0183): `ISaslExchange` became `GetInitialResponseAsync` / `RespondAsync`, each taking a `CancellationToken`; SMTP, IMAP and POP3 await the initial response once, before the AUTH command, with the transfer's token, so no wire byte changes. `ISecurityContext` grew `Wrap`/`Unwrap` members (not an optional interface, which the Sspi and Fallback decorators would hide); refusals: `InvalidOperationException` before completion, `NotSupportedException` from curl's own NTLM, `null` for a message that does not verify.
- Learned: SSPI's NTLM negotiates no signing key unless the client asks for protection, so wrapped messages fail to verify. Added `SecurityContextRequest.MessageProtection` (default `None`, which keeps HTTP's NTLM/Negotiate tokens unchanged); BL-538 decides by measurement what SASL GSSAPI asks for. With `EncryptAndSign` negotiated, SSPI's NTLM seals even a sign-only wrap, so the test does not pin the encrypted flag on that route.
- Added the ADR-0183 file to `touches`: the acceptance criteria require an ADR, and no task in Doing names it.
- Measured: `Measure-CodeQuality.ps1` reports 100% line and branch and 0 failing members for Curl.Authentication, Curl.Protocol.Abstractions, Smtp, Imap and Pop3.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. SASL exchanges are awaited (GetInitialResponseAsync/RespondAsync) and established security contexts wrap and unwrap on the system and hand-built Kerberos routes (ADR-0183)
