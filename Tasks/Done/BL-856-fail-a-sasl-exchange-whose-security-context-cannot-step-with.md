---
id: BL-856
title: Fail a SASL exchange whose security context cannot step with exit 94
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-538]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests, Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests, Curl.Protocol.Imap.UnitLibrary, Curl.Protocol.Imap.UnitTests, Curl.Protocol.Pop3.UnitLibrary, Curl.Protocol.Pop3.UnitTests, Documentation/Planning/Decisions/ADR-0203-a-sasl-security-context-that-cannot-make-its-first-token-fails-with-exit-94.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-09-29
completed: 2026-09-29
---
# BL-856 — Fail a SASL exchange whose security context cannot step with exit 94

## Goal

When the GSSAPI or NTLM security context cannot make a token, `curl smtp://`, `imap://` and `pop3://` fail with exit 94 and `curl: (94) An authentication function returned an error`, sending nothing more, as curl 8.21.0 does - not with `*` and exit 67.

## Context

- Follow-up of BL-538 (ADR-0184). `SecurityContextSaslExchange` answers `null` when a step fails, which `ISaslExchange`'s contract makes the handler cancel with `*` and exit 67.
- Measured 2026-09-29 with curl 8.21.0 (Schannel) and `Record-CurlExchange.ps1 -Smtp -SmtpReply 'EHLO=250-localhost\r\n250 AUTH GSSAPI','AUTH=334 '` with `-u 'DOMAIN\u:p'` (no KDC): without `--sasl-ir` curl sends `AUTH GSSAPI`, reads `334 `, closes, exit 94; with `--sasl-ir` it sends no `AUTH` at all, closes, exit 94. `-u :` does the same.
- The contract lives in `Curl.Protocol.Abstractions.UnitLibrary/ISaslExchange.cs` (e.g. an exception carrying `CurlExitCode.AuthError`, as HTTP's `HttpAuthenticationFailedException`), and each mail handler must turn it into the exit.

## Acceptance criteria

- [x] A `Curl.Authentication.UnitTests` test shows a GSSAPI and an NTLM exchange whose first step answers `NoCredentials` signal exit 94 rather than answering `null`.
- [x] `Curl.Protocol.Smtp.UnitTests`, `.Imap.UnitTests` and `.Pop3.UnitTests` each pin: without `--sasl-ir`, `AUTH <mech>`, the empty challenge, then close with exit 94; with `--sasl-ir`, no `AUTH` command and exit 94.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for every library touched.

## Notes

- Delivered directly rather than through the full `/feature` agent stages: the contract
  (`SaslAuthenticationFailedException`, BL-781) and each session's catch already existed,
  so the change was one throw plus moving when the handlers ask for the initial response.
- Decision recorded in ADR-0203: `SecurityContextSaslExchange.GetInitialResponseAsync`
  throws exit 94 when the first step fails; SMTP, POP3 and IMAP ask for the initial
  response before the command only under `--sasl-ir` (IMAP also when the server offers
  `SASL-IR`), otherwise at the first continuation - curl's `Curl_sasl_start` `force_ir`
  rule, which is what yields the measured "AUTH GSSAPI, 334, close" without `--sasl-ir`.
- Later context steps that fail still answer `null` (exit 67); only the first step was
  measured, so it stays out of scope.
- POP3: `+OK` before the initial response was made is now `Login denied` for every
  mechanism (it was already so when one was pending), matching curl's state machine.
- Added `Documentation/Planning/Decisions/` ADR-0203 and its README row to `touches`:
  the ADR the decision rules require; no task in Doing names either file.
- Tests: `Begin_NoCredentialsForTheFirstToken_FailsWithExit94` (Authentication, NTLM and
  GSSAPI rows); `ExecuteAsync_ExchangeCannotMakeItsInitialResponse_FailsWithAuthErrorSendingNothingMore`
  in the SMTP, IMAP (plus a server-`SASL-IR` row) and POP3 `SaslAuthError` tests.
  `Measure-CodeQuality.ps1`: 100% line and branch, no failing member, in all five libraries.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. A GSSAPI or NTLM SASL context that cannot make its first token fails smtp, imap and pop3 with exit 94, AUTH sent only without --sasl-ir, as curl 8.21.0
