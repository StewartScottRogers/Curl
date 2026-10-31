---
id: BL-4390
title: Make --ohttp-relay honour --fail and refuse options it cannot carry instead of dropping them
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extension.ObliviousHttp.UnitLibrary/Relay/InnerResponseOutput.cs, Curl.Extension.ObliviousHttp.UnitLibrary/Relay/InnerRequest.cs, Curl.Extension.ObliviousHttp.UnitTests/Relay]
requirement: none
created: 2026-10-31
completed:
---
# BL-4390 — Make --ohttp-relay honour --fail and refuse options it cannot carry instead of dropping them

## Goal

--ohttp-relay exits 22 with no body for an inner response of 400 or above under -f, and either carries or refuses with exit 2 each option it cannot silently drop (-u, --oauth2-bearer, -b, -e, --compressed).

## Context

Shift-end review 2026-10-31 (code-reviewer). Curl.Extension.ObliviousHttp.UnitLibrary/Relay/InnerResponseOutput.cs:17-33 always writes the decapsulated body and returns TransferResult.Success, never reading context.Http.Fail: `curl -f --ohttp-relay R https://t/missing` writes the 404 body and exits 0. Relay/InnerRequest.cs:25-44 builds the inner request only from -H, -A and -d; -T, -I, -r, -z and -F are refused (exit 2) but -u, --oauth2-bearer, -b, -e and --compressed are dropped silently, so `-u alice:secret` sends no Authorization. See CoapProtocolHandler.cs:135 for the -f pattern.

## Acceptance criteria

- [ ] `-f` with an inner 404 returns exit 22 and writes no body, proven by a test in Curl.Extension.ObliviousHttp.UnitTests/Relay.
- [ ] Each of -u, --oauth2-bearer, -b, -e and --compressed is either carried into the encapsulated request or refused with exit 2, each proven by a test.
- [ ] dotnet build is clean, the fast tests pass and the library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-10-31: Created.
