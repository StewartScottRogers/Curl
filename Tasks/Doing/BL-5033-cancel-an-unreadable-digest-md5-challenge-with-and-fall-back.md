---
id: BL-5033
title: Cancel an unreadable DIGEST-MD5 challenge with * and fall back to the next SASL mechanism
priority: Low
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Authentication.UnitLibrary/SaslAuthenticator.cs, Curl.Original.Protocol.Smtp.UnitLibrary/SmtpSaslAuthentication.cs, Curl.Authentication.UnitTests/SaslDigestMd5Tests.cs, Curl.Original.Protocol.Smtp.UnitTests/SmtpProtocolHandlerSaslCancelTests.cs]
requirement: none
created: 2026-11-12
completed:
---
# BL-5033 — Cancel an unreadable DIGEST-MD5 challenge with * and fall back to the next SASL mechanism

## Goal

When an SMTP server's `AUTH DIGEST-MD5` challenge is unreadable, curl-as-Curl sends `*` to cancel and falls back to the next advertised mechanism instead of aborting with exit 94.

## Context

From the outside fitness review of 2026-11-12 (commit 2d2ad7f8b, `preview` curl-win-x64.zip; REF = curl 8.22.0 Schannel, REF2 = curl 8.18.0 OpenSSL), finding STD-037 (Low, Confirmed).

Area: SMTP SASL mechanism fallback.

Repro:
1. Run an SMTP mock on 127.0.0.1:45018. It advertises `250-AUTH DIGEST-MD5 NTLM` in the EHLO reply and answers `AUTH DIGEST-MD5` with `334 ` (an empty challenge).
2. `curl -sS smtp://127.0.0.1:45018/x -u user:secret --mail-from a@b --mail-rcpt c@d -T m.txt`

Expected (REF): REF sends `*` to cancel, then `AUTH NTLM` with a Type-1 message. Against this mock it ends with exit 67 (`Login denied`).

Actual (PORT): exit 94, `curl: (94) An authentication function returned an error`. It closes after `AUTH DIGEST-MD5`, with no `*` cancel, no fallback and no QUIT.

Where to start: `Curl.Authentication.UnitLibrary/SaslAuthenticator.cs` (`DigestMd5Answer`, `DigestMd5Failed`, about lines 165-217), together with the cancel path in `Curl.Original.Protocol.Smtp.UnitLibrary/SmtpSaslAuthentication.cs`. Note `SmtpProtocolHandlerSaslCancelTests.cs` already tests a cancel path; read it first to reuse its stand-in.

## Acceptance criteria

- [ ] A unit test in `Curl.Original.Protocol.Smtp.UnitTests/SmtpProtocolHandlerSaslCancelTests.cs` (server advertising `AUTH DIGEST-MD5 NTLM`, replying `334 ` to `AUTH DIGEST-MD5`) shows the client sends, in order, `AUTH DIGEST-MD5`, `*`, then `AUTH NTLM` with an NTLMSSP Type-1 message.
- [ ] The same test ends with `CurlExitCode` 67 and message `Login denied` when the server then refuses NTLM, and the client sends QUIT as it does for other login failures.
- [ ] A unit test in `Curl.Authentication.UnitTests/SaslDigestMd5Tests.cs` shows an unreadable DIGEST-MD5 challenge is reported as a cancel/unusable-mechanism outcome (not an authentication-function error) so callers can fall back.
- [ ] Regression: a readable DIGEST-MD5 challenge still completes, and DIGEST-MD5 as the only mechanism with an unreadable challenge still fails with the exit code measured from curl for that case.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, including what curl does when DIGEST-MD5 is the only mechanism. If POP3 and IMAP share the same defect, do not widen this task; file follow-ups.

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing. Criteria at claim: 0deefb6f da2c2878 8d134e0b 84bd9464 a0b49a29.
