---
id: BL-781
title: Carry SSPI's exit 94 out of a SASL exchange the Schannel build rejects
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-537]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests, Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests, Curl.Protocol.Imap.UnitLibrary, Curl.Protocol.Imap.UnitTests, Curl.Protocol.Pop3.UnitLibrary, Curl.Protocol.Pop3.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-781 — Carry SSPI's exit 94 out of a SASL exchange the Schannel build rejects

## Goal

On Windows, a DIGEST-MD5 challenge that SSPI rejects fails the transfer with exit 94 (`CURLE_AUTH_ERROR`, "An authentication function returned an error") as curl 8.21.0's Schannel build does, instead of the exit 67 a cancelled exchange gives.

## Context

- BL-537 measured it (Notes there, ADR-0139): with `Record-CurlExchange.ps1 -Smtp -SmtpReply 'AUTH=334 <challenge>'`, the Schannel build exits 94 for a DIGEST-MD5 challenge with no `nonce`, no `algorithm`, or `qop="auth-int"` alone, sending nothing after the challenge; the OpenSSL build cancels with `*` and exits 67.
- `ISaslExchange.Respond` (`Curl.Protocol.Abstractions.UnitLibrary`) can only answer bytes or `null`, which every mail handler turns into `*` and exit 67. `SaslDigestMd5.AnswerAsSspi` returns `null` for these cases for now.
- The contract needs a third outcome ("fail with this exit code, send nothing"), and the SMTP, IMAP and POP3 handlers must honour it. Measure what the Schannel build writes to the server (if anything) and its stderr before pinning.

## Acceptance criteria

- [ ] `ISaslExchange` can report an authentication-function failure, documented in its XML comments.
- [ ] `SaslAuthenticator` constructed for SSPI reports it for the three measured challenges; the OpenSSL path still answers `null`.
- [ ] SMTP, IMAP and POP3 handler tests pin exit 94 and the bytes sent, matching the measured Schannel exchange.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports no failing member in the touched libraries.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
