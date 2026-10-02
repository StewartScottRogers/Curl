---
id: BL-1137
title: Print curl's incoming NTLM message too big when an NTLM AUTHENTICATE message's responses overflow
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1114]
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests]
requirement: none
created: 2026-10-01
completed: 2026-10-01
---
# BL-1137 — Print curl's incoming NTLM message too big when an NTLM AUTHENTICATE message's responses overflow

## Goal

When the NTLMv2 response (which carries the challenge's whole target information) takes the AUTHENTICATE message past 1024 bytes, Curl fails with exit 100 and prints curl's `incoming NTLM message too big`, not `user + domain + hostname too big for NTLM`.

## Context

- curl 8.21.0 `lib/vauth/ntlm.c` `Curl_auth_create_ntlm_type3_message` lines 776-803: the responses check prints `incoming NTLM message too big`, the names check `user + domain + hostname too big for NTLM`; both return `CURLE_TOO_LARGE`.
- BL-1114 added `NtlmAuthenticateMessage.TryEncode(out byte[]? message, out NtlmMessageFailure failure)` with `NtlmMessageFailure.ResponsesTooLarge` and `NamesTooLarge`.
- `Curl.Authentication.UnitLibrary/HandBuiltNtlmSecurityContext.cs` calls the old overload and always reports `NtlmHttpAuthenticator.Type3TooLargeMessage`. Switch it to the new overload and add a constant for the responses message.

## Acceptance criteria

- [x] A test in `Curl.Authentication.UnitTests` with a CHALLENGE whose target information takes the NTLMv2 response past 1024 bytes gets exit 100 (`CurlExitCode.TooLarge`) and `incoming NTLM message too big`.
- [x] The existing too-long user name test still gets `user + domain + hostname too big for NTLM`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean; the fast tests pass; `Measure-CodeQuality.ps1 -Library Curl.Authentication.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- 2026-10-01: Already delivered by BL-1128 (commit 33ec518d), which this task duplicates: `HandBuiltNtlmSecurityContext` uses the `TryEncode(out message, out failure)` overload and keeps `AnswerRefusedBecause`; `NtlmHttpAuthenticator.ResponsesTooLargeMessage` is `incoming NTLM message too big`. Tests `CreateAuthorizationAsync_TargetInformationPushesResponsesPastCurlsBuffer_FailsWithIncomingMessageTooBig` and the existing too-long user name test (`NtlmHttpAuthenticatorTests.cs:140`) cover both criteria. Verified here: build -warnaserror clean, all fast tests green (Authentication 738 passed, 4 skipped), Measure-CodeQuality 100% line and branch, 0 failing members. No code change.

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. Already delivered by BL-1128: responses overflow prints incoming NTLM message too big, exit 100; gates re-verified
