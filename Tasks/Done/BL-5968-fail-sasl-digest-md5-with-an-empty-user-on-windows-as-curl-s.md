---
id: BL-5968
title: Fail SASL DIGEST-MD5 with an empty user on Windows as curl's SSPI build does
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Authentication.UnitLibrary/SaslAuthenticator.cs, Curl.Authentication.UnitLibrary/SaslDigestMd5.cs, Curl.Authentication.UnitTests/SaslAuthenticatorTests.cs, Documentation/Planning/Decisions/ADR-1395-smtp-gives-a-bearer-token-without-a-user-an-empty-credential-and-a-withheld-token-no-mechanism.md]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-5968 — Fail SASL DIGEST-MD5 with an empty user on Windows as curl's SSPI build does

## Goal

On Windows, SASL DIGEST-MD5 with an empty user ends as curl 8.22.0's Schannel build does: nothing is sent after the challenge and the transfer exits 67.

## Context

Found in BL-5963. Measured with `Record-CurlExchange.ps1 -Smtp -SmtpReply 'EHLO=250-localhost\r\n250 AUTH DIGEST-MD5'`, both with `--oauth2-bearer tok --login-options AUTH=DIGEST-MD5` (no `-u`) and with `-u ':' --login-options AUTH=DIGEST-MD5`, URL `smtp://127.0.0.1:<port>/`: curl sends `EHLO`, `AUTH DIGEST-MD5`, then nothing, and exits 67. Curl answers the challenge with `username=""` and exits 0. With `-u u:p` both answer and exit 0. curl passes SSPI no identity for an empty user, so SSPI uses the logged-on account (`Curl_auth_create_digest_md5_message`, the SSPI build). The OpenSSL build on Linux and macOS has its own DIGEST-MD5 code (`SaslDigestMd5.AnswerAsCurl`); keep its behaviour unless a measurement says otherwise. `AnswerAsSspi` returning `null` becomes exit 94, so this case needs its own path to exit 67.

## Acceptance criteria

- [x] With the SSPI behaviour selected and an empty user, the DIGEST-MD5 exchange gives no answer and the handler's transfer exits 67, pinned by a test in `Curl.Authentication.UnitTests`.
- [x] With the SSPI behaviour not selected, an empty user is still answered, pinned by a test.
- [x] Curl's SMTP transcript and exit code match real curl for both command lines above, recorded under Notes.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Authentication.UnitLibrary` reports no failing member, `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-11-25: `SaslAuthenticator` refuses the empty user before reading the challenge: no
  answer and no cancel reason, which the SMTP, POP3 and IMAP handlers end as exit 67 with
  nothing sent (ADR-1398). `SaslDigestMd5.cs` needed no change. The old test that expected
  an SSPI answer without a credential now covers the curl build only
  (`Begin_DigestMd5EmptyUserCurlBuild_AnswersWithAnEmptyUser`); the SSPI side is
  `Begin_DigestMd5EmptyUserSspi_GivesNoAnswerAndNoCancelReason`.
- Added ADR-1395 to `touches`: its Consequences said Curl still answered. No task in Doing
  names it.
- Measured against curl 8.22.0 (Schannel build) with
  `Record-CurlExchange.ps1 -Smtp -SmtpReply 'EHLO=250-localhost\r\n250 AUTH DIGEST-MD5'`,
  `-CurlArgs -sv <case> --login-options AUTH=DIGEST-MD5 smtp://127.0.0.1:18968/`; Curl's
  transcript and exit code matched in each case:
  - `--oauth2-bearer tok` (no `-u`), `-u :`, `-u :p`: `EHLO`, `AUTH DIGEST-MD5`, 334,
    nothing, exit 67.
  - `-u :` with a challenge without `nonce`: the same, exit 67 (`-u u:p` exits 94).
  - `-u :` with an empty challenge: `*`, `Authentication cancelled`, exit 67.
  - `-u u:p`: both answers sent, 235, exit 0 (`EHLO`, `AUTH`, answer, empty line, `HELP`,
    `QUIT`).
- 2026-11-25: `dotnet build` 0 warnings; fast tests passed (all test projects);
  `Measure-CodeQuality.ps1` 0 failing members.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: 73e6648d 04c510bc 71fbd602 7c8712e7.
- 2026-11-25: Doing -> Done. SASL DIGEST-MD5 with an empty user under the SSPI behaviour sends nothing and exits 67, as curl 8.22.0's Schannel build does
