---
id: BL-1219
title: Write curl's SASL 'not chosen with password' and 'is missing' -v lines when no IMAP mechanism can be used
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Imap.UnitLibrary, Curl.Protocol.Imap.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1219 — Write curl's SASL 'not chosen with password' and 'is missing' -v lines when no IMAP mechanism can be used

## Goal

When an IMAP login fails with exit 67 because no offered mechanism can be used, `ImapAuthentication` writes the `-v` lines curl 8.21.0's `Curl_sasl_is_blocked` writes: `SASL: no auth mechanism offered could be selected` followed by `SASL: auth EXTERNAL not chosen with password` and each `SASL: <mechanism> is missing <what>` / `not builtin` line, instead of `no overlap` whenever a mechanism curl knows was offered.

## Context

- Today `Curl.Protocol.Imap.UnitLibrary/ImapAuthentication.cs` `NoWayToLogIn` (BL-1060) writes `no auth mechanism offered could be selected` only for SCRAM (`not builtin`) and otherwise `SASL: no overlap between offered and configured auth mechanisms` once any known mechanism was offered. The texts live in `ImapInfoLines.cs`.
- curl 8.21.0, `lib/curl_sasl.c` at `curl-8_21_0` (https://github.com/curl/curl/blob/curl-8_21_0/lib/curl_sasl.c), `Curl_sasl_is_blocked` (lines ~846-905) and `sasl_unchosen` (lines ~812-843). `enabled` is the offered mechanisms the login options allow (`AUTH=` absent means every mechanism but EXTERNAL). None offered or recognised: `no auth mechanism was offered or recognized`; none enabled: `no overlap between offered and configured auth mechanisms`; otherwise `no auth mechanism offered could be selected`, then `SASL: auth EXTERNAL not chosen with password` when EXTERNAL is enabled and a password was given, then, for each enabled mechanism in the order GSSAPI, SCRAM-SHA-256, SCRAM-SHA-1, DIGEST-MD5, CRAM-MD5, NTLM, OAUTHBEARER, XOAUTH2: `not builtin`, `not supported by the platform/libraries`, `is missing CURLOPT_XOAUTH2_BEARER` (OAUTHBEARER and XOAUTH2 without `--oauth2-bearer`) or `is missing username`.
- Measured 2026-10-02, curl 8.21.0 (mingw, Schannel), `Record-CurlExchange.ps1 -Imap -ImapReply 'GREETING=* OK [CAPABILITY IMAP4rev1 AUTH=XOAUTH2 LOGINDISABLED] ready','CAPABILITY=* CAPABILITY IMAP4rev1 AUTH=XOAUTH2 LOGINDISABLED\r\nOK done' -CurlArgs '-v','-u','user:secret','imap://127.0.0.1:<port>/INBOX'`: `> A001 CAPABILITY`, then `* SASL: no auth mechanism offered could be selected` / `* SASL: XOAUTH2 is missing CURLOPT_XOAUTH2_BEARER` / `curl: (67) Login denied`, exit 67. (Pass `-ImapReply` values from a `.ps1` file: through `bash` the array arrives as one string.)
- The SMTP twin is BL-1242; keep this library's own copy.

## Acceptance criteria

- [ ] Before the code change, `Notes` records curl's lines for `AUTH=EXTERNAL LOGINDISABLED` offered with `--login-options AUTH=EXTERNAL -u user:secret`, and for `AUTH=OAUTHBEARER AUTH=SCRAM-SHA-1 LOGINDISABLED` with `-u user:secret`.
- [ ] Tests in `Curl.Protocol.Imap.UnitTests` pin the measured XOAUTH2 case and the two newly measured ones: the `-v` lines in order, nothing sent after `CAPABILITY`, and exit 67 `Login denied`.
- [ ] The existing BL-1060 tests and every other IMAP test pass unchanged.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes with no test needing `TestCategory=Integration`; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Imap.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-10-02: Created.
