---
id: BL-1226
title: Write curl's 'Target Info Offset Len is set incorrect' -v line for an NTLM challenge whose target info is out of range
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1226 — Write curl's 'Target Info Offset Len is set incorrect' -v line for an NTLM challenge whose target info is out of range

## Goal

When the hand-built NTLM path (curl's OpenSSL build, Linux and macOS) refuses a Type 2 message because its target information lies outside the message, `NtlmHttpAuthenticator` writes curl 8.21.0's `NTLM handshake failure (bad type-2 message). Target Info Offset Len is set incorrect by the peer` before the `NTLM handshake failure (bad type-2 message)` line it writes today.

## Context

- Today `Curl.Ntlm.UnitLibrary/NtlmChallengeMessage.cs` `Decode` already fails such a message with `NtlmMessageFailure.TargetInfoOutOfRange`, but `Curl.Authentication.UnitLibrary/HandBuiltNtlmSecurityContext.cs` `Answer` turns every decode failure into `SecurityContextStatus.MalformedToken` and keeps no reason, and `NtlmHttpAuthenticator.CreateAuthenticateAsync` (`NtlmHttpAuthenticator.cs`, around lines 197-223) then writes only `NtlmHandshakeLines.BadType2`.
- curl 8.21.0, `lib/vauth/ntlm.c` at `curl-8_21_0` (https://github.com/curl/curl/blob/curl-8_21_0/lib/vauth/ntlm.c): `ntlm_decode_type2_target` (lines 256-288) writes `infof "NTLM handshake failure (bad type-2 message). Target Info Offset Len is set incorrect by the peer"` when a non-zero target info length has an offset past the message, an offset plus length past it, or an offset inside the 48-byte header, and returns `CURLE_BAD_CONTENT_ENCODING`; its caller `Curl_auth_decode_ntlm_type2_message` (lines 372-378) then writes `NTLM handshake failure (bad type-2 message)`. A message under 32 bytes or with a wrong signature or type writes only the second line (lines 363-368).
- Windows matches the Schannel build, which decodes Type 2 in SSPI (`lib/vauth/ntlm_sspi.c`) and never writes the first line: the `matchesSspiBuild` branch of `CreateAuthenticateAsync` stays as it is. This case cannot be measured on Windows; pin it from the source and say so in the test comment.
- Use `Curl.Authentication.UnitTests`'s existing NTLM tests (`NtlmHttpAuthenticatorTests` and its fakes) with the authenticator built for the OpenSSL build.

## Acceptance criteria

- [ ] Tests in `Curl.Authentication.UnitTests`, with the authenticator built as for the OpenSSL build, pin both lines in order for a Type 2 whose target info runs past the message end and for one whose offset is inside the header, and only `NTLM handshake failure (bad type-2 message)` for a Type 2 shorter than 32 bytes and for one with the wrong signature.
- [ ] A test pins that the SSPI-build authenticator writes neither line for the out-of-range message (it keeps today's Type 3 failure path).
- [ ] The decode reason reaches the authenticator through a member whose name says what it holds; `Curl.Ntlm.UnitLibrary` is not changed.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes with no test needing `TestCategory=Integration`; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Authentication.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
