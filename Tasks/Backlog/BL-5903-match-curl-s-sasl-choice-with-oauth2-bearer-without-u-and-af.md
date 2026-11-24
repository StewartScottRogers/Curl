---
id: BL-5903
title: Match curl's SASL choice with --oauth2-bearer without -u, and after a redirect withholds the token
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-5880]
touches: [Curl.Authentication.UnitLibrary/SaslMechanismRanking.cs, Curl.Authentication.UnitTests/SaslAuthenticatorTests.cs, Curl.Original.Protocol.Imap.UnitLibrary/ImapAuthentication.cs, Curl.Original.Protocol.Imap.UnitTests/ImapProtocolHandlerAuthenticationTests.cs, Curl.Original.Protocol.Pop3.UnitLibrary/Pop3Login.cs, Curl.Original.Protocol.Pop3.UnitTests/Pop3ProtocolHandlerNoLoginMechanismTests.cs, Curl.Original.Protocol.Smtp.UnitLibrary/SmtpSaslAuthentication.cs, Curl.Original.Protocol.Smtp.UnitTests]
requirement: none
created: 2026-11-24
completed:
---
# BL-5903 — Match curl's SASL choice with --oauth2-bearer without -u, and after a redirect withholds the token

## Goal

With `--oauth2-bearer`, Curl chooses the same login as curl 8.22.0 over IMAP, POP3 and SMTP when there is no `-u`, and after a redirect has withheld the token.

## Context

Found in BL-5880 (ADR-1347). Two cases it left:

- No `-u`: measured, curl 8.22.0 with `--oauth2-bearer tok --login-options AUTH=PLAIN imap://...` against `AUTH=PLAIN` sends `AUTHENTICATE PLAIN`; Curl sends nothing after `CAPABILITY` (exit 67), because `SaslMechanismRanking.CanUsePassword` needs a credential. Measure the other mechanisms, POP3 and SMTP.
- A redirect that withholds the token (`MailRequestOptions.BearerTokenWithheld`, upstream test779): only `ImapAuthentication` reads it, and only to hold back `LOGIN`. The SASL request has no token then, so a password mechanism may be chosen over IMAP, POP3 or SMTP, and POP3 may send `APOP` or `USER`/`PASS`. Measure what curl sends when the server offers `PLAIN` or `USER`.

## Acceptance criteria

- [ ] Both cases are measured against real curl with `Record-CurlExchange.ps1` for IMAP, POP3 and SMTP, and Curl sends the same commands and exits with the same code.
- [ ] A focused unit test in each changed library's own `.UnitTests` project pins the behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-24: Created.
