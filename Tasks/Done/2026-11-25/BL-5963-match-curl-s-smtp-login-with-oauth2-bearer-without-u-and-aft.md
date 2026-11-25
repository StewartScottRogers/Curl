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
completed: 2026-11-25
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

- [x] Both cases are measured against real curl with `Record-CurlExchange.ps1` over SMTP, the commands and exit codes are recorded under Notes, and Curl sends the same commands and exits with the same code.
- [x] A focused unit test in `Curl.Original.Protocol.Smtp.UnitTests` pins each of the two cases (ADR-0482).
- [x] `Measure-CodeQuality.ps1 -Library Curl.Original.Protocol.Smtp.UnitLibrary` reports no member of `SmtpSaslAuthentication.cs` outside a gate.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Split from BL-5925.
- Continues the stashed work of BL-5925.
- The stash also holds the IMAP (BL-5961) and POP3 (BL-5962) changes. Keep only the files this task touches; restore every other stashed file to `HEAD` before committing.
- Run the quality measurement for this one library only, with `-ReportPath`: one run over all three libraries timed out after 18 minutes with no report while other lanes were building.
- The IMAP and POP3 files from the stash were restored to `HEAD`. Decision in ADR-1395.
- Measured 2026-11-25 against curl 8.22.0 (Schannel build) with `Record-CurlExchange.ps1 -Smtp -SmtpReply 'EHLO=250-localhost\r\n250 AUTH <offered>'`, `-CurlArgs -sv --oauth2-bearer tok [--login-options <options>]`, then `smtp://127.0.0.1:18963/` (no `-u`) or, for a redirect, `-L --proto-redir =smtp http://127.0.0.1:18964/`, a loopback server answering `302` to `smtp://v@127.0.0.1:18963/`. "Nothing" means no command after `EHLO`; `P` is `PLAIN LOGIN CRAM-MD5`.

  | Case | Offered | Options | curl sends after `EHLO` | Exit |
  | --- | --- | --- | --- | --- |
  | no `-u` | P | none | nothing | 67 |
  | no `-u` | P | `AUTH=PLAIN` | `AUTH PLAIN`, `AAA=`, `HELP`, `QUIT` | 0 |
  | no `-u` | P | `AUTH=LOGIN` | `AUTH LOGIN`, two empty lines, `HELP`, `QUIT` | 0 |
  | no `-u` | P | `AUTH=CRAM-MD5` or `AUTH=*` | `AUTH CRAM-MD5`, answer for an empty user, `HELP`, `QUIT` | 0 |
  | no `-u` | `DIGEST-MD5` | `AUTH=DIGEST-MD5` | `AUTH DIGEST-MD5`, nothing after the challenge | 67 |
  | no `-u` | `NTLM` | `AUTH=NTLM` | `AUTH NTLM`, nothing more | 67 |
  | no `-u` | `EXTERNAL PLAIN` | `AUTH=EXTERNAL` | `AUTH EXTERNAL`, nothing more | 67 |
  | redirect | P | none | nothing (`-v`: no overlap) | 67 |
  | redirect | P | `AUTH=PLAIN` | `AUTH PLAIN`, `AHYA`, `HELP`, `QUIT` | 0 |
  | redirect | P | `AUTH=*` | `AUTH CRAM-MD5`, answer for `v`, `HELP`, `QUIT` | 0 |
  | redirect | `EXTERNAL PLAIN`; `SCRAM-SHA-256 PLAIN` | none | nothing (`-v`: no overlap) | 67 |
  | redirect | `OAUTHBEARER XOAUTH2 PLAIN` | none | nothing (`-v`: both bearer mechanisms miss the token) | 67 |
  | redirect | `XOAUTH2 PLAIN` | `AUTH=XOAUTH2` | nothing (`-v`: XOAUTH2 misses the token) | 67 |
  | redirect | `FOO` | none | nothing (`-v`: none offered or recognized) | 67 |
  | redirect to `smtp://127.0.0.1:18963/` (no user) | `XOAUTH2 PLAIN`; P with `AUTH=PLAIN` | | `HELP`, `QUIT` | 0 |
  | redirect to no user | `EXTERNAL PLAIN` | `AUTH=EXTERNAL` | `AUTH EXTERNAL`, nothing more | 67 |

- Curl sends the same commands and exits with the same code in every row but one: `AUTH=DIGEST-MD5` with an empty user, where Curl answers the challenge and exits 0. The cause is in `Curl.Authentication.UnitLibrary`'s SSPI DIGEST-MD5, outside this task's touches, and `-u ':'` shows the same difference without a bearer token, so it predates this change. Filed as BL-5968.
- Beyond the stashed work, this run found that after a redirect Curl wrote `SCRAM-SHA-256 not builtin` where curl writes `no overlap`; fixed and pinned.
- Results: Smtp unit tests 442 passed, 7 skipped; full fast tests passed; the quality report shows 0 failing members.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: 75eadb9d 9157c065 13f9e547 3d836d5e.
- 2026-11-25: Doing -> Done. SMTP matches curl 8.22.0 for --oauth2-bearer without -u and after a redirect withholds the token (ADR-1395); the DIGEST-MD5 empty-user difference is BL-5968.
