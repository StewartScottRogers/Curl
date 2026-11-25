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
completed: 2026-11-25
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

- [x] Both cases are measured against real curl with `Record-CurlExchange.ps1` over POP3, the commands and exit codes are recorded under Notes, and Curl sends the same commands and exits with the same code.
- [x] A focused unit test in `Curl.Original.Protocol.Pop3.UnitTests` pins each of the two cases (ADR-0482).
- [x] `Measure-CodeQuality.ps1 -Library Curl.Original.Protocol.Pop3.UnitLibrary` reports no member of `Pop3Login.cs` outside a gate.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Split from BL-5925.
- Continues the stashed work of BL-5925.
- The stash also holds the IMAP (BL-5961) and SMTP (BL-5963) changes. Keep only the files this task touches; restore every other stashed file to `HEAD` before committing.
- Run the quality measurement for this one library only, with `-ReportPath`: one run over all three libraries timed out after 18 minutes with no report while other lanes were building.
- 2026-11-25: measured curl 8.22.0 and Curl (Debug build) with
  `Record-CurlExchange.ps1 -Port <p> -Pop3 -Pop3Reply <CAPA, greeting> -CurlArgs ...`.
  No `-u`: `-sS --oauth2-bearer tok [--login-options O] pop3://127.0.0.1:<p>/1`. Redirect:
  `-sS -L --proto-redir =pop3 --oauth2-bearer tok [--login-options O] http://127.0.0.1:<h>/`,
  `<h>` a one-shot PowerShell server answering `302` to `pop3://v@127.0.0.1:<p>/1` or
  `pop3://127.0.0.1:<p>/1`. "ts" = default greeting with APOP timestamp and
  `SASL PLAIN LOGIN XOAUTH2` + `USER`; "user" = greeting `+OK hi` and `CAPA` `USER` only.
  Curl sent the same commands and exited the same in all 26 cases:

  | URL | `O` | ts: sent after `CAPA`, exit | user: sent after `CAPA`, exit |
  | --- | --- | --- | --- |
  | no `-u` | none | `AUTH XOAUTH2`, 67 | nothing, 67 |
  | no `-u` | `AUTH=PLAIN` | `AUTH PLAIN`, `AAA=`, `RETR`, 0 | nothing, 67 |
  | no `-u` | `AUTH=LOGIN` | `AUTH LOGIN`, two empty lines, `RETR`, 0 | nothing, 67 |
  | no `-u` | `AUTH=*` | `AUTH XOAUTH2`, 67 | `USER `, `PASS `, `RETR`, 0 |
  | no `-u` | `AUTH=+APOP` | `APOP  <digest>`, `RETR`, 0 | nothing, 67 |
  | no `-u` | `AUTH=XOAUTH2` | `AUTH XOAUTH2`, 67 | nothing, 67 |
  | no `-u` | `AUTH=EXTERNAL` | nothing, 67 | nothing, 67 |
  | redirect, user `v` | none | nothing, 67 | nothing, 67 |
  | redirect, user `v` | `AUTH=PLAIN` | `AUTH PLAIN`, `AHYA`, `RETR`, 0 | nothing, 67 |
  | redirect, user `v` | `AUTH=*` | `AUTH PLAIN`, `AHYA`, `RETR`, 0 | `USER v`, `PASS `, `RETR`, 0 |
  | redirect, no user | none, `AUTH=PLAIN`, `AUTH=*` | `RETR`, 0 | `RETR`, 0 |

  (The `AUTH XOAUTH2` exits of 67 are the recorder's `+OK` before the token was sent.)
- The same cases with `-v`, and `SASL SCRAM-SHA-1 CRAM-MD5 DIGEST-MD5 NTLM EXTERNAL` with
  no `-u`, matched on every `SASL:` and `curl:` line except one: `AUTH=SCRAM-SHA-1` made
  curl write `SASL: SCRAM-SHA-1 not builtin` and Curl `no overlap`. Fixed in
  `NoWayToLogIn`, which now reads the same credential the login does; pinned by a test.
- Decision recorded in ADR-1394. Quality: Pop3 library 100% line, 100% branch, 0 failing.
- The IMAP and SMTP stashed files were not restored to `HEAD`: the restore command was
  denied by the run's permissions. They are left uncommitted and are not in this task's
  commit (they remain in stash `eaa63ca596238e2eeebde20362385daf8a0b38dc` for BL-5961 and
  BL-5963). The final fast test run therefore included them in the worktree.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: dad0a42d e85fd8e6 deffc82c 3d836d5e.
- 2026-11-25: Doing -> Done. POP3 matches curl 8.22.0 with a bearer token and no -u, and after a redirect withholds it (ADR-1394)
