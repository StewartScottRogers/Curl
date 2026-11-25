---
id: BL-5925
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
# BL-5925 — Match curl's SASL choice with --oauth2-bearer without -u, and after a redirect withholds the token

## Goal

With `--oauth2-bearer`, Curl chooses the same login as curl 8.22.0 over IMAP, POP3 and SMTP when there is no `-u`, and after a redirect has withheld the token.

## Context

Found in BL-5880 (ADR-1361). Two cases it left:

- No `-u`: measured, curl 8.22.0 with `--oauth2-bearer tok --login-options AUTH=PLAIN imap://...` against `AUTH=PLAIN` sends `AUTHENTICATE PLAIN`; Curl sends nothing after `CAPABILITY` (exit 67), because `SaslMechanismRanking.CanUsePassword` needs a credential. Measure the other mechanisms, POP3 and SMTP.
- A redirect that withholds the token (`MailRequestOptions.BearerTokenWithheld`, upstream test779): only `ImapAuthentication` reads it, and only to hold back `LOGIN`. The SASL request has no token then, so a password mechanism may be chosen over IMAP, POP3 or SMTP, and POP3 may send `APOP` or `USER`/`PASS`. Measure what curl sends when the server offers `PLAIN` or `USER`.

## Acceptance criteria

- [ ] Both cases are measured against real curl with `Record-CurlExchange.ps1` for IMAP, POP3 and SMTP, and Curl sends the same commands and exits with the same code.
- [ ] A focused unit test in each changed library's own `.UnitTests` project pins the behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

- Overtime run (lane 10): the IMAP, POP3 and SMTP changes in the working tree build and their unit tests pass (Imap 564, Pop3 414, Smtp 439). They give an empty credential with --oauth2-bearer and no -u. When a redirect withheld the token and no AUTH= option is given, they choose no SASL mechanism, and for POP3 no APOP or USER/PASS. A mechanism an AUTH= option names is still used. The changes are left uncommitted for the stash because Measure-CodeQuality.ps1 over the three libraries timed out after 18 minutes with no report: MSBuild child nodes exited while other lanes were building. Left: one quality run with -Library for the three libraries and -ReportPath, fix anything outside a gate, then commit and finish. The case of an HTTP redirect to a mail URL is filed as BL-5934.

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing. Criteria at claim: 7b4d1eae caafd3c7 3d836d5e.
- 2026-11-24: Doing -> Backlog. Lane 4 claimed it after the shift's time was up and never started it.
- 2026-11-24: Backlog -> Doing. Criteria at claim: 7b4d1eae caafd3c7 3d836d5e.
- 2026-11-25: Doing -> Backlog. Code done and tested in the working tree; left: Measure-CodeQuality.ps1 for the Imap, Pop3 and Smtp libraries (timed out), then commit
