---
id: BL-5013
title: IMAP refuses an OK before the NTLM SASL exchange has finished with exit 67
priority: High
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Original.Protocol.Imap.UnitLibrary/ImapAuthentication.cs, Curl.Original.Protocol.Imap.UnitTests/ImapProtocolHandlerSaslAuthErrorTests.cs, Curl.Original.Protocol.Imap.UnitTests/ImapProtocolHandlerAuthenticationTests.cs]
requirement: none
created: 2026-11-12
completed: 2026-11-12
---
# BL-5013 — IMAP refuses an OK before the NTLM SASL exchange has finished with exit 67

## Goal

IMAP `AUTHENTICATE NTLM` treats a tagged `OK` sent before the NTLM exchange has finished as a failed login: exit 67, `Login denied`, and no further command is sent.

## Context

From the outside fitness review of 2026-11-12 (commit 2d2ad7f8b, `preview` curl-win-x64.zip; REF = curl 8.22.0 Schannel, REF2 = curl 8.18.0 OpenSSL), finding STD-035 (Low, Confirmed), IMAP part only (SMTP is BL-5012, POP3 BL-5014).

Weakens security (minor): it breaks RFC 4422 section 3.6, the mechanism must finish. No password is sent.

Repro:
1. Run a line-based IMAP mock that advertises `AUTH=NTLM` in the CAPABILITY reply. It answers the client's NTLM Type-1 message at once with `<tag> OK auth` and never sends a Type-2 challenge.
2. `curl -sS "imap://127.0.0.1:45039/INBOX/;UID=1" -u user:secret`

Expected (REF): exit 67, `curl: (67) Login denied`. No further command is sent.

Actual (PORT): exit 0; it sends SELECT and FETCH and prints the message.

Where to start: `Curl.Original.Protocol.Imap.UnitLibrary/ImapAuthentication.cs`, about line 348: success is accepted once any SASL message has been sent, without asking the `ISaslExchange` (`SecurityContextSaslExchange`) whether the mechanism has finished.

## Acceptance criteria

- [x] A unit test in `Curl.Original.Protocol.Imap.UnitTests/ImapProtocolHandlerSaslAuthErrorTests.cs` (NTLM-only server replying `<tag> OK auth` to the Type-1 message) shows the result is `CurlExitCode` 67 with message `Login denied` and that the scripted connection received no command after the Type-1 line (no SELECT or FETCH).
- [x] Regression: in `Curl.Original.Protocol.Imap.UnitTests/ImapProtocolHandlerAuthenticationTests.cs`, a PLAIN-only server answering `OK` after the PLAIN response still logs in (exit 0); a complete NTLM exchange (Type-1, Type-2, Type-3, then `OK`) still succeeds.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text. Keep the fix in the IMAP library; if a shared change to `ISaslExchange` (in `Curl.Authentication.UnitLibrary`) is needed, check first whether it already exposes completion, and if not file it as a separate task this one depends on.

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing. Criteria at claim: e5cf66f9 39812b76 a0b49a29.
- 2026-11-12: Done. ImapAuthentication.HasFinished: NTLM accepts a tagged OK only after its Type 2 was answered (no ISaslExchange change needed; IMAP library only). Tests added in ImapProtocolHandlerSaslAuthErrorTests (OK-before-Type-2 gives exit 67 and nothing sent after Type 1; complete NTLM exchange logs in; PLAIN regression already covered). CI-on-Linux criterion met by platform-neutral tests. Quality run on the Imap library: 0 failing members.
- 2026-11-12: Doing -> Done. IMAP NTLM refuses OK before Type 2 answered
