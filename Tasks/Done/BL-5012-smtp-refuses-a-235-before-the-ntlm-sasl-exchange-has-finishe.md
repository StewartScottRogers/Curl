---
id: BL-5012
title: SMTP refuses a 235 before the NTLM SASL exchange has finished with exit 67
priority: High
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Original.Protocol.Smtp.UnitLibrary/SmtpSaslAuthentication.cs, Curl.Original.Protocol.Smtp.UnitTests/SmtpProtocolHandlerSaslAuthErrorTests.cs, Curl.Original.Protocol.Smtp.UnitTests/SmtpProtocolHandlerAuthenticationTests.cs]
requirement: none
created: 2026-11-12
completed: 2026-11-12
---
# BL-5012 — SMTP refuses a 235 before the NTLM SASL exchange has finished with exit 67

## Goal

SMTP `AUTH NTLM` treats a `235` sent before the NTLM exchange has finished as a failed login: exit 67, `Login denied`, and no further command is sent.

## Context

From the outside fitness review of 2026-11-12 (commit 2d2ad7f8b, `preview` curl-win-x64.zip; REF = curl 8.22.0 Schannel, REF2 = curl 8.18.0 OpenSSL), finding STD-035 (Low, Confirmed), SMTP part only (IMAP is BL-5013, POP3 BL-5014).

Weakens security (minor): it breaks RFC 4422 section 3.6, the mechanism must finish. No password is sent.

Repro:
1. Run a line-based SMTP mock that advertises only NTLM (`250-AUTH NTLM` in the EHLO reply). It answers the client's NTLM Type-1 message at once with `235 ok` and never sends a Type-2 challenge.
2. `curl -sS smtp://127.0.0.1:45019/x -u user:secret --mail-from a@b --mail-rcpt c@d -T m.txt`

Expected (REF): exit 67, `curl: (67) Login denied`. No further command is sent.

Actual (PORT): exit 0; it sends MAIL FROM, RCPT TO and DATA and delivers the mail.

Where to start: `Curl.Original.Protocol.Smtp.UnitLibrary/SmtpSaslAuthentication.cs`, about line 372: `reply.Code == Authenticated && messageSent ? ExchangeOutcome.Accepted` accepts success once any SASL message has been sent, without asking the `ISaslExchange` (`SecurityContextSaslExchange`) whether the mechanism has finished.

## Acceptance criteria

- [x] A unit test in `Curl.Original.Protocol.Smtp.UnitTests/SmtpProtocolHandlerSaslAuthErrorTests.cs` (NTLM-only server replying `235 ok` to the Type-1 message) shows the result is `CurlExitCode` 67 with message `Login denied` and that the scripted connection received no command after the Type-1 line (no MAIL FROM, RCPT TO or DATA).
- [x] Regression: in `Curl.Original.Protocol.Smtp.UnitTests/SmtpProtocolHandlerAuthenticationTests.cs`, a PLAIN-only server answering `235` after the PLAIN response still logs in and the transfer completes (exit 0); a complete NTLM exchange (Type-1, Type-2, Type-3, then `235`) still succeeds.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text. Keep the fix in the SMTP library; if a shared change to `ISaslExchange` (in `Curl.Authentication.UnitLibrary`) is needed to ask whether the mechanism has finished, check first whether it already exposes that, and if not file it as a separate task that this one depends on.

## Notes (continued)

Fix kept in SMTP library: ISaslExchange has no "finished" query, so NTLM accepts 235 only after the Type-2 challenge was handed to the exchange (challengesHanded > 0). Quality run: 0 failing members. Windows-only run; tests are platform-neutral.

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing. Criteria at claim: 9e3543e9 21f8543a a0b49a29.
- 2026-11-12: Doing -> Done. NTLM 235 before the Type-2 challenge is now refused with exit 67; tests and quality run green
