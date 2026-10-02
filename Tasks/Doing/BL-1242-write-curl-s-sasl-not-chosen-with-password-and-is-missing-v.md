---
id: BL-1242
title: Write curl's SASL 'not chosen with password' and 'is missing' -v lines when no SMTP mechanism can be used
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1242 — Write curl's SASL 'not chosen with password' and 'is missing' -v lines when no SMTP mechanism can be used

## Goal

When an SMTP login fails with exit 67 because no offered mechanism can be used, `SmtpSaslAuthentication` writes the `-v` lines curl 8.21.0's `Curl_sasl_is_blocked` writes: `SASL: no auth mechanism offered could be selected` followed by `SASL: auth EXTERNAL not chosen with password` and each `SASL: <mechanism> is missing <what>` / `not builtin` line, instead of `no overlap` whenever a mechanism curl knows was offered.

## Context

- Today `Curl.Protocol.Smtp.UnitLibrary/SmtpSaslAuthentication.cs` `ReportNoWayToLogIn` (BL-1061) writes `no auth mechanism offered could be selected` only when the offered mechanisms the login options allow are SCRAM (`not builtin`), and otherwise `SASL: no overlap between offered and configured auth mechanisms` as soon as any known mechanism was offered. The line texts live in `SmtpConnectionInfoLines.cs`.
- curl 8.21.0, `lib/curl_sasl.c` at `curl-8_21_0` (https://github.com/curl/curl/blob/curl-8_21_0/lib/curl_sasl.c), `Curl_sasl_is_blocked` (lines ~846-905) and `sasl_unchosen` (lines ~812-843). `enabled` is the offered mechanisms the login options allow (`AUTH=` absent means every mechanism but EXTERNAL; `AUTH=*` includes EXTERNAL). None offered or recognised: `no auth mechanism was offered or recognized`; offered but none enabled: `no overlap between offered and configured auth mechanisms`; otherwise `no auth mechanism offered could be selected`, then `SASL: auth EXTERNAL not chosen with password` when EXTERNAL is enabled and a password was given (`sasl_choose_external`, lines ~300-310, picks EXTERNAL only without one), then, for each enabled mechanism in the order GSSAPI, SCRAM-SHA-256, SCRAM-SHA-1, DIGEST-MD5, CRAM-MD5, NTLM, OAUTHBEARER, XOAUTH2: `SASL: <name> not builtin` when the build lacks it, `SASL: <name> not supported by the platform/libraries` when the platform does, else `SASL: <name> is missing CURLOPT_XOAUTH2_BEARER` for OAUTHBEARER and XOAUTH2 without `--oauth2-bearer`, and `SASL: <name> is missing username` without a user.
- Measured 2026-10-02, curl 8.21.0 (mingw, Schannel), `Record-CurlExchange.ps1 -Smtp -CurlArgs '-v','-u','user:secret','--mail-from','a@b','--mail-rcpt','c@d','-T','NUL','smtp://127.0.0.1:<port>/'`:
  - `-SmtpReply 'EHLO=250-localhost\r\n250 AUTH XOAUTH2'`: `* SASL: no auth mechanism offered could be selected` / `* SASL: XOAUTH2 is missing CURLOPT_XOAUTH2_BEARER` / `curl: (67) Login denied`, exit 67.
  - the same with `--login-options AUTH=EXTERNAL` and `-SmtpReply 'EHLO=250-localhost\r\n250 AUTH EXTERNAL'`: `* SASL: no auth mechanism offered could be selected` / `* SASL: auth EXTERNAL not chosen with password` / `curl: (67) Login denied`, exit 67.
- Keep BL-1061's SCRAM lines and their platform handling as they are; the new lines join the same list in curl's order.

## Acceptance criteria

- [ ] Before the code change, `Notes` records (with `Record-CurlExchange.ps1 -Smtp`) curl's lines for two more cases: OAUTHBEARER offered with `-u user:secret`, and `AUTH OAUTHBEARER XOAUTH2 SCRAM-SHA-1` offered with `-u user:secret`.
- [ ] Tests in `Curl.Protocol.Smtp.UnitTests` pin both measured cases above and the two newly measured ones: the `-v` lines in order, nothing sent after `EHLO`, and exit 67 `Login denied`.
- [ ] The existing BL-1061 tests and every other SMTP test pass unchanged.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes with no test needing `TestCategory=Integration`; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Smtp.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- The same gap exists in IMAP (BL-1219) and POP3 (BL-1221); each library keeps its own copy, as a protocol library never references another.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
