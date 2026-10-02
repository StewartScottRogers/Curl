---
id: BL-1128
title: Fail an NTLM answer too big for its responses with curl's incoming NTLM message too big
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1114]
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests]
requirement: none
created: 2026-10-01
completed:
---
# BL-1128 — Fail an NTLM answer too big for its responses with curl's incoming NTLM message too big

## Goal

When the hand-built NTLM context refuses an AUTHENTICATE message because the responses overflow curl's 1024-byte buffer (a challenge with large target information), the transfer fails with exit 100 and curl's `incoming NTLM message too big`; only the names-too-big case keeps `user + domain + hostname too big for NTLM`.

## Context

- BL-1114 makes `Curl.Ntlm.UnitLibrary/NtlmAuthenticateMessage.cs` say which check refused the message (a `TryEncode` overload with `out NtlmMessageFailure`, one value per check); read its Notes for the final names. curl 8.21.0's two messages and where they come from are in BL-1114 (`lib/vauth/ntlm.c` lines 776-803).
- Today: `Curl.Authentication.UnitLibrary/HandBuiltNtlmSecurityContext.cs` `Answer` calls the old `TryEncode(out byte[]?)` and returns `SecurityContextStatus.Refused` for either case; `NtlmHttpAuthenticator.cs` (around line 185) turns `Refused` into `HttpAuthenticationFailedException(CurlExitCode.TooLarge, Type3TooLargeMessage)` every time.
- Keep the change inside `Curl.Authentication.UnitLibrary`: `SecurityContextStep` and `SecurityContextStatus` live in `Curl.Protocol.Abstractions.UnitLibrary` and are shared by every security context, so do not change them here. The hand-built context can keep the reason it refused (for example a property the authenticator reads from the context it created) or the authenticator can be handed the message another way inside this library. If no clean shape exists without changing the Abstractions types, stop, file that contract change as its own task, and move this one to `Blocked` on it.
- The hand-built context is the one the OpenSSL-matching builds use (Linux and macOS); the tests construct it directly, so they are platform-neutral.
- The failure is reported the way `Type3TooLargeMessage` is today (curl's `failf` text as the error message); check whether a `-v` line follows it in the existing tests and keep that.

## Acceptance criteria

- [ ] A test in `Curl.Authentication.UnitTests/NtlmHttpAuthenticatorTests.cs` answers a CHALLENGE whose target information pushes the NTLMv2 response past the buffer and asserts `HttpAuthenticationFailedException` with exit 100 and `incoming NTLM message too big`.
- [ ] The existing test that pins `user + domain + hostname too big for NTLM` (around line 140) still passes unchanged.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Authentication.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-10-01: Created.
