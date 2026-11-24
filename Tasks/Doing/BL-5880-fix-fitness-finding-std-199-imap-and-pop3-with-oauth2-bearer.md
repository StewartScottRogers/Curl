---
id: BL-5880
title: Fix fitness finding STD-199: IMAP and POP3 with --oauth2-bearer fall back to a cleartext password LOGIN or APOP when the server offers no OAuth mechanism, and refuse an explicit AUTH=PLAIN
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Imap.UnitLibrary/ImapAuthentication.cs, Curl.Original.Protocol.Pop3.UnitLibrary/Pop3Login.cs]
requirement: none
created: 2026-11-24
completed:
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

- [ ] repro 1 exits 67 `Login denied` and the `-v` output has no `LOGIN` line; repro 2 exits 67 with no `APOP` or `USER` line; repro 3 sends `AUTHENTICATE PLAIN` and exits 0.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-24: Created from fitness review 2026-11-24T1358.
- 2026-11-24: Backlog -> Doing. Criteria at claim: 742311db 69116afe 3d836d5e.
