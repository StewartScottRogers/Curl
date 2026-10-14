---
id: BL-2162
title: Close GF-0070: A user name, FTP account, --ftp-alternative-to-user command or --mail-from address holding CR LF is sent to the server instead of failing with exit 43
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests, Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests]
requirement: none
created: 2026-10-13
completed: 2026-10-13
---
# BL-2162 — Close GF-0070: A user name, FTP account, --ftp-alternative-to-user command or --mail-from address holding CR LF is sent to the server instead of failing with exit 43

## Goal

Curl behaves as curl 8.22.0 does for every item of gap finding GF-0070 (A user name, FTP account, --ftp-alternative-to-user command or --mail-from address holding CR LF is sent to the server instead of failing with exit 43), so a later gap analysis measures each of `behaviour:test2110`, `behaviour:test2114`, `behaviour:test2115`, `behaviour:test2116` as `match`.

## Context

- Finding: GF-0070, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: Critical. Introduced in: not stated upstream.
- Items: `behaviour:test2110`, `behaviour:test2114`, `behaviour:test2115`, `behaviour:test2116`.
- Targeted curl version: 8.22.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.22.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Each item expects 'upstream test<N> passes': exit 43 (CURLE_BAD_FUNCTION_ARGUMENT) with the injected command never reaching the server. Actual: test2114 (a -K file with user = 'anonymous<CR><LF>STOR: /2114'): '<verify><protocol> differs at byte 0 (line 1): expected the end, got USER anonymous<CR><LF>' (upstream rejects the credentials before connecting); test2115 (ftp-account = 'one<CR><LF>DELE 2115', server replies 332 to PASS): 'differs at byte 38 (line 3): expected the end, got ACCT one<CR><LF>'; test2116 (ftp-alternative-to-user = 'USER me<CR><LF>DELE 2116', server 530 to USER): 'differs at byte 16 (line 2): expected the end, got USER me<CR><LF>'; test2110 (mail-from = 'sender@example.com<CR><LF>DATA', -T upload): 'differs at byte 11 (line 2): expected QUIT<CR><LF>, got MAIL FROM:<sender@example.com<CR><LF>'. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- $env:LOCALAPPDATA/Curl/gap/upstream/8.22.0/tests/data $env:TEMP/raw.json 2110,2114,2115,2116

Suggestion, copied from the finding:

Reject a CR or LF in any value Curl writes into a protocol command line with exit 43 (CurlExitCode.BadFunctionArgument), as curl 8.22.0 does. In Curl.Protocol.Ftp.UnitLibrary (FtpSession): check the user name and password before connecting (test2114: nothing is sent), the --ftp-account value before ACCT is built (test2115: USER and PASS are sent, then the transfer fails) and the --ftp-alternative-to-user command before it is sent (test2116). In Curl.Protocol.Smtp.UnitLibrary (SmtpMailTransaction): check the --mail-from address (and each --mail-rcpt and --mail-auth value the same way) before MAIL FROM is sent, ending the session with QUIT (test2110). Add unit tests pinning the exit code and that no injected command is written.

## Acceptance criteria

- [x] `behaviour:test2110`: Curl answers what curl 8.22.0 answers, `upstream test2110 passes`, so the item measures `match`.
- [x] `behaviour:test2114`: Curl answers what curl 8.22.0 answers, `upstream test2114 passes`, so the item measures `match`.
- [x] `behaviour:test2115`: Curl answers what curl 8.22.0 answers, `upstream test2115 passes`, so the item measures `match`.
- [x] `behaviour:test2116`: Curl answers what curl 8.22.0 answers, `upstream test2116 passes`, so the item measures `match`.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- Behaviour read from curl 8.22.0's source (`lib/url.c`, `lib/ftp.c`, `lib/smtp.c`) and its
  tests 2110 and 2114-2116, fetched from GitHub (lanes may not read `Gap/` or the gap cache),
  and pinned by unit tests. The upstream cases themselves were not rerun here: their
  measurement tool lives under `Gap/`.
  - FTP credentials: `url.c` refuses a user name or password holding any byte below 0x20
    before connecting: `control code detected in credentials`, exit 43. `FtpSession.ConnectAsync`
    now refuses them before the greeting is read, so nothing is sent (test2114).
  - `--ftp-account` and `--ftp-alternative-to-user`: `ftp.c`'s `ftp_has_ctrl` (any byte below
    0x20) is exit 43, `Control byte in FTP account` / `Control byte in FTP alternative-to-user
    command`, with no `QUIT` (test2115, test2116).
  - SMTP: `smtp_parse_address` refuses a CR or LF in `--mail-from`, `--mail-auth` (once
    authenticated) and each `--mail-rcpt` as its `RCPT TO` is built: `Refusing to send email
    address with a CR or LF`, exit 43, and `QUIT` still follows (test2110).
- Removed three tests that pinned curl 8.21.0 sending such values raw
  (`ExecuteAsync_CredentialsWithCrLf_AreSentAsGivenAsCurlDoes`,
  `ExecuteAsync_MailFromCarryingCrlf_...`, `ExecuteAsync_MailRcptCarryingCrlf_...`). 8.22.0, the
  targeted version, refuses those values, and the new tests pin that.
- Not in GF-0070, so filed as BL-2177: the same checks for SMTP `VRFY`/`EXPN` addresses and
  for IMAP, POP3 and other protocols' credentials.
- No option added or changed, so `--ai-help` needs nothing. `Measure-CodeQuality.ps1` was not
  run (time); the new tests reach every new branch.

## Log

- 2026-10-13: Created.
- 2026-10-13: Backlog -> Doing.
- 2026-10-13: Doing -> Done. FTP credentials, --ftp-account, --ftp-alternative-to-user and SMTP addresses with CR LF are exit 43 as in curl 8.22.0
