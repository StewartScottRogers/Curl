---
id: BL-781
title: Carry SSPI's exit 94 out of a SASL exchange the Schannel build rejects
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-537]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests, Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests, Curl.Protocol.Imap.UnitLibrary, Curl.Protocol.Imap.UnitTests, Curl.Protocol.Pop3.UnitLibrary, Curl.Protocol.Pop3.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-781 — Carry SSPI's exit 94 out of a SASL exchange the Schannel build rejects

## Goal

On Windows, a DIGEST-MD5 challenge that SSPI rejects fails the transfer with exit 94 (`CURLE_AUTH_ERROR`, "An authentication function returned an error") as curl 8.21.0's Schannel build does, instead of the exit 67 a cancelled exchange gives.

## Context

- BL-537 measured it (Notes there, ADR-0139): with `Record-CurlExchange.ps1 -Smtp -SmtpReply 'AUTH=334 <challenge>'`, the Schannel build exits 94 for a DIGEST-MD5 challenge with no `nonce`, no `algorithm`, or `qop="auth-int"` alone, sending nothing after the challenge; the OpenSSL build cancels with `*` and exits 67.
- `ISaslExchange.Respond` (`Curl.Protocol.Abstractions.UnitLibrary`) can only answer bytes or `null`, which every mail handler turns into `*` and exit 67. `SaslDigestMd5.AnswerAsSspi` returns `null` for these cases for now.
- The contract needs a third outcome ("fail with this exit code, send nothing"), and the SMTP, IMAP and POP3 handlers must honour it. Measure what the Schannel build writes to the server (if anything) and its stderr before pinning.

## Acceptance criteria

- [x] `ISaslExchange` can report an authentication-function failure, documented in its XML comments.
- [x] `SaslAuthenticator` constructed for SSPI reports it for the three measured challenges; the OpenSSL path still answers `null`.
- [x] SMTP, IMAP and POP3 handler tests pin exit 94 and the bytes sent, matching the measured Schannel exchange.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports no failing member in the touched libraries.

## Notes

- **Touches widened** (no task in Doing names it): `Documentation/Planning/Decisions`, for
  ADR-0139's BL-781 amendment and its index row.
- **Measured 2026-09-29**, curl 8.21.0 Schannel, `-u user:pencil`, challenge
  `realm="localhost",qop="auth",algorithm=md5-sess,charset=utf-8` (no nonce), with
  `Record-CurlExchange.ps1 -Smtp` (EHLO override `250 AUTH DIGEST-MD5 PLAIN`, `AUTH=334 <b64>`),
  `-Imap` (GREETING and CAPABILITY overrides with `AUTH=DIGEST-MD5`, `AUTHENTICATE=+ <b64>\r\nNO failed`)
  and `-Pop3` (CAPA override `SASL DIGEST-MD5 PLAIN`, `AUTH=+ <b64>`): every protocol sent
  nothing after `AUTH DIGEST-MD5` / `A002 AUTHENTICATE DIGEST-MD5` (no answer, no `*`, no
  QUIT or LOGOUT), exit 94, stderr `curl: (94) An authentication function returned an error\r\n`.
- **Decision** (ADR-0139 amendment, decided by Claude under Stewart's delegation): a new
  `SaslAuthenticationFailedException(CurlExitCode, message)` in Abstractions, which
  `ISaslExchange.RespondAsync` may throw - the same shape as HTTP's
  `HttpAuthenticationFailedException`, so no existing exchange changes. `SaslAuthenticator`
  throws it from the SSPI DIGEST-MD5 path when `SaslDigestMd5.AnswerAsSspi` rejects; the
  SMTP, IMAP and POP3 sessions catch it in `RunAsync` and return its exit code and message.
- `Begin_DigestMd5ChallengeCurlCancels_AnswersNull` used the OS-dependent authenticator; it
  now pins the OpenSSL path explicitly, so it holds on every platform.
- Tests: Abstractions 602, Authentication 608 (+3 skipped), SMTP 227, IMAP 334, POP3 211;
  whole fast run green. `Measure-CodeQuality.ps1` on the five libraries: 100% line, 100%
  branch, 0 failing members, worst CRAP 10.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. A DIGEST-MD5 challenge SSPI rejects now fails SMTP, IMAP and POP3 with exit 94 and sends nothing more, as curl's Schannel build does
