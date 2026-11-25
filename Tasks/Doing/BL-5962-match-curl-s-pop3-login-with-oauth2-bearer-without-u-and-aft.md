---
id: BL-5962
title: Match curl's POP3 login with --oauth2-bearer without -u and after a redirect withholds the token
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-5880]
touches: [Curl.Original.Protocol.Pop3.UnitLibrary/Pop3Login.cs, Curl.Original.Protocol.Pop3.UnitTests/Pop3ProtocolHandlerNoLoginMechanismTests.cs]
requirement: none
created: 2026-11-25
completed:
---
# BL-5962 — Match curl's POP3 login with --oauth2-bearer without -u and after a redirect withholds the token

## Goal

Over POP3 with `--oauth2-bearer`, Curl chooses the same login as curl 8.22.0 when there is no `-u`, and after a redirect has withheld the token.

## Context

Found in BL-5880 (ADR-1361). Two cases:

- No `-u`: over IMAP, curl 8.22.0 with `--oauth2-bearer tok --login-options AUTH=PLAIN` sends the `PLAIN` login, while Curl sends no login (exit 67) because `SaslMechanismRanking.CanUsePassword` needs a credential. Measure `PLAIN` and the other mechanisms over POP3.
- A redirect that withholds the token (`MailRequestOptions.BearerTokenWithheld`, upstream test779): before this work POP3 does not read it, so a password mechanism, `APOP` or `USER`/`PASS` may be sent. Measure what curl sends when the server offers `PLAIN` or `USER`.

The stashed change gives an empty credential with `--oauth2-bearer` and no `-u`, and when a redirect withheld the token and no `AUTH=` option is given it chooses no SASL mechanism, `APOP` or `USER`/`PASS`; a mechanism an `AUTH=` option names is still used. Its tests passed (Pop3 414).

## Acceptance criteria

- [ ] Both cases are measured against real curl with `Record-CurlExchange.ps1` over POP3, the commands and exit codes are recorded under Notes, and Curl sends the same commands and exits with the same code.
- [ ] A focused unit test in `Curl.Original.Protocol.Pop3.UnitTests` pins each of the two cases (ADR-0482).
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Original.Protocol.Pop3.UnitLibrary` reports no member of `Pop3Login.cs` outside a gate.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

- Split from BL-5925.
- Continues the stashed work of BL-5925.
- The stash also holds the IMAP (BL-5961) and SMTP (BL-5963) changes. Keep only the files this task touches; restore every other stashed file to `HEAD` before committing.
- Run the quality measurement for this one library only, with `-ReportPath`: one run over all three libraries timed out after 18 minutes with no report while other lanes were building.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: dad0a42d e85fd8e6 deffc82c 3d836d5e.
