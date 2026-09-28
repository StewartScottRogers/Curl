---
id: BL-774
title: Cancel an SMTP SASL exchange with * on an undecodable challenge and try the next mechanism
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-537]
touches: [Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests]
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

- [ ] Before any code change, `Notes` holds curl 8.21.0's request lines, stderr and exit code measured with `Record-CurlExchange.ps1 -Smtp` for two cases: (a) server offers `AUTH CRAM-MD5 PLAIN` and answers `AUTH CRAM-MD5` with a 334 whose text is not valid base64; (b) server offers only `AUTH CRAM-MD5` with the same bad challenge.
- [ ] Tests in `Curl.Protocol.Smtp.UnitTests`, driven through a fake connection and a fake SASL authenticator, pin for case (a) the `*` cancel line, the read of the server's reply, the retry `AUTH PLAIN ...` and success; and for case (b) the `*` cancel line, the final exit code (`CurlExitCode` 67 unless the measurement says otherwise) and stderr text, all as measured.
- [ ] Mechanisms that do not decode their challenge (PLAIN, LOGIN, EXTERNAL, XOAUTH2, OAUTHBEARER) keep the ADR-0133 behaviour; an existing or new test pins it.
- [ ] ADR-0133 has an amendment section stating the cancel-and-retry behaviour for challenge-decoding mechanisms and the curl version measured.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes; no test needs `TestCategory=Integration`.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Smtp.UnitLibrary` reports 100% line and 100% branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
