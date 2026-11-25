---
id: BL-5963
title: Match curl's SMTP login with --oauth2-bearer without -u and after a redirect withholds the token
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-5880]
touches: [Curl.Original.Protocol.Smtp.UnitLibrary/SmtpSaslAuthentication.cs, Curl.Original.Protocol.Smtp.UnitTests/SmtpProtocolHandlerAuthenticationTests.cs, Curl.Original.Protocol.Smtp.UnitTests/SmtpProtocolHandlerNoLoginMechanismTests.cs]
requirement: none
created: 2026-11-25
completed:
---
# BL-5963 — Match curl's SMTP login with --oauth2-bearer without -u and after a redirect withholds the token

## Goal

Over SMTP with `--oauth2-bearer`, Curl chooses the same login as curl 8.22.0 when there is no `-u`, and after a redirect has withheld the token.

## Context

Found in BL-5880 (ADR-1361). Two cases:

- No `-u`: over IMAP, curl 8.22.0 with `--oauth2-bearer tok --login-options AUTH=PLAIN` sends the `PLAIN` login, while Curl sends no login (exit 67) because `SaslMechanismRanking.CanUsePassword` needs a credential. Measure `PLAIN` and the other mechanisms over SMTP.
- A redirect that withholds the token (`MailRequestOptions.BearerTokenWithheld`, upstream test779): before this work SMTP does not read it, so a password mechanism may be chosen. Measure what curl sends when the server offers `PLAIN`.

The stashed change gives an empty credential with `--oauth2-bearer` and no `-u`, and when a redirect withheld the token and no `AUTH=` option is given it chooses no SASL mechanism; a mechanism an `AUTH=` option names is still used. Its tests passed (Smtp 439).

## Acceptance criteria

- [ ] Both cases are measured against real curl with `Record-CurlExchange.ps1` over SMTP, the commands and exit codes are recorded under Notes, and Curl sends the same commands and exits with the same code.
- [ ] A focused unit test in `Curl.Original.Protocol.Smtp.UnitTests` pins each of the two cases (ADR-0482).
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Original.Protocol.Smtp.UnitLibrary` reports no member of `SmtpSaslAuthentication.cs` outside a gate.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

- Split from BL-5925.
- Continues the stashed work of BL-5925.
- The stash also holds the IMAP (BL-5961) and POP3 (BL-5962) changes. Keep only the files this task touches; restore every other stashed file to `HEAD` before committing.
- Run the quality measurement for this one library only, with `-ReportPath`: one run over all three libraries timed out after 18 minutes with no report while other lanes were building.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: 75eadb9d 9157c065 13f9e547 3d836d5e.
