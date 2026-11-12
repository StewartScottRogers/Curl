---
id: BL-5014
title: POP3 refuses a +OK before the NTLM SASL exchange has finished with exit 67
priority: High
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Original.Protocol.Pop3.UnitLibrary/Pop3Login.cs, Curl.Original.Protocol.Pop3.UnitTests/Pop3ProtocolHandlerSaslAuthErrorTests.cs, Curl.Original.Protocol.Pop3.UnitTests/Pop3ProtocolHandlerLoginTests.cs]
requirement: none
created: 2026-11-12
completed:
---
# BL-5014 — POP3 refuses a +OK before the NTLM SASL exchange has finished with exit 67

## Goal

POP3 `AUTH NTLM` treats a `+OK` sent before the NTLM exchange has finished as a failed login: exit 67, `Login denied`, and no further command is sent.

## Context

From the outside fitness review of 2026-11-12 (commit 2d2ad7f8b, `preview` curl-win-x64.zip; REF = curl 8.22.0 Schannel, REF2 = curl 8.18.0 OpenSSL), finding STD-035 (Low, Confirmed), POP3 part only (SMTP is BL-5012, IMAP BL-5013).

Weakens security (minor): it breaks RFC 4422 section 3.6, the mechanism must finish. No password is sent.

Repro:
1. Run a line-based POP3 mock that advertises `SASL NTLM` in the CAPA reply. It answers the client's NTLM Type-1 message at once with `+OK` and never sends a Type-2 challenge.
2. `curl -sS pop3://127.0.0.1:45026/1 -u user:secret`

Expected (REF): exit 67, `curl: (67) Login denied`. No further command is sent.

Actual (PORT): exit 0; it sends RETR 1 and prints the message.

Where to start: `Curl.Original.Protocol.Pop3.UnitLibrary/Pop3Login.cs`, about lines 290-292: success is accepted once any SASL message has been sent, without asking the `ISaslExchange` (`SecurityContextSaslExchange`) whether the mechanism has finished.

## Acceptance criteria

- [ ] A unit test in `Curl.Original.Protocol.Pop3.UnitTests/Pop3ProtocolHandlerSaslAuthErrorTests.cs` (NTLM-only server replying `+OK` to the Type-1 message) shows the result is `CurlExitCode` 67 with message `Login denied` and that the scripted connection received no command after the Type-1 line (no RETR).
- [ ] Regression: in `Curl.Original.Protocol.Pop3.UnitTests/Pop3ProtocolHandlerLoginTests.cs`, a PLAIN-only server answering `+OK` after the PLAIN response still logs in (exit 0); a complete NTLM exchange (Type-1, Type-2, Type-3, then `+OK`) still succeeds.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text. Keep the fix in the POP3 library; if a shared change to `ISaslExchange` (in `Curl.Authentication.UnitLibrary`) is needed, check first whether it already exposes completion, and if not file it as a separate task this one depends on.

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing. Criteria at claim: ddba1fe3 58097ba0 a0b49a29.
