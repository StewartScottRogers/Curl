---
id: BL-774
title: Cancel an SMTP SASL exchange with * on an undecodable challenge and try the next mechanism
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-537]
touches: [Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed:
---
# BL-774 — Cancel an SMTP SASL exchange with * on an undecodable challenge and try the next mechanism

## Goal

When an SMTP server sends a 334 challenge that a challenge-decoding mechanism (CRAM-MD5, DIGEST-MD5) cannot decode, `SmtpSaslAuthentication` in `Curl.Protocol.Smtp.UnitLibrary` sends `*` to cancel, reads the server's reply whatever it is, drops that mechanism from the offered set and starts again with the next mechanism curl would choose, ending in exit 67 when none remains, byte for byte as curl 8.21.0 does.

## Context

- BL-541 / ADR-0133 (`Documentation/Planning/Decisions/ADR-0133-smtp-auth-follows-curls-framing-and-fails-every-refusal-with-67.md`) made `SmtpSaslAuthentication` hand an undecodable 334 challenge (not valid base64) to the `ISaslExchange` as empty, and treat `ISaslExchange.Respond` returning null as exit 67 "Login denied" with nothing sent. That matches curl 8.21.0 for PLAIN, LOGIN, EXTERNAL, XOAUTH2 and OAUTHBEARER, which never read their challenge.
- For mechanisms that decode the challenge (CRAM-MD5 and DIGEST-MD5, answered by BL-537), curl's `lib/sasl.c` answers a bad challenge differently: it sends `*` (the SASL cancel), reads the server's reply whatever it is, removes that mechanism from the offered set and starts over with the next mechanism it would choose. When no mechanism remains it fails with exit 67 (`CURLE_LOGIN_DENIED`; stderr "Login denied" or "Authentication cancelled" - measure which).
- Upstream references: https://curl.se/libcurl/c/libcurl-errors.html (`CURLE_LOGIN_DENIED` 67), https://curl.se/docs/manpage.html (`--login-options`, `--sasl-authzid`), and `lib/sasl.c` in curl 8.21.0 (`SASL_CANCEL` state).
- Where to start: `Curl.Protocol.Smtp.UnitLibrary/SmtpSaslAuthentication.cs`; tests in `Curl.Protocol.Smtp.UnitTests/SmtpProtocolHandlerAuthenticationTests.cs` with `Fakes/SmtpRun.cs` and `Fakes/FakeSaslAuthenticator.cs`.
- Measure with `Record-CurlExchange.ps1 -Smtp`; extend the script if it cannot script a non-base64 334 reply, rather than writing a new server.
- If ADR-0133 is amended, add `Documentation/Planning/Decisions` to this task's `touches` before editing it.

## Acceptance criteria

- [x] Before any code change, `Notes` holds curl 8.21.0's request lines, stderr and exit code measured with `Record-CurlExchange.ps1 -Smtp` for two cases: (a) server offers `AUTH CRAM-MD5 PLAIN` and answers `AUTH CRAM-MD5` with a 334 whose text is not valid base64; (b) server offers only `AUTH CRAM-MD5` with the same bad challenge.
- [ ] Tests in `Curl.Protocol.Smtp.UnitTests`, driven through a fake connection and a fake SASL authenticator, pin for case (a) the `*` cancel line, the read of the server's reply, the retry `AUTH PLAIN ...` and success; and for case (b) the `*` cancel line, the final exit code (`CurlExitCode` 67 unless the measurement says otherwise) and stderr text, all as measured.
- [ ] Mechanisms that do not decode their challenge (PLAIN, LOGIN, EXTERNAL, XOAUTH2, OAUTHBEARER) keep the ADR-0133 behaviour; an existing or new test pins it.
- [ ] ADR-0133 has an amendment section stating the cancel-and-retry behaviour for challenge-decoding mechanisms and the curl version measured.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes; no test needs `TestCategory=Integration`.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Smtp.UnitLibrary` reports 100% line and 100% branch coverage and no failing member.

## Notes

### Measured (curl 8.21.0 mingw Schannel, `Record-CurlExchange.ps1 -Smtp`, 2026-09-29, `-u user:secret`, no `--sasl-ir`)

(a) `-SmtpReply 'EHLO=250-localhost\r\n250 AUTH CRAM-MD5 PLAIN','AUTH=334 !!!notbase64','AUTH=334 ','*=501 cancelled','AHVZZXIAC2VJCMV0=235 ok'`:
`AUTH CRAM-MD5` / `< 334 !!!notbase64` / `*` / `< 501 cancelled` / `AUTH PLAIN` / `< 334 ` / `AHVzZXIAc2VjcmV0` / `< 235 ok` / `MAIL FROM:<a@b>` ... `QUIT`; stderr empty, exit 0.

(b) `-SmtpReply 'EHLO=250-localhost\r\n250 AUTH CRAM-MD5','AUTH=334 !!!notbase64','*=501 cancelled'`:
`AUTH CRAM-MD5` / `< 334 !!!notbase64` / `*` / `< 501 cancelled`, then nothing (no QUIT); stderr `curl: (67) Authentication cancelled`, exit 67.

Extra: EHLO `AUTH NTLM DIGEST-MD5 LOGIN`, every AUTH answered `334 !!!notbase64`, `*` answered `334 odd`:
`AUTH DIGEST-MD5` / `*` / `< 334 odd` (read and ignored) / `AUTH NTLM` / `< 334 !!!notbase64` / Type 1 sent (NTLM's first 334 is not read) / 502 -> exit 67 `Login denied`.
Extra: DIGEST-MD5 with a valid-base64 but meaningless challenge (`bm9ub25jZT0x`) -> exit 94 on the Schannel (SSPI) build, nothing sent; out of scope here (not a base64 failure), `Respond` null keeps ADR-0133's 67.

### Design (implemented, uncommitted in lane-2 - see Log)

From curl's `lib/sasl.c`: `get_server_message` treats empty text or text starting `=` as an empty message, and a base64 failure is `CURLE_BAD_CONTENT_ENCODING`, which `Curl_sasl_continue` answers with `*` (`SASL_CANCEL`); on the next reply, whatever it is, the mechanism is XORed out of `authmechs` and `Curl_sasl_start` runs again; no mechanism left is `SASL_IDLE`, which `smtp_state_auth_resp` fails as 67 "Authentication cancelled". Mechanisms that call `get_server_message`: GSSAPI (every challenge), CRAM-MD5, DIGEST-MD5 (first only; `rspauth` is not read) and NTLM (Type 2, the first challenge its exchange answers, since Type 1 is the pending initial response).

- `SmtpSaslAuthentication`: `ExchangeAsync` returns `ExchangeOutcome` (Accepted/Refused/Cancelled); `AuthenticateAsync` loops `ChooseMechanism` over `offered`, removing a cancelled mechanism; none left after a cancel -> `SmtpSessionMessages.AuthenticationCancelled`. `DecodeChallenge(reply, mechanism, index)` returns null (cancel) only for a non-base64 challenge that `ReadsChallenge` (GSSAPI any index; CRAM-MD5/DIGEST-MD5/NTLM index 0). Split into `SendAuthAsync`, `AnswerAsync`, `CancelAsync`, `ReadReplyAsync`, `Outcome` to keep complexity <= 10.
- Tests: `Curl.Protocol.Smtp.UnitTests/SmtpProtocolHandlerSaslCancelTests.cs` with fake `Fakes/RankedSaslAuthenticator.cs` (cases a and b, reply to `*` of 501/334/235, every mechanism cancelled, cancel then refused, server closes after `*`, empty/`=` CRAM-MD5 challenges, DIGEST-MD5 rspauth, NTLM Type 2, GSSAPI later challenge, and PLAIN/LOGIN/EXTERNAL/XOAUTH2/OAUTHBEARER still handed an empty challenge).
- Verified in lane-2 on 2026-09-29: Smtp.UnitTests 209 passed; `Measure-CodeQuality.ps1 -Library Curl.Protocol.Smtp.UnitLibrary` 100% line, 100% branch, 0 failing members.

### ADR-0133 amendment to add (text ready)

```
## Amendment - BL-774, 2026-09-29

Decided by Claude under Stewart's delegation. Measured against curl 8.21.0 (mingw,
Schannel) with `Record-CurlExchange.ps1 -Smtp`: point 7 now holds only for mechanisms that
ignore their challenge (PLAIN, LOGIN, EXTERNAL, XOAUTH2, OAUTHBEARER). A `334` whose text
is not base64 (empty text or text starting `=` is an empty challenge, not a bad one), for a
mechanism that reads it - every GSSAPI challenge, the first CRAM-MD5, DIGEST-MD5 and NTLM
answer - is cancelled with `*`. The server's reply to `*` is read whatever it is (501, 334
and 235 alike), the mechanism is dropped from the offered set and the authenticator chooses
again; with none left the transfer fails with exit 67 `Authentication cancelled` and no
`QUIT`. A challenge the exchange cannot answer (`Respond` null) is still 67 `Login denied`.
```

### Why back in Backlog

Criterion 4 needs `Documentation/Planning/Decisions` (ADR-0133); BL-610, in Doing, touches that folder. Per the shift rules the folder is added to `touches` and the task returns to Backlog; the code above is left uncommitted for the shift to stash, and a rerun needs only to restore it (or redo it from these Notes), paste the amendment, run the gates and tick the boxes.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Backlog. Needs Documentation/Planning/Decisions (ADR-0133 amendment), which BL-610 in Doing touches; code and tests done and uncommitted, see Notes
