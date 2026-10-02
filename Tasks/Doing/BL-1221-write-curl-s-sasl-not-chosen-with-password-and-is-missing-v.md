---
id: BL-1221
title: Write curl's SASL 'not chosen with password' and 'is missing' -v lines when no POP3 mechanism can be used
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Pop3.UnitLibrary, Curl.Protocol.Pop3.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1221 — Write curl's SASL 'not chosen with password' and 'is missing' -v lines when no POP3 mechanism can be used

## Goal

When a POP3 login fails with exit 67 because no offered SASL mechanism can be used and neither APOP nor `USER` is possible, `Pop3Login` writes the `-v` lines curl 8.21.0's `Curl_sasl_is_blocked` writes: `SASL: no auth mechanism offered could be selected` followed by `SASL: auth EXTERNAL not chosen with password` and each `SASL: <mechanism> is missing <what>` / `not builtin` line, instead of `no overlap` whenever a mechanism curl knows was offered.

## Context

- Today `Curl.Protocol.Pop3.UnitLibrary/Pop3Login.cs` `NoWayToLogIn(Pop3LoginOptions, Pop3Capabilities?)` (BL-810) writes `no auth mechanism offered could be selected` only for SCRAM (`not builtin`) and otherwise `SASL: no overlap between offered and configured auth mechanisms` once `CAPA` listed any known mechanism. The texts live in `Pop3SessionMessages.cs`.
- curl 8.21.0, `lib/curl_sasl.c` at `curl-8_21_0` (https://github.com/curl/curl/blob/curl-8_21_0/lib/curl_sasl.c), `Curl_sasl_is_blocked` (lines ~846-905) and `sasl_unchosen` (lines ~812-843): none offered or recognised gives `no auth mechanism was offered or recognized`; offered but none the login options allow (`AUTH=` absent means every mechanism but EXTERNAL) gives `no overlap between offered and configured auth mechanisms`; otherwise `no auth mechanism offered could be selected`, then `SASL: auth EXTERNAL not chosen with password` when EXTERNAL is allowed and a password was given, then for each allowed offered mechanism in the order GSSAPI, SCRAM-SHA-256, SCRAM-SHA-1, DIGEST-MD5, CRAM-MD5, NTLM, OAUTHBEARER, XOAUTH2: `not builtin`, `not supported by the platform/libraries`, `is missing CURLOPT_XOAUTH2_BEARER` (OAUTHBEARER and XOAUTH2 without `--oauth2-bearer`) or `is missing username`.
- Measured 2026-10-02, curl 8.21.0 (mingw, Schannel), `Record-CurlExchange.ps1 -Pop3 -Pop3Reply 'GREETING=+OK POP3 ready','CAPA=+OK\r\nSASL XOAUTH2\r\n.' -CurlArgs '-v','-u','user:secret','pop3://127.0.0.1:<port>/1'` (array values passed from a `.ps1` file): `> CAPA`, then `* SASL: no auth mechanism offered could be selected` / `* SASL: XOAUTH2 is missing CURLOPT_XOAUTH2_BEARER` / `curl: (67) Login denied`, exit 67.
- The SMTP and IMAP twins are BL-1242 and BL-1219; keep this library's own copy.

## Acceptance criteria

- [ ] Before the code change, `Notes` records curl's lines for `CAPA` listing `SASL EXTERNAL` with `--login-options AUTH=EXTERNAL -u user:secret`, and `SASL OAUTHBEARER SCRAM-SHA-1` with `-u user:secret`.
- [ ] Tests in `Curl.Protocol.Pop3.UnitTests` pin the measured XOAUTH2 case and the two newly measured ones: the `-v` lines in order, nothing sent after `CAPA`, and exit 67 `Login denied`.
- [ ] The existing BL-810 tests and every other POP3 test pass unchanged.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes with no test needing `TestCategory=Integration`; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Pop3.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
