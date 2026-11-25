---
id: BL-5968
title: Fail SASL DIGEST-MD5 with an empty user on Windows as curl's SSPI build does
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Authentication.UnitLibrary/SaslAuthenticator.cs, Curl.Authentication.UnitLibrary/SaslDigestMd5.cs, Curl.Authentication.UnitTests/SaslAuthenticatorTests.cs]
requirement: none
created: 2026-11-25
completed:
---
# BL-5968 — Fail SASL DIGEST-MD5 with an empty user on Windows as curl's SSPI build does

## Goal

On Windows, SASL DIGEST-MD5 with an empty user ends as curl 8.22.0's Schannel build does: nothing is sent after the challenge and the transfer exits 67.

## Context

Found in BL-5963. Measured with `Record-CurlExchange.ps1 -Smtp -SmtpReply 'EHLO=250-localhost\r\n250 AUTH DIGEST-MD5'`, both with `--oauth2-bearer tok --login-options AUTH=DIGEST-MD5` (no `-u`) and with `-u ':' --login-options AUTH=DIGEST-MD5`, URL `smtp://127.0.0.1:<port>/`: curl sends `EHLO`, `AUTH DIGEST-MD5`, then nothing, and exits 67. Curl answers the challenge with `username=""` and exits 0. With `-u u:p` both answer and exit 0. curl passes SSPI no identity for an empty user, so SSPI uses the logged-on account (`Curl_auth_create_digest_md5_message`, the SSPI build). The OpenSSL build on Linux and macOS has its own DIGEST-MD5 code (`SaslDigestMd5.AnswerAsCurl`); keep its behaviour unless a measurement says otherwise. `AnswerAsSspi` returning `null` becomes exit 94, so this case needs its own path to exit 67.

## Acceptance criteria

- [ ] With the SSPI behaviour selected and an empty user, the DIGEST-MD5 exchange gives no answer and the handler's transfer exits 67, pinned by a test in `Curl.Authentication.UnitTests`.
- [ ] With the SSPI behaviour not selected, an empty user is still answered, pinned by a test.
- [ ] Curl's SMTP transcript and exit code match real curl for both command lines above, recorded under Notes.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Authentication.UnitLibrary` reports no failing member, `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: 73e6648d 04c510bc 71fbd602 7c8712e7.
