---
id: BL-1220
title: Cancel an IMAP SASL exchange with * on an undecodable challenge and try the next mechanism
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1219]
touches: [Curl.Protocol.Imap.UnitLibrary, Curl.Protocol.Imap.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1220 — Cancel an IMAP SASL exchange with * on an undecodable challenge and try the next mechanism

## Goal

When an IMAP server answers `AUTHENTICATE` with a `+` challenge that a challenge-reading mechanism cannot base64-decode, `ImapAuthentication` sends `*`, reads the server's reply whatever it is, drops that mechanism from the offered ones and starts again with the next one curl would choose, falling back to `LOGIN` when allowed and otherwise ending with exit 67 `Authentication cancelled`, as curl 8.21.0 does.

## Context

- Today `Curl.Protocol.Imap.UnitLibrary/ImapAuthentication.cs` (around line 116-122) hands a challenge that is not base64 to every mechanism as empty (ADR-0133's rule, copied from SMTP), so a CRAM-MD5 exchange answers garbage.
- SMTP already does this: BL-774 (`Tasks/Done/2026-09-29_0225/BL-774-cancel-an-smtp-sasl-exchange-with-on-an-undecodable-challeng.md`) built it in `Curl.Protocol.Smtp.UnitLibrary/SmtpSaslAuthentication.cs` and its Notes give the design: which mechanisms read a challenge (GSSAPI every one; CRAM-MD5, DIGEST-MD5 and NTLM the first one their exchange answers), empty text or text starting `=` is an empty challenge, the `ExchangeOutcome` loop over the offered mechanisms, and the guard for a mechanism that was never offered. Copy the design, not the code (a protocol library never references another).
- curl 8.21.0: `lib/curl_sasl.c` `get_server_message` fails a bad base64 challenge with `CURLE_BAD_CONTENT_ENCODING`, which `Curl_sasl_continue` answers with `*` (`SASL_CANCEL`); on the next reply the mechanism is removed and `Curl_sasl_start` runs again; none left is `SASL_IDLE`, which `lib/imap.c` (lines ~1143-1150 at `curl-8_21_0`) answers with `LOGIN` unless the server said `LOGINDISABLED` or the login options rule out clear text, and otherwise with `failf "Authentication cancelled"`, exit 67.
- Measured 2026-10-02, curl 8.21.0 (mingw, Schannel), `Record-CurlExchange.ps1 -Imap` (from a `.ps1` file) with `-ImapReply 'GREETING=* OK [CAPABILITY IMAP4rev1 AUTH=CRAM-MD5 AUTH=PLAIN] ready','CAPABILITY=* CAPABILITY IMAP4rev1 AUTH=CRAM-MD5 AUTH=PLAIN\r\nOK done','AUTHENTICATE=+ !!!notbase64\r\nBAD cancelled','AUTHENTICATE=+ \r\nOK done' -CurlArgs '-v','-u','user:secret','imap://127.0.0.1:<port>/INBOX'`: `> A002 AUTHENTICATE CRAM-MD5` / `< + !!!notbase64` / `< A002 BAD cancelled` / `> *` / `> A003 AUTHENTICATE PLAIN` / `< + ` / `> AHVzZXIAc2VjcmV0` ... then `LIST` and `LOGOUT`, exit 0. The recorder sends an overridden reply's lines at once, so `A002 BAD cancelled` arrived before curl's `*`; curl still sent `*` and took that line as the reply to it. A server that answers `*` only after receiving it cannot be scripted with today's recorder.

## Acceptance criteria

- [x] Before the code change, `Notes` holds the transcript, stderr and exit code measured as above for a second case: `AUTH=CRAM-MD5` alone with `LOGINDISABLED`, every `AUTHENTICATE` answered `+ !!!notbase64\r\nBAD cancelled`.
- [x] Tests in `Curl.Protocol.Imap.UnitTests` pin the measured fallback to PLAIN (the `*` line, the read of the reply, `AUTHENTICATE PLAIN` and success), the `LOGIN` fallback when no mechanism is left and `LOGIN` is allowed, and the second case's exit 67 `Authentication cancelled` with nothing sent after the reply to `*`.
- [x] Mechanisms that do not read their challenge (PLAIN, LOGIN, EXTERNAL, XOAUTH2, OAUTHBEARER) keep ADR-0133's empty-challenge behaviour; a test pins it.
- [x] `dotnet build Curl.slnx -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes with no test needing `TestCategory=Integration`; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Imap.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

### Measured (curl 8.21.0 mingw Schannel, `Record-CurlExchange.ps1 -Imap` from a `.ps1`, 2026-10-02, `-v -u user:secret imap://127.0.0.1:<port>/INBOX`)

(b) `-ImapReply 'GREETING=* OK [CAPABILITY IMAP4rev1 AUTH=CRAM-MD5 LOGINDISABLED] ready','CAPABILITY=* CAPABILITY IMAP4rev1 AUTH=CRAM-MD5 LOGINDISABLED\r\nOK done','AUTHENTICATE=+ !!!notbase64\r\nBAD cancelled'`:
transcript `> A001 CAPABILITY` / `< * CAPABILITY IMAP4rev1 AUTH=CRAM-MD5 LOGINDISABLED` / `< A001 OK done` / `> A002 AUTHENTICATE CRAM-MD5` / `< + !!!notbase64` / `< A002 BAD cancelled` / `> *` / `< * BAD Command not recognized`, then nothing (no LOGOUT). stderr (-v, after the exchange): `* Authentication cancelled` / `* closing connection #0` / `curl: (67) Authentication cancelled`; no `SASL:` lines. Exit 67.

(c) The same without `LOGINDISABLED`: after `> *`, `> A003 LOGIN user secret` / `< A003 OK LOGIN completed` (the recorder's `* BAD` for `*` read as untagged), `LIST`, `LOGOUT`, exit 0: with no mechanism left, curl falls back to `LOGIN`.

### Design

As BL-774 for SMTP: `ImapAuthentication.ExchangeAsync` returns `ExchangeOutcome` (Accepted/Refused/Cancelled). `DecodeChallenge(continuation, mechanism, index)` returns null (cancel) only for a non-base64 challenge not starting `=` that the mechanism reads (GSSAPI every one; CRAM-MD5, DIGEST-MD5, NTLM the first one handed). Cancel sends `*`, reads the next tagged or `+` response whatever it is, drops the mechanism from the offered list and chooses again; none left (or a cancelled mechanism that was never offered) goes to `LOGIN` when `MayLogIn`, else exit 67 `Authentication cancelled`, written by `-v` as curl does, with no `SASL:` lines.

### Result

- Tests: `Curl.Protocol.Imap.UnitTests/ImapProtocolHandlerSaslCancelTests.cs` (measured cases a, b, c; the reply to `*` as tagged BAD/NO/OK or another `+`; cancel then refused; a cancelled mechanism never offered stops; NTLM Type 2 and a later GSSAPI challenge cancelled; DIGEST-MD5 rspauth and CRAM-MD5 empty/`=` handed empty; PLAIN, LOGIN, EXTERNAL, XOAUTH2, OAUTHBEARER keep ADR-0133's empty challenge), with fakes `Fakes/RankedSaslAuthenticator.cs` (copied from SMTP's tests) and `Fakes/StubbornSaslAuthenticator.cs`.
- Imap.UnitTests 412 passed; `Measure-CodeQuality.ps1 -Library Curl.Protocol.Imap.UnitLibrary`: 100% line, 100% branch, 0 failing members, worst CRAP 10. `dotnet build Curl.slnx -warnaserror` clean; fast tests green.
- No ADR change: ADR-0133's BL-774 amendment already decides this rule; IMAP now follows it, and `Documentation/Planning/Decisions` is outside this task's `touches`.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. IMAP AUTHENTICATE cancels a non-base64 challenge for CRAM-MD5, DIGEST-MD5, NTLM and GSSAPI with *, reads the reply, tries the next mechanism, then LOGIN or exit 67 Authentication cancelled
