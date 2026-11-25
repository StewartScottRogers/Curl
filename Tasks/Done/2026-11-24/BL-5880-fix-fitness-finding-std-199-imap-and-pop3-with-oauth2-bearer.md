---
id: BL-5880
title: Fix fitness finding STD-199: IMAP and POP3 with --oauth2-bearer fall back to a cleartext password LOGIN or APOP when the server offers no OAuth mechanism, and refuse an explicit AUTH=PLAIN
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Imap.UnitLibrary/ImapAuthentication.cs, Curl.Original.Protocol.Pop3.UnitLibrary/Pop3Login.cs, Curl.Original.Protocol.Pop3.UnitLibrary/Pop3LoginOptions.cs, Curl.Authentication.UnitLibrary/SaslMechanismRanking.cs, Curl.Protocol.Abstractions.UnitLibrary/SaslRequest.cs, Curl.Original.Protocol.Imap.UnitTests/ImapProtocolHandlerAuthenticationTests.cs, Curl.Original.Protocol.Imap.UnitTests/Fakes/FakeSaslAuthenticator.cs, Curl.Original.Protocol.Pop3.UnitTests/Pop3ProtocolHandlerNoLoginMechanismTests.cs, Curl.Original.Protocol.Pop3.UnitTests/Pop3LoginOptionsTests.cs, Curl.Authentication.UnitTests/SaslAuthenticatorTests.cs]
requirement: none
created: 2026-11-24
completed: 2026-11-24
---
# BL-5880 — Fix fitness finding STD-199: IMAP and POP3 with --oauth2-bearer fall back to a cleartext password LOGIN or APOP when the server offers no OAuth mechanism, and refuse an explicit AUTH=PLAIN

## Goal

STD-199 from the outside fitness review of 2026-11-24 13:58 no longer reproduces: IMAP and POP3 with --oauth2-bearer fall back to a cleartext password LOGIN or APOP when the server offers no OAuth mechanism, and refuse an explicit AUTH=PLAIN. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-24T1358` (commit `1e5d591`, the `preview` release binaries). That review was a deeper second pass of the same commit as `Curl-Fitness-Review-2026-11-24T0708`, covering areas the earlier pass did not test.

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer's mock servers were Python, and some findings used real servers in Docker (Dovecot, GreenMail, Samba, Mosquitto, Kafka, Prosody, SeaweedFS, Azurite); rebuild any mock you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Check that it still reproduces on the current branch first.

The finding, verbatim:

- **Severity:** Medium (security weakening: the password is sent in cleartext where curl sends nothing) · **Confidence:** Confirmed (2 runs each, GreenMail and Dovecot) · **Area:** IMAP / POP3 SASL mechanism choice, Windows build. SMTP matches REF.
- **Server:** Dovecot 2.3.21: `docker run -d --name dovecot -v <dir>:/etc/dovecot/conf.d:ro -p 36243:143 -p 36211:110 dovecot/dovecot:2.3.21`, where `<dir>/99.conf` holds `auth_mechanisms = plain login cram-md5 digest-md5 scram-sha-1 scram-sha-256 apop` and `disable_plaintext_auth = no` (the image's static passdb accepts any user with password `pass`). GreenMail 2.1.2 (`greenmail/standalone:2.1.2`, users `alice:secret`) gives the same results. Neither offers XOAUTH2 or OAUTHBEARER.
- **Repro:**
  1. `curl -v -u alice:pass --oauth2-bearer tok imap://127.0.0.1:36243/`
  2. `curl -v -u alice:pass --oauth2-bearer tok pop3://127.0.0.1:36211/`
  3. `curl -v -u alice:pass --oauth2-bearer tok --login-options AUTH=PLAIN imap://127.0.0.1:36243/`
  4. `curl -v -u alice:pass --oauth2-bearer tok --login-options "AUTH=*" imap://127.0.0.1:36243/`
- **Expected (REF 8.22.0):** 1 and 2: `curl: (67) Login denied`, and no credential is sent after `CAPABILITY` / `CAPA`. 3: `A002 AUTHENTICATE PLAIN …`, exit 0. 4: `AUTHENTICATE DIGEST-MD5`, exit 0.
- **Actual (.NET):** 1: sends `A002 LOGIN alice pass` in cleartext, exit 0. 2: sends `APOP alice <digest>` then `LIST`, exit 0. 3: `curl: (67) Login denied`. 4: sends `LOGIN alice pass`.
- **Likely location:** `Curl.Original.Protocol.Imap.UnitLibrary/ImapAuthentication.cs` (`IsPreferred`, `MayLogIn`: the LOGIN fallback is allowed whenever a bearer token is set) and `Curl.Original.Protocol.Pop3.UnitLibrary/Pop3Login.cs` (`BearerMechanisms` handling). With a bearer token and no explicit `AUTH=`, only XOAUTH2/OAUTHBEARER may be used; an explicit `AUTH=` choice must be honoured.
- **Acceptance check:** repro 1 exits 67 `Login denied` and the `-v` output has no `LOGIN` line; repro 2 exits 67 with no `APOP` or `USER` line; repro 3 sends `AUTHENTICATE PLAIN` and exits 0.

## Acceptance criteria

- [x] repro 1 exits 67 `Login denied` and the `-v` output has no `LOGIN` line; repro 2 exits 67 with no `APOP` or `USER` line; repro 3 sends `AUTHENTICATE PLAIN` and exits 0.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Measured curl 8.22.0 (Schannel) with `Record-CurlExchange.ps1`; the table is in ADR-1361. Curl now sends the same commands in every case measured except one, no `-u` with `AUTH=PLAIN` (older behaviour, filed as BL-5925 with the withheld-token cases). The `-v` `no overlap` line and `curl: (67) Login denied` match for IMAP and POP3.
- The three repros, run against the recorder with Curl: 1 exits 67 after `A001 CAPABILITY` only; 2 exits 67 after `CAPA` only; 3 sends `AUTHENTICATE PLAIN` and exits 0.
- Done directly, without the feature pipeline's subagents, to stay inside the lane's cost cap.
- `touches` widened: refusing `AUTH=PLAIN` came from `SaslMechanismRanking` (never a password mechanism with a token), which also affected SMTP, and POP3 could not tell `AUTH=*` from no option (`Pop3LoginOptions`). No other task in Doing named any added file. SMTP needed no code change: it already passed the `AUTH=` value as the required mechanism, and its measured answers now match.
- Quality: `Measure-CodeQuality.ps1` for Imap, Pop3 and Authentication, 0 failing members. `Curl.Protocol.Abstractions` changed only a doc comment. Fast tests: the one failure of the full run (an IMAP assertion expecting the old `RequiredMechanism`) was updated and that project rerun green.
- Platform: no test or output added depends on the platform. `dotnet format whitespace` on the changed files is clean; the full `dotnet format` did not finish within 10 minutes while other lanes built.

## Log

- 2026-11-24: Created from fitness review 2026-11-24T1358.
- 2026-11-24: Backlog -> Doing. Criteria at claim: 742311db 69116afe 3d836d5e.
- 2026-11-24: Doing -> Done. IMAP and POP3 with --oauth2-bearer send no password unless an AUTH= login option allows it, and AUTH= choices are honoured as curl 8.22.0 does
