---
id: BL-1222
title: Cancel a POP3 SASL exchange with * on an undecodable challenge and fall back as curl does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1221]
touches: [Curl.Protocol.Pop3.UnitLibrary, Curl.Protocol.Pop3.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1222 — Cancel a POP3 SASL exchange with * on an undecodable challenge and fall back as curl does

## Goal

When a POP3 server answers `AUTH` with a `+` challenge that a challenge-reading mechanism cannot base64-decode, `Pop3Login` sends `*`, reads the server's reply whatever it is, drops that mechanism and tries the next one curl would choose; with none left it falls back to APOP, then `USER`/`PASS`, where the server and the login options allow them, and otherwise ends with exit 67 `Authentication cancelled`, as curl 8.21.0 does.

## Context

- Today `Curl.Protocol.Pop3.UnitLibrary/Pop3Login.cs` (around lines 101-108) hands a challenge that is not base64 to every mechanism as empty, so a CRAM-MD5 exchange answers garbage.
- SMTP already does this: BL-774 (`Tasks/Done/2026-09-29_0225/BL-774-cancel-an-smtp-sasl-exchange-with-on-an-undecodable-challeng.md`) built it in `Curl.Protocol.Smtp.UnitLibrary/SmtpSaslAuthentication.cs`, and its Notes give the design: which mechanisms read a challenge (GSSAPI every one; CRAM-MD5, DIGEST-MD5 and NTLM the first one their exchange answers), empty text or text starting `=` is an empty challenge, the loop over the offered mechanisms, and the guard for a mechanism that was never offered. Copy the design, not the code.
- curl 8.21.0: `lib/curl_sasl.c` answers a bad base64 challenge with `*` (`SASL_CANCEL`), removes the mechanism on the next reply and starts again; none left is `SASL_IDLE`, which `lib/pop3.c` lines 983-996 at `curl-8_21_0` answers with APOP when offered and allowed, else `USER` when clear text is allowed, else `failf "Authentication cancelled"`, exit 67.
- Measured 2026-10-02, curl 8.21.0 (mingw, Schannel), `Record-CurlExchange.ps1 -Pop3 -CurlArgs '-v','-u','user:secret','pop3://127.0.0.1:<port>/1'` (array values passed from a `.ps1` file):
  - `-Pop3Reply 'GREETING=+OK POP3 ready','CAPA=+OK\r\nSASL CRAM-MD5\r\n.','AUTH=+ !!!notbase64','*=-ERR cancelled'`: `> CAPA` ... `> AUTH CRAM-MD5` / `< + !!!notbase64` / `> *` / `< -ERR cancelled`, then nothing more sent; stderr `curl: (67) Authentication cancelled`, exit 67.
  - the same with `CAPA=+OK\r\nSASL CRAM-MD5\r\nUSER\r\n.`: after `< -ERR cancelled`, `> USER user` / `< +OK User accepted` / `> PASS secret` / `< +OK Logged in` / `> RETR 1` ... `> QUIT`, exit 0.

## Acceptance criteria

- [x] Before the code change, `Notes` holds the measured transcript for a third case: a greeting carrying an APOP timestamp and `CAPA` listing `SASL CRAM-MD5` without `USER`, the challenge refused as above.
- [x] Tests in `Curl.Protocol.Pop3.UnitTests` pin all three measured cases byte for byte (commands sent, the `*` line, the read of its reply, the fallback or the exit 67 `Authentication cancelled`), and a case where a second offered mechanism (`SASL CRAM-MD5 PLAIN`) is tried after the cancel.
- [x] Mechanisms that do not read their challenge (PLAIN, LOGIN, EXTERNAL, XOAUTH2, OAUTHBEARER) keep today's empty-challenge behaviour; a test pins it.
- [x] `dotnet build Curl.slnx -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes with no test needing `TestCategory=Integration`; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Pop3.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

### Measured (curl 8.21.0 mingw Schannel, `Record-CurlExchange.ps1 -Pop3`, 2026-10-02, `-v -u user:secret pop3://127.0.0.1:<port>/1`)

(c) APOP timestamp, no `USER`: `-Pop3Reply 'GREETING=+OK POP3 ready <1896.697170952@dbc.mtview.ca.us>','CAPA=+OK\r\nSASL CRAM-MD5\r\n.','AUTH=+ !!!notbase64','*=-ERR cancelled'`:
`> CAPA` / `< +OK` / `< SASL CRAM-MD5` / `< .` / `> AUTH CRAM-MD5` / `< + !!!notbase64` / `> *` / `< -ERR cancelled` / `> APOP user 3f18b52881e44c0cc6067f46e0ced7bc` / `< +OK Logged in` / `> RETR 1` ... `> QUIT`; exit 0.

(a) re-measured with `-v`: after `< -ERR cancelled` stderr has `* Authentication cancelled` then `curl: (67) Authentication cancelled`, exit 67, so the message is one `-v` writes.

(d) `CAPA=+OK\r\nSASL CRAM-MD5 PLAIN\r\n.`, `AUTH=+ !!!notbase64`, `*=-ERR cancelled`, `AUTH=+ `, `AHVZZXIAC2VJCMV0=+OK Logged in`: `> AUTH CRAM-MD5` / `< + !!!notbase64` / `> *` / `< -ERR cancelled` / `> AUTH PLAIN` / `< + ` / `> AHVzZXIAc2VjcmV0` / `< +OK Logged in` / `> RETR 1`; exit 0.

### Design

BL-774's SMTP design carried over to `Pop3Login`: the SASL step loops `ChooseMechanism` over a copy of the offered mechanisms; an exchange whose challenge-reading mechanism (GSSAPI every challenge; CRAM-MD5, DIGEST-MD5, NTLM the first challenge handed to the exchange) gets a non-base64 challenge (not empty, not starting `=`) sends `*`, reads one response whatever it is, and the mechanism is removed; a chosen mechanism not among the offered stops the loop. After a cancel with none left, curl's `SASL_IDLE` path (`lib/pop3.c`) runs: APOP when the greeting had a timestamp and the options allow it, `USER`/`PASS` when `CAPA` listed `USER` and the options allow any way, otherwise exit 67 `Authentication cancelled` with no `SASL:` `-v` lines.

### Implemented

- `Pop3Login`: `TrySaslAsync` / `TryMechanismsAsync` loop, `AuthenticateAsync` returns whether it was cancelled, `SendAuthAsync` split out, `AnswerToAsync` returns the line to send (`*` = `CancelLine` to cancel), `DecodeChallenge(response, mechanism, index)` + `ReadsChallenge`. New `Pop3SessionMessages.AuthenticationCancelled` (written by `-v`, as measured) and `Pop3DiagnosticLogLines.MechanismCancelled` (warning).
- Tests: `Curl.Protocol.Pop3.UnitTests/Pop3ProtocolHandlerSaslCancelTests.cs` with fake `Fakes/RankedSaslAuthenticator.cs`: cases (a), (b), (c), (d), any response to `*`, every mechanism cancelled, cancel then refused, `AUTH=CRAM-MD5`, a chosen mechanism never offered, empty/`=` CRAM-MD5 challenges, DIGEST-MD5 later challenge, NTLM Type 2, GSSAPI later challenge, and PLAIN/LOGIN/EXTERNAL/XOAUTH2/OAUTHBEARER still handed an empty challenge.
- Results: Pop3.UnitTests 293 passed; `Measure-CodeQuality.ps1 -Library Curl.Protocol.Pop3.UnitLibrary` 100% line, 100% branch, 0 failing members (worst CRAP 10); `dotnet build Curl.slnx -warnaserror` clean; fast tests green. No ADR needed: the behaviour is curl's, measured, and BL-774 already records the design under ADR-0133's amendment.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. POP3 AUTH cancels a non-base64 challenge for CRAM-MD5, DIGEST-MD5, NTLM and GSSAPI with *, tries the next mechanism, then APOP or USER/PASS, else exit 67 Authentication cancelled
