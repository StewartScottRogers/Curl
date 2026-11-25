---
id: BL-5961
title: Match curl's IMAP login with --oauth2-bearer without -u and after a redirect withholds the token
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-5880]
touches: [Curl.Original.Protocol.Imap.UnitLibrary/ImapAuthentication.cs, Curl.Original.Protocol.Imap.UnitTests/ImapProtocolHandlerAuthenticationTests.cs]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-5961 — Match curl's IMAP login with --oauth2-bearer without -u and after a redirect withholds the token

## Goal

Over IMAP with `--oauth2-bearer`, Curl chooses the same login as curl 8.22.0 when there is no `-u`, and after a redirect has withheld the token.

## Context

Found in BL-5880 (ADR-1361). Two cases:

- No `-u`: measured, curl 8.22.0 with `--oauth2-bearer tok --login-options AUTH=PLAIN imap://...` against `AUTH=PLAIN` sends `AUTHENTICATE PLAIN`; before this work Curl sends nothing after `CAPABILITY` (exit 67), because `SaslMechanismRanking.CanUsePassword` needs a credential. Measure the other mechanisms too.
- A redirect that withholds the token (`MailRequestOptions.BearerTokenWithheld`, upstream test779): before this work `ImapAuthentication` reads it only to hold back `LOGIN`, so a password mechanism may be chosen. Measure what curl sends when the server offers `PLAIN`.

The stashed change gives an empty credential with `--oauth2-bearer` and no `-u`, and when a redirect withheld the token and no `AUTH=` option is given it chooses no SASL mechanism; a mechanism an `AUTH=` option names is still used. Its tests passed (Imap 564).

## Acceptance criteria

- [x] Both cases are measured against real curl with `Record-CurlExchange.ps1` over IMAP, the commands and exit codes are recorded under Notes, and Curl sends the same commands and exits with the same code.
- [x] A focused unit test in `Curl.Original.Protocol.Imap.UnitTests` pins each of the two cases (ADR-0482).
- [x] `Measure-CodeQuality.ps1 -Library Curl.Original.Protocol.Imap.UnitLibrary` reports no member of `ImapAuthentication.cs` outside a gate.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Split from BL-5925.
- Continues the stashed work of BL-5925.
- The stash also holds the POP3 (BL-5962) and SMTP (BL-5963) changes. Keep only the files this task touches; restore every other stashed file to `HEAD` before committing.
- Run the quality measurement for this one library only, with `-ReportPath`: one run over all three libraries timed out after 18 minutes with no report while other lanes were building.
- Decision and measured table: ADR-1394. `ADR-1394` and its README row are the two additions outside `touches`.
- Measured 2026-11-25 with curl 8.22.0 and then with `Curl.Console\bin\Debug\net10.0\curl.exe`; the commands sent and exit codes were identical in all 27 cases. Each command was `Record-CurlExchange.ps1 -Port 18161 -Imap -ImapReply 'CAPABILITY=* CAPABILITY IMAP4rev1 <AUTH=...>\r\nOK done' -CurlArgs <args>`, where `<args>` is `-sS <options> imap://127.0.0.1:18161/`, or for a redirect `-sS -L --proto-redir +imap <options> http://127.0.0.1:18162/` with a loopback server answering `302` to `imap://[user@]127.0.0.1:18161/`. Every `<options>` starts `--oauth2-bearer tok`; none has `-u`. Below, "login" is `--login-options`, and the commands are those after `A001 CAPABILITY` (`B001` after a redirect); a successful run goes on with `LIST "" *` and `LOGOUT`.
  - No login option, server `AUTH=PLAIN`, `LOGIN`, `CRAM-MD5`, `DIGEST-MD5`, `NTLM`, `EXTERNAL` or none: nothing, exit 67.
  - No login option, server `AUTH=XOAUTH2` or `OAUTHBEARER`: `AUTHENTICATE <name>`, exit 67 (the server answers `OK` with no continuation).
  - Login `AUTH=PLAIN`: `AUTHENTICATE PLAIN`, `AAA=`, exit 0. Login `AUTH=LOGIN`: `AUTHENTICATE LOGIN`, two empty lines, exit 0.
  - Login `AUTH=` `CRAM-MD5`, `DIGEST-MD5`, `NTLM`, `EXTERNAL`, `XOAUTH2` or `OAUTHBEARER`, server offering it: `AUTHENTICATE <name>`, exit 67.
  - Login `AUTH=+LOGIN`, server `AUTH=PLAIN`: `LOGIN` with two empty arguments, exit 0.
  - Redirect to `v@`, server `AUTH=PLAIN`, `LOGIN`, `CRAM-MD5`, `EXTERNAL` or `XOAUTH2`: nothing, exit 67. With login `AUTH=PLAIN`: `AUTHENTICATE PLAIN`, `AHYA`, exit 0.
  - Redirect to `v:p@`, server `AUTH=PLAIN` or none: nothing, exit 67. Redirect with no user, server `AUTH=PLAIN`: no login, `LIST`, exit 0.
- Results: build clean; fast tests 58216 passed, 0 failed (Imap 564 passed, 2 skipped); quality 0 failing members. The tests are in memory, so platform-neutral.
- The POP3 and SMTP files from the stash are not committed. `git checkout HEAD -- <those files>` was denied by the run's permissions, so they are left modified in the lane worktree (the build and tests above ran with them present); the stash still holds them for BL-5962 and BL-5963.
- `dotnet format --verify-no-changes` reports an imports-ordering error at line 1 of `ImapProtocolHandlerAuthenticationTests.cs`; this task changed no `using` line.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: 9e9f2782 63065152 000323c1 3d836d5e.
- 2026-11-25: Doing -> Done. IMAP with --oauth2-bearer logs in as curl does without -u and after a redirect withholds the token
