---
id: BL-541
title: Authenticate an SMTP session with AUTH and the SASL authenticator
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-540, BL-536]
touches: [Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-541 — Authenticate an SMTP session with AUTH and the SASL authenticator

## Goal

With `-u` (or `--oauth2-bearer`), the SMTP handler authenticates after `EHLO` (and after `STARTTLS`) with `AUTH <mech>` through the injected SASL authenticator, following `334` continuations, and fails a `535` or other refusal with curl 8.21.0's exit 67 and message.

## Context

- Conformance audit 2026-09-28, rows 23 and 34. SASL contract and choice: BL-533's ADR, BL-534, BL-536.
- Measure with `Record-CurlExchange.ps1 -Smtp`: `-u u:p` with `AUTH PLAIN LOGIN` offered, with `--sasl-ir`, with `--login-options AUTH=LOGIN`, with the server answering `535`, with no `AUTH` advertised but `-u` given, and with `--oauth2-bearer`.

## Acceptance criteria

- [x] Measured first as above; request lines, stderr and exit code copied into Notes.
- [x] `Curl.Protocol.Smtp.UnitTests` pin each case's client bytes and outcome through a fake connection and a fake SASL authenticator where the mechanism's bytes are not the point.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Smtp.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- **Touches widened** to `Documentation/Planning/Decisions` for ADR-0133 and its index row;
  no task in `Doing` (BL-510, BL-766) names it.
- **Measured** 2026-09-28, curl 8.21.0 (mingw, Schannel), `Record-CurlExchange.ps1 -Smtp`,
  curl running `-sS --mail-from a@b --mail-rcpt c@d -T mail.txt <args> smtp://127.0.0.1:<port>/x`,
  EHLO overridden to `250-localhost / 250-AUTH PLAIN LOGIN / 250 SIZE 1000` unless stated
  (`>` curl, `<` server):
  - `-u u:p`: `> AUTH PLAIN`, `< 334 `, `> AHUAcA==`, `< 235`, then `MAIL FROM`. Exit 0.
  - `-u u:p --sasl-ir`: `> AUTH PLAIN AHUAcA==`, `< 235`. Exit 0.
  - `--login-options AUTH=LOGIN`: `> AUTH LOGIN`, `< 334 VXNlcm5hbWU6`, `> dQ==`,
    `< 334 UGFzc3dvcmQ6`, `> cA==`, `< 235`. With `--sasl-ir`: `> AUTH LOGIN dQ==`,
    `< 334 UGFzc3dvcmQ6`, `> cA==`. Exit 0.
  - AUTH answered `535 5.7.8 bad` (with or without `--sasl-ir`), `504 nope`, or `250 ok`:
    stderr `curl: (67) Login denied`, exit 67, nothing more sent, no `QUIT`.
  - `--sasl-ir`, `AUTH PLAIN AHUAcA==` answered `334 `: exit 67, no reply sent.
  - EHLO without `AUTH`, with `-u u:p`: no `AUTH`, mail sent, exit 0. No `-u`: same.
  - `--oauth2-bearer tok -u u: --sasl-ir` with `AUTH OAUTHBEARER XOAUTH2 PLAIN LOGIN`:
    `> AUTH OAUTHBEARER bixhPXUsAWhvc3Q9MTI3LjAuMC4xAXBvcnQ9MTgzMTABYXV0aD1CZWFyZXIgdG9rAQE=`,
    exit 0. Without `--sasl-ir`: `> AUTH OAUTHBEARER`, `< 235` at once, exit 67 `Login denied`.
    Without `-u`: `n,a=,...`, exit 0. `AUTH=XOAUTH2`: `> AUTH XOAUTH2 dXNlcj11AWF1dGg9QmVhcmVyIHRvawEB`.
  - `--login-options AUTH=EXTERNAL --sasl-ir`, no `-u`: `> AUTH EXTERNAL =`, exit 0.
  - `-u : --sasl-ir --login-options AUTH=LOGIN`: `> AUTH LOGIN =`, `> =`.
  - `smtp://u:p;AUTH=LOGIN@127.0.0.1:<port>/x`: `> AUTH LOGIN`.
  - `250 AUTH PLAIN`, `250 AUTH LOGIN`, `250 AUTH LOGIN PLAIN` as the final line: every
    mechanism used (ADR-0123's last-mechanism quirk does not reproduce). `250-auth PLAIN`:
    `AUTH PLAIN`. `250-AUTH=PLAIN`, `250-AUTH<tab>PLAIN`: no `AUTH`. Two `AUTH` lines: joined.
    `250-AUTH PLAINX LOGIN`: `AUTH LOGIN`. `250-AUTH ` bare: exit 67 `Login denied`, no `AUTH`.
  - EHLO refused, HELO accepted: no `AUTH`. Server hangs up after `AUTH PLAIN`: exit 56
    `response reading failed (errno: 0)`.
  - `--sasl-ir` with a 368-`u` user (base64 496): inline; 371 `u`s (base64 500): after `334 `.
- **Decisions** (ADR-0133, decided by Claude under Stewart's delegation): generic framing
  in `SmtpSaslAuthentication`; a `null` from `Respond` is exit 67 with nothing sent (not `*`,
  which curl does not send for the built mechanisms); an undecodable challenge is handed
  over empty; the first `AUTH=` login option wins.
- `Curl.Console` does not register `SmtpProtocolHandler` yet, so the new constructor taking
  an `ISaslAuthenticator` is wired by BL-545, which already requires `-u u:p` to reach
  `AUTH` through the composed authenticator.
- **Review:** no correctness bugs. `ISaslExchange`'s docs (Abstractions, which BL-510 holds)
  still say LOGIN has no initial response and that `null` from `Respond` means cancelling with `*`.
  Both are noted on BL-751, which already rewrites those docs.
- **Follow-up filed:** BL-774 (cancel with `*` and try the next mechanism on an
  undecodable challenge, once BL-537 builds CRAM-MD5 and DIGEST-MD5).
- **Verified:** `dotnet build Curl.slnx -warnaserror` 0 warnings; fast tests green
  (Curl.Protocol.Smtp.UnitTests 124 passed); `Measure-CodeQuality.ps1 -Library
  Curl.Protocol.Smtp.UnitLibrary`: 100% line, 100% branch, 0 failing members, worst CRAP 10.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. SMTP authenticates with AUTH through the injected SASL authenticator after EHLO and STARTTLS, following 334s, and fails refusals with 67 Login denied as curl 8.21.0 does
