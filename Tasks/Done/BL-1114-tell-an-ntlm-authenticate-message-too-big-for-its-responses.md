---
id: BL-1114
title: Tell an NTLM AUTHENTICATE message too big for its responses from one too big for its names
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Ntlm.UnitLibrary, Curl.Ntlm.UnitTests]
requirement: none
created: 2026-10-01
completed: 2026-10-01
---
# BL-1114 — Tell an NTLM AUTHENTICATE message too big for its responses from one too big for its names

## Goal

`NtlmAuthenticateMessage` says which of curl's two size checks refused a message - the responses overflowing the 1024-byte buffer (curl's `incoming NTLM message too big`) or the domain, user and workstation not fitting after them (curl's `user + domain + hostname too big for NTLM`) - so the caller can print the message curl prints; today both are one `false`.

## Context

- curl 8.21.0, `lib/vauth/ntlm.c` `Curl_auth_create_ntlm_type3_message` (lines 776-803 at https://github.com/curl/curl/blob/curl-8_21_0/lib/vauth/ntlm.c): `if(ntresplen + size > sizeof(ntlmbuf)) { failf(data, "incoming NTLM message too big"); result = CURLE_TOO_LARGE; }`, then `if(size + userlen + domlen + hostlen >= NTLM_BUFSIZE) { failf(data, "user + domain + hostname too big for NTLM"); result = CURLE_TOO_LARGE; }`. The NTLMv2 response carries the challenge's whole target information, so a server sending a large target info triggers the first message, not the second.
- Curl today: `Curl.Ntlm.UnitLibrary/NtlmAuthenticateMessage.cs` `TryEncode(out byte[]? message)` applies both checks (`responsesEnd > CurlBufferSize || messageLength >= CurlBufferSize`) and returns `false` for either; `NtlmMessageFailure.TooLarge` documents one failure. The only caller, `Curl.Authentication.UnitLibrary/HandBuiltNtlmSecurityContext.cs`, therefore always reports `NtlmHttpAuthenticator.Type3TooLargeMessage` (`user + domain + hostname too big for NTLM`), which is wrong for the first case.
- Keep `TryEncode(out byte[]? message)` as it is so `Curl.Authentication.UnitLibrary` builds untouched; add an overload with `out NtlmMessageFailure failure` and split `TooLarge` into two values named for the two checks (for example `ResponsesTooLarge` and `NamesTooLarge`), each doc comment quoting curl's message. Using the new overload in `Curl.Authentication.UnitLibrary` is a follow-up task, not this one.

## Acceptance criteria

- [x] New tests in `Curl.Ntlm.UnitTests/NtlmAuthenticateMessageTests.cs`: an NT response that takes the message past 1024 bytes after the header gives the responses value; responses that fit but names that reach 1024 bytes give the names value; a message of exactly 1023 bytes encodes, one of 1024 does not (the `>=` check), and responses ending at exactly 1024 bytes pass the first check (the `>` check).
- [x] The existing `TryEncode(out byte[]? message)` returns what it returned before for every case (existing tests unchanged and passing).
- [x] `NtlmMessageFailure.TooLarge` no longer exists, and no file in the solution names it.
- [x] `dotnet build Curl.slnx -warnaserror` is clean; the fast tests pass; `Measure-CodeQuality.ps1 -Library Curl.Ntlm.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Added `TryEncode(out byte[]? message, out NtlmMessageFailure failure)`; the old overload forwards to it and discards the failure, so its results are unchanged. `TooLarge` is split into `ResponsesTooLarge` (curl's `>` check) and `NamesTooLarge` (its `>=` check), checked in curl's order.
- Responses ending at exactly 1024 bytes pass the first check; with empty names the message is then 1024 bytes and the second check refuses it as `NamesTooLarge`, as in curl.
- Measured: Curl.Ntlm.UnitLibrary 100% line, 100% branch, 46 members, 0 failing, worst CRAP 10. Ntlm tests: 66 passed; fast suite green.
- Follow-up filed: BL-1135 (use the new overload in Curl.Authentication.UnitLibrary so Curl prints `incoming NTLM message too big`).

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. NtlmAuthenticateMessage.TryEncode reports ResponsesTooLarge or NamesTooLarge, telling curl's two NTLM_BUFSIZE checks apart
