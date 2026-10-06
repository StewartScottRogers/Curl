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
completed: 2026-10-02
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

- [x] Before the code change, `Notes` records curl's lines for `AUTH=EXTERNAL LOGINDISABLED` offered with `--login-options AUTH=EXTERNAL -u user:secret`, and for `AUTH=OAUTHBEARER AUTH=SCRAM-SHA-1 LOGINDISABLED` with `-u user:secret`.
- [x] Tests in `Curl.Protocol.Imap.UnitTests` pin the measured XOAUTH2 case and the two newly measured ones: the `-v` lines in order, nothing sent after `CAPABILITY`, and exit 67 `Login denied`.
- [x] The existing BL-1060 tests and every other IMAP test pass unchanged.
- [x] `dotnet build Curl.slnx -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes with no test needing `TestCategory=Integration`; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Imap.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Measured 2026-10-02 before the code change, curl 8.21.0 (mingw, Schannel), `Record-CurlExchange.ps1 -Imap` with the `-ImapReply` overrides passed from a `.ps1` file:
  - `AUTH=EXTERNAL LOGINDISABLED`, `-v --login-options AUTH=EXTERNAL -u user:secret`: `> A001 CAPABILITY`, `< A001 OK done`, `* SASL: no auth mechanism offered could be selected`, `* SASL: auth EXTERNAL not chosen with password`, `curl: (67) Login denied`, exit 67.
  - `AUTH=OAUTHBEARER AUTH=SCRAM-SHA-1 LOGINDISABLED`, `-v -u user:secret`: `> A001 CAPABILITY`, `< A001 OK done`, `* SASL: no auth mechanism offered could be selected`, `* SASL: SCRAM-SHA-1 not builtin`, `* SASL: OAUTHBEARER is missing CURLOPT_XOAUTH2_BEARER`, `curl: (67) Login denied`, exit 67.
  - Also `AUTH=GSSAPI LOGINDISABLED` with `-u user:secret`: only `could be selected`, no reason line (user without a domain, so GSSAPI is not chosen; `sasl_unchosen` has nothing to say with a user).
- curl's "enabled" set is offered & `prefmech`. `prefmech` is the `AUTH=` options when given (`AUTH=*` any but EXTERNAL, `AUTH=+LOGIN` none), else `OAUTHBEARER|XOAUTH2` with `--oauth2-bearer` (the tool sets `CURLAUTH_BEARER`, which `Curl_sasl_init` maps; this is why BL-1060 measured `no overlap` for a bearer against PLAIN/LOGIN), else any but EXTERNAL. Added `ImapLoginOptions.NamesMechanisms` and `Prefers`.
- Decided (sensible default): the per-mechanism lines follow `sasl_unchosen` with the Schannel build's macros as `lib/curl_sasl.c` reads them: GSSAPI built in and supported (only `is missing username`), SCRAM `not builtin`, and DIGEST-MD5, CRAM-MD5 and NTLM also `not builtin` because `CURL_SASL_DIGEST`/`CURL_SASL_NTLM` are inverted upstream. In practice curl picks DIGEST-MD5 even without a user (measured: `--oauth2-bearer tok --login-options AUTH=DIGEST-MD5;...` sent `AUTHENTICATE DIGEST-MD5`), so those lines are reached only through states the injected authenticator refuses; they are pinned from source, not measured.
- `ReportUnchosen` first measured cyclomatic complexity 18; split into `UnchosenLines` and `IsExternalRefusedForPassword`. Measure-CodeQuality: Imap library 100% line, 100% branch, 0 failing members, worst CRAP 10. `Curl.Protocol.Imap.UnitTests`: 392 passed (9 new rows in `ExecuteAsync_NoMechanismSelectable_WritesWhyEachWasNotChosen`; the BL-1060 rows unchanged).

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. IMAP exit 67 with no selectable SASL mechanism now writes curl's 'not chosen with password', 'is missing' and 'not builtin' -v lines
