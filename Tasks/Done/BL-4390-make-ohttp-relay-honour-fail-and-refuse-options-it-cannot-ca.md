---
id: BL-4390
title: Make --ohttp-relay honour --fail and refuse options it cannot carry instead of dropping them
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extension.ObliviousHttp.UnitLibrary/Relay/InnerResponseOutput.cs, Curl.Extension.ObliviousHttp.UnitLibrary/Relay/InnerRequest.cs, Curl.Extension.ObliviousHttp.UnitLibrary/CLAUDE.md, Curl.Extension.ObliviousHttp.UnitTests/Relay, Curl.Protocol.Abstractions.UnitLibrary/HttpRequestOptions.cs, Curl.Engine.UnitLibrary/HttpRequestOptionsMapping.cs, Curl.Engine.UnitTests/HttpRequestOptionsMappingTests.cs]
requirement: none
created: 2026-10-31
completed: 2026-10-31
---
# BL-4390 — Make --ohttp-relay honour --fail and refuse options it cannot carry instead of dropping them

## Goal

--ohttp-relay exits 22 with no body for an inner response of 400 or above under -f, and either carries or refuses with exit 2 each option it cannot silently drop (-u, --oauth2-bearer, -b, -e, --compressed).

## Context

Shift-end review 2026-10-31 (code-reviewer). Curl.Extension.ObliviousHttp.UnitLibrary/Relay/InnerResponseOutput.cs:17-33 always writes the decapsulated body and returns TransferResult.Success, never reading context.Http.Fail: `curl -f --ohttp-relay R https://t/missing` writes the 404 body and exits 0. Relay/InnerRequest.cs:25-44 builds the inner request only from -H, -A and -d; -T, -I, -r, -z and -F are refused (exit 2) but -u, --oauth2-bearer, -b, -e and --compressed are dropped silently, so `-u alice:secret` sends no Authorization. See CoapProtocolHandler.cs:135 for the -f pattern.

## Acceptance criteria

- [x] `-f` with an inner 404 returns exit 22 and writes no body, proven by a test in Curl.Extension.ObliviousHttp.UnitTests/Relay.
- [x] Each of -u, --oauth2-bearer, -b, -e and --compressed is either carried into the encapsulated request or refused with exit 2, each proven by a test.
- [x] dotnet build is clean, the fast tests pass and the library keeps 100% line and branch coverage.

## Notes

- Widened touches: the hook could not see -b (the cookie store lives in the HTTP handler), so HttpRequestOptions gained CookieStrings and HasCookieFiles, set by HttpRequestOptionsMapping; no other Doing task touches them.
- Decisions: -u (Basic from context.Credentials), --oauth2-bearer, -e and -b strings are carried as authorization, referer and cookie headers unless -H gives them; --compressed and -b files are refused with exit 2 (the inner response would need decoding, a file needs the cookie store). Only Basic is carried for -u.
- Build, ObliviousHttp tests (151) and Measure-CodeQuality for the three libraries are green (100% line and branch).

## Log

- 2026-10-31: Created.
- 2026-10-31: Backlog -> Doing. Criteria at claim: 9162473c f86e4ea3 f874b647.
- 2026-10-31: Doing -> Done. --ohttp-relay honours -f (exit 22, no body) and carries or refuses -u, --oauth2-bearer, -b, -e, --compressed
