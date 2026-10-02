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
completed: 2026-10-02
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

- [x] `grep -rn refusedChallengeFailsTransfer --include=*.cs` finds nothing; the new
      name is used at every call site and in the XML doc and
      `Curl.Authentication.UnitLibrary/CLAUDE.md`.
- [x] `dotnet build -warnaserror` is clean and
      `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

- Renamed to `matchesSspiBuild` (the name the task suggested): `true` matches the SSPI build (exit 94), `false` curl's own NTLM (exit 100 only for an oversized Type 3). Pure rename, 13 .cs files plus `Curl.Authentication.UnitLibrary/CLAUDE.md`, which now names the parameter. Build clean, fast tests green.

## Log

- 2026-09-29: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. NtlmHttpAuthenticator's SSPI-build switch is named matchesSspiBuild at every call site
