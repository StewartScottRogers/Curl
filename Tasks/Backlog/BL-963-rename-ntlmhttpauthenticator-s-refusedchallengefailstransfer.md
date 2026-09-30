---
id: BL-963
title: Rename NtlmHttpAuthenticator's refusedChallengeFailsTransfer to say it picks the SSPI build
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-849]
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests, Curl.Console, Curl.Networking.UnitTests, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-963 — Rename NtlmHttpAuthenticator's refusedChallengeFailsTransfer to say it picks the SSPI build

## Goal

`NtlmHttpAuthenticator`'s second constructor parameter is named for what it picks - the
SSPI build's failure (exit 94 for any unanswerable Type 2) against curl's own NTLM's (exit
100 only for a Type 3 past 1024 bytes) - with no behaviour change.

## Context

Since BL-849, `refusedChallengeFailsTransfer: false` also fails a transfer (exit 100 when
the hand-built context answers `Refused`), so the name says less than the parameter does
("say what it does"). A name such as `matchesSspiBuild` fits. BL-849 did not rename it
because the call sites in `Curl.Console/CurlComposition.cs`,
`Curl.Networking.UnitTests/TcpConnectorTests.ProxyAuth.cs` and
`Curl.Protocol.Http.UnitTests` (`HttpProtocolHandlerTests.NegotiateHandshake.cs`,
`HttpProtocolHandlerTests.ProxyAuthentication.cs`) were outside its `touches` and held by
other lanes.

## Acceptance criteria

- [ ] `grep -rn refusedChallengeFailsTransfer --include=*.cs` finds nothing; the new
      name is used at every call site and in the XML doc and
      `Curl.Authentication.UnitLibrary/CLAUDE.md`.
- [ ] `dotnet build -warnaserror` is clean and
      `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-09-29: Created.
